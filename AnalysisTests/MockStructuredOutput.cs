/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.AnalysisTests
{
    internal sealed class MockStructuredOutput : IStructuredOutput
    {
        internal MockStructuredOutput()
        {
        }

        public void AddProperty(string key, string value)
        {
        }

        public void AddProperty(string key, long value)
        {
        }

        public void AddProperty(string key, bool value)
        {
        }

        public void AddDisplayString(string message)
        {
        }

        public void AddDisplayString(string format, params object[] args)
        {
        }

        public void AddDisplayStringLine(string message)
        {
        }

        public void AddDisplayStringLine(string format, params object[] args)
        {
        }

        public void AddDisplayStringLineIndented(int indent, string format, params object[] args)
        {
        }

        public void BeginArray(string key)
        {
        }

        public void BeginElement()
        {
        }

        public void EndElement()
        {
        }

        public void EndArray()
        {
        }

        public void BeginChild(string key)
        {
        }

        public void EndChild()
        {
        }
    }
}
