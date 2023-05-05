// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;

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
                CheckType(CommandLineArgumentType.String);
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

        void CheckType(CommandLineArgumentType type)
        {
            if (m_type != type)
            {
                throw new CommandException($"argument is not {type}; found {m_type}");
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

        internal CommandLineArgument Indirect(MemorySnapshot? memorySnapshot)
        {
            if (memorySnapshot == null)
            {
                throw new CommandException($"no active memory snapshot");
            }

            NativeWord address = AsNativeWord(memorySnapshot.Native);
            MemoryView memoryView = memorySnapshot.GetMemoryViewForAddress(address);
            if (!memoryView.IsValid)
            {
                throw new CommandException($"cannot indirect through address {address}");
            }
            NativeWord value = memoryView.ReadNativeWord(0, memorySnapshot.Native);
            return FromInteger(value.Value);
        }
    }
}
