/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    // Context-free syntax:
    //
    // <reference classifier file> ::= { <group> | <typespec rules> }*
    // <typespec rules> ::= <type spec> { { <rules keyword> }+ <field pattern or selector> }+ ";"
    // <rules keyword> ::= OWNS | OWNS_DYNAMIC | WEAK | EXTERNAL | <tag> | <tag condition>
    //
    // Tokens:
    //
    // <group> ::= "[" <non WS character>* "]"
    // <type spec> ::= <non WS character>+ ":" <non WS character>+
    // <tag> ::= "TAG(" { <non WS character>+ // "," }+ ")"
    // <tag condition> ::= ( "TAG_IF_ZERO" | "TAG_IF_NONZERO" ) "(" { <non WS character>+ // "," }+ ")"
    sealed class ReferenceClassifierParser
    {
        readonly string? m_groupNamePrefix;
        readonly ReferenceClassifierFileTokenizer m_tokenizer;
        readonly IEnumerator<(ReferenceClassifierFileTokenizer.Token token, string value)> m_enumerator;
        readonly Dictionary<string, List<Rule>> m_groupedRules;
        string m_groupName;

        ReferenceClassifierParser(string filename, string? groupNamePrefix, Dictionary<string, List<Rule>> groupedRules)
        {
            m_groupNamePrefix = groupNamePrefix;
            m_tokenizer = new(filename);
            m_enumerator = m_tokenizer.GetTokens().GetEnumerator();
            m_groupedRules = groupedRules;
            m_groupName = ReferenceClassifierGroup.ResolveGroupName(m_groupNamePrefix, groupName: null);
        }

        internal static void Load(string filename, string? groupNamePrefix, Dictionary<string, List<Rule>> groupedRules)
        {
            ReferenceClassifierParser parser = new(filename, groupNamePrefix, groupedRules);
            parser.Parse();
        }

        void Parse()
        {
            try
            {
                while (m_enumerator.MoveNext())
                {
                    if (m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.Import)
                    {
                        if (!m_enumerator.MoveNext() || m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.String)
                        {
                            m_tokenizer.ParseError("IMPORT directive needs to be followed by a string");
                        }

                        string? groupNamePrefix = m_groupName.Equals("anonymous", StringComparison.Ordinal) ? m_groupNamePrefix : m_groupName;
                        Load(m_enumerator.Current.value, groupNamePrefix, m_groupedRules);
                    }
                    else if (m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.Group)
                    {
                        string? declaredGroupName = m_enumerator.Current.value.Length > 0 ? m_enumerator.Current.value : null;
                        m_groupName = ReferenceClassifierGroup.ResolveGroupName(m_groupNamePrefix, declaredGroupName);
                    }
                    else if (m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.String)
                    {
                        TypeSpec typeSpec = TypeSpec.Parse(m_enumerator.Current.value);
                        if (!m_enumerator.MoveNext())
                        {
                            m_tokenizer.ParseError("unterminated rule");
                        }

                        do
                        {
                            ParseRules(typeSpec);
                        }
                        while (m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.Semicolon);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                m_tokenizer.ParseError(ex.Message);
            }
        }

        void ParseRules(TypeSpec typeSpec)
        {
            List<Func<string, Rule>> makeRules = new();
            while (true)
            {
                bool doneWithKeywords = false;
                string location = m_tokenizer.Location;
                switch (m_enumerator.Current.token)
                {
                    case ReferenceClassifierFileTokenizer.Token.Owns:
                    case ReferenceClassifierFileTokenizer.Token.OwnsDynamic:
                        {
                            bool isDynamic = m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.OwnsDynamic;
                            makeRules.Add(value => new OwnsRule(location, typeSpec, value, isDynamic: isDynamic));
                        }
                        break;
                    case ReferenceClassifierFileTokenizer.Token.Weak:
                        makeRules.Add(value => new WeakRule(location, typeSpec, value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.External:
                        makeRules.Add(value => new ExternalRule(location, typeSpec, value));
                        break;
                    case ReferenceClassifierFileTokenizer.Token.FuseWith:
                        // TODO: implement FUSE_WITH rule
                        m_tokenizer.ParseError($"{m_enumerator.Current.token} rule not yet implemented");
                        break;
                    case ReferenceClassifierFileTokenizer.Token.Tag:
                    case ReferenceClassifierFileTokenizer.Token.TagDynamic:
                        {
                            bool isDynamic = m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.TagDynamic;
                            string tag = m_enumerator.Current.value;
                            makeRules.Add(value => new TagSelectorRule(location, typeSpec, value, tag, isDynamic: isDynamic));
                        }
                        break;
                    case ReferenceClassifierFileTokenizer.Token.TagIfZero:
                    case ReferenceClassifierFileTokenizer.Token.TagIfNonZero:
                        {
                            string tag = m_enumerator.Current.value;
                            bool tagIfNonZero = m_enumerator.Current.token == ReferenceClassifierFileTokenizer.Token.TagIfNonZero;
                            makeRules.Add(value => new TagConditionRule(location, typeSpec, value, tag, tagIfNonZero: tagIfNonZero));
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
                        m_tokenizer.ParseError("type spec needs to be followed by at least one rule keyword");
                    }
                    break;
                }

                if (!m_enumerator.MoveNext())
                {
                    m_tokenizer.ParseError("unterminated rule");
                }
            }

            if (m_enumerator.Current.token != ReferenceClassifierFileTokenizer.Token.String)
            {
                m_tokenizer.ParseError("rule keywords must be followed by a field pattern or selector");
            }

            string value = m_enumerator.Current.value;
            foreach (Func<string, Rule> makeRule in makeRules)
            {
                AddRule(makeRule(value));
            }

            if (!m_enumerator.MoveNext())
            {
                m_tokenizer.ParseError("unterminated rule");
            }
        }

        void AddRule(Rule rule)
        {
            if (!m_groupedRules.TryGetValue(m_groupName, out List<Rule>? rules))
            {
                rules = new();
                m_groupedRules.Add(m_groupName, rules);
            }
            rules.Add(rule);
        }
    }
}
