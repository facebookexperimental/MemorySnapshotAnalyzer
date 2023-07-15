// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

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
    }

    public abstract class Rule
    {
        public ClassSpec Spec;
    };

    public sealed class FieldPatternRule : Rule
    {
        // A field name, or (if ending in "*") a field prefix.
        public string? FieldPattern;
    }

    public sealed class FieldPathRule : Rule
    {
        // Path of fields to dereference. Note that these are full field names, not patterns.
        // The special field name "[]" represents array indexing (covering all elements of the array).
        public string[]? FieldNames;
    }
}
