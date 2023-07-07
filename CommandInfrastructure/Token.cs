﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.CommandProcessing
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
