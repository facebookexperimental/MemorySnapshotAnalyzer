using System;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NamedArgumentAttribute : Attribute
    {
        public NamedArgumentAttribute(string name)
        {
        }
    }
}
