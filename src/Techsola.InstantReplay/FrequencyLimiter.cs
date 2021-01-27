using System;
using System.Diagnostics;

namespace Techsola.InstantReplay
{
    internal sealed class FrequencyLimiter
    {
        private readonly long stopwatchTimestampDuration;
        private readonly long?[] occurrences;
        private int nextIndex;

        public FrequencyLimiter(FrequencyLimit limit)
        {
            stopwatchTimestampDuration = (limit.WithinDuration.Ticks / TimeSpan.TicksPerSecond) * Stopwatch.Frequency;
            occurrences = new long?[limit.MaximumCount];
        }

        public bool TryAddOccurrence(long stopwatchTimestamp)
        {
            if (stopwatchTimestampDuration == 0 || occurrences.Length == 0) return false;

            if (occurrences[(nextIndex == 0 ? occurrences.Length : nextIndex) - 1] is { } previousTimestamp
                && stopwatchTimestamp < previousTimestamp)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stopwatchTimestamp),
                    stopwatchTimestamp,
                    "The stopwatch timestamp must not be earlier than the last reported timestamp.");
            }

            if (occurrences[nextIndex] is { } oldestTimestamp
                && (stopwatchTimestamp - oldestTimestamp) < stopwatchTimestampDuration)
            {
                return false;
            }

            occurrences[nextIndex] = stopwatchTimestamp;
            nextIndex++;
            if (nextIndex == occurrences.Length) nextIndex = 0;
            return true;
        }
    }
}
