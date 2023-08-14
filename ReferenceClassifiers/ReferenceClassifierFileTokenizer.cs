/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    sealed class ReferenceClassifierFileTokenizer
    {
        internal enum Token
        {
            Group = 1,
            String,
            Semicolon,
            Owns,
            Weak,
            External,
            FuseWith,
            Tag,
            TagIfZero,
            TagIfNonZero,
        }

        static readonly Dictionary<string, Token> s_keywords = new()
        {
            { ";", Token.Semicolon },
            { "OWNS", Token.Owns },
            { "WEAK", Token.Weak },
            { "EXTERNAL", Token.External },
            { "FUSE_WITH", Token.FuseWith },
        };

        readonly string m_filename;
        int m_lineNumber;

        internal ReferenceClassifierFileTokenizer(string filename)
        {
            m_filename = filename;
            m_lineNumber = 1;
        }

        internal int LineNumber => m_lineNumber;

        internal IEnumerable<(Token token, string value)> GetTokens()
        {
            foreach (string line in File.ReadAllLines(m_filename))
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

                    if (wordTrimmed.Length > 2 && wordTrimmed[0] == '[' && wordTrimmed[^1] == ']')
                    {
                        string value = wordTrimmed[1..^1];
                        yield return (Token.Group, value);
                    }
                    else if (wordTrimmed.Length >= 2 && wordTrimmed[0] == '"' && wordTrimmed[^1] == '"')
                    {
                        string value = wordTrimmed[1..^1];
                        yield return (Token.String, value);
                    }
                    else if (s_keywords.TryGetValue(wordTrimmed, out Token token))
                    {
                        yield return (token, string.Empty);
                    }
                    else if (wordTrimmed.StartsWith("TAG(") && wordTrimmed[^1] == ')')
                    {
                        string value = wordTrimmed["TAG(".Length..^1];
                        yield return (Token.Tag, value);
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
                    else
                    {
                        throw new FileFormatException($"unrecognized token \"{wordTrimmed}\"");
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
