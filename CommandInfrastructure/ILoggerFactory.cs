/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public interface ILoggerFactory
    {
        public ILogger MakeLogger();
    }

    public sealed class MemoryLoggerFactory : ILoggerFactory
    {
        public ILogger MakeLogger()
        {
            return new MemoryLogger();
        }
    }

    public sealed class ConsoleLoggerFactory : ILoggerFactory
    {
        public ILogger MakeLogger()
        {
            return new ConsoleLogger();
        }
    }
}
