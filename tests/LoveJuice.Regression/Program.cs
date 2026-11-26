using System;
using LoveJuice;

internal static class Program {
    private static int checks;
    private static void Check(bool condition, string message) {
        checks++;
        if (!condition) throw new InvalidOperationException("LoveJuice: " + message);
    }

    private sealed class LegacySkinEffectsFixture {
        internal int Writes;
        private int _drool = 2;
        public int DroolLevel { get { return _drool; } set { Writes++; _drool = value; } }
        public int SalivaLevel { get { return 3; } }
        public int SweatLevel { get { return 1; } }
        public int PussyJuiceLevel { get { return 0; } }
        public int TearLevel { get { throw new InvalidOperationException("unavailable"); } }
    }
    private static void CheckLegacySkinEffects() {
        Check(SkinEffectValueReader.LegacyProperty("DroolFace") == "DroolLevel" &&
              SkinEffectValueReader.LegacyProperty("SalivaFace") == "SalivaLevel" &&
              SkinEffectValueReader.LegacyProperty("WetBody") == "SweatLevel",
              "Installed SkinEffects property mappings");
        var fixture = new LegacySkinEffectsFixture();
        int level;
        Check(SkinEffectValueReader.TryReadLegacy(fixture, "DroolFace", out level) && level == 2,
              "Read installed legacy drool property");
        Check(SkinEffectValueReader.TryReadLegacy(fixture, "SalivaFace", out level) && level == 3,
              "Read installed legacy saliva property");
        Check(SkinEffectValueReader.TryReadLegacy(fixture, "WetBody", out level) && level == 1,
              "Read installed legacy sweat property");
        Check(SkinEffectValueReader.TryReadLegacy(fixture, "PussyJuiceBody", out level) && level == 0,
              "Explicit zero is distinct from absent data");
        Check(!SkinEffectValueReader.TryReadLegacy(fixture, "Unknown", out level) && level == -1,
              "Unknown source does not become zero");
        Check(!SkinEffectValueReader.TryReadLegacy(fixture, "TearFace", out level) && level == -1,
              "Unreadable property is not mistaken for zero");
        Check(fixture.Writes == 0, "Readback never writes external SkinEffects state");
    }

    private static void CheckLiquidLevelEvents() {
        var events = new LiquidLevelEvents();
        LiquidLevelBursts first = events.Observe(3, 2, 2);
        Check(first.Face == 0 && first.Body == 0 && first.Special == 0,
            "A loaded card does not cause an initial burst.");
        LiquidLevelBursts changed = events.Observe(4, 5, 3);
        Check(changed.Face == 1 && changed.Body == 3 && changed.Special == 1,
            "A real level increase triggers three independently bounded channels.");
        changed = events.Observe(4, 5, 3);
        Check(changed.Face == 0 && changed.Body == 0 && changed.Special == 0,
            "Unchanged level never retriggers.");
        changed = events.Observe(0, 1, 0);
        Check(changed.Face == 0 && changed.Body == 0 && changed.Special == 0,
            "Resetting a visual overlay does not emit.");
        changed = events.Observe(255, 255, 255);
        Check(changed.Face == 3 && changed.Body == 3 && changed.Special == 3,
            "Large jumps remain bounded by the native particle budget.");
        events.Reset();
        changed = events.Observe(1, 2, 3);
        Check(changed.Face == 0 && changed.Body == 0 && changed.Special == 0,
            "New scene re-establishes a clean baseline.");
    }

    public static int Main() {
        try {
            CheckLegacySkinEffects();
            CheckLiquidLevelEvents();
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
