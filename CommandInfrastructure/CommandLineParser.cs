// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    // context-free grammar:
    //
    // <command line> ::= [ <command> { <arg> }+ ]
    // <command> ::= <ident>
    // <arg> ::= <atom> | <expr>
    // <expr> ::= <or-expr>
    // <or-expr> ::= <xor-expr>
    //            |  <xor-expr> '|' <or-expr>
    // <xor-expr> ::= <and-expr>
    //             |  <and-expr> '^' <xor-expr>
    // <and-expr> ::= <rel-expr>
    //             |  <rel-expr> '&' <and-expr>
    // <rel-expr> ::= <shift-expr>
    //             |  <shift-expr> '>' <rel-expr>
    //             |  <shift-expr> '<' <rel-expr>
    //             |  <shift-expr> '>=' <rel-expr>
    //             |  <shift-expr> '<=' <rel-expr>
    // <shift-expr> ::= <add-expr>
    //               |  <add-expr> '<<' <shift-expr>
    //               |  <add-expr> '>>' <shift-expr>
    //               |  <add-expr> '>>>' <shift-expr>
    // <add-expr> ::= <mult-expr>
    //             |  <mult-expr> '+' <add-expr>
    //             |  <mult-expr> '-' <add-expr>
    // <mult-expr> ::= <unary-expr>
    //              |  <unary-expr> '*' <mult-expr>
    //              |  <unary-expr> '/' <mult-expr>
    //              |  <unary-expr> '%' <mult-expr>
    // <unary-expr> ::= <prim-expr>
    //               |  '~' <unary-expr>
    //               |  '-' <unary-expr>
    //               |  '*' <unary-expr>                        // dereference as native pointer value
    //               |  '@' <cast-rest>                         // type implicitly as object or explicitly as struct
    // <prim-expr> ::= <integer>
    //              |  <string>
    //              |  <variable>                               // access value from evaluation history
    //              |  '(' <expr> ')'
    //              |  <prim-expr> '.' ident                    // access object/struct field
    //              |  <prim-expr> '[' { <expr> // ',' }+ ']'   // access array element
    // <cast-rest> ::= '<' { <ident> // '.' }+ '>' <prim-expr>  // explicit typing with qualified name
    //              |  <prim-expr>                              // implicit typing, extracting object type from vtable field
    //
    // tokens:
    //
    // <ident> = [a-zA-Z_][a-zA-Z0-9_]*
    // <integer> = [0-9]+|0x[0-9a-zA-Z]+
    // <string> = "[^"\r\n]+"
    // <variable> = \$[0-9]+
    // <atom> = '[a-zA-Z0-9_]*
    // <whitespace> = SPC TAB
    sealed class CommandLineParser
    {
        readonly CommandLineTokenizer m_tokenizer;
        readonly Context m_context;
        readonly string? m_commandName;
        readonly CommandLineArgument[] m_args;

        public static CommandLine? Parse(string line, Context context)
        {
            var parser = new CommandLineParser(line, context);
            if (parser.m_commandName == null)
            {
                return null;
            }
            return new CommandLine(parser.m_commandName, parser.m_args.ToArray());
        }

        CommandLineParser(string line, Context context)
        {
            m_tokenizer = new CommandLineTokenizer(line);
            m_context = context;

            var args = new List<CommandLineArgument>();
            if (m_tokenizer.Peek() != Token.Eof)
            {
                m_commandName = m_tokenizer.TokenValue;
                Expect(Token.Ident);

                while (m_tokenizer.Peek() != Token.Eof)
                {
                    args.Add(ParseArg());
                }
            }
            m_args = args.ToArray();
        }

        CommandLineArgument ParseArg()
        {
            if (m_tokenizer.Peek() == Token.Atom)
            {
                CommandLineArgument result = CommandLineArgument.FromAtom(m_tokenizer.TokenValue!);
                m_tokenizer.Consume();
                return result;
            }
            else
            {
                return ParseExpr();
            }
        }

        CommandLineArgument ParseExpr()
        {
            return ParseOrExpr();
        }

        CommandLineArgument ParseOrExpr()
        {
            CommandLineArgument result = ParseXorExpr();
            while (m_tokenizer.Peek() == Token.Or)
            {
                m_tokenizer.Consume();
                CommandLineArgument other = ParseXorExpr();
                result = result.BitwiseOr(other);
            }
            return result;
        }

        CommandLineArgument ParseXorExpr()
        {
            CommandLineArgument result = ParseAndExpr();
            while (m_tokenizer.Peek() == Token.Hat)
            {
                m_tokenizer.Consume();
                CommandLineArgument other = ParseAndExpr();
                result = result.BitwiseXor(other);
            }
            return result;
        }

        CommandLineArgument ParseAndExpr()
        {
            CommandLineArgument result = ParseRelExpr();
            while (m_tokenizer.Peek() == Token.Ampersand)
            {
                m_tokenizer.Consume();
                CommandLineArgument other = ParseRelExpr();
                result = result.BitwiseAnd(other);
            }
            return result;
        }

        CommandLineArgument ParseRelExpr()
        {
            CommandLineArgument result = ParseShiftExpr();
            while (IsRelOp(m_tokenizer.Peek()))
            {
                Token token = m_tokenizer.Peek();
                m_tokenizer.Consume();
                CommandLineArgument other = ParseShiftExpr();
                result = result.RelOp(token, other);
            }
            return result;
        }

        bool IsRelOp(Token token)
        {
            return token == Token.GreaterThan || token == Token.LessThan || token == Token.GreaterThanOrEqual || token == Token.LessThanOrEqual;
        }

        CommandLineArgument ParseShiftExpr()
        {
            CommandLineArgument result = ParseAddExpr();
            while (IsShiftOp(m_tokenizer.Peek()))
            {
                Token token = m_tokenizer.Peek();
                m_tokenizer.Consume();
                CommandLineArgument other = ParseAddExpr();
                result = result.ShiftOp(token, other);
            }
            return result;
        }

        bool IsShiftOp(Token token)
        {
            return token == Token.LeftShift || token == Token.RightShift || token == Token.RightShift3;
        }

        CommandLineArgument ParseAddExpr()
        {
            CommandLineArgument result = ParseMultExpr();
            while (IsAddOp(m_tokenizer.Peek()))
            {
                Token token = m_tokenizer.Peek();
                m_tokenizer.Consume();
                CommandLineArgument other = ParseMultExpr();
                result = result.AddOp(token, other);
            }
            return result;
        }

        bool IsAddOp(Token token)
        {
            return token == Token.Plus || token == Token.Minus;
        }

        CommandLineArgument ParseMultExpr()
        {
            CommandLineArgument result = ParseUnaryExpr();
            while (IsMultOp(m_tokenizer.Peek()))
            {
                Token token = m_tokenizer.Peek();
                m_tokenizer.Consume();
                CommandLineArgument other = ParseUnaryExpr();
                result = result.MultOp(token, other);
            }
            return result;
        }

        bool IsMultOp(Token token)
        {
            return token == Token.Star || token == Token.Slash || token == Token.Percent;
        }

        CommandLineArgument ParseUnaryExpr()
        {
            switch (m_tokenizer.Peek())
            {
                case Token.Tilde:
                    {
                        m_tokenizer.Consume();
                        CommandLineArgument result = ParseUnaryExpr();
                        result = result.BitwiseComplement();
                        return result;
                    }
                case Token.Minus:
                    {
                        m_tokenizer.Consume();
                        CommandLineArgument result = ParseUnaryExpr();
                        result = result.Negate();
                        return result;
                    }
                case Token.Star:
                    {
                        m_tokenizer.Consume();
                        CommandLineArgument result = ParseUnaryExpr();
                        result = result.Indirect(m_context);
                        return result;
                    }
                case Token.At:
                    m_tokenizer.Consume();
                    return ParseCastRest();
                default:
                    return ParsePrimExpr();
            }
        }

        CommandLineArgument ParsePrimExpr()
        {
            CommandLineArgument? result = null;

            switch (m_tokenizer.Peek())
            {
                case Token.Integer:
                    string value = m_tokenizer.TokenValue!;
                    if (value.StartsWith("0x"))
                    {
                        if (ulong.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out ulong integer))
                        {
                            result = CommandLineArgument.FromInteger(integer);
                        }
                    }
                    else
                    {
                        if (ulong.TryParse(value, out ulong integer))
                        {
                            result = CommandLineArgument.FromInteger(integer);
                        }
                    }

                    m_tokenizer.Consume();
                    break;
                case Token.String:
                    result = CommandLineArgument.FromString(m_tokenizer.TokenValue!);
                    m_tokenizer.Consume();
                    break;
                case Token.Variable:
                    // TODO: evaluate variable reference: look up in history
                    m_tokenizer.Consume();
                    break;
                case Token.OpenParen:
                    m_tokenizer.Consume();
                    result = ParseExpr();
                    Expect(Token.CloseParen);
                    break;
                default:
                    throw new CommandException($"parse error at {m_tokenizer.Peek()}");
            }

            while (true)
            {
                switch (m_tokenizer.Peek())
                {
                    case Token.Dot:
                        m_tokenizer.Consume();
                        Expect(Token.Ident);
                        // TODO: evaluate dot access: need object representation for CommandLineArgument
                        break;
                    case Token.OpenBracket:
                        m_tokenizer.Consume();
                        int rank = 0;
                        while (true)
                        {
                            rank++;
                            ParseExpr();
                            if (m_tokenizer.Peek() == Token.Comma)
                            {
                                m_tokenizer.Consume();
                            }
                            else
                            {
                                break;
                            }
                        }
                        Expect(Token.CloseBracket);
                        // TODO: evaluate array access: need object representation in CommandLineArgument
                        break;
                    default:
                        return result!;
                }
            }
        }

        CommandLineArgument ParseCastRest()
        {
            if (m_tokenizer.Peek() == Token.LessThan)
            {
                var sb = new StringBuilder();
                sb.Append(m_tokenizer.TokenValue!);
                Expect(Token.Ident);

                while (m_tokenizer.Peek() == Token.Dot)
                {
                    m_tokenizer.Consume();
                    sb.Append(m_tokenizer.TokenValue!);
                    Expect(Token.Ident);
                }
                Expect(Token.GreaterThan);
            }
            CommandLineArgument result = ParsePrimExpr();
            // TODO: evaluate cast: need object representation in CommandLineArgument
            return result;
        }

        void Expect(Token expected)
        {
            Token actual = m_tokenizer.Peek();
            if (actual != expected)
            {
                throw new CommandException($"parse error: found {actual}; expected {expected}");
            }
            m_tokenizer.Consume();
        }
    }
}
