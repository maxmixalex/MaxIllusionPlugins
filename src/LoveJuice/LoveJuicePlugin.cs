using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using KKAPI;
using MaximumUI;
using UnityEngine;

[assembly: System.Reflection.AssemblyFileVersion(LoveJuice.LoveJuicePlugin.Version)]

namespace LoveJuice {
    [BepInDependency(KoikatuAPI.GUID)]
    [BepInDependency("Marco.SkinEffects", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("MaxMixAlex.FemboyMode", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    [BepInPlugin(GUID, Name, Version)]
    public sealed class LoveJuicePlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.LoveJuice";
        public const string Name = "LoveJuice";
        public const string Version = "0.3.5";

        private static readonly FieldInfo FemaleField = typeof(HSceneProc).GetField("lstFemale",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo MaleField = typeof(HSceneProc).GetField("male",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo Male1Field = typeof(HSceneProc).GetField("male1",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static LoveJuicePlugin instance;

        private ConfigEntry<bool> masterEnabled, face, body, intimate, malePre, milk, shower, special, collision;
        private ConfigEntry<int> maxActive, previewChannel;
        private ConfigEntry<float> interval, gravity, drag, lifetime, scale, launchSpeed, milkThreshold, malePreThreshold;
        private ConfigEntry<KeyboardShortcut> preview, panelShortcut;
        private LoveJuiceView panel;
        private bool panelVisible;
        private int panelRevision;
        private float nextPanelRefresh;
        private string panelStatus = "就绪。";
        private const string MountId = GUID + ".window";
        private readonly OptionalEffectBridge integrations = new OptionalEffectBridge();
        private readonly Dictionary<ChaControl, Actor> actors = new Dictionary<ChaControl, Actor>();
        private readonly LiveActorRoster<ChaControl> roster = new LiveActorRoster<ChaControl>();
        private StudioLiquidPool visuals;
        private readonly SalivaFilament saliva = new SalivaFilament();
        private HSceneProc scene;
        private float nextProbe, nextResolve;
        private readonly System.Random random = new System.Random();

        private sealed class Actor {
            internal ChaControl Character;
            internal Transform Mouth, Torso, Pelvis, ChestLeft, ChestRight, Alternate;
            internal Transform TrackedTop, TrackedHeadRoot, TrackedBodyRoot;
            internal int LastBoneProbe;
            internal bool ChestLeftIsFallback, ChestRightIsFallback;
            internal float FaceTime, BodyTime, IntimateTime, MalePreTime, MilkTime, ShowerTime, SpecialTime;
            internal int FaceLevel, BodyLevel, IntimateLevel, SpecialLevel;
            internal readonly LiquidLevelEvents LevelChanges = new LiquidLevelEvents();
        }

        public bool IsEnabled { get { return isActiveAndEnabled && masterEnabled != null && masterEnabled.Value; } }
        public static int ActiveDrops { get { return instance != null && instance.visuals != null ? instance.visuals.ActiveCount : 0; } }

        // Optional readback/write interface for independent integrations.
        // The adapter never touches game overlays or another plugin's state.
        private ConfigEntry<bool> FindToggle(string key) {
            switch (key) {
                case "Enabled": return masterEnabled;
                case "Face": return face;
                case "Body": return body;
                case "Intimate": return intimate;
                case "MalePre": return malePre;
                case "Milk": return milk;
                case "Shower": return shower;
                case "Special": return special;
                default: return null;
            }
        }

        public static bool TryGetSetting(string key, out bool value) {
            value = false;
            LoveJuicePlugin plugin = instance;
            if (plugin == null || !plugin.isActiveAndEnabled) return false;
            ConfigEntry<bool> setting = plugin.FindToggle(key);
            if (setting == null) return false;
            value = setting.Value;
            return true;
        }

        public static bool TrySetSetting(string key, bool value) {
            LoveJuicePlugin plugin = instance;
            if (plugin == null || !plugin.isActiveAndEnabled) return false;
            ConfigEntry<bool> setting = plugin.FindToggle(key);
            if (setting == null) return false;
            setting.Value = value;
            if (plugin.panelVisible) plugin.RefreshPanel();
            return setting.Value == value;
        }

        public static bool ShowControlPanel() {
            LoveJuicePlugin plugin = instance;
            if (plugin == null || !plugin.isActiveAndEnabled) return false;
            plugin.OpenPanel();
            return true;
        }

        private void Awake() {
            instance = this;
            masterEnabled = Config.Bind("General", "Enabled", false,
                "Enable native Studio liquid drips in active H scenes. Off by default until selected by the user.");
            preview = Config.Bind("General", "Preview key", new KeyboardShortcut(KeyCode.F9),
                "Emit native Studio particles from the currently selected active H scene actor.");
            panelShortcut = Config.Bind("General", "Control panel key",
                new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                "Ctrl+F9 toggles the LoveJuice window; F9 emits the selected preview.");
            previewChannel = Config.Bind("General", "Preview channel", 0,
                "Select the F9 native Studio preview channel: 0 water, 1 saliva, 2 body, 3 shower, 4 milk, 5 special, 6 intimate, 7 male pre-fluid.");
            face = Config.Bind("Sources", "Face drips", true,
                "Show native CyuNoVR kissing saliva strings, or a lightweight matching filament when the native effect is absent. No face-level shower particles.");
            body = Config.Bind("Sources", "Body drips", true,
                "Read current body overlay / optional SkinEffects levels, including JuiceControl changes.");
            intimate = Config.Bind("Sources", "Intimate fluid drips", true,
                "Use optional SkinEffects PussyJuiceBody readback to emit native Studio droplets near the female pelvis.");
            malePre = Config.Bind("Sources", "Male pre-fluid drips", false,
                "Opt-in native water droplets during active animation loops above a male gauge threshold. Primary active male only.");
            malePreThreshold = Config.Bind("Sources", "Male pre-fluid gauge threshold", 45f,
                "Minimum native male gauge for optional periodic pre-fluid drips; this is a proxy, not a dedicated native event.");
            milk = Config.Bind("Sources", "Milk emission", false,
                "Enable independent chest-source droplets during active loop scenes above the gauge threshold.");
            milkThreshold = Config.Bind("Sources", "Milk gauge threshold", 65f,
                "Female gauge needed for the optional milk channel; independent of body overlay state.");
            shower = Config.Bind("Sources", "Shower dripping", false,
                "Enable liquid shower prefabs during animation states containing 'shower'.");
            special = Config.Bind("Sources", "Marked geometry drips", false,
                "Enable the independent native liquid channel when FemboyMode marks a character.");
            collision = Config.Bind("Physics", "World collision", true,
                "Recycle a drop when its ballistic trajectory first touches a world collider.");
            maxActive = Config.Bind("Performance", "Max active drops", 48,
                "Hard native particle budget across all pooled Studio emitters.");
            interval = Config.Bind("Performance", "Emission interval", .26f,
                "Seconds between emissions from a source at one intensity level.");
            gravity = Config.Bind("Physics", "Gravity", 8f, "World-space droplet gravity.");
            drag = Config.Bind("Physics", "Air damping", 1.0f, "Ballistic velocity damping.");
            lifetime = Config.Bind("Physics", "Drop lifetime", 1.6f, "Maximum seconds a droplet stays visible.");
            scale = Config.Bind("Visual", "Prefab scale multiplier", .045f,
                "Multiply the original Studio liquid ParticleSystem transform scale.");
            launchSpeed = Config.Bind("Physics", "Launch speed", .35f,
                "Additional initial speed, relative to the source bone orientation.");
            visuals = new StudioLiquidPool(message => Logger.LogWarning(message));
            Logger.LogInfo(Name + " " + Version + " loaded; native Studio liquid catalog available on demand.");
        }

        private static Transform Bone(ChaControl character, params string[] names) {
            if (character == null) return null;
            Transform root = character.objTop == null ? character.transform : character.objTop.transform;
            if (root == null) return null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int k = 0; k < names.Length; k++) {
                string desired = names[k];
                for (int i = 0; i < transforms.Length; i++) {
                    Transform item = transforms[i];
                    if (item != null && string.Equals(item.name, desired, StringComparison.OrdinalIgnoreCase))
                        return item;
                }
            }
            return null;
        }

        private static Transform HeadRoot(ChaControl cha) {
            return cha.objHeadBone != null ? cha.objHeadBone.transform :
                cha.objHead != null ? cha.objHead.transform : cha.transform;
        }

        private static Transform BodyRoot(ChaControl cha) {
            return cha.objBodyBone != null ? cha.objBodyBone.transform : cha.transform;
        }

        private static void RebindActorBones(Actor actor) {
            ChaControl cha = actor.Character;
            if (cha == null) return;
            Transform headRoot = HeadRoot(cha), bodyRoot = BodyRoot(cha);
            Transform left = Bone(cha, "cf_J_Bust01_L", "cf_J_Bust00_L", "cf_s_bust03_L");
            Transform right = Bone(cha, "cf_J_Bust01_R", "cf_J_Bust00_R", "cf_s_bust03_R");
            actor.TrackedTop = cha.objTop == null ? null : cha.objTop.transform;
            actor.TrackedHeadRoot = headRoot;
            actor.TrackedBodyRoot = bodyRoot;
            actor.Mouth = Bone(cha, "cf_J_MouthBase", "cf_J_Mouth", "cf_J_MouthMove", "cf_J_Head") ?? headRoot;
            actor.Torso = Bone(cha, "cf_J_Spine03", "cf_J_Spine02", "cf_J_Hips") ?? bodyRoot;
            actor.Pelvis = Bone(cha, "cf_J_Kokan", "cf_J_Hips", "cf_J_Pelvis");
            actor.ChestLeft = left ?? bodyRoot;
            actor.ChestRight = right ?? bodyRoot;
            actor.ChestLeftIsFallback = left == null;
            actor.ChestRightIsFallback = right == null;
            actor.Alternate = Bone(cha, "cm_j_dan100", "cm_J_dan100", "cm_J_dan100_00");
            actor.LastBoneProbe = Time.frameCount;
        }

        private static void EnsureActorBones(Actor actor) {
            ChaControl cha = actor == null ? null : actor.Character;
            if (cha == null) return;
            Transform top = cha.objTop == null ? null : cha.objTop.transform;
            bool rigChanged = !ReferenceEquals(actor.TrackedTop, top) ||
                !ReferenceEquals(actor.TrackedHeadRoot, HeadRoot(cha)) ||
                !ReferenceEquals(actor.TrackedBodyRoot, BodyRoot(cha));
            bool unresolved = ReferenceEquals(actor.Mouth, actor.TrackedHeadRoot) ||
                actor.Alternate == null || actor.Pelvis == null ||
                actor.ChestLeftIsFallback || actor.ChestRightIsFallback;
            if (rigChanged || unresolved && Time.frameCount - actor.LastBoneProbe >= 180)
                RebindActorBones(actor);
        }

        private Actor DiscoverActor(ChaControl cha) {
            if (cha == null) return null;
            Actor actor = new Actor { Character = cha };
            RebindActorBones(actor);
            return actor;
        }

        private bool HasActiveSceneCharacter(ChaControl character) {
            return character != null && actors.ContainsKey(character) &&
                character.visibleAll && character.hiPoly && scene != null && scene.flags != null;
        }

        // Optional public integration for other plugins. No dependency is
        // required, and calls outside the current H scene do not create objects.
        public static bool Emit(ChaControl character, LoveJuiceChannel channel, int count) {
            return instance != null && instance.EmitExplicit(character, channel, count);
        }

        private bool EmitExplicit(ChaControl character, LoveJuiceChannel kind, int count) {
            if (!IsEnabled || visuals == null || !StudioLiquidCatalog.Valid(kind) ||
                !HasActiveSceneCharacter(character)) return false;
            Actor actor;
            if (!actors.TryGetValue(character, out actor)) return false;
            bool spawned = false;
            count = Math.Max(0, Math.Min(12, count));
            for (int i = 0; i < count; i++) spawned |= EmitFrom(actor, kind, i);
            return spawned;
        }

        private float RandomSigned() { return (float)(random.NextDouble() * 2.0 - 1.0); }

        private bool EmitFrom(Actor actor, LoveJuiceChannel kind, int n) {
            // Saliva is an elastic kiss filament, NOT a native Studio spray.
            // Deliberate and automatic face events must never fire shower-like jets.
            if (kind == LoveJuiceChannel.Saliva) return false;
            if (kind == LoveJuiceChannel.Intimate && actor.Character.sex != 1) return false;
            if (kind == LoveJuiceChannel.MalePre && actor.Character.sex != 0) return false;
            Transform source = kind == LoveJuiceChannel.Saliva ? actor.Mouth :
                kind == LoveJuiceChannel.Intimate ? actor.Pelvis :
                kind == LoveJuiceChannel.MalePre ? actor.Alternate :
                kind == LoveJuiceChannel.Milk ? (n % 2 == 0 ? actor.ChestLeft : actor.ChestRight) :
                kind == LoveJuiceChannel.Special && actor.Alternate != null ? actor.Alternate : actor.Torso;
            if (source == null || visuals == null ||
                (kind == LoveJuiceChannel.Special && actor.Alternate == null)) return false;
            bool leftChest = n % 2 == 0;
            Vector3 local = kind == LoveJuiceChannel.Saliva ?
                new Vector3(0f, -.035f, .045f) :
                kind == LoveJuiceChannel.Intimate ? new Vector3(0f, -.02f, .025f) :
                kind == LoveJuiceChannel.MalePre ? new Vector3(0f, -.01f, .035f) :
                kind == LoveJuiceChannel.Milk ?
                    (leftChest ? actor.ChestLeftIsFallback : actor.ChestRightIsFallback)
                        ? new Vector3(leftChest ? -.12f : .12f, .34f, .055f)
                        : new Vector3(0f, -.035f, .035f) :
                new Vector3(0f, -.08f, .025f);
            Vector3 origin = source.TransformPoint(local);
            // The Studio liquid ITEM supplies its native material, but not
            // its continuous shower emitter. Individual droplets begin near
            // the body, move gently, then fall under world gravity.
            float speed = DropletPhysics.Clamp(launchSpeed.Value, 0f, 5f) * .14f;
            Vector3 velocity = source.TransformDirection(new Vector3(
                RandomSigned() * .012f, -.22f * speed,
                RandomSigned() * .015f));
            velocity += Vector3.down * (.045f + speed * .5f);
            if (kind == LoveJuiceChannel.MalePre)
                velocity += source.forward * (.025f + speed * .25f);
            if (kind == LoveJuiceChannel.Milk)
                velocity += source.forward * .015f;
            return visuals.Spawn(kind, origin, velocity,
                DropletPhysics.Clamp(scale.Value, .001f, .3f),
                DropletPhysics.Clamp(lifetime.Value, .2f, 8f),
                Math.Max(1, Math.Min(128, maxActive.Value)),
                gravity.Value, drag.Value, collision.Value);
        }

        private void OnSceneChanged(HSceneProc next) {
            panelRevision++;
            panelStatus = next == null ? "尚未进入场景。" : "已连接当前场景。";
            scene = next;
            actors.Clear();
            roster.Replace(null);
            integrations.Reset();
            nextResolve = 0f;
            if (visuals != null) visuals.Attach(scene == null ? null : scene.transform);
            saliva.Attach(scene == null ? null : scene.transform);
        }

        private void AddActorToRoster(ChaControl cha, HashSet<ChaControl> seen, List<ChaControl> order) {
            if (cha == null || !cha.visibleAll || !cha.hiPoly || !seen.Add(cha)) return;
            order.Add(cha);
            Actor existing;
            if (!actors.TryGetValue(cha, out existing)) actors.Add(cha, DiscoverActor(cha));
            else EnsureActorBones(existing);
        }

        private void RefreshRoster() {
            if (scene == null) return;
            HashSet<ChaControl> seen = new HashSet<ChaControl>();
            List<ChaControl> order = new List<ChaControl>();
            // The vanilla H scene has a distinct male field in addition
            // to the 1–2 heroine list. Read *both*; don't enumerate every
            // ChaControl in the scene, which could include inactive NPCs.
            IList female = FemaleField == null ? null : FemaleField.GetValue(scene) as IList;
            if (female != null)
                foreach (object entry in female) AddActorToRoster(entry as ChaControl, seen, order);
            ChaControl male = MaleField == null ? null : MaleField.GetValue(scene) as ChaControl;
            AddActorToRoster(male, seen, order);
            // Optional second male performer in 3P/MMF scenes.
            ChaControl male1 = Male1Field == null ? null : Male1Field.GetValue(scene) as ChaControl;
            AddActorToRoster(male1, seen, order);
            List<ChaControl> stale = null;
            foreach (ChaControl cha in actors.Keys)
                if (!seen.Contains(cha)) {
                    if (stale == null) stale = new List<ChaControl>();
                    stale.Add(cha);
                }
            if (stale != null) foreach (ChaControl cha in stale) actors.Remove(cha);
            if (roster.Replace(order)) {
                panelRevision++;
                nextPanelRefresh = 0f;
            }
        }

        private static string ActorDisplayName(ChaControl character) {
            if (character == null) return "角色";
            string name = character.chaFile != null && character.chaFile.parameter != null
                ? character.chaFile.parameter.fullname : null;
            return string.IsNullOrEmpty(name) ? character.name : name;
        }

        private string[] ActorNames() {
            string[] names = new string[roster.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = (i + 1) + ". " + ActorDisplayName(roster.At(i));
            return names;
        }

        private int SkinLevel(ChaControl cha, params string[] kinds) {
            int level = -1;
            foreach (string kind in kinds) {
                int value = integrations.Level(cha, kind);
                if (value >= 0) level = Math.Max(level, value);
            }
            return level;
        }

        private void UpdateLevels(Actor actor) {
            ChaControl cha = actor.Character;
            int fromFace = cha.GetSiruFlags(ChaFileDefine.SiruParts.SiruKao);
            int fromBody = Math.Max(cha.GetSiruFlags(ChaFileDefine.SiruParts.SiruFrontUp),
                cha.GetSiruFlags(ChaFileDefine.SiruParts.SiruFrontDown));
            actor.FaceLevel = Math.Max(fromFace, Math.Max(0, SkinLevel(cha, "DroolFace", "SalivaFace")));
            actor.BodyLevel = Math.Max(fromBody, Math.Max(0, SkinLevel(cha, "WetBody")));
            // The installed legacy SkinEffects exposes this separately from
            // sweat; body overlays must not trigger the intimate source.
            actor.IntimateLevel = cha.sex == 1 ? Math.Max(0, SkinLevel(cha, "PussyJuiceBody")) : 0;
            actor.SpecialLevel = special.Value && integrations.IsMarked(cha) ? actor.BodyLevel : 0;

            LiquidLevelBursts bursts = actor.LevelChanges.Observe(
                actor.FaceLevel, actor.BodyLevel, actor.SpecialLevel, actor.IntimateLevel);
            // Match the currently selected effect sources; one readback rise
            // produces a bounded native burst before the periodic drips.
            if (body.Value) for (int i = 0; i < bursts.Body; i++)
                EmitFrom(actor, LoveJuiceChannel.Body, i);
            if (intimate.Value) for (int i = 0; i < bursts.Intimate; i++)
                EmitFrom(actor, LoveJuiceChannel.Intimate, i);
            if (special.Value) for (int i = 0; i < bursts.Special; i++)
                EmitFrom(actor, LoveJuiceChannel.Special, i);
        }

        private void TickSource(Actor actor, LoveJuiceChannel kind, int level,
            ref float timer, float delta) {
            if (level <= 0) { timer = 0f; return; }
            float period = DropletPhysics.Clamp(interval.Value, .06f, 4f) /
                Mathf.Sqrt(Mathf.Clamp(level, 1f, 10f));
            timer += delta;
            if (timer < period) return;
            timer -= period;
            timer = Mathf.Min(timer, period);
            EmitFrom(actor, kind, random.Next(0, 2));
        }

        private void TickSaliva(float delta)
        {
            if (scene == null || scene.flags == null || !face.Value) {
                saliva.Tick(false, false, Vector3.zero, Vector3.zero, delta);
                return;
            }
            Actor nearestFemale = null, nearestMale = null;
            float closest = float.MaxValue;
            foreach (Actor femaleActor in actors.Values) {
                if (femaleActor.Character == null || femaleActor.Character.sex != 1 ||
                    femaleActor.Mouth == null) continue;
                foreach (Actor maleActor in actors.Values) {
                    if (maleActor.Character == null || maleActor.Character.sex != 0 ||
                        maleActor.Mouth == null) continue;
                    float distance = Vector3.Distance(
                        femaleActor.Mouth.position, maleActor.Mouth.position);
                    if (distance < closest) {
                        closest = distance;
                        nearestFemale = femaleActor;
                        nearestMale = maleActor;
                    }
                }
            }
            if (nearestFemale == null || nearestMale == null) {
                saliva.Tick(false, false, Vector3.zero, Vector3.zero, delta);
                return;
            }

            bool kiss = LiquidSourceTriggers.KissAnimationSignal(
                scene.flags.nowAnimStateName,
                scene.flags.click == HFlag.ClickKind.mouth);
            saliva.Tick(true, kiss,
                nearestFemale.Mouth.position, nearestMale.Mouth.position,
                delta);
        }

        private void Update() {
            if (panelShortcut != null && panelShortcut.Value.IsDown() &&
                !MaximumUIRuntime.IsAnyTextFieldFocused && !MaximumUIRuntime.IsModalOpen) {
                if (panelVisible) ClosePanel(); else OpenPanel();
            }
            if (panelVisible && Time.unscaledTime >= nextPanelRefresh) {
                nextPanelRefresh = Time.unscaledTime + .2f;
                RefreshPanel();
            }
            if (!IsEnabled) {
                if (scene != null) OnSceneChanged(null);
                return;
            }
            if (Time.unscaledTime >= nextProbe) {
                nextProbe = Time.unscaledTime + .65f;
                HSceneProc observed = UnityEngine.Object.FindObjectOfType<HSceneProc>();
                if (!ReferenceEquals(scene, observed)) OnSceneChanged(observed);
            }
            if (scene == null || scene.flags == null || scene.isEnd) return;
            if ((int)scene.flags.mode < 0) return;
            if (Time.unscaledTime >= nextResolve) {
                nextResolve = Time.unscaledTime + .3f;
                RefreshRoster();
                foreach (Actor actor in actors.Values) UpdateLevels(actor);
            }
            if (preview.Value.IsDown() && !panelShortcut.Value.IsDown())
                PreviewFromPanel();
            float dt = DropletPhysics.Clamp(Time.deltaTime, 0f, .15f);
            string state = scene.flags.nowAnimStateName ?? string.Empty;
            bool showerActive = state.IndexOf("shower", StringComparison.OrdinalIgnoreCase) >= 0 ||
                state.IndexOf("シャワー", StringComparison.Ordinal) >= 0;
            bool activeLoop = state.IndexOf("Loop", StringComparison.Ordinal) >= 0;
            ChaControl primaryMale = MaleField == null ? null : MaleField.GetValue(scene) as ChaControl;
            if (primaryMale == null && Male1Field != null)
                primaryMale = Male1Field.GetValue(scene) as ChaControl;
            int milkLevel = milk.Value && activeLoop &&
                scene.flags.gaugeFemale >= DropletPhysics.Clamp(milkThreshold.Value, 0f, 100f)
                ? Mathf.Clamp(Mathf.RoundToInt(scene.flags.gaugeFemale / 33f), 1, 3) : 0;
            foreach (Actor actor in actors.Values) {
                if (body.Value) TickSource(actor, LoveJuiceChannel.Body, actor.BodyLevel, ref actor.BodyTime, dt);
                if (intimate.Value && actor.Character.sex == 1)
                    TickSource(actor, LoveJuiceChannel.Intimate, actor.IntimateLevel, ref actor.IntimateTime, dt);
                if (malePre.Value) {
                    int maleLevel = LiquidSourceTriggers.MalePreLevel(actor.Character.sex, activeLoop,
                        ReferenceEquals(actor.Character, primaryMale), scene.flags.gaugeMale, malePreThreshold.Value);
                    TickSource(actor, LoveJuiceChannel.MalePre, maleLevel, ref actor.MalePreTime, dt);
                }
                // Do not turn a scene-wide female gauge into milk emissions
                // on the male's fallback torso anchor.
                if (milk.Value && actor.Character.sex == 1)
                    TickSource(actor, LoveJuiceChannel.Milk, milkLevel, ref actor.MilkTime, dt);
                if (shower.Value && showerActive)
                    TickSource(actor, LoveJuiceChannel.Shower, 1, ref actor.ShowerTime, dt);
                if (special.Value) TickSource(actor, LoveJuiceChannel.Special,
                    actor.SpecialLevel, ref actor.SpecialTime, dt);
            }
            TickSaliva(dt);
            visuals.Tick(dt, gravity.Value, drag.Value, collision.Value);
        }

        private void OpenPanel() {
            if (panel == null) {
                MaximumUIRuntime.EnsureHost();
                panel = new LoveJuiceView();
                MaximumUIRuntime.Mount(MountId, panel, new MountOptions {
                    Visible = false, BuildEveryFrame = false,
                    SoftMouseCapture = true, SoftKeyboardCapture = true, SortOrder = 145
                });
            }
            panelVisible = true;
            panelRevision++;
            RefreshPanel();
            MaximumUIRuntime.SetVisible(MountId, true);
            MaximumUIRuntime.Focus(MountId);
        }

        private void ClosePanel() {
            panelVisible = false;
            panelRevision++;
            if (panel != null) MaximumUIRuntime.SetVisible(MountId, false);
        }

        private void PreviewFromPanel() {
            if (!IsEnabled || scene == null || scene.flags == null || scene.isEnd ||
                roster.Selected == null) {
                panelStatus = "当前无可用角色或效果未启用。";
                return;
            }
            LoveJuiceChannel channel = (LoveJuiceChannel)previewChannel.Value;
            if (channel == LoveJuiceChannel.Saliva) {
                panelStatus = "口水拉丝只在游戏原生亲吻动作中出现；不支持喷射式预览。";
                return;
            }
            int emitted = 0;
            Actor actor;
            if (actors.TryGetValue(roster.Selected, out actor))
                for (int i = 0; i < 4; i++)
                    if (EmitFrom(actor, channel, i)) emitted++;
            panelStatus = emitted > 0
                ? "已向 " + ActorDisplayName(roster.Selected) + " 发射 " + emitted + " 颗原生粒子。"
                : "未发射：素材、粒子预算或骨骼不可用；请检查 BepInEx 日志。";
        }

        private void RefreshPanel() {
            if (!panelVisible || panel == null) return;
            int token = panelRevision;
            Func<bool> live = () => panelVisible && panelRevision == token;
            LoveJuiceViewModel m = new LoveJuiceViewModel {
                Version = Version, Enabled = IsEnabled,
                HasScene = scene != null && scene.flags != null && !scene.isEnd,
                ActorCount = roster.Count, ActorNames = ActorNames(),
                SelectedActorIndex = roster.SelectedIndex, ActiveParticles = ActiveDrops,
                Status = panelStatus, Integration = integrations.HasSkinEffects
                    ? "SkinEffects：已连接（仅读取状态）。"
                    : "SkinEffects：未连接，使用游戏自身状态。",
                Face = face.Value, Body = body.Value, Intimate = intimate.Value, MalePre = malePre.Value, Milk = milk.Value,
                Shower = shower.Value, Special = special.Value, Collision = collision.Value,
                MaximumParticles = maxActive.Value, PreviewChannel = previewChannel.Value,
                Interval = interval.Value, Gravity = gravity.Value, Damping = drag.Value,
                Lifetime = lifetime.Value, Scale = scale.Value, LaunchSpeed = launchSpeed.Value,
                MilkThreshold = milkThreshold.Value, MalePreThreshold = malePreThreshold.Value,
                Close = () => { if (live()) ClosePanel(); }
            };
            m.ChangeEnabled = value => { if (live()) { masterEnabled.Value = value; RefreshPanel(); } };
            m.ChangeFace = value => { if (live()) { face.Value = value; RefreshPanel(); } };
            m.ChangeBody = value => { if (live()) { body.Value = value; RefreshPanel(); } };
            m.ChangeIntimate = value => { if (live()) { intimate.Value = value; RefreshPanel(); } };
            m.ChangeMalePre = value => { if (live()) { malePre.Value = value; RefreshPanel(); } };
            m.ChangeMilk = value => { if (live()) { milk.Value = value; RefreshPanel(); } };
            m.ChangeShower = value => { if (live()) { shower.Value = value; RefreshPanel(); } };
            m.ChangeSpecial = value => { if (live()) { special.Value = value; RefreshPanel(); } };
            m.ChangeCollision = value => { if (live()) { collision.Value = value; RefreshPanel(); } };
            m.ChangeInterval = value => { if (live()) { interval.Value = DropletPhysics.Clamp(value, .06f, 4f); RefreshPanel(); } };
            m.ChangeGravity = value => { if (live()) { gravity.Value = DropletPhysics.Clamp(value, 0f, 30f); RefreshPanel(); } };
            m.ChangeDamping = value => { if (live()) { drag.Value = DropletPhysics.Clamp(value, 0f, 10f); RefreshPanel(); } };
            m.ChangeLifetime = value => { if (live()) { lifetime.Value = DropletPhysics.Clamp(value, .2f, 8f); RefreshPanel(); } };
            m.ChangeScale = value => { if (live()) { scale.Value = DropletPhysics.Clamp(value, .001f, .3f); RefreshPanel(); } };
            m.ChangeLaunchSpeed = value => { if (live()) { launchSpeed.Value = DropletPhysics.Clamp(value, 0f, 5f); RefreshPanel(); } };
            m.ChangeMilkThreshold = value => { if (live()) { milkThreshold.Value = DropletPhysics.Clamp(value, 0f, 100f); RefreshPanel(); } };
            m.ChangeMalePreThreshold = value => { if (live()) { malePreThreshold.Value = DropletPhysics.Clamp(value, 0f, 100f); RefreshPanel(); } };
            m.ChangeMaximum = value => { if (live()) { maxActive.Value = Mathf.Clamp(value, 1, 128); RefreshPanel(); } };
            m.ChangePreviewChannel = value => { if (live() && value >= 0 && value <= 7) { previewChannel.Value = value; RefreshPanel(); } };
            m.SelectActor = value => {
                if (!live() || scene == null || scene.flags == null || scene.isEnd) return;
                if (roster.Select(value)) {
                    panelRevision++;
                    panelStatus = "当前预览角色：" + ActorDisplayName(roster.Selected);
                    RefreshPanel();
                }
            };
            m.Preview = () => { if (live()) { PreviewFromPanel(); RefreshPanel(); } };
            if (!panel.Model.SameDisplayAs(m)) {
                panel.Model = m;
                MaximumUIRuntime.MarkDirty(panel);
            }
        }

        private void OnDisable() {
            ClosePanel();
            if (panel != null) MaximumUIRuntime.Unmount(MountId);
            panel = null;
            OnSceneChanged(null);
        }
        private void OnDestroy() {
            ClosePanel();
            if (panel != null) MaximumUIRuntime.Unmount(MountId);
            panel = null;
            OnSceneChanged(null);
            if (visuals != null) visuals.Dispose();
            if (ReferenceEquals(instance, this)) instance = null;
        }
    }
}
