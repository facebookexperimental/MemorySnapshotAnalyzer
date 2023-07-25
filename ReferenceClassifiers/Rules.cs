// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.IO;
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

        TypeSpec(string assembly, string typeName)
        {
            Assembly = assembly;
            TypeName = typeName;
        }

        public static TypeSpec Parse(string value)
        {
            int indexOfColon = value.IndexOf(':');
            if (indexOfColon == -1)
            {
                throw new FileFormatException("type name must be prefixed with an assembly name");
            }

            return new TypeSpec(value[..indexOfColon], value[(indexOfColon + 1)..]);
        }

        public bool AssemblyMatches(ReadOnlySpan<char> assemblyWithoutExtension)
        {
            return assemblyWithoutExtension.Equals(WithoutExtension(Assembly), StringComparison.OrdinalIgnoreCase);
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
        public TypeSpec TypeSpec { get; private set; }

        protected Rule(TypeSpec typeSpec)
        {
            TypeSpec = typeSpec;
        }

        protected static List<string> ParseSelector(string value)
        {
            var pieces = new List<string>();
            int startIndex = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '[')
                {
                    if (i + 1 == value.Length || value[i + 1] != ']')
                    {
                        throw new FileFormatException($"invalid field pattern syntax; '[' must be immediately followed by ']'");
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

            return pieces;
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
    };

    public abstract class OwnsRule : Rule
    {
        protected OwnsRule(TypeSpec typeSpec) : base(typeSpec) { }

        public static OwnsRule Parse(TypeSpec typeSpec, string value)
        {
            List<string> fieldPattern = ParseSelector(value);
            if (fieldPattern.Count == 1)
            {
                return new OwnsFieldPatternRule(typeSpec, fieldPattern[0]);
            }
            else
            {
                return new OwnsSelectorRule(typeSpec, fieldPattern.ToArray());
            }
        }
    }

    public sealed class OwnsFieldPatternRule : OwnsRule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string FieldPattern { get; private set; }

        public OwnsFieldPatternRule(TypeSpec typeSpec, string fieldPattern) : base(typeSpec)
        {
            FieldPattern = fieldPattern;
        }

        public override string ToString()
        {
            return $"{TypeSpec} OWNS \"{FieldPattern}\";";
        }
    }

    public sealed class OwnsSelectorRule : OwnsRule
    {
        // Path of fields to dereference. Note that these are full field names, not patterns.
        // The special field name "[]" represents array indexing (covering all elements of the array).
        public string[] Selector { get; private set; }

        public OwnsSelectorRule(TypeSpec typeSpec, string[] selector) : base(typeSpec)
        {
            Selector = selector;
        }

        public override string ToString()
        {
            return $"{TypeSpec} OWNS \"{StringifySelector(Selector)}\";";
        }
    }

    public sealed class WeakRule : Rule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string FieldPattern { get; private set; }

        public WeakRule(TypeSpec typeSpec, string fieldPattern) : base(typeSpec)
        {
            FieldPattern = fieldPattern;
        }

        public override string ToString()
        {
            return $"{TypeSpec} WEAK \"{FieldPattern}\";";
        }
    }

    public sealed class ExternalRule : Rule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string FieldPattern { get; private set; }

        public ExternalRule(TypeSpec typeSpec, string fieldPattern) : base(typeSpec)
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
        public string Tag { get; private set; }

        public TagSelectorRule(TypeSpec typeSpec, string selector, string tag) : base(typeSpec)
        {
            Selector = ParseSelector(selector).ToArray();
            Tag = tag;
        }

        public override string ToString()
        {
            return $"{TypeSpec} TAG({Tag}) \"{StringifySelector(Selector)}\";";
        }
    }

    public sealed class TagConditionRule : Rule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string FieldPattern { get; private set; }
        public string Tag { get; private set; }
        public bool TagIfNonZero { get; private set; }

        public TagConditionRule(TypeSpec typeSpec, string fieldPattern, string tag, bool tagIfNonZero) : base(typeSpec)
        {
            FieldPattern = fieldPattern;
            Tag = tag;
            TagIfNonZero = tagIfNonZero;
        }

        public override string ToString()
        {
            if (TagIfNonZero)
            {
                return $"{TypeSpec} TAG_IF_NONZERO({Tag}) \"{FieldPattern}\";";
            }
            else
            {
                return $"{TypeSpec} TAG_IF_ZERO({Tag}) \"{FieldPattern}\";";
            }
        }
    }
}
