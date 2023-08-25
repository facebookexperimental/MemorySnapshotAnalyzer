/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System;
using System.Collections.Generic;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.ReferenceClassifierAttributes;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class ReferenceClassifierMetadataReader
    {
        static readonly string LOG_SOURCE = "ReferenceClassifierMetadataReader";

        readonly string m_dllFilename;
        readonly MetadataReader m_metadataReader;
        readonly string? m_groupNamePrefix;
        readonly ILogger m_logger;
        readonly Dictionary<string, List<Rule>> m_result;
        string? m_assemblyName;
        TypeDefinition m_currentTypeDefinition;
        FieldDefinition m_currentFieldDefinition;
        PropertyDefinition m_currentPropertyDefinition;
        bool m_isProcessingProperty;
        TypeSpec? m_typeSpec;
        string? m_fieldName;
        string? m_attributeGroupName;
        string? m_selector;
        bool m_isDynamic;

        public static void LoadFromDllFilename(string dllFilename, string? groupNamePrefix, ILogger logger, Dictionary<string, List<Rule>> result)
        {
            try
            {
                using var streamReader = new StreamReader(dllFilename);
                using var peReader = new PEReader(streamReader.BaseStream);

                var reader = new ReferenceClassifierMetadataReader(dllFilename, peReader.GetMetadataReader(), groupNamePrefix, logger, result);
                reader.ProcessTypeDefinitions();
            }
            catch (Exception ex) when (ex is IOException || ex is BadImageFormatException)
            {
                logger.Log(LOG_SOURCE, dllFilename, ex.Message);
            }
        }

        ReferenceClassifierMetadataReader(string dllFilename, MetadataReader metadataReader, string? groupNamePrefix, ILogger logger, Dictionary<string, List<Rule>> result)
        {
            m_dllFilename = dllFilename;
            m_metadataReader = metadataReader;
            m_groupNamePrefix = groupNamePrefix;
            m_logger = logger;
            m_result = result;
        }

        void AddRule(Rule rule, string? groupName)
        {
            string resolvedGroupName;
            if (m_groupNamePrefix != null)
            {
                resolvedGroupName = groupName != null ? $"{m_groupNamePrefix}.{groupName}" : m_groupNamePrefix;
            }
            else
            {
                resolvedGroupName = groupName ?? "anonymous";
            }

            List<Rule>? rules;
            if (!m_result.TryGetValue(resolvedGroupName, out rules))
            {
                rules = new List<Rule>();
                m_result.Add(resolvedGroupName, rules);
            }
            rules.Add(rule);
        }

        string GetAssemblyName()
        {
            if (m_assemblyName == null)
            {
                AssemblyDefinition assemblyDefinition = m_metadataReader.GetAssemblyDefinition();
                m_assemblyName = m_metadataReader.GetString(assemblyDefinition.Name);
            }
            return m_assemblyName;
        }

        TypeSpec GetTypeSpec()
        {
            if (m_typeSpec == null)
            {
                string namespaceName = m_metadataReader.GetString(m_currentTypeDefinition.Namespace);
                string typeName = m_metadataReader.GetString(m_currentTypeDefinition.Name);
                m_typeSpec = new TypeSpec(GetAssemblyName(), $"{namespaceName}.{typeName}");
            }
            return m_typeSpec;
        }

        string GetFieldName()
        {
            if (m_fieldName == null)
            {
                if (m_isProcessingProperty)
                {

                    string propertyName = m_metadataReader.GetString(m_currentPropertyDefinition.Name);
                    m_fieldName = $"<{propertyName}>k__BackingField";
                }
                else
                {
                    m_fieldName = m_metadataReader.GetString(m_currentFieldDefinition.Name);
                }
            }
            return m_fieldName;
        }

        string BuildSelector()
        {
            if (string.IsNullOrEmpty(m_selector))
            {
                return GetFieldName();
            }
            else if (m_selector[0] == '[')
            {
                return $"{GetFieldName()}{m_selector}";
            }
            else
            {
                return $"{GetFieldName()}.{m_selector}";
            }
        }

        void ProcessTypeDefinitions()
        {
            foreach (TypeDefinitionHandle typeDefinitionHandle in m_metadataReader.TypeDefinitions)
            {
                m_currentTypeDefinition = m_metadataReader.GetTypeDefinition(typeDefinitionHandle);
                m_typeSpec = null;
                try
                {
                    ProcessFieldDefinitions();
                    ProcessPropertyDefinitions();
                }
                catch (Exception ex) when (ex is IOException || ex is BadImageFormatException)
                {
                    m_logger.Log(LOG_SOURCE, GetTypeSpec().ToString(), ex.Message);
                }
            }
        }

        void ProcessFieldDefinitions()
        {
            m_isProcessingProperty = false;
            foreach (FieldDefinitionHandle fieldDefinitionHandle in m_currentTypeDefinition.GetFields())
            {
                m_currentFieldDefinition = m_metadataReader.GetFieldDefinition(fieldDefinitionHandle);
                m_fieldName = null;
                ProcessCustomAttributes();
            }
        }

        void ProcessPropertyDefinitions()
        {
            m_isProcessingProperty = true;
            foreach (PropertyDefinitionHandle propertyDefinitionHandle in m_currentTypeDefinition.GetProperties())
            {
                m_currentPropertyDefinition = m_metadataReader.GetPropertyDefinition(propertyDefinitionHandle);
                m_fieldName = null;
                ProcessCustomAttributes();
            }
        }

        void ProcessCustomAttributes()
        {
            CustomAttributeHandleCollection customAttributeHandles =
                m_isProcessingProperty ? m_currentPropertyDefinition.GetCustomAttributes() : m_currentFieldDefinition.GetCustomAttributes();
            foreach (CustomAttributeHandle customAttributeHandle in customAttributeHandles)
            {
                try
                {
                    CustomAttribute customAttribute = m_metadataReader.GetCustomAttribute(customAttributeHandle);
                    ProcessCustomAttribute(customAttribute);
                }
                catch (Exception ex) when (ex is IOException || ex is BadImageFormatException)
                {
                    m_logger.Log(LOG_SOURCE, $"{GetTypeSpec()}.{GetFieldName()}", ex.Message);
                }
            }
        }

        void ProcessCustomAttribute(CustomAttribute customAttribute)
        {
            (string namespaceName, string typeName) = GetCustomAttributeType(customAttribute, m_metadataReader);
            Rule? rule = null;
            if (namespaceName == typeof(OwnsAttribute).Namespace)
            {
                BlobReader blobReader = m_metadataReader.GetBlobReader(customAttribute.Value);
                int version = blobReader.ReadUInt16();
                if (version != 1)
                {
                    throw new BadImageFormatException("unsupported custom attribute blob version");
                }

                if (typeName == typeof(OwnsAttribute).Name)
                {
                    ReadCommonNamedArguments(blobReader);
                    rule = new OwnsRule(m_dllFilename, GetTypeSpec(), BuildSelector(), m_isDynamic);
                }
                else if (typeName == typeof(TagAttribute).Name)
                {
                    string? tags = blobReader.ReadSerializedString();
                    if (tags == null)
                    {
                        throw new BadImageFormatException("tag argument of TagAttribute cannot be null");
                    }

                    ReadCommonNamedArguments(blobReader);
                    rule = new TagSelectorRule(m_dllFilename, GetTypeSpec(), BuildSelector(), tags, m_isDynamic);
                }
                else if (typeName == typeof(TagContainingObjectAttribute).Name)
                {
                    string? tags = blobReader.ReadSerializedString();
                    if (tags == null)
                    {
                        throw new BadImageFormatException("tag argument of TagContainingObjectAttribute cannot be null");
                    }

                    TagCondition tagCondition = (TagCondition)blobReader.ReadInt32();
                    if (tagCondition != TagCondition.FIELD_IS_ZERO && tagCondition != TagCondition.FIELD_IS_NONZERO)
                    {
                        throw new BadImageFormatException("invalid tag condition");
                    }

                    ReadCommonNamedArguments(blobReader);
                    rule = new TagConditionRule(m_dllFilename, GetTypeSpec(), GetFieldName(), tags,
                        tagIfNonZero: tagCondition == TagCondition.FIELD_IS_NONZERO);
                }
            }

            if (rule != null)
            {
                AddRule(rule, m_attributeGroupName);
            }
        }

        (string namespaceName, string typeName) GetCustomAttributeType(CustomAttribute customAttribute, MetadataReader metadataReader)
        {
            EntityHandle entityHandle;
            if (customAttribute.Constructor.Kind == HandleKind.MethodDefinition)
            {
                entityHandle = metadataReader.GetMethodDefinition((MethodDefinitionHandle)customAttribute.Constructor).GetDeclaringType();
            }
            else if (customAttribute.Constructor.Kind == HandleKind.MemberReference)
            {
                entityHandle = metadataReader.GetMemberReference((MemberReferenceHandle)customAttribute.Constructor).Parent;
            }
            else
            {
                throw new BadImageFormatException("unrecognized custom attribute constructor reference");
            }

            if (entityHandle.Kind == HandleKind.TypeDefinition)
            {
                TypeDefinition typeDefinition = metadataReader.GetTypeDefinition((TypeDefinitionHandle)entityHandle);
                return (metadataReader.GetString(typeDefinition.Namespace), metadataReader.GetString(typeDefinition.Name));
            }
            else if (entityHandle.Kind == HandleKind.TypeReference)
            {
                TypeReference typeReference = metadataReader.GetTypeReference((TypeReferenceHandle)entityHandle);
                return (metadataReader.GetString(typeReference.Namespace), metadataReader.GetString(typeReference.Name));
            }
            else
            {
                throw new BadImageFormatException("unable to read custom attribute type name");
            }
        }

        void ReadCommonNamedArguments(BlobReader blobReader)
        {
            m_attributeGroupName = null;
            m_selector = null;
            m_isDynamic = false;

            ushort numberOfNamedArguments = blobReader.ReadUInt16();
            for (int i = 0; i < numberOfNamedArguments; i++)
            {
                byte kind = blobReader.ReadByte();
                if (kind != 0x54)
                {
                    throw new BadImageFormatException("invalid named argument kind");
                }

                byte elementType = blobReader.ReadByte();
                string? propertyName = blobReader.ReadSerializedString();
                if (propertyName == null)
                {
                    throw new BadImageFormatException("property name cannot be null");
                }

                if (propertyName.Equals(nameof(OwnsAttribute.GroupName), StringComparison.Ordinal) && elementType == 0x0E)
                {
                    m_attributeGroupName = blobReader.ReadSerializedString();
                }
                else if (propertyName.Equals(nameof(OwnsAttribute.Selector), StringComparison.Ordinal) && elementType == 0x0E)
                {
                    m_selector = blobReader.ReadSerializedString();
                }
                else if (propertyName.Equals(nameof(OwnsAttribute.IsDynamic), StringComparison.Ordinal) && elementType == 0x02)
                {
                    m_isDynamic = blobReader.ReadByte() != 0;
                }
                else
                {
                    throw new BadImageFormatException($"unrecognized property \"{propertyName}\" of type {elementType:X02}");
                }
            }
        }
    }
}
