/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class ReferenceClassifierTokenizer
    {
        public enum Token
        {
            Group = 1,
            String,
            Regex,
            Semicolon,
            Import,
            Owns,
            OwnsDynamic,
            External,
            FuseWith,
            Tag,
            TagDynamic,
            TagIfZero,
            TagIfNonZero,
        }

        static readonly Dictionary<string, Token> s_keywords = new()
        {
            { ";", Token.Semicolon },
            { "IMPORT", Token.Import },
            { "OWNS", Token.Owns },
            { "OWNS_DYNAMIC", Token.OwnsDynamic },
            { "EXTERNAL", Token.External },
            { "FUSE_WITH", Token.FuseWith },
        };

        readonly string m_filename;
        int m_lineNumber;

        public ReferenceClassifierTokenizer(string filename)
        {
            m_filename = filename;
            m_lineNumber = 1;
        }

        public string Location => $"{m_filename}:{m_lineNumber}";

        [DoesNotReturn]
        public void ParseError(string message)
        {
            throw new ParseErrorException(message, m_filename, m_lineNumber);
        }

        string[] ReadFile()
        {
            try
            {
                return File.ReadAllLines(m_filename);
            }
            catch
            {
                string altFilename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, m_filename);
                if (altFilename != m_filename)
                {
                    try
                    {
                        return File.ReadAllLines(altFilename);
                    }
                    catch
                    {
                    }
                }
                throw;
            }
        }

        public IEnumerable<(Token token, string value)> GetTokens()
        {
            foreach (string line in ReadFile())
            {
                string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    if (word[0] == '#')
                    {
                        // The rest of the line is a comment.
                        break;
                    }

                    bool terminated = word[^1] == ';';
                    string wordTrimmed = terminated ? word[..^1] : word;

                    if (wordTrimmed.Length >= 2 && wordTrimmed[0] == '[' && wordTrimmed[^1] == ']')
                    {
                        string value = wordTrimmed[1..^1];
                        yield return (Token.Group, value);
                    }
                    else if (wordTrimmed.Length >= 2 && wordTrimmed[0] == '"' && wordTrimmed[^1] == '"')
                    {
                        string value = wordTrimmed[1..^1];
                        yield return (Token.String, value);
                    }
                    else if (wordTrimmed.Length >= 2 && wordTrimmed[0] == '/' && wordTrimmed[^1] == '/')
                    {
                        string value = wordTrimmed[1..^1];
                        yield return (Token.Regex, value);
                    }
                    else if (s_keywords.TryGetValue(wordTrimmed, out Token token))
                    {
                        yield return (token, string.Empty);
                    }
                    else if (wordTrimmed.StartsWith("OWNS(") && wordTrimmed[^1] == ')')
                    {
                        string value = wordTrimmed["OWNS(".Length..^1];
                        yield return (Token.Owns, value);
                    }
                    else if (wordTrimmed.StartsWith("OWNS_DYNAMIC(") && wordTrimmed[^1] == ')')
                    {
                        string value = wordTrimmed["OWNS_DYNAMIC(".Length..^1];
                        yield return (Token.OwnsDynamic, value);
                    }
                    else if (wordTrimmed.StartsWith("TAG(") && wordTrimmed[^1] == ')')
                    {
                        string value = wordTrimmed["TAG(".Length..^1];
                        yield return (Token.Tag, value);
                    }
                    else if (wordTrimmed.StartsWith("TAG_DYNAMIC(") && wordTrimmed[^1] == ')')
                    {
                        string value = wordTrimmed["TAG_DYNAMIC(".Length..^1];
                        yield return (Token.TagDynamic, value);
                    }
                    else if (wordTrimmed.StartsWith("TAG_IF_ZERO(") && wordTrimmed[^1] == ')')
                    {
                        string value = wordTrimmed["TAG_IF_ZERO(".Length..^1];
                        yield return (Token.TagIfZero, value);
                    }
                    else if (wordTrimmed.StartsWith("TAG_IF_NONZERO(") && wordTrimmed[^1] == ')')
                    {
                        string value = wordTrimmed["TAG_IF_NONZERO(".Length..^1];
                        yield return (Token.TagIfNonZero, value);
                    }
                    else if (wordTrimmed.Length > 0)
                    {
                        ParseError($"unrecognized token \"{wordTrimmed}\"");
                    }

                    if (terminated)
                    {
                        yield return (Token.Semicolon, string.Empty);
                    }
                }

                m_lineNumber++;
            }
        }
    }
}
