// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    static class ReferenceClassifierLoader
    {
        static readonly char[] s_assemblySeparator = new char[] { ',', ':' };

        internal static Dictionary<string, List<Rule>> Load(string filename)
        {
            try
            {
                var rules = new List<Rule>();
                int lineNumber = 1;
                foreach (string line in File.ReadAllLines(filename))
                {
                    string lineTrimmed = line.Trim();
                    if (lineTrimmed.Length > 0 && !lineTrimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    {
                        int firstComma = lineTrimmed.IndexOfAny(s_assemblySeparator);
                        int lastComma = lineTrimmed.LastIndexOf(',');
                        if (firstComma < 0 || lastComma < 0 || firstComma == lastComma)
                        {
                            throw new FileFormatException($"invalid syntax on line {lineNumber}");
                        }

                        List<string> fieldPattern = ParseFieldPattern(lineTrimmed[(lastComma + 1)..].Trim(), lineNumber);
                        if (fieldPattern.Count == 1)
                        {
                            rules.Add(new FieldPatternRule
                            {
                                Spec = new ClassSpec
                                {
                                    Assembly = lineTrimmed[..firstComma].Trim(),
                                    ClassName = lineTrimmed[(firstComma + 1)..lastComma].Trim()
                                },
                                FieldPattern = fieldPattern[0]
                            });
                        }
                        else
                        {
                            rules.Add(new FieldPathRule
                            {
                                Spec = new ClassSpec
                                {
                                    Assembly = lineTrimmed[..firstComma].Trim(),
                                    ClassName = lineTrimmed[(firstComma + 1)..lastComma].Trim()
                                },
                                FieldNames = fieldPattern.ToArray()
                            }); ;
                        }
                    }

                    lineNumber++;
                }

                return new Dictionary<string, List<Rule>>
                {
                    { "defaultGroup", rules }
                };
            }
            catch (IOException ex)
            {
                throw new FileFormatException(ex.Message);
            }
        }

        static List<string> ParseFieldPattern(string fieldPattern, int lineNumber)
        {
            var pieces = new List<string>();
            int startIndex = 0;
            for (int i = 0; i < fieldPattern.Length; i++)
            {
                if (fieldPattern[i] == '[')
                {
                    if (i + 1 == fieldPattern.Length || fieldPattern[i + 1] != ']')
                    {
                        throw new FileFormatException($"invalid field pattern syntax on line {lineNumber}; '[' must be immediately followed by ']'");
                    }

                    if (i > startIndex)
                    {
                        pieces.Add(fieldPattern[startIndex..i]);
                    }

                    pieces.Add("[]");
                    startIndex = i + 2;
                    i++;
                }
                else if (fieldPattern[i] == '.')
                {
                    if (i > startIndex)
                    {
                        pieces.Add(fieldPattern[startIndex..i]);
                    }

                    startIndex = i + 1;
                }
            }

            if (startIndex != fieldPattern.Length)
            {
                pieces.Add(fieldPattern[startIndex..]);
            }

            return pieces;
        }
    }
}
