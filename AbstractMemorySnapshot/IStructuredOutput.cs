/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public interface IStructuredOutput
    {
        void AddProperty(string key, string value);

        void AddProperty(string key, long value);

        void AddDisplayString(string message);

        void AddDisplayString(string format, params object[] args);

        void AddDisplayStringLine(string message);

        void AddDisplayStringLine(string format, params object[] args);

        void AddDisplayStringLineIndented(int indent, string format, params object[] args);

        void BeginArray(string key);

        void BeginElement();

        void EndElement();

        void EndArray();

        void BeginChild(string key);

        void EndChild();
    }
}
