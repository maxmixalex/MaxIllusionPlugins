using System;

namespace LoveJuice {
    public enum LoveJuiceChannel { Water = 0, Saliva = 1, Body = 2, Shower = 3, Milk = 4, Special = 5, Intimate = 6, MalePre = 7 }

    // Identifiers verified against the installed abdata/studio/info/00.unity3d
    // item catalogue. Do not ship or embed the original game's assets.
    internal static class StudioLiquidCatalog {
        internal const string Bundle = "studio/00.unity3d";
        internal const string Manifest = "studio00";

        internal static string Prefab(LoveJuiceChannel channel) {
            switch (channel) {
                case LoveJuiceChannel.Water: return "p_koi_syu_LiquidWater_00_00";
                case LoveJuiceChannel.Saliva: return "p_koi_syu_LiquidSio_00_00";
                case LoveJuiceChannel.Body: return "p_koi_syu_LiquidSiru_00_00";
                case LoveJuiceChannel.Intimate: return "p_koi_syu_LiquidSiru_00_00";
                case LoveJuiceChannel.MalePre: return "p_koi_syu_LiquidWater_00_00";
                case LoveJuiceChannel.Shower: return "p_koi_syu_LiquidWater_00_00";
                case LoveJuiceChannel.Milk: return "p_koi_syu_LiquidWater_00_00";
                case LoveJuiceChannel.Special: return "p_koi_syu_LiquidKancho_00_00";
                default: return null;
            }
        }
        internal static bool Valid(LoveJuiceChannel channel) {
            return Prefab(channel) != null;
        }
    }

    internal struct DropletMotion {
        internal float X, Y, Z, VX, VY, VZ, Age;
    }

    // Fixed-step-compatible ballistic integration. Used for both the actual
    // Studio prefab transform and isolated C# regression tests.
    internal static class DropletPhysics {
        internal static float Clamp(float v, float low, float high) {
            return float.IsNaN(v) || float.IsInfinity(v) ? low : Math.Max(low, Math.Min(high, v));
        }

        internal static DropletMotion Step(DropletMotion state, float delta, float gravity, float damping) {
            float dt = Clamp(delta, 0f, .05f);
            float g = Clamp(gravity, 0f, 30f), d = Clamp(damping, 0f, 10f);
            // Semi-implicit Euler is stable for streaming Unity frames;
            // split long frames at the caller to avoid large jumps.
            float drag = (float)Math.Exp(-d * dt);
            state.VX *= drag;
            state.VY = (state.VY - g * dt) * drag;
            state.VZ *= drag;
            state.X += state.VX * dt;
            state.Y += state.VY * dt;
            state.Z += state.VZ * dt;
            state.Age += dt;
            return state;
        }
    }
}
