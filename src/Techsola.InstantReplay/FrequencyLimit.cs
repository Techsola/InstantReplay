using System;

namespace Techsola.InstantReplay
{
    internal readonly struct FrequencyLimit
    {
        public FrequencyLimit(uint maximumCount, TimeSpan withinDuration)
        {
            MaximumCount = maximumCount;
            WithinDuration = withinDuration;
        }

        public uint MaximumCount { get; }
        public TimeSpan WithinDuration { get; }

        public override string ToString() => $"Up to {MaximumCount} times within {WithinDuration}";
    }
}
