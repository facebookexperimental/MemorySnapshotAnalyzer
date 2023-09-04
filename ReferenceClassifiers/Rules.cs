/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class TypeSpec
    {
        // Can be a full assembly name (without .dll extension). Matched case-insensitively,
        // and assemblies that include a .dll extension are considered to match.
        public string Assembly { get; private set; }

        // Fully qualified type name.
        public string TypeName { get; private set; }

        public TypeSpec(string assembly, string typeName)
        {
            Assembly = new string(WithoutExtension(assembly));
            TypeName = typeName;
        }

        public static TypeSpec Parse(string value)
        {
            int indexOfColon = value.IndexOf(':');
            if (indexOfColon == -1)
            {
                throw new ArgumentException("type name must be prefixed with an assembly name");
            }

            return new TypeSpec(value[..indexOfColon], value[(indexOfColon + 1)..]);
        }

        public bool AssemblyMatches(ReadOnlySpan<char> assemblyWithoutExtension)
        {
            return assemblyWithoutExtension.Equals(Assembly, StringComparison.OrdinalIgnoreCase);
        }

        public static ReadOnlySpan<char> WithoutExtension(string assemblyName)
        {
            ReadOnlySpan<char> assemblySpan = assemblyName.AsSpan();
            if (assemblySpan.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return assemblySpan[..^4];
            }
            else
            {
                return assemblySpan;
            }
        }

        public override string ToString()
        {
            return $"\"{Assembly}:{TypeName}\"";
        }
    }

    public abstract class Rule
    {
        public string Location { get; private set; }
        public TypeSpec TypeSpec { get; private set; }

        protected Rule(string location, TypeSpec typeSpec)
        {
            Location = location;
            TypeSpec = typeSpec;
        }

        protected static string[] ParseSelector(string value)
        {
            var pieces = new List<string>();
            int startIndex = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '[')
                {
                    if (i + 1 == value.Length || value[i + 1] != ']')
                    {
                        throw new ArgumentException($"invalid field pattern syntax; '[' must be immediately followed by ']'");
                    }

                    if (i > startIndex)
                    {
                        pieces.Add(value[startIndex..i]);
                    }

                    pieces.Add("[]");
                    startIndex = i + 2;
                    i++;
                }
                else if (value[i] == '.')
                {
                    if (i > startIndex)
                    {
                        pieces.Add(value[startIndex..i]);
                    }

                    startIndex = i + 1;
                }
            }

            if (startIndex != value.Length)
            {
                pieces.Add(value[startIndex..]);
            }

            return pieces.ToArray();
        }

        protected static string StringifySelector(string[] selector)
        {
            StringBuilder sb = new();
            foreach (string fieldName in selector)
            {
                if (sb.Length > 0 && !fieldName.Equals("[]", StringComparison.Ordinal))
                {
                    sb.Append('.');
                }
                sb.Append(fieldName);
            }
            return sb.ToString();
        }

        protected static string[] ParseTags(string tags)
        {
            return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        protected static string StringifyTags(string[] tags)
        {
            StringBuilder sb = new();
            foreach (string tag in tags)
            {
                if (sb.Length > 0)
                {
                    sb.Append(',');
                }
                sb.Append(tag);
            }
            return sb.ToString();
        }
    };

    public sealed class OwnsRule : Rule
    {
        // Path of fields to dereference. Note that these except for the first field, these are full field names, not patterns.
        // The special field name "[]" represents array indexing (covering all elements of the array).
        public string[] Selector { get; private set; }
        public int Weight { get; private set; }
        public bool IsDynamic { get; private set; }

        public OwnsRule(string location, TypeSpec typeSpec, string selector, int weight, bool isDynamic) : base(location, typeSpec)
        {
            Debug.Assert(weight != 0);

            Selector = ParseSelector(selector);
            Weight = weight;
            IsDynamic = isDynamic;
        }

        public override string ToString()
        {
            string keyword = IsDynamic ? "OWNS_DYNAMIC" : "OWNS";
            if (Weight == 1)
            {
                return $"{TypeSpec} {keyword} \"{StringifySelector(Selector)}\";";
            }
            else
            {
                return $"{TypeSpec} {keyword}({Weight}) \"{StringifySelector(Selector)}\";";
            }
        }
    }

    public sealed class ExternalRule : Rule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string FieldPattern { get; private set; }

        public ExternalRule(string location, TypeSpec typeSpec, string fieldPattern) : base(location, typeSpec)
        {
            FieldPattern = fieldPattern;
        }

        public override string ToString()
        {
            return $"{TypeSpec} EXTERNAL \"{FieldPattern}\";";
        }
    }

    public sealed class TagSelectorRule : Rule
    {
        // Path of fields to dereference. Note that these are full field names, not patterns.
        // The special field name "[]" represents array indexing (covering all elements of the array).
        public string[] Selector { get; private set; }
        public string[] Tags { get; private set; }
        public bool IsDynamic { get; private set; }

        public TagSelectorRule(string location, TypeSpec typeSpec, string selector, string tags, bool isDynamic) : base(location, typeSpec)
        {
            Selector = ParseSelector(selector);
            Tags = ParseTags(tags);
            IsDynamic = isDynamic;
        }

        public override string ToString()
        {
            string keyword = IsDynamic ? "TAG_DYNAMIC" : "TAG";
            return $"{TypeSpec} {keyword}({StringifyTags(Tags)}) \"{StringifySelector(Selector)}\";";
        }
    }

    public sealed class TagConditionRule : Rule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string FieldPattern { get; private set; }
        public string[] Tags { get; private set; }
        public bool TagIfNonZero { get; private set; }

        public TagConditionRule(string location, TypeSpec typeSpec, string fieldPattern, string tags, bool tagIfNonZero) : base(location, typeSpec)
        {
            FieldPattern = fieldPattern;
            Tags = ParseTags(tags);
            TagIfNonZero = tagIfNonZero;
        }

        public override string ToString()
        {
            if (TagIfNonZero)
            {
                return $"{TypeSpec} TAG_IF_NONZERO({StringifyTags(Tags)}) \"{FieldPattern}\";";
            }
            else
            {
                return $"{TypeSpec} TAG_IF_ZERO({StringifyTags(Tags)}) \"{FieldPattern}\";";
            }
        }
    }
}
