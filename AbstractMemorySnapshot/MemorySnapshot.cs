/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class MemorySnapshot : IDisposable
    {
        protected MemorySnapshot() {}

        public abstract void Dispose();

        public abstract string Filename { get; }

        public abstract string Format { get; }

        public abstract Native Native { get; }

        public abstract TraceableHeap ManagedHeap(ReferenceClassifierFactory referenceClassifierFactory);

        public abstract TraceableHeap NativeHeap(ReferenceClassifierFactory referenceClassifierFactory);
    }
}
