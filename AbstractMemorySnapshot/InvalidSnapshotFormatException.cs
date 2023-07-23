﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public class InvalidSnapshotFormatException : Exception
    {
        public InvalidSnapshotFormatException(string message) : base(message) { }
    }
}