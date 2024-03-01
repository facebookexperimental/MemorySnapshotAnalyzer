/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public sealed class PassthroughStructuredOutput : IStructuredOutput
    {
        readonly IOutput m_output;

        public PassthroughStructuredOutput(IOutput output)
        {
            m_output = output;
        }

        public void AddProperty(string key, string value)
        {
        }

        public void AddProperty(string key, long value)
        {
        }

        public void AddDisplayString(string message)
        {
            m_output.Write(message);
        }

        public void AddDisplayString(string format, params object[] args)
        {
            m_output.Write(format, args);
        }

        public void AddDisplayStringLine(string message)
        {
            m_output.WriteLine(message);
        }

        public void AddDisplayStringLine(string format, params object[] args)
        {
            m_output.WriteLine(format, args);
        }

        public void AddDisplayStringLineIndented(int indent, string format, params object[] args)
        {
            m_output.WriteLineIndented(indent, format, args);
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
