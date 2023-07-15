// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Text;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public struct ClassSpec
    {
        // Can be a full assembly name (without .dll extension). Matched case-insensitively,
        // and assemblies that include a .dll extension are considered to match.
        public string Assembly;

        // Fully qualified class name.
        public string ClassName;

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
            return $"{Assembly}:{ClassName}";
        }
    }

    public abstract class Rule
    {
        public ClassSpec Spec;
    };

    public sealed class FieldPatternRule : Rule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string? FieldPattern;

        public override string ToString()
        {
            return $"{Spec} {FieldPattern}";
        }
    }

    public sealed class FieldPathRule : Rule
    {
        // Path of fields to dereference. Note that these are full field names, not patterns.
        // The special field name "[]" represents array indexing (covering all elements of the array).
        public string[]? FieldNames;

        public override string ToString()
        {
            StringBuilder sb = new(Spec.ToString());
            sb.Append(' ');
            foreach (string fieldName in FieldNames!)
            {
                if (sb.Length > 0 && !fieldName.Equals("[]", StringComparison.Ordinal))
                {
                    sb.Append('.');
                }
                sb.Append(fieldName);
            }
            return sb.ToString();
        }
    }
}
