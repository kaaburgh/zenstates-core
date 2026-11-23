using System;
using System.Collections.Generic;
using ZenStates.Core.Hardware.Apob;

internal static class Program
{
    private static int assertions;

    private static int Infer(uint[] clocks, bool[] active, int channels)
    {
        return ApobTimingBlockGrouping.InferStatesPerChannel(clocks, active, channels);
    }

    private static void Expect(int actual, int expected, string scenario)
    {
        assertions++;
        if (actual != expected)
            throw new Exception("FAIL: " + scenario + " (expected " + expected + ", got " + actual + ")");
    }

    private static void Main()
    {
        // Owner's BD790i X3D: system configuration has two physical channels;
        // GEN timings has four records [3000,1600] x 2, with P0 active.
        var memClks = new uint[] { 3000, 1600, 3000, 1600 };
        var active = new bool[] { true, false, true, false };
        Expect(Infer(memClks, active, 2), 2, "Zen4 desktop two channels by two P-states");

        int states = Infer(memClks, active, 2);
        Expect(0 / states, 0, "block 0 is physical Ch A");
        Expect(1 / states, 0, "block 1 remains physical Ch A, P1");
        Expect(2 / states, 1, "block 2 is physical Ch B, P0");
        Expect(3 / states, 1, "block 3 remains physical Ch B, P1");
        Expect(3 % states, 1, "block 3 has second memory P-state");

        Expect(Infer(new uint[] { 3000, 2600, 1600, 3000, 2600, 1600 },
                     new bool[] { true, false, false, true, false, false }, 2),
               3, "two channels by three matching P-states");
        Expect(Infer(new uint[] { 2400, 2400, 1000, 2400, 2400, 1000 },
                     new bool[] { false, true, false, false, true, false }, 2),
               3, "duplicate clocks permitted if an independent slower state exists");

        Expect(Infer(new uint[] { 3000, 1600 }, new bool[] { true, true }, 2),
               0, "ordinary two physical channels never grouped");
        Expect(Infer(memClks, active, 4), 0, "four physical-channel records preclude grouping");
        Expect(Infer(memClks, active, 3), 0, "non-divisible physical channel count");
        Expect(Infer(memClks, active, 0), 0, "unknown channel count");
        Expect(Infer(new uint[] { 3000, 1600, 3000, 1500 }, active, 2),
               0, "different P-state frequency sequence across channels");
        Expect(Infer(new uint[] { 3000, 1600, 1600, 3000 }, active, 2),
               0, "interleaved per-channel ordering not silently guessed");
        Expect(Infer(new uint[] { 3000, 3000, 3000, 3000 }, active, 2),
               0, "equal clocks cannot distinguish P-states from channels");
        Expect(Infer(new uint[] { 1600, 3000, 1600, 3000 }, active, 2),
               0, "ascending order rejected");
        Expect(Infer(memClks, new bool[] { true, false, false, false }, 2),
               0, "incomplete active-channel evidence rejected");
        Expect(Infer(memClks, new bool[] { true, true, true, true }, 2),
               0, "multiple active blocks per channel rejected");
        Expect(Infer(memClks, new bool[] { true, false, false, true }, 2),
               0, "different active P-state index between channels rejected");
        Expect(Infer(null, active, 2), 0, "missing clocks fail closed");
        Expect(Infer(memClks, null, 2), 0, "missing active flags fail closed");
        Expect(Infer(memClks, new bool[] { true }, 2), 0, "misaligned arrays fail closed");
        Expect(Infer(new uint[] { 3000, 1600, 0, 3000, 1600, 0 },
                     new bool[] { true, false, false, true, false, false }, 2),
               0, "zero-speed P-state rejected");
        Expect(Infer(new uint[] { 3000, 2500, 2000, 1600, 1200,
                                  3000, 2500, 2000, 1600, 1200 },
                     new bool[] { true, false, false, false, false,
                                  true, false, false, false, false }, 2),
               0, "more than four unknown P-states not inferred");

        Console.WriteLine("OK: " + assertions + " APOB channel grouping assertions");
    }
}
