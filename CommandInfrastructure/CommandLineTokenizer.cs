/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    sealed class CommandLineTokenizer
    {
        readonly string m_line;
        Token m_nextToken;
        string? m_tokenValue;
        int m_cursor = 0;

        public CommandLineTokenizer(string line)
        {
            m_line = line;
            m_cursor = 0;
            Consume();
        }

        public Token Peek()
        {
            return m_nextToken;
        }

        public string? TokenValue => m_tokenValue;

        public void Consume()
        {
            m_nextToken = GetNext(out m_tokenValue);
        }

        Token GetNext(out string? token)
        {
            token = null;
            while (m_cursor < m_line.Length)
            {
                switch (m_line[m_cursor++])
                {
                    case ' ':
                    case '\t':
                        break;
                    case '\'':
                        return ScanAtom(out token);
                    case '$':
                        return ScanVariable(out token);
                    case '<':
                        return ScanLessThan();
                    case '>':
                        return ScanGreaterThan();
                    case '.':
                        return Token.Dot;
                    case '[':
                        return Token.OpenBracket;
                    case ']':
                        return Token.CloseBracket;
                    case ',':
                        return Token.Comma;
                    case '@':
                        return Token.At;
                    case '*':
                        return Token.Star;
                    case '(':
                        return Token.OpenParen;
                    case ')':
                        return Token.CloseParen;
                    case '-':
                        return Token.Minus;
                    case '~':
                        return Token.Tilde;
                    case '%':
                        return Token.Percent;
                    case '/':
                        return Token.Slash;
                    case '+':
                        return Token.Plus;
                    case '&':
                        return Token.Ampersand;
                    case '^':
                        return Token.Hat;
                    case '|':
                        return Token.Or;
                    case '"':
                        return ScanString(out token);
                    case 'a': case 'b': case 'c': case 'd': case 'e': case 'f': case 'g': case 'h': case 'i': case 'j': case 'k': case 'l': case 'm':
                    case 'n': case 'o': case 'p': case 'q': case 'r': case 's': case 't': case 'u': case 'v': case 'w': case 'x': case 'y': case 'z':
                    case 'A': case 'B': case 'C': case 'D': case 'E': case 'F': case 'G': case 'H': case 'I': case 'J': case 'K': case 'L': case 'M':
                    case 'N': case 'O': case 'P': case 'Q': case 'R': case 'S': case 'T': case 'U': case 'V': case 'W': case 'X': case 'Y': case 'Z':
                    case '_':
                        return ScanIdent(m_cursor - 1, out token);
                    case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9':
                        return ScanInteger(out token);
                    default:
                        throw new CommandException($"invalid character at position {m_cursor - 1}");
                }
            }
            return Token.Eof;
        }

        Token ScanString(out string token)
        {
            int start = m_cursor - 1;
            var sb = new StringBuilder();
            while (m_cursor < m_line.Length)
            {
                if (m_line[m_cursor] == '"')
                {
                    m_cursor++;
                    token = sb.ToString();
                    return Token.String;
                }
                sb.Append(m_line[m_cursor++]);
            }
            throw new CommandException($"unterminated string at position {start}");
        }

        Token ScanIdent(int start, out string token)
        {
            while (m_cursor < m_line.Length && IsIdentRestChar(m_line[m_cursor]))
            {
                m_cursor++;
            }

            token = m_line.Substring(start, m_cursor - start);
            switch (token)
            {
                case "typeof":
                    return Token.Typeof;
                default:
                    return Token.Ident;
            }
        }

        bool IsIdentRestChar(char c)
        {
            return c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_';
        }

        Token ScanAtom(out string token)
        {
            ScanIdent(m_cursor, out token);
            return Token.Atom;
        }

        Token ScanInteger(out string token)
        {
            int start = m_cursor - 1;
            if (m_cursor < m_line.Length && m_line[m_cursor] == 'x')
            {
                m_cursor++;
                while (m_cursor < m_line.Length && IsHexChar(m_line[m_cursor]))
                {
                    m_cursor++;
                }
            }
            else
            {
                while (m_cursor < m_line.Length && IsDecimalChar(m_line[m_cursor]))
                {
                    m_cursor++;
                }
            }
            token = m_line.Substring(start, m_cursor - start);
            return Token.Integer;
        }

        bool IsHexChar(char c)
        {
            return c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F' || c >= '0' && c <= '9';
        }

        bool IsDecimalChar(char c)
        {
            return c >= '0' && c <= '9';
        }

        Token ScanVariable(out string token)
        {
            ScanInteger(out token);
            return Token.Variable;
        }

        Token ScanLessThan()
        {
            if (m_cursor < m_line.Length)
            {
                switch (m_line[m_cursor])
                {
                    case '<':
                        m_cursor++;
                        return Token.LeftShift;
                    case '=':
                        m_cursor++;
                        return Token.LessThanOrEqual;
                }
            }
            return Token.LessThan;
        }

        Token ScanGreaterThan()
        {
            if (m_cursor < m_line.Length)
            {
                switch (m_line[m_cursor])
                {
                    case '=':
                        m_cursor++;
                        return Token.GreaterThanOrEqual;
                    case '>':
                        m_cursor++;
                        if (m_line[m_cursor] == '>')
                        {
                            m_cursor++;
                            return Token.RightShift3;
                        }
                        return Token.RightShift;
                }
            }
            return Token.GreaterThan;
        }
    }
}
