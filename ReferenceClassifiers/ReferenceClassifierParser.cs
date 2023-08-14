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
    // Context-free syntax:
    //
    // <reference classifier file> ::= { <group> | <typespec rules> }*
    // <typespec rules> ::= <type spec> { { <rules keyword> }+ <field pattern or selector> }+ ";"
    // <rules keyword> ::= OWNS | WEAK | EXTERNAL | <tag> | <tag condition>
    //
    // Tokens:
    //
    // <group> ::= "[" <non WS character>* "]"
    // <type spec> ::= <non WS character>+ ":" <non WS character>+
    // <tag> ::= "TAG(" { <non WS character>+ // "," }+ ")"
    // <tag condition> ::= ( "TAG_IF_ZERO" | "TAG_IF_NONZERO" ) "(" { <non WS character>+ // "," }+ ")"
    sealed class ReferenceClassifierParser
    {
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
                        if (!m_enumerator.MoveNext())
                        {
                            throw new FileFormatException("unterminated rule");
                        }

                        do
                        {
                            ParseRules(typeSpec);
                        }
                        while (m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.Semicolon);
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
            List<Func<string, Rule>> makeRules = new();
            while (true)
            {
                bool doneWithKeywords = false;
                switch (m_enumerator.Current.token)
                {
                    case ReferenceClassifierFileTokenizer.Token.Owns:
                        makeRules.Add(value => new OwnsRule(typeSpec, value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.Weak:
                        makeRules.Add(value => new WeakRule(typeSpec, value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.External:
                        makeRules.Add(value => new ExternalRule(typeSpec, value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.FuseWith:
                        // TODO: implement FUSE_WITH rule
                        throw new FileFormatException($"{m_enumerator.Current.token} rule not yet implemented");
                    case ReferenceClassifierFileTokenizer.Token.Tag:
                        {
                            string tag = m_enumerator.Current.value;
                            makeRules.Add(value => new TagSelectorRule(typeSpec, value, tag));
                        }
                        break;
                    case ReferenceClassifierFileTokenizer.Token.TagIfZero:
                    case ReferenceClassifierFileTokenizer.Token.TagIfNonZero:
                        {
                            string tag = m_enumerator.Current.value;
                            bool tagIfNonZero = m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.TagIfNonZero;
                            makeRules.Add(value => new TagConditionRule(typeSpec, value, tag, tagIfNonZero: tagIfNonZero));
                        }
                        break;
                    default:
                        doneWithKeywords = true;
                        break;
                }

                if (doneWithKeywords)
                {
                    if (makeRules.Count == 0)
                    {
                        throw new FileFormatException("type spec needs to be followed by at least one rule keyword");
                    }
                    break;
                }

                if (!m_enumerator.MoveNext())
                {
                    throw new FileFormatException("unterminated rule");
                }
            }

            if (m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.String)
            {
                throw new FileFormatException("rule keywords must be followed by a field pattern or selector");
            }

            string value = m_enumerator.Current.value;
            foreach (Func<string, Rule> makeRule in makeRules)
            {
                AddRule(makeRule(value));
            }

            if (!m_enumerator.MoveNext())
            {
                throw new FileFormatException("unterminated rule");
            }
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
