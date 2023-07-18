// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public class CommandException : Exception
    {
        public CommandException(string message) : base(message) { }
    }
}
