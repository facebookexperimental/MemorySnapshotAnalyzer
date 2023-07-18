// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Linq;
using System.Text;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    public sealed class CommandLineArgument
    {
        readonly CommandLineArgumentType m_type;
        readonly string? m_atomOrStringValue;
        readonly ulong m_integerValue;

        CommandLineArgument(CommandLineArgumentType type, string value)
        {
            m_type = type;
            m_atomOrStringValue = value;
        }

        CommandLineArgument(ulong value)
        {
            m_type = CommandLineArgumentType.Integer;
            m_integerValue = value;
        }

        public static CommandLineArgument FromAtom(string value)
        {
            return new CommandLineArgument(CommandLineArgumentType.Atom, value);
        }

        public static CommandLineArgument FromString(string value)
        {
            return new CommandLineArgument(CommandLineArgumentType.String, value);
        }

        public static CommandLineArgument FromInteger(ulong value)
        {
            return new CommandLineArgument(value);
        }

        public CommandLineArgumentType ArgumentType => m_type;

        public string AtomValue
        {
            get
            {
                CheckType(CommandLineArgumentType.Atom);
                return m_atomOrStringValue!;
            }
        }

        public string StringValue
        {
            get
            {
                CheckType(CommandLineArgumentType.String, CommandLineArgumentType.Atom);
                return m_atomOrStringValue!;
            }
        }

        public ulong IntegerValue
        {
            get
            {
                CheckType(CommandLineArgumentType.Integer);
                return m_integerValue;
            }
        }

        public NativeWord AsNativeWord(Native native)
        {
            return native.From(IntegerValue);
        }

        void CheckType(params CommandLineArgumentType[] types)
        {
            if (!types.Contains(m_type))
            {
                var sb = new StringBuilder("argument is not ");
                bool first = true;
                foreach (var type in types)
                {
                    if (!first)
                    {
                        sb.Append(" or ");
                    }
                    first = false;
                    sb.Append(type.ToString());
                }
                sb.Append($"; found {m_type}");
                throw new CommandException(sb.ToString());
            }
        }

        internal CommandLineArgument BitwiseOr(CommandLineArgument other)
        {
            return FromInteger(IntegerValue | other.IntegerValue);
        }

        internal CommandLineArgument BitwiseXor(CommandLineArgument other)
        {
            return FromInteger(IntegerValue ^ other.IntegerValue);
        }

        internal CommandLineArgument BitwiseAnd(CommandLineArgument other)
        {
            return FromInteger(IntegerValue & other.IntegerValue);
        }

        internal CommandLineArgument RelOp(Token token, CommandLineArgument other)
        {
            switch (token)
            {
                case Token.GreaterThan:
                    return FromInteger(IntegerValue > other.IntegerValue ? 1UL : 0UL);
                case Token.LessThan:
                    return FromInteger(IntegerValue < other.IntegerValue ? 1UL : 0UL);
                case Token.GreaterThanOrEqual:
                    return FromInteger(IntegerValue >= other.IntegerValue ? 1UL : 0UL);
                case Token.LessThanOrEqual:
                    return FromInteger(IntegerValue <= other.IntegerValue ? 1UL : 0UL);
                default:
                    throw new ArgumentException($"unrecognized token {token}");
            }
        }

        internal CommandLineArgument ShiftOp(Token token, CommandLineArgument other)
        {
            switch (token)
            {
                case Token.LeftShift:
                    return FromInteger(IntegerValue << (int)other.IntegerValue);
                case Token.RightShift:
                    return FromInteger(IntegerValue >> (int)other.IntegerValue);
                // TODO: set C# language version to 11.0 in the csproj
                //case Token.RightShift3:
                //    return CommandLineArgument.FromInteger(IntegerValue >>> (int)other.IntegerValue);
                default:
                    throw new ArgumentException($"unrecognized token {token}");
            }
        }

        internal CommandLineArgument AddOp(Token token, CommandLineArgument other)
        {
            switch (token)
            {
                case Token.Plus:
                    return FromInteger(IntegerValue + other.IntegerValue);
                case Token.Minus:
                    return FromInteger(IntegerValue - other.IntegerValue);
                default:
                    throw new ArgumentException($"unrecognized token {token}");
            }
        }

        internal CommandLineArgument MultOp(Token token, CommandLineArgument other)
        {
            switch (token)
            {
                case Token.Star:
                    return FromInteger(IntegerValue * other.IntegerValue);
                case Token.Slash:
                    return FromInteger(IntegerValue / other.IntegerValue);
                case Token.Percent:
                    return FromInteger(IntegerValue % other.IntegerValue);
                default:
                    throw new ArgumentException($"unrecognized token {token}");
            }
        }

        internal CommandLineArgument BitwiseComplement()
        {
            return FromInteger(~IntegerValue);
        }

        internal CommandLineArgument Negate()
        {
            return FromInteger((ulong)-(long)IntegerValue);
        }

        internal CommandLineArgument Indirect(Context context)
        {
            if (context.CurrentMemorySnapshot == null)
            {
                throw new CommandException("no active memory snapshot");
            }

            context.EnsureTraceableHeap();
            NativeWord address = AsNativeWord(context.CurrentTraceableHeap!.Native);
            SegmentedHeap? segmentedHeap = context.CurrentTraceableHeap!.SegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            MemoryView memoryView = segmentedHeap.GetMemoryViewForAddress(address);
            if (!memoryView.IsValid)
            {
                throw new CommandException($"cannot indirect through address {address}");
            }
            NativeWord value = memoryView.ReadNativeWord(0, context.CurrentTraceableHeap.Native);
            return FromInteger(value.Value);
        }

        internal CommandLineArgument Typeof(Context context)
        {
            if (context.CurrentMemorySnapshot == null)
            {
                throw new CommandException("no active memory snapshot");
            }

            context.EnsureTracedHeap();
            NativeWord addressOrIndex = AsNativeWord(context.CurrentTraceableHeap!.Native);
            int typeIndex;
            if (addressOrIndex.Value < (ulong)context.CurrentTracedHeap!.NumberOfPostorderNodes)
            {
                // Assume the argument is an object index.
                int postorderIndex = (int)addressOrIndex.Value;
                typeIndex = context.CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
            }
            else
            {
                // Check whether the argument corresponds to an object address.
                int postorderIndex = context.CurrentTracedHeap!.ObjectAddressToPostorderIndex(addressOrIndex);
                if (postorderIndex == -1)
                {
                    // See whether the argument is a memory address we can read a type index from.
                    typeIndex = context.CurrentTraceableHeap.TryGetTypeIndex(addressOrIndex);
                }
                else
                {
                    typeIndex = context.CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                }
            }

            if (typeIndex == -1)
            {
                throw new CommandException("could not determine type index");
            }

            return new CommandLineArgument((ulong)typeIndex);
        }

        public TypeSet ResolveTypeIndexOrPattern(Context context, bool includeDerived)
        {
            if (context.CurrentMemorySnapshot == null)
            {
                throw new CommandException("no active memory snapshot");
            }

            context.EnsureTraceableHeap();
            var typeSet = new TypeSet(context.CurrentTraceableHeap!.TypeSystem);
            if (ArgumentType == CommandLineArgumentType.Integer)
            {
                ulong value = IntegerValue;
                if (value >= (ulong)context.CurrentTraceableHeap.TypeSystem.NumberOfTypeIndices)
                {
                    throw new CommandException("could not find type with given address or index");
                }

                typeSet.Add((int)value);
            }
            else if (ArgumentType == CommandLineArgumentType.String)
            {
                typeSet.AddTypesByName(StringValue);
                if (includeDerived)
                {
                    typeSet.AddDerivedTypes();
                }
            }
            else
            {
                throw new CommandException("not a type index or pattern");
            }

            return typeSet;
        }
    }
}
