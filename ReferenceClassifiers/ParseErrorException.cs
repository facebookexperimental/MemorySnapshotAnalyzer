/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.IO;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public class ParseErrorException : IOException
    {
        public ParseErrorException(string message, string filename, int lineNumber)
            : base($"ERROR: {filename}:{lineNumber}: {message}")
        {
        }
    }
}
