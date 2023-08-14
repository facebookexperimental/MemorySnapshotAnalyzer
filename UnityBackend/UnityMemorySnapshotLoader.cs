/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.IO;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    public sealed class UnityMemorySnapshotLoader : MemorySnapshotLoader
    {
        public override MemorySnapshot? TryLoad(string filename)
        {
            var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var memorySnapshotFile = new UnityMemorySnapshotFile(filename, fileStream);
            if (memorySnapshotFile.CheckSignature())
            {
                try
                {
                    return memorySnapshotFile.Load();
                }
                catch (Exception)
                {
                    memorySnapshotFile.Dispose();
                    throw;
                }
            }

            memorySnapshotFile.Dispose();
            return null;
        }
    }
}
