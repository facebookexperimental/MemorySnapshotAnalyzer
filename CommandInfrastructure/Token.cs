/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    enum Token
    {
        Atom,
        Variable,
        Integer,
        String,
        Ident,
        LessThan,
        GreaterThan,
        Dot,
        OpenBracket,
        CloseBracket,
        Comma,
        At,
        Star,
        OpenParen,
        CloseParen,
        Minus,
        Tilde,
        Percent,
        Slash,
        Plus,
        RightShift3,
        RightShift,
        LeftShift,
        LessThanOrEqual,
        GreaterThanOrEqual,
        Ampersand,
        Hat,
        Or,
        Typeof,
        Eof,
    }
}
