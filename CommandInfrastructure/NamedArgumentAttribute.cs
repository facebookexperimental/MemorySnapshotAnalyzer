// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NamedArgumentAttribute : Attribute
    {
        public NamedArgumentAttribute(string name)
        {
        }
    }
}
