using System;
using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// Infers repeated memory P-state blocks only when the independent MEM type-25
    /// channel count and the running memory clock corroborate the arrangement.
    /// The GEN type-3 parser cannot derive physical channels from block count
    /// alone: some desktop AGESA versions store multiple memory P-states per
    /// physical channel without publishing a fixed P-state stride.
    /// </summary>
    internal static class ApobTimingBlockGrouping
    {
        /// <summary>
        /// Returns the number of P-state records per physical channel, or zero
        /// when there is insufficient or contradictory evidence. No records are
        /// modified; callers retain the original per-block data and offsets.
        /// </summary>
        public static int InferStatesPerChannel(
            IList<uint> memClks, IList<bool> active, int physicalChannelCount)
        {
            if (memClks == null || active == null || memClks.Count != active.Count
                || physicalChannelCount < 2 || memClks.Count <= physicalChannelCount
                || memClks.Count % physicalChannelCount != 0)
                return 0;

            int states = memClks.Count / physicalChannelCount;
            // No general rule is justified for larger undocumented layouts.
            if (states < 2 || states > 4)
                return 0;

            int activeCount = 0;
            for (int i = 0; i < active.Count; i++)
                if (active[i]) activeCount++;
            // A complete active P-state for each independently identified channel
            // is required before assigning timing blocks to physical channels.
            if (activeCount != physicalChannelCount)
                return 0;

            bool differentSpeeds = false;
            int activeInFirstGroup = 0;
            for (int state = 0; state < states; state++)
            {
                if (memClks[state] == 0)
                    return 0;
                if (active[state])
                    activeInFirstGroup++;
                if (state > 0)
                {
                    if (memClks[state] > memClks[state - 1])
                        return 0;
                    if (memClks[state] < memClks[state - 1])
                        differentSpeeds = true;
                }
            }
            if (!differentSpeeds || activeInFirstGroup != 1)
                return 0;

            for (int channel = 1; channel < physicalChannelCount; channel++)
            {
                for (int state = 0; state < states; state++)
                {
                    int i = channel * states + state;
                    if (memClks[i] != memClks[state] || active[i] != active[state])
                        return 0;
                }
            }

            return states;
        }
    }
}
