/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public sealed class ConsoleLogger : ILogger
    {
        public void Clear(string source)
        {
            // Cannot undo the fact that we already printed messages.
        }

        public void Log(string source, string context, string message)
        {
            Console.Error.WriteLine("{0}: {1}: {2}", source, context, message);
        }

        public void Flush(Action<string> writeLine)
        {
            // All messages have been flushed immediately.
        }
    }
}
