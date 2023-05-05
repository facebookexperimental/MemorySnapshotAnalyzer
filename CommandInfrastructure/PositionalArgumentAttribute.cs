using System;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PositionalArgumentAttribute : Attribute
    {
        public PositionalArgumentAttribute(int index, bool optional)
        {
        }
    }
}
