/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using NUnit.Framework;

namespace MemorySnapshotAnalyzer.ReferenceClassifiersTests
{
    [TestFixture]
    public sealed class ReferenceClassifierTokenizerTest
    {
        [Test]
        public void TestTokenizer()
        {
            ReferenceClassifierTokenizer tokenizer = new("test_tokenizer.rcl");

            IEnumerator<(ReferenceClassifierTokenizer.Token token, string value)> tokens = tokenizer.GetTokens().GetEnumerator();
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Group, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Group, "group.name");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.String, @"this\is\a\string");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Semicolon, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Semicolon, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Import, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.String, "test_tokenizer_import.rcl");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Owns, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Owns, "0");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Owns, "1");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Owns, "-1");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.OwnsDynamic, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.OwnsDynamic, "765");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.External, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.FuseWith, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Tag, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Tag, "myTag");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.Tag, "1,2");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.TagDynamic, "");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.TagDynamic, "tag");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.TagIfZero, "zero_tag");
            ExpectToken(tokens, ReferenceClassifierTokenizer.Token.TagIfNonZero, "nonzero");
            Assert.That(tokens.MoveNext(), Is.False);
        }

        void ExpectToken(IEnumerator<(ReferenceClassifierTokenizer.Token token, string value)> tokens, ReferenceClassifierTokenizer.Token expectedToken, string expectedValue)
        {
            Assert.That(tokens.MoveNext(), Is.True);
            Assert.That(tokens.Current.token, Is.EqualTo(expectedToken));
            Assert.That(tokens.Current.value, Is.EqualTo(expectedValue));
        }
    }
}
