// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpTypeCommand : Command
    {
        public DumpTypeCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [PositionalArgument(0, optional: true)]
        public CommandLineArgument? TypeIndexOrPattern;

        [FlagArgument("recursive")]
        public bool Recursive;

        [FlagArgument("verbose")]
        public bool Verbose;

        [FlagArgument("statics")]
        public bool Statics;

        [FlagArgument("includederived")]
        public bool IncludeDerived;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            if (TypeIndexOrPattern == null)
            {
                // Dump all types.
                Output.WriteLine("Number of type indices: {0}", typeSystem.NumberOfTypeIndices);
                for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
                {
                    DumpType(typeIndex, 0);
                }
            }
            else
            {
                TypeSet typeSet = TypeIndexOrPattern.ResolveTypeIndexOrPattern(Context, IncludeDerived);
                foreach (int typeIndex in typeSet.TypeIndices)
                {
                    DumpType(typeIndex, 0);
                }
            }
        }

        void DumpType(int typeIndex, int indent)
        {
            if (Statics && !HasStaticFields(typeIndex))
            {
                return;
            }

            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            Output.WriteLineIndented(indent, "Type {0}: qualified name {1}:{2}, {3} with base size {4}, rank {5}",
                typeIndex,
                typeSystem.Assembly(typeIndex),
                typeSystem.QualifiedName(typeIndex),
                typeSystem.IsValueType(typeIndex) ? "value type" : typeSystem.IsArray(typeIndex) ? "array" : "object",
                typeSystem.BaseSize(typeIndex),
                typeSystem.Rank(typeIndex));

            if (Verbose || Statics)
            {
                int numberOfFields = typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    bool isStatic = typeSystem.FieldIsStatic(typeIndex, fieldNumber);
                    int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);
                    if (Statics && isStatic || !Statics && !isStatic)
                    {
                        Output.WriteLineIndented(indent + 1, "{0} field {1} (number {2}) at offset {3}: {4} type {5} (index {6})",
                            typeSystem.FieldIsStatic(typeIndex, fieldNumber) ? "Static" : "Instance",
                            typeSystem.FieldName(typeIndex, fieldNumber),
                            fieldNumber,
                            typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true),
                            typeSystem.IsValueType(fieldTypeIndex) ? "value" : typeSystem.IsArray(fieldTypeIndex) ? "array" : "object",
                            typeSystem.QualifiedName(fieldTypeIndex),
                            fieldTypeIndex);
                    }

                    if (Statics && isStatic)
                    {
                        MemoryView staticFieldBytesView = typeSystem.StaticFieldBytes(typeIndex, fieldNumber);
                        if (staticFieldBytesView.IsValid)
                        {
                            DumpFieldMemory(staticFieldBytesView, typeSystem.FieldType(typeIndex, fieldNumber), indent + 2);
                        }
                        else
                        {
                            Output.WriteLineIndented(indent + 2, "Uninitialized");
                        }
                    }
                }
            }

            int baseOrElementTypeIndex = typeSystem.BaseOrElementTypeIndex(typeIndex);
            if (baseOrElementTypeIndex >= 0)
            {
                if (Verbose)
                {
                    Output.WriteLineIndented(indent + 1, "{0} type {1} (index {2})",
                        typeSystem.IsArray(typeIndex) ? "Element" : "Base",
                        typeSystem.QualifiedName(baseOrElementTypeIndex),
                        baseOrElementTypeIndex);
                }

                if (Recursive)
                {
                    DumpType(baseOrElementTypeIndex, indent + 1);
                }
            }
        }

        bool HasStaticFields(int typeIndex)
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            int numberOfFields = typeSystem.NumberOfFields(typeIndex);
            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                bool isStatic = typeSystem.FieldIsStatic(typeIndex, fieldNumber);
                if (isStatic)
                {
                    return true;
                }
            }
            return false;
        }

        public override string HelpText => "dumptype [<type index or pattern>] ['exact]] ['recursive] ['verbose] ['statics] ['includederived]";
    }
}
