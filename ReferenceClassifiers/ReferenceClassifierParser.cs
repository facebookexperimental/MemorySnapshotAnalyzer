﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    sealed class ReferenceClassifierParser
    {
        static readonly char[] s_assemblySeparator = new char[] { ',', ':' };

        readonly ReferenceClassifierFileTokenizer m_tokenizer;
        readonly IEnumerator<(ReferenceClassifierFileTokenizer.Token token, string value)> m_enumerator;
        readonly Dictionary<string, List<Rule>> m_result;
        string m_groupName;

        ReferenceClassifierParser(string filename)
        {
            m_tokenizer = new(filename);
            m_enumerator = m_tokenizer.GetTokens().GetEnumerator();
            m_result = new();
            m_groupName = "anonymous";
        }

        internal static Dictionary<string, List<Rule>> Load(string filename)
        {
            try
            {
                ReferenceClassifierParser loader = new(filename);
                loader.Parse();
                return loader.m_result;
            }
            catch (IOException ex)
            {
                throw new FileFormatException(ex.Message);
            }
        }

        void Parse()
        {
            try
            {
                while (m_enumerator.MoveNext())
                {
                    if (m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.Group)
                    {
                        m_groupName = m_enumerator.Current.value;
                    }
                    else if (m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.String)
                    {
                        TypeSpec typeSpec = TypeSpec.Parse(m_enumerator.Current.value);
                        if (m_enumerator.MoveNext())
                        {
                            ParseRules(typeSpec);
                        }
                        else
                        {
                            throw new FileFormatException("found EOF after type spec");
                        }
                    }
                }
            }
            catch (FileFormatException ex)
            {
                throw new FileFormatException($"{ex.Message} on line {m_tokenizer.LineNumber}");
            }
        }

        void ParseRules(TypeSpec typeSpec)
        {
            do
            {
                switch (m_enumerator.Current.token)
                {
                    case ReferenceClassifierFileTokenizer.Token.Owns:
                        if (!m_enumerator.MoveNext() || m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.String)
                        {
                            throw new FileFormatException("OWNS must be followed by field pattern or selector");
                        }
                        AddRule(OwnsRule.Parse(typeSpec, m_enumerator.Current.value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.Weak:
                        if (!m_enumerator.MoveNext() || m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.String)
                        {
                            throw new FileFormatException("WEAK must be followed by field pattern");
                        }
                        AddRule(new WeakRule(typeSpec, m_enumerator.Current.value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.External:
                        if (!m_enumerator.MoveNext() || m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.String)
                        {
                            throw new FileFormatException("EXTERNAL must be followed by field pattern");
                        }
                        AddRule(new ExternalRule(typeSpec, m_enumerator.Current.value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.FuseWith:
                        // TODO: implement FUSE_WITH rule
                        throw new FileFormatException($"{m_enumerator.Current.token} rule not yet implemented");
                    case ReferenceClassifierFileTokenizer.Token.TagIfZero:
                    case ReferenceClassifierFileTokenizer.Token.TagIfNonZero:
                        {
                            string tag = m_enumerator.Current.value;
                            bool tagIfNonZero = m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.TagIfNonZero;
                            if (!m_enumerator.MoveNext() || m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.String)
                            {
                                throw new FileFormatException("TAG_IF_*(...) must be followed by field pattern");
                            }
                            AddRule(new TagRule(typeSpec, m_enumerator.Current.value, tag, tagIfNonZero: tagIfNonZero));
                        }
                        break;
                    default:
                        throw new FileFormatException($"unexpected token {m_enumerator.Current.token}");
                }

                if (!m_enumerator.MoveNext())
                {
                    throw new FileFormatException("unterminated rule");
                }
            }
            while (m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.Semicolon);
        }

        void AddRule(Rule rule)
        {
            if (!m_result.TryGetValue(m_groupName, out List<Rule>? rules))
            {
                rules = new();
                m_result.Add(m_groupName, rules);
            }
            rules.Add(rule);
        }
    }
}