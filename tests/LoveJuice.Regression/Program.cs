using System;
using LoveJuice;

internal static class Program {
    private static int checks;
    private static void Check(bool condition, string message) {
        checks++;
        if (!condition) throw new InvalidOperationException("LoveJuice: " + message);
    }

    public static int Main() {
        try {
            Check(StudioLiquidCatalog.Bundle == "studio/00.unity3d" &&
                StudioLiquidCatalog.Manifest == "studio00", "Verified installed bundle path/manifest");
            Check(StudioLiquidCatalog.Prefab(LoveJuiceChannel.Water) ==
                "p_koi_syu_LiquidWater_00_00", "Real Studio water item");
            Check(StudioLiquidCatalog.Prefab(LoveJuiceChannel.Saliva) ==
                "p_koi_syu_LiquidSio_00_00", "Real Studio light-liquid item");
            Check(StudioLiquidCatalog.Prefab(LoveJuiceChannel.Body) ==
                "p_koi_syu_LiquidSiru_00_00", "Real Studio viscous-liquid item");
            Check(StudioLiquidCatalog.Prefab(LoveJuiceChannel.Special) ==
                "p_koi_syu_LiquidKancho_00_00", "Real Studio heavy-liquid item");
            Check(StudioLiquidCatalog.Prefab(LoveJuiceChannel.Shower) ==
                "p_koi_syu_LiquidShower_01_00", "Real Studio shower-liquid item");
            Check(!StudioLiquidCatalog.Valid((LoveJuiceChannel)77), "Unknown item is not replaced by synthetic visuals");
            DropletMotion initial = new DropletMotion { Y = 1f, VY = 0f, VX = 1f };
            DropletMotion next = DropletPhysics.Step(initial, .03f, 9.8f, 0f);
            Check(next.Y < initial.Y && next.VY < 0f, "Gravity accelerates downward");
            Check(next.X > initial.X && next.Age == .03f, "Trajectory advances in world coordinates");
            DropletMotion upper = DropletPhysics.Step(initial, 1f, 9.8f, 0f);
            Check(upper.Age <= .051f, "Delta clamps load spikes");
            DropletMotion invalid = DropletPhysics.Step(initial, float.NaN, float.PositiveInfinity, float.NaN);
            Check(invalid.Age == 0f && invalid.Y == 1f, "Invalid data cannot produce NaN trajectory");
            for (int i = 0; i < 200; i++) next = DropletPhysics.Step(next, .03f, 8f, .9f);
            Check(!float.IsNaN(next.Y) && !float.IsInfinity(next.Y), "Trajectory remains finite");
            Console.WriteLine("LoveJuice native asset catalogue and ballistic assertions: " + checks + " passed.");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
