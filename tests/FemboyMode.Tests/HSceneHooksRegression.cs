
using System;
using System.Reflection;
using UnityEngine.UI;

namespace FemboyMode {
    // Exercises the *actual source* of the Harmony postfix and both
    // prefixes against fake HSprite buttons. This is NOT a Unity game test.
    internal static class HSceneHooksRegression {
        private static readonly BindingFlags Hooks =
            BindingFlags.NonPublic | BindingFlags.Static;

        private static bool InvokePrefix(string name, HSprite sprite) {
            MethodInfo method = typeof(FemboyHSceneHooks).GetMethod(name, Hooks);
            Require(method != null, "missing handler " + name);
            return (bool)method.Invoke(null, new object[] { sprite });
        }

        private static void Apply(HSprite sprite) {
            MethodInfo method = typeof(FemboyHSceneHooks).GetMethod(
                "RestrictFrontOptions", Hooks);
            Require(method != null, "missing Sonyu restriction postfix");
            method.Invoke(null, new object[] { sprite });
        }

        internal static bool NativeFrontAllowed(HSprite sprite) {
            return InvokePrefix("PreventNonAnalAction", sprite);
        }

        private static void Require(bool result, string why) {
            if (!result) throw new InvalidOperationException(
                "Femboy Sonyu fallback regression: " + why);
        }

        internal static void Run() {
            var sprite = new HSprite();
            sprite.flags.isFreeH = true;
            sprite.flags.nowAnimationInfo =
                new HSceneProc.AnimationListInfo(false);
            Button[] buttons = sprite.sonyu.categoryActionButton.lstButton.ToArray();

            // A front-only imported pose: only the native with-voice option
            // exists; do not synthesize a disabled silent variant.
            buttons[0].gameObject.SetActive(true);
            Apply(sprite);
            Require(!buttons[0].gameObject.activeSelf &&
                    buttons[2].gameObject.activeSelf &&
                    !buttons[3].gameObject.activeSelf,
                "front-only pose has a single anal-labelled native bubble");
            Require(InvokePrefix("RedirectAnalWithoutVoice", sprite),
                "unavailable silent variant never redirects");
            Require(!InvokePrefix("RedirectAnalWithVoice", sprite),
                "native anal with-voice callback is intercepted for alias");
            Require(sprite.FrontVoiceCalls == 1 &&
                    sprite.AnalVoiceCalls == 0 &&
                    sprite.FrontSilentCalls == 0,
                "synthetic bubble invokes the original normal insertion");
            sprite.OnInsertClick();
            Require(sprite.FrontVoiceCalls == 1,
                "front insertion remains blocked after alias returns");

            // Supported native anal actions never redirect to front.
            sprite.flags.nowAnimationInfo =
                new HSceneProc.AnimationListInfo(false);
            buttons[0].gameObject.SetActive(true);
            buttons[2].gameObject.SetActive(true);
            Apply(sprite);
            Require(InvokePrefix("RedirectAnalWithVoice", sprite) &&
                    sprite.FrontVoiceCalls == 1,
                "genuine anal action keeps native anal callback");
            sprite.OnInsertAnalClick();
            Require(sprite.AnalVoiceCalls == 1,
                "genuine anal handler remains functional");

            // The second seven-button page must dispatch its matching silent
            // front-only handler (not the normal page's or the anal handler).
            sprite.flags.nowAnimationInfo =
                new HSceneProc.AnimationListInfo(true);
            buttons[7].gameObject.SetActive(false);
            buttons[8].gameObject.SetActive(true);
            buttons[9].gameObject.SetActive(false);
            buttons[10].gameObject.SetActive(false);
            Apply(sprite);
            Require(!buttons[9].gameObject.activeSelf &&
                    buttons[10].gameObject.activeSelf,
                "female initiative shows only available silent bubble");
            Require(!InvokePrefix("RedirectAnalWithoutVoice", sprite) &&
                    sprite.FrontSilentCalls == 1 &&
                    sprite.AnalSilentCalls == 0,
                "female-initiative silent bubble runs ordinary native action");

            // Exceptions must never leave a general front insertion bypass.
            sprite.flags.nowAnimationInfo =
                new HSceneProc.AnimationListInfo(false);
            buttons[0].gameObject.SetActive(true);
            buttons[2].gameObject.SetActive(false);
            buttons[3].gameObject.SetActive(false);
            Apply(sprite);
            sprite.ThrowOnNormal = true;
            bool threw = false;
            try { InvokePrefix("RedirectAnalWithVoice", sprite); }
            catch (TargetInvocationException exception) {
                threw = exception.InnerException is InvalidOperationException;
            }
            sprite.ThrowOnNormal = false;
            Require(threw && !NativeFrontAllowed(sprite),
                "exception restores front-action guard and disarms alias");

            // A change to an unmarked heroine restores the original policy.
            FemboyModePlugin.Marked = false;
            buttons[0].gameObject.SetActive(true);
            Apply(sprite);
            Require(buttons[0].gameObject.activeSelf &&
                    InvokePrefix("RedirectAnalWithVoice", sprite) &&
                    NativeFrontAllowed(sprite),
                "unmarked heroine is unaffected");
            FemboyModePlugin.Marked = true;
            Console.WriteLine("Femboy Sonyu real-hook stub regression passed.");
        }
    }
}
