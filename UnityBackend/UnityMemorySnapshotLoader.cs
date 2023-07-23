﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

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