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
using System.Security.AccessControl;

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
        int m_weight;
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
            string resolvedGroupName = ReferenceClassifierGroup.ResolveGroupName(m_groupNamePrefix, groupName);

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
                string namespaceName, typeName;
                if (m_currentTypeDefinition.IsNested)
                {
                    TypeDefinition enclosingTypeDefinition = m_metadataReader.GetTypeDefinition(m_currentTypeDefinition.GetDeclaringType());
                    namespaceName = m_metadataReader.GetString(enclosingTypeDefinition.Namespace);
                    string enclosingTypeName = m_metadataReader.GetString(enclosingTypeDefinition.Name);
                    string nestedTypeName = m_metadataReader.GetString(m_currentTypeDefinition.Name);
                    typeName = $"{enclosingTypeName}.{nestedTypeName}";
                }
                else
                {
                    namespaceName = m_metadataReader.GetString(m_currentTypeDefinition.Namespace);
                    typeName = m_metadataReader.GetString(m_currentTypeDefinition.Name);
                }
                string qualifiedName = namespaceName.Length > 0 ? $"{namespaceName}.{typeName}" : typeName;
                m_typeSpec = new TypeSpec(GetAssemblyName(), qualifiedName);
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
                    if (m_weight != 0)
                    {
                        rule = new OwnsRule(m_dllFilename, GetTypeSpec(), BuildSelector(), m_weight, m_isDynamic);
                    }
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

        static (string namespaceName, string typeName) GetCustomAttributeType(CustomAttribute customAttribute, MetadataReader metadataReader)
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

        // From ECMA 335 specification; see also https://github.com/dotnet/runtime/blob/76d17b25252ce14b7e36c2e6854fe416db60f5cf/src/coreclr/inc/corhdr.h#L952
        enum CorSerializationType : byte
        {
            SERIALIZATION_TYPE_PROPERTY = 0x54,
        }

        // From ECMA 335 specification; see also https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/src/System/Reflection/Metadata/Internal/CorElementType.cs#L10
        enum CorElementType : byte
        {
            ELEMENT_TYPE_BOOLEAN = 0x2,
            ELEMENT_TYPE_I4 = 0x8,
            ELEMENT_TYPE_STRING = 0x0E,
        }

        void ReadCommonNamedArguments(BlobReader blobReader)
        {
            m_attributeGroupName = null;
            m_selector = null;
            m_weight = 1;
            m_isDynamic = false;

            ushort numberOfNamedArguments = blobReader.ReadUInt16();
            for (int i = 0; i < numberOfNamedArguments; i++)
            {
                byte kind = blobReader.ReadByte();
                if (kind != (byte)CorSerializationType.SERIALIZATION_TYPE_PROPERTY)
                {
                    throw new BadImageFormatException("invalid named argument kind");
                }

                byte elementType = blobReader.ReadByte();
                string? propertyName = blobReader.ReadSerializedString();
                if (propertyName == null)
                {
                    throw new BadImageFormatException("property name cannot be null");
                }

                if (propertyName.Equals(nameof(OwnsAttribute.GroupName), StringComparison.Ordinal) && elementType == (byte)CorElementType.ELEMENT_TYPE_STRING)
                {
                    m_attributeGroupName = blobReader.ReadSerializedString();
                }
                else if (propertyName.Equals(nameof(OwnsAttribute.Selector), StringComparison.Ordinal) && elementType == (byte)CorElementType.ELEMENT_TYPE_STRING)
                {
                    m_selector = blobReader.ReadSerializedString();
                }
                else if (propertyName.Equals(nameof(OwnsAttribute.Weight), StringComparison.Ordinal) && elementType == (byte)CorElementType.ELEMENT_TYPE_I4)
                {
                    m_weight = blobReader.ReadInt32();
                }
                else if (propertyName.Equals(nameof(OwnsAttribute.IsDynamic), StringComparison.Ordinal) && elementType == (byte)CorElementType.ELEMENT_TYPE_BOOLEAN)
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
