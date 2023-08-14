/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public class InvalidSnapshotFormatException : Exception
    {
        public InvalidSnapshotFormatException(string message) : base(message) { }
    }
}
