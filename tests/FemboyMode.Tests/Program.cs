using System;
using FemboyMode;

internal static class Program
{
    private static int failures;

    private static void Check(bool condition, string description)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL: " + description);
    }

    private static int Main()
    {
        Check(FemboyRules.FrontInsertStartIndex(false) == 0,
            "normal initiative uses buttons 0/1");
        Check(FemboyRules.FrontInsertStartIndex(true) == 7,
            "female initiative uses buttons 7/8");
        Check(FemboyRules.HasBothFrontButtons(2, 0),
            "two buttons enough on the first page");
        Check(!FemboyRules.HasBothFrontButtons(1, 0),
            "one button cannot contain both actions");
        Check(FemboyRules.HasBothFrontButtons(9, 7),
            "nine buttons enough for female-initiative page");
        Check(!FemboyRules.HasBothFrontButtons(8, 7),
            "eight buttons cannot contain both actions");
        Check(!FemboyRules.HasBothFrontButtons(16, -1),
            "negative action index is rejected");

        Check(FemboyRules.SafeBoneScale(-2f) == 0.5f,
            "clamp small scales");
        Check(FemboyRules.SafeBoneScale(7f) == 2f,
            "clamp large scales");
        Check(FemboyRules.SafeBoneScale(1.25f) == 1.25f,
            "preserve valid scales");
        Check(FemboyRules.SafeBoneScale(float.NaN) == 1f,
            "bad card NaN uses neutral scaling");
        Check(FemboyRules.SafeBoneScale(float.PositiveInfinity) == 1f,
            "infinity uses neutral scaling");
        Check(FemboyRules.SafeBoneScale(float.NegativeInfinity) == 1f,
            "negative infinity uses neutral scaling");

        long sceneA = FemboyRules.EffectKey(123, 0);
        Check(sceneA != FemboyRules.EffectKey(123, 1),
            "separate heroines have separate effect keys");
        Check(sceneA != FemboyRules.EffectKey(124, 0),
            "separate scenes have separate effect keys");

        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " headless rules tests failed.");
            return 1;
        }

        Console.WriteLine("All 15 FemboyMode rules assertions passed.");
        return 0;
    }
}
