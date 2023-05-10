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
        public CommandLineArgument? AddressOrIndexOrSubstring;

        [NamedArgument("assembly")]
        public string? Assembly;

        [FlagArgument("recursive")]
        public bool Recursive;

        [FlagArgument("exact")]
        public bool ExactMatch;

        [FlagArgument("verbose")]
        public bool Verbose;

        [FlagArgument("statics")]
        public bool Statics;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            ITypeSystem typeSystem = CurrentManagedHeap.TypeSystem;

            if (AddressOrIndexOrSubstring == null)
            {
                // Dump all types.
                Output.WriteLine("Number of type indices: {0}", typeSystem.NumberOfTypeIndices);
                for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
                {
                    DumpType(typeIndex, 0);
                }
            }
            else if (AddressOrIndexOrSubstring.ArgumentType == CommandLineArgumentType.Integer)
            {
                // Dump by address and/or index.
                NativeWord address = AddressOrIndexOrSubstring.AsNativeWord(CurrentMemorySnapshot.Native);
                int typeIndex = typeSystem.TypeInfoAddressToIndex(address);
                if (typeIndex < 0)
                {
                    ulong value = AddressOrIndexOrSubstring.IntegerValue;
                    if (value >= (ulong)typeSystem.NumberOfTypeIndices)
                    {
                        throw new CommandException("could not find type with given address or index");
                    }
                    typeIndex = (int)value;
                }

                DumpType(typeIndex, 0);
            }
            else if (AddressOrIndexOrSubstring.ArgumentType == CommandLineArgumentType.String)
            {
                // Dump by name.
                string value = AddressOrIndexOrSubstring.StringValue;

                for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
                {
                    if (Assembly != null)
                    {
                        if (typeSystem.Assembly(typeIndex) != Assembly)
                        {
                            continue;
                        }
                    }

                    if (ExactMatch)
                    {
                        if (typeSystem.QualifiedName(typeIndex) == value)
                        {
                            DumpType(typeIndex, 0);
                        }
                    }
                    else if (typeSystem.QualifiedName(typeIndex).Contains(value))
                    {
                        DumpType(typeIndex, 0);
                    }
                }
            }
            else
            {
                throw new CommandException("unrecognized type");
            }
        }

        void DumpType(int typeIndex, int indent)
        {
            if (Statics && !HasStaticFields(typeIndex))
            {
                return;
            }

            ITypeSystem typeSystem = CurrentManagedHeap.TypeSystem;

            Output.WriteLineIndented(indent, "Type {0} at address {1}: qualified name {2}:{3}, {4} with base size {5}, rank {6}",
                typeIndex,
                typeSystem.TypeInfoAddress(typeIndex),
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
                        Output.WriteLineIndented(indent + 1, "{0} field {1} (index {2}) at offset {3}: {4} type {5} (index {6}), field size {7}",
                            typeSystem.FieldIsStatic(typeIndex, fieldNumber) ? "Static" : "Instance",
                            typeSystem.FieldName(typeIndex, fieldNumber),
                            fieldNumber,
                            typeSystem.FieldOffset(typeIndex, fieldNumber),
                            typeSystem.IsValueType(fieldTypeIndex) ? "value" : typeSystem.IsArray(fieldTypeIndex) ? "array" : "object",
                            typeSystem.QualifiedName(fieldTypeIndex),
                            fieldTypeIndex,
                            CurrentManagedHeap.GetFieldSize(fieldTypeIndex));
                    }

                    if (Statics && isStatic)
                    {
                        MemoryView staticFieldBytesView = typeSystem.StaticFieldBytes(typeIndex, fieldNumber);
                        if (staticFieldBytesView.IsValid)
                        {
                            DumpFieldValue(staticFieldBytesView, typeSystem.FieldType(typeIndex, fieldNumber), indent + 2);
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
            ITypeSystem typeSystem = CurrentManagedHeap.TypeSystem;

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

        public override string HelpText => "dumptype [<address>|<index>|<substring> ['assembly <assembly>] ['exact]] ['recursive] ['verbose] ['statics]";
    }
}
