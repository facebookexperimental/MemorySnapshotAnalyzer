/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;

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
                Output.AddProperty("numberOfTypeIndices", typeSystem.NumberOfTypeIndices);
                Output.AddDisplayStringLine("Number of type indices: {0}", typeSystem.NumberOfTypeIndices);
                Output.BeginArray("types");
                for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
                {
                    if (!Statics || HasStaticFields(typeIndex))
                    {
                        Output.BeginElement();
                        DumpType(typeIndex, 0);
                        Output.EndElement();
                    }
                }
                Output.EndArray();
            }
            else
            {
                Output.BeginArray("types");
                TypeSet typeSet = TypeIndexOrPattern.ResolveTypeIndexOrPattern(Context, IncludeDerived);
                foreach (int typeIndex in typeSet.TypeIndices)
                {
                    if (!Statics || HasStaticFields(typeIndex))
                    {
                        Output.BeginElement();
                        DumpType(typeIndex, 0);
                        Output.EndElement();
                    }
                }
                Output.EndArray();
            }
        }

        void DumpType(int typeIndex, int indent)
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            string kind = typeSystem.Kind(typeIndex);
            typeSystem.OutputType(Output, "type", typeIndex);
            Output.AddProperty("kind", kind);
            Output.AddProperty("baseSize", typeSystem.BaseSize(typeIndex));
            Output.AddProperty("rank", typeSystem.Rank(typeIndex));
            Output.AddDisplayStringLineIndented(indent, "Type {0}: qualified name {1}:{2}, {3} type with base size {4}, rank {5}",
                typeIndex,
                typeSystem.Assembly(typeIndex),
                typeSystem.QualifiedName(typeIndex),
                kind,
                typeSystem.BaseSize(typeIndex),
                typeSystem.Rank(typeIndex));

            if (Verbose || Statics)
            {
                Output.BeginArray("fields");

                int numberOfFields = typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    Output.BeginElement();

                    bool isStatic = typeSystem.FieldIsStatic(typeIndex, fieldNumber);
                    int fieldTypeIndex = typeSystem.FieldType(typeIndex, fieldNumber);
                    if (Statics && isStatic || !Statics && !isStatic)
                    {
                        Output.AddProperty("isStatic", typeSystem.FieldIsStatic(typeIndex, fieldNumber));
                        Output.AddProperty("fieldName", typeSystem.FieldName(typeIndex, fieldNumber));
                        Output.AddProperty("fieldNumber", fieldNumber);
                        Output.AddProperty("fieldOffset", typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true));
                        Output.AddProperty("fieldKind", typeSystem.Kind(typeIndex));
                        typeSystem.OutputType(Output, "fieldType", fieldTypeIndex);
                        Output.AddDisplayStringLineIndented(indent + 1, "{0} field {1} (number {2}) at offset {3}: {4} type {5}:{6} (index {7})",
                            typeSystem.FieldIsStatic(typeIndex, fieldNumber) ? "Static" : "Instance",
                            typeSystem.FieldName(typeIndex, fieldNumber),
                            fieldNumber,
                            typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: true),
                            typeSystem.Kind(fieldTypeIndex),
                            typeSystem.Assembly(fieldTypeIndex),
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
                            Output.AddProperty("isInitialized", false);
                            Output.AddDisplayStringLineIndented(indent + 2, "Uninitialized");
                        }
                    }

                    Output.EndElement();
                }

                Output.EndArray();
            }

            int baseOrElementTypeIndex = typeSystem.BaseOrElementTypeIndex(typeIndex);
            if (baseOrElementTypeIndex >= 0)
            {
                if (Verbose)
                {
                    typeSystem.OutputType(Output, typeSystem.IsArray(typeIndex) ? "elementType" : "baseType", baseOrElementTypeIndex);
                    Output.AddDisplayStringLineIndented(indent + 1, "{0} type {1} (index {2})",
                        typeSystem.IsArray(typeIndex) ? "Element" : "Base",
                        typeSystem.QualifiedName(baseOrElementTypeIndex),
                        baseOrElementTypeIndex);
                }

                if (Recursive)
                {
                    Output.BeginChild("baseType");
                    DumpType(baseOrElementTypeIndex, indent + 1);
                    Output.EndChild();
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
