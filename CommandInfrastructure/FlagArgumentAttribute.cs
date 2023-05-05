// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FlagArgumentAttribute : Attribute
    {
        public FlagArgumentAttribute(string name)
        {
        }
    }
}
