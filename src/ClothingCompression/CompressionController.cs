using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;

namespace ClothingCompression {
    internal sealed class CompressionController : CharaCustomFunctionController {
        private const string ShapePrefix = "MaxMixAlex.ClothingCompression.";
        private static readonly string[] ThighLeftBones = { "cf_j_thigh00_L", "cf_s_thigh01_L", "cm_j_thigh00_L", "cm_s_thigh01_L" };
        private static readonly string[] ThighRightBones = { "cf_j_thigh00_R", "cf_s_thigh01_R", "cm_j_thigh00_R", "cm_s_thigh01_R" };
        private static readonly string[] KneeLeftBones = { "cf_j_leg01_L", "cf_s_kneeB_L", "cf_s_leg_L", "cf_s_leg01_L", "cm_j_leg01_L", "cm_s_kneeB_L", "cm_s_leg_L", "cm_s_leg01_L" };
        private static readonly string[] KneeRightBones = { "cf_j_leg01_R", "cf_s_kneeB_R", "cf_s_leg_R", "cf_s_leg01_R", "cm_j_leg01_R", "cm_s_kneeB_R", "cm_s_leg_R", "cm_s_leg01_R" };
        private static readonly string[] LowerThighInfluenceLeft = { "cf_j_leg01_L", "cf_j_thigh00_L", "cf_s_thigh01_L", "cf_s_thigh02_L", "cf_s_thigh03_L", "cf_s_kneeB_L", "cm_j_leg01_L", "cm_j_thigh00_L", "cm_s_thigh01_L", "cm_s_thigh02_L", "cm_s_thigh03_L", "cm_s_kneeB_L" };
        private static readonly string[] LowerThighInfluenceRight = { "cf_j_leg01_R", "cf_j_thigh00_R", "cf_s_thigh01_R", "cf_s_thigh02_R", "cf_s_thigh03_R", "cf_s_kneeB_R", "cm_j_leg01_R", "cm_j_thigh00_R", "cm_s_thigh01_R", "cm_s_thigh02_R", "cm_s_thigh03_R", "cm_s_kneeB_R" };
        private static readonly string[] KneeInfluenceLeft = { "cf_j_leg01_L", "cf_j_thigh00_L", "cf_s_kneeB_L", "cf_s_leg_L", "cf_s_leg01_L", "cm_j_leg01_L", "cm_j_thigh00_L", "cm_s_kneeB_L", "cm_s_leg_L", "cm_s_leg01_L" };
        private static readonly string[] KneeInfluenceRight = { "cf_j_leg01_R", "cf_j_thigh00_R", "cf_s_kneeB_R", "cf_s_leg_R", "cf_s_leg01_R", "cm_j_leg01_R", "cm_j_thigh00_R", "cm_s_kneeB_R", "cm_s_leg_R", "cm_s_leg01_R" };
        // KK has no separately skinned forearm joint. arm00 supplies the coordinate
        // frame while elbow/hand helper bones identify the actual forearm vertices.
        private static readonly string[] ArmLeftBones = { "cf_j_arm00_L", "cm_j_arm00_L" };
        private static readonly string[] ArmRightBones = { "cf_j_arm00_R", "cm_j_arm00_R" };
        private static readonly string[] UpperArmLeftBones = { "cf_j_arm00_L", "cf_s_arm01_L", "cf_s_arm02_L", "cf_s_arm03_L", "cm_j_arm00_L", "cm_s_arm01_L", "cm_s_arm02_L", "cm_s_arm03_L" };
        private static readonly string[] UpperArmRightBones = { "cf_j_arm00_R", "cf_s_arm01_R", "cf_s_arm02_R", "cf_s_arm03_R", "cm_j_arm00_R", "cm_s_arm01_R", "cm_s_arm02_R", "cm_s_arm03_R" };
        private static readonly string[] ForearmInfluenceLeft = { "cf_s_elbo_L", "cf_s_elboback_L", "cf_s_hand_L", "cf_j_hand_L", "cm_s_elbo_L", "cm_s_elboback_L", "cm_s_hand_L", "cm_j_hand_L" };
        private static readonly string[] ForearmInfluenceRight = { "cf_s_elbo_R", "cf_s_elboback_R", "cf_s_hand_R", "cf_j_hand_R", "cm_s_elbo_R", "cm_s_elboback_R", "cm_s_hand_R", "cm_j_hand_R" };

        private readonly List<DeformationEntry> entries = new List<DeformationEntry>();
        private readonly List<MeshState> meshes = new List<MeshState>();
        private readonly HashSet<string> persistedEntryIds = new HashSet<string>();
        private List<ItemDescriptor> frameItems;
        private int itemFrame = -1;
        private Coroutine pendingApply;
        private string equipmentSignature = string.Empty;
        private string visibilitySignature = string.Empty;
        private string rendererSignature = string.Empty;
        private string diagnosticSignature = string.Empty;
        private string shapeGeneration = string.Empty;
        private int selectedIndex;

        internal IList<DeformationEntry> Entries { get { return entries; } }
        internal int SelectedIndex { get { return selectedIndex; } set { selectedIndex = Mathf.Clamp(value, 0, Math.Max(0, entries.Count - 1)); } }
        internal DeformationEntry Selected { get { return entries.Count == 0 ? null : entries[Mathf.Clamp(selectedIndex, 0, entries.Count - 1)]; } }
        internal bool Dirty { get; private set; }
        internal string Status { get; private set; } = string.Empty;
        internal string CharacterIdentityKey {
            get {
                string name = ChaFileControl == null || ChaFileControl.parameter == null ? "Unknown" : ChaFileControl.parameter.fullname;
                // Card filenames are unstable when an autosave/versioning plugin is installed.
                return (ChaControl == null ? -1 : ChaControl.sex) + ":" + name;
            }
        }
        internal int CoordinateIndex { get { return ChaControl == null || ChaControl.fileStatus == null ? -1 : ChaControl.fileStatus.coordinateType; } }
        internal string CharacterKey { get { return MakeCoordinateKey(CoordinateIndex); } }
        internal string MakeCoordinateKey(int coordinate) { return CharacterIdentityKey + ":coord:" + coordinate; }

        protected override void OnReload(GameMode currentGameMode, bool maintainState) {
            itemFrame = -1;
            ClearMeshes();
            if (!maintainState) LoadLibraryEntries();
            RequestApply(0.6f);
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode) { }

        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState) {
            itemFrame = -1;
            ClearMeshes();
            if (!maintainState) LoadLibraryEntries();
            RequestApply(0.3f);
        }

        protected override void Update() {
            base.Update();
            if (ChaControl == null || Time.frameCount % 15 != 0) return;
            List<ItemDescriptor> items = GetItems();
            string equipment = string.Join("|", items.Select(x => x.Key + "@" + x.Slot).OrderBy(x => x).ToArray());
            if (equipment != equipmentSignature) {
                equipmentSignature = equipment;
                if (!Dirty) { LoadLibraryEntries(); RequestApply(0.2f); return; }
            }
            if (entries.Count > 0 && pendingApply == null) {
                string renderers = GetRendererSignature();
                if (!string.Equals(renderers, rendererSignature, StringComparison.Ordinal)) {
                    rendererSignature = renderers;
                    if (ClothingCompressionPlugin.VerboseDiagnostics.Value)
                        ClothingCompressionPlugin.Log.LogInfo("Detected replaced character/clothing mesh; rebuilding compression shapes.");
                    RequestApply(0.2f); return;
                }
            }
            string visibility = GetVisibilitySignature();
            if (visibility != visibilitySignature) {
                // Garment deformation is baked into our private mesh clone because KK's
                // Unity 5.6 renderer can ignore/reset runtime clothing blend shapes in H
                // scenes. Visibility therefore changes which deltas must be baked.
                RequestApply(0f);
                return;
            }
            if (entries.Count > 0 && pendingApply == null && WeightsNeedRefresh()) ApplyWeights();
        }

        protected override void OnDestroy() {
            if (pendingApply != null) StopCoroutine(pendingApply);
            pendingApply = null;
            ClearMeshes();
            base.OnDestroy();
        }

        internal DeformationEntry Add(DeformationKind kind) {
            DeformationEntry value = DeformationEntry.Create(kind);
            entries.Add(value); selectedIndex = entries.Count - 1; Changed(true); return value;
        }

        internal void DuplicateSelected() {
            if (Selected == null) return;
            entries.Insert(selectedIndex + 1, Selected.Clone(true)); selectedIndex++; Changed(true);
        }

        internal void DeleteSelected() {
            if (Selected == null) return;
            entries.RemoveAt(selectedIndex); selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, entries.Count - 1)); Changed(true);
        }

        internal void MoveSelected(int offset) {
            if (Selected == null) return;
            int destination = Mathf.Clamp(selectedIndex + offset, 0, entries.Count - 1);
            if (destination == selectedIndex) return;
            DeformationEntry value = Selected; entries.RemoveAt(selectedIndex); entries.Insert(destination, value); selectedIndex = destination; Dirty = true;
        }

        internal void Changed(bool rebuild) {
            Dirty = true;
            if (rebuild) RequestApply();
            else {
                ApplyWeights();
                // Enabled/visibility changes also affect baked garment vertices. Updating
                // body blend weights alone must not mark those garments as up-to-date.
                if (!string.Equals(GetVisibilitySignature(), visibilitySignature, StringComparison.Ordinal)) RequestApply(0f);
            }
            ClothingCompressionPlugin.Instance.RefreshMakerControls(this);
        }

        internal int Save() {
            try {
                int count = ClothingCompressionPlugin.Library.SaveEntries(CharacterKey, entries, persistedEntryIds);
                CapturePersistedEntryIds();
                Dirty = false;
                Status = count == 0 ? "没有可保存的已绑定形变。" : "已保存 " + count + " 个形变到 " + ClothingCompressionPlugin.Library.Path;
                ClothingCompressionPlugin.Log.LogMessage(Status);
                return count;
            } catch (Exception ex) {
                Status = "保存失败，当前编辑已保留；请检查 XML 文件和日志，修复后重载。";
                ClothingCompressionPlugin.Log.LogError(Status + " " + ex);
                return 0;
            }
        }

        internal bool ImportEntry(DeformationEntry source) {
            if (source == null) { Status = "所选服装搭配没有可复制的形变。"; ClothingCompressionPlugin.Log.LogMessage(Status); return false; }
            entries.Add(source.Clone(true)); selectedIndex = entries.Count - 1; Dirty = true; RequestApply();
            ClothingCompressionPlugin.Instance.RefreshMakerControls(this);
            Status = "已追加所选形变的副本；请检查绑定对象是否适用于当前服装搭配。";
            ClothingCompressionPlugin.Log.LogMessage(Status);
            return true;
        }

        internal void ReloadFromDisk() {
            if (!ClothingCompressionPlugin.Library.Reload()) {
                Status = "读取 XML 失败，已保留当前形变；请修复文件后重新加载。";
                return;
            }
            LoadLibraryEntries(); RequestApply(0f);
        }

        internal int ImportFromCoordinate(int coordinate) {
            if (coordinate < 0 || coordinate == CoordinateIndex) { Status = "请选择另一个服装搭配。"; return 0; }
            List<DeformationEntry> imported = ClothingCompressionPlugin.Library.LoadCoordinateEntries(MakeCoordinateKey(coordinate));
            foreach (DeformationEntry entry in imported) entries.Add(entry.Clone(true));
            if (imported.Count > 0) {
                selectedIndex = entries.Count - 1; Dirty = true; RequestApply();
                ClothingCompressionPlugin.Instance.RefreshMakerControls(this);
            }
            Status = imported.Count == 0 ? "所选搭配没有已保存的角色级形变。" : "已从其他搭配复制 " + imported.Count + " 个形变；请检查并重新绑定不匹配的物品。";
            return imported.Count;
        }

        internal void RequestApply(float delay = 0.12f) {
            if (pendingApply != null) StopCoroutine(pendingApply);
            pendingApply = StartCoroutine(ApplyAfterDelay(delay));
        }

        internal void ApplyNow() {
            if (pendingApply != null) StopCoroutine(pendingApply);
            pendingApply = null;
            try { RebuildShapes(); }
            catch (Exception ex) { ClothingCompressionPlugin.Log.LogError("Failed to rebuild compression shapes: " + ex); Status = "形变重算失败，请查看日志。"; }
        }

        private IEnumerator ApplyAfterDelay(float delay) {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return new WaitForEndOfFrame(); pendingApply = null; ApplyNow();
        }

        private void LoadLibraryEntries() {
            if (ChaControl == null) return;
            List<ItemDescriptor> items = GetItems();
            entries.Clear(); entries.AddRange(ClothingCompressionPlugin.Library.LoadEntries(CharacterKey, items));
            CapturePersistedEntryIds();
            // 0.2.8 briefly used out-of-range joint-relative values for LowerThigh.
            // The replacement whole-leg mode is normalized from ankle (0) to hip (1).
            foreach (DeformationEntry entry in entries.Where(x => x.Kind == DeformationKind.LowerThigh && (x.Position < 0f || x.Position > 1f))) entry.Position = 0.58f;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, entries.Count - 1));
            equipmentSignature = string.Join("|", items.Select(x => x.Key + "@" + x.Slot).OrderBy(x => x).ToArray());
            visibilitySignature = GetVisibilitySignature(); Dirty = false;
            Status = entries.Count == 0 ? "当前装备没有已保存形变。" : "已从 XML 加载 " + entries.Count + " 个形变。";
            ClothingCompressionPlugin.Instance.RefreshMakerControls(this);
        }

        private void CapturePersistedEntryIds() {
            persistedEntryIds.Clear();
            foreach (DeformationEntry entry in entries)
                if (entry != null && entry.BindingKind != BoundItemKind.None && !string.IsNullOrEmpty(entry.Id)) persistedEntryIds.Add(entry.Id);
        }

        private List<ItemDescriptor> GetItems() {
            if (frameItems == null || itemFrame != Time.frameCount) {
                frameItems = ItemCatalog.Get(ChaControl);
                itemFrame = Time.frameCount;
            }
            return frameItems;
        }

        private void RebuildShapes() {
            ClearMeshes();
            if (ChaControl == null || entries.Count == 0) {
                visibilitySignature = string.Empty;
                return;
            }
            // A character transferred between Maker and an H scene can retain a mesh
            // containing our old blend shapes even though this controller has no MeshState
            // for it. Use a generation-specific name so that rebuilding that inherited mesh
            // never collides with the old shape. ApplyWeights disables all older generations.
            shapeGeneration = Guid.NewGuid().ToString("N");
            SkinnedMeshRenderer body = FindBodyRenderer();
            if (body == null) { Status = "找不到角色身体网格 o_body_a。"; return; }
            ClothingCompressionPlugin.MeshAccess.Apply();
            try {
                Dictionary<string, RegionFrame> bodyFrames = new Dictionary<string, RegionFrame>();
                MeshState bodyState = BuildMesh(body, entries, false, bodyFrames);
                if (bodyState != null) meshes.Add(bodyState);
                // The primary binding controls visibility and XML matching. Every equipped
                // skinned item that contains the target bones follows the deformation so
                // overlapping layers (for example stockings plus a bodysuit) cannot clip.
                int skipped = 0;
                foreach (SkinnedMeshRenderer renderer in GetCharacterRenderers()) {
                    if (renderer == null || renderer.sharedMesh == null || renderer == body) continue;
                    try {
                        MeshState state = BuildMesh(renderer, entries, true, bodyFrames);
                        if (state != null) meshes.Add(state);
                    }
                    catch (Exception ex) {
                        skipped++;
                        ClothingCompressionPlugin.Log.LogWarning("Skipped clothing mesh during compression rebuild: renderer=" + renderer.name +
                            ", mesh=" + (renderer.sharedMesh == null ? "<null>" : renderer.sharedMesh.name) + ", error=" + ex);
                    }
                }
                if (skipped > 0) ClothingCompressionPlugin.Log.LogWarning("Compression rebuild skipped " + skipped + " incompatible clothing mesh(es).");
            } finally { ClothingCompressionPlugin.MeshAccess.Undo(); }
            ApplyWeights();
            visibilitySignature = GetVisibilitySignature();
            rendererSignature = GetRendererSignature();
            Status = "已生成 " + entries.Count + " 个身体形变；同步网格 " + Math.Max(0, meshes.Count - 1) + " 个。";
            if (ClothingCompressionPlugin.VerboseDiagnostics.Value)
                ClothingCompressionPlugin.Log.LogInfo(Status);
            LogMeshDiagnostics();
        }

        private MeshState BuildMesh(SkinnedMeshRenderer renderer, IList<DeformationEntry> sourceEntries, bool garment, Dictionary<string, RegionFrame> bodyFrames) {
            Transform[] bones = renderer.bones;
            bool hasApplicableRegion = false;
            foreach (DeformationEntry entry in sourceEntries) {
                if (garment && (entry.BindingKind == BoundItemKind.None || !entry.Enabled || !IsBindingVisible(entry))) continue;
                if (entry.Left && FindBone(bones, GetBones(entry.Kind, true)) >= 0 ||
                    entry.Right && FindBone(bones, GetBones(entry.Kind, false)) >= 0) {
                    hasApplicableRegion = true;
                    break;
                }
            }
            // Hair, facial meshes and unrelated accessories often have no target bones.
            // Do not clone/read their entire mesh merely to discover this in AddRegion.
            if (!hasApplicableRegion) return null;
            Mesh source = renderer.sharedMesh;
            Mesh generated = UnityEngine.Object.Instantiate(source);
            try {
                generated.name = source.name + " (ClothingCompression)";
                Vector3[] vertices = generated.vertices; BoneWeight[] weights = generated.boneWeights; Matrix4x4[] bindPoses = generated.bindposes;
                if (vertices == null || vertices.Length == 0 || weights == null || weights.Length != vertices.Length || bindPoses == null || bindPoses.Length == 0)
                    throw new InvalidOperationException("Mesh has incompatible vertex, bone-weight, or bind-pose data.");
                MeshState state = new MeshState { Renderer = renderer, Source = source, Generated = generated };
                Vector3[] bakedVertices = garment ? (Vector3[])vertices.Clone() : null;
                bool hasGarmentRegion = false;
                foreach (DeformationEntry entry in sourceEntries) {
                    // The editor's unbound mode previews body deformation only. Baked
                    // garment deltas also depend on visibility, so inactive entries can
                    // be omitted until the next visibility-triggered rebuild.
                    if (garment && (entry.BindingKind == BoundItemKind.None || !entry.Enabled || !IsBindingVisible(entry))) continue;
                    Vector3[] deltas = new Vector3[vertices.Length];
                    if (entry.Left) {
                        string key = entry.Id + ":L"; RegionFrame frame; bodyFrames.TryGetValue(key, out frame);
                        RegionFrame calculated = AddRegion(deltas, vertices, weights, bindPoses, bones, GetBones(entry.Kind, true), entry, garment, frame);
                        if (!garment && calculated != null) bodyFrames[key] = calculated;
                    }
                    if (entry.Right) {
                        string key = entry.Id + ":R"; RegionFrame frame; bodyFrames.TryGetValue(key, out frame);
                        RegionFrame calculated = AddRegion(deltas, vertices, weights, bindPoses, bones, GetBones(entry.Kind, false), entry, garment, frame);
                        if (!garment && calculated != null) bodyFrames[key] = calculated;
                    }
                    if (!deltas.Any(x => x.sqrMagnitude > 0.0000000001f)) continue;
                    state.RegionCount++;
                    for (int i = 0; i < deltas.Length; i++) state.MaxDelta = Mathf.Max(state.MaxDelta, deltas[i].magnitude);
                    if (garment) {
                        hasGarmentRegion = true;
                        if (entry.Enabled && IsBindingVisible(entry))
                            for (int i = 0; i < bakedVertices.Length; i++) bakedVertices[i] += deltas[i];
                        continue;
                    }
                    string shapeName = ShapePrefix + shapeGeneration + "." + entry.Id;
                    generated.AddBlendShapeFrame(shapeName, 100f, deltas, new Vector3[vertices.Length], new Vector3[vertices.Length]);
                    state.ShapeIndices[entry.Id] = generated.GetBlendShapeIndex(shapeName);
                }
                if (garment) {
                    if (!hasGarmentRegion) { UnityEngine.Object.Destroy(generated); return null; }
                    state.BakedDeltas = new Vector3[bakedVertices.Length];
                    for (int i = 0; i < bakedVertices.Length; i++) state.BakedDeltas[i] = bakedVertices[i] - vertices[i];
                    generated.vertices = bakedVertices;
                    generated.RecalculateBounds();
                    state.Baked = true;
                    // Reassigning even the same mesh is required to invalidate Unity 5.6's
                    // cached skinned vertex data in non-Maker scenes.
                    renderer.sharedMesh = generated;
                    renderer.sharedMesh = renderer.sharedMesh;
                    return state;
                }
                if (state.ShapeIndices.Count == 0) { UnityEngine.Object.Destroy(generated); return null; }
                renderer.sharedMesh = generated; return state;
            }
            catch {
                if (generated != null) UnityEngine.Object.Destroy(generated);
                throw;
            }
        }

        private void ApplyWeights() {
            foreach (MeshState state in meshes) {
                if (state.Renderer == null || state.Renderer.sharedMesh != state.Generated) continue;
                // Scene transitions can carry an older generated mesh into the new scene.
                // Its weights may still be 100, so explicitly disable every inherited
                // ClothingCompression shape before enabling this generation.
                for (int i = 0; i < state.Generated.blendShapeCount; i++) {
                    string name = state.Generated.GetBlendShapeName(i);
                    if (!string.IsNullOrEmpty(name) && name.StartsWith(ShapePrefix, StringComparison.Ordinal))
                        state.Renderer.SetBlendShapeWeight(i, 0f);
                }
                if (state.Baked) continue;
                foreach (DeformationEntry entry in entries) {
                    int index; if (!state.ShapeIndices.TryGetValue(entry.Id, out index)) continue;
                    bool active = entry.Enabled && IsBindingVisible(entry);
                    state.Renderer.SetBlendShapeWeight(index, active ? 100f : 0f);
                }
            }
        }

        private bool IsBindingVisible(DeformationEntry entry) {
            if (entry.BindingKind == BoundItemKind.None) return true;
            ItemDescriptor item = ItemCatalog.Resolve(GetItems(), entry);
            return item != null && item.Visible;
        }

        private string GetVisibilitySignature() {
            return string.Join("|", entries.Select(x => x.Id + ":" + (x.Enabled && IsBindingVisible(x) ? "1" : "0")).ToArray());
        }

        private bool WeightsNeedRefresh() {
            foreach (MeshState state in meshes) {
                if (state.Renderer == null || state.Renderer.sharedMesh != state.Generated) return false;
                if (state.Baked) {
                    for (int i = 0; i < state.Generated.blendShapeCount; i++) {
                        string name = state.Generated.GetBlendShapeName(i);
                        if (!string.IsNullOrEmpty(name) && name.StartsWith(ShapePrefix, StringComparison.Ordinal) &&
                            Mathf.Abs(state.Renderer.GetBlendShapeWeight(i)) > 0.01f) return true;
                    }
                    continue;
                }
                foreach (DeformationEntry entry in entries) {
                    int index; if (!state.ShapeIndices.TryGetValue(entry.Id, out index)) continue;
                    float expected = entry.Enabled && IsBindingVisible(entry) ? 100f : 0f;
                    if (Mathf.Abs(state.Renderer.GetBlendShapeWeight(index) - expected) > 0.01f) return true;
                }
            }
            return false;
        }

        private string GetRendererSignature() {
            List<string> values = new List<string>();
            SkinnedMeshRenderer body = FindBodyRenderer();
            if (body != null && body.sharedMesh != null) values.Add("B:" + body.GetInstanceID() + ":" + body.sharedMesh.GetInstanceID());
            foreach (SkinnedMeshRenderer renderer in GetCharacterRenderers())
                if (renderer != null && renderer != body && renderer.sharedMesh != null) values.Add("G:" + renderer.GetInstanceID() + ":" + renderer.sharedMesh.GetInstanceID());
            values.Sort(StringComparer.Ordinal); return string.Join("|", values.ToArray());
        }

        private List<SkinnedMeshRenderer> GetCharacterRenderers() {
            List<SkinnedMeshRenderer> result = new List<SkinnedMeshRenderer>();
            HashSet<int> seenRoots = new HashSet<int>();
            HashSet<int> seenRenderers = new HashSet<int>();
            Action<GameObject> addRoot = root => {
                if (root == null || !seenRoots.Add(root.GetInstanceID())) return;
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (renderer != null && seenRenderers.Add(renderer.GetInstanceID())) result.Add(renderer);
            };
            if (ChaControl == null) return result;
            // Maker exposes item roots reliably through objClothes/objAccessory. H scenes
            // can reparent or recreate those meshes without updating the catalog roots, so
            // the complete character hierarchy is the primary source and the arrays are
            // retained as fallbacks for externally parented objects.
            addRoot(ChaControl.gameObject);
            addRoot(ChaControl.objBody);
            if (ChaControl.objClothes != null) foreach (GameObject root in ChaControl.objClothes) addRoot(root);
            if (ChaControl.objAccessory != null) foreach (GameObject root in ChaControl.objAccessory) addRoot(root);
            return result;
        }

        private void ClearMeshes() {
            foreach (MeshState state in meshes) {
                if (state.Renderer != null && state.Renderer.sharedMesh == state.Generated) state.Renderer.sharedMesh = state.Source;
                else if (state.Renderer != null && state.Renderer.sharedMesh != null && state.Baked && state.BakedDeltas != null)
                    RecoverExternalBakedClone(state);
                if (state.Generated != null) UnityEngine.Object.Destroy(state.Generated);
            }
            meshes.Clear(); rendererSignature = string.Empty;
        }

        private static void RecoverExternalBakedClone(MeshState state) {
            Mesh current = state.Renderer.sharedMesh;
            if (current == null || state.Source == null || current.vertexCount != state.Source.vertexCount || current.vertexCount != state.BakedDeltas.Length) return;
            Vector3[] currentVertices = current.vertices, sourceVertices = state.Source.vertices;
            double cleanError = 0d, bakedError = 0d;
            for (int i = 0; i < currentVertices.Length; i++) {
                Vector3 fromSource = currentVertices[i] - sourceVertices[i];
                cleanError += fromSource.sqrMagnitude;
                bakedError += (fromSource - state.BakedDeltas[i]).sqrMagnitude;
            }
            // Some plugins clone our generated mesh, while others replace it with a fresh
            // source mesh. Only reverse our displacement when the actual vertex data is
            // measurably closer to the baked result; never rely on names or clone suffixes.
            if (bakedError + 1e-12d >= cleanError) {
                if (ClothingCompressionPlugin.VerboseDiagnostics.Value)
                    ClothingCompressionPlugin.Log.LogInfo("External mesh replacement is clean; no compression unbake needed: " + state.Renderer.name);
                return;
            }
            Mesh recovered = UnityEngine.Object.Instantiate(current);
            Vector3[] recoveredVertices = recovered.vertices;
            for (int i = 0; i < recoveredVertices.Length; i++) recoveredVertices[i] -= state.BakedDeltas[i];
            recovered.vertices = recoveredVertices; recovered.RecalculateBounds();
            recovered.name = current.name + " (CompressionRecovered)";
            state.Renderer.sharedMesh = recovered;
            ClothingCompressionPlugin.Log.LogInfo("Recovered externally cloned clothing mesh before compression rebuild: " + state.Renderer.name);
            if (ClothingCompressionPlugin.VerboseDiagnostics.Value)
                ClothingCompressionPlugin.Log.LogInfo("Recovery comparison: cleanError=" + cleanError.ToString("0.000000000") +
                    ", bakedError=" + bakedError.ToString("0.000000000"));
        }

        private void LogMeshDiagnostics() {
            if (!ClothingCompressionPlugin.VerboseDiagnostics.Value) return;
            string topology = Application.loadedLevelName + "|" + string.Join("|", meshes.Select(x =>
                x.Renderer == null || x.Source == null ? "<missing>" : GetTransformPath(x.Renderer.transform) + ":" + x.Source.name + ":" + x.Source.vertexCount + ":" + x.Renderer.bones.Length).ToArray());
            if (string.Equals(topology, diagnosticSignature, StringComparison.Ordinal)) return;
            diagnosticSignature = topology;
            ClothingCompressionPlugin.Log.LogInfo("Compression mesh diagnostic: scene=" + Application.loadedLevelName + ", character=" + CharacterIdentityKey +
                ", entries=" + string.Join("; ", entries.Select(x => x.Kind + "[pos=" + x.Position.ToString("0.###") + ",width=" + x.Width.ToString("0.###") +
                    ",depth=" + x.Depth.ToString("0.###") + ",clear=" + x.GarmentClearanceX.ToString("0.###") + "/" + x.GarmentClearanceY.ToString("0.###") + "/" + x.GarmentClearanceZ.ToString("0.###") + "]").ToArray()));
            foreach (MeshState state in meshes) {
                if (state.Renderer == null || state.Source == null) continue;
                ClothingCompressionPlugin.Log.LogInfo("Compression mesh: path=" + GetTransformPath(state.Renderer.transform) +
                    ", mesh=" + state.Source.name + ", verts=" + state.Source.vertexCount + ", bones=" + state.Renderer.bones.Length +
                    ", rootBone=" + (state.Renderer.rootBone == null ? "<null>" : state.Renderer.rootBone.name) +
                    ", vertices=" + GetVertexFingerprint(state.Source) + ", skin=" + GetSkinningFingerprint(state.Source, state.Renderer.bones) + ", regions=" + state.RegionCount +
                    ", maxDelta=" + state.MaxDelta.ToString("0.000000") + ", mode=" + (state.Baked ? "baked" : "blendshape"));
            }
        }

        private string GetTransformPath(Transform value) {
            if (value == null) return "<null>";
            List<string> names = new List<string>(); Transform stop = ChaControl == null ? null : ChaControl.transform;
            while (value != null) { names.Add(value.name); if (value == stop) break; value = value.parent; }
            names.Reverse(); return string.Join("/", names.ToArray());
        }

        private static string GetSkinningFingerprint(Mesh mesh, Transform[] bones) {
            unchecked {
                int hash = 17;
                Matrix4x4[] bindPoses = mesh.bindposes;
                for (int i = 0; i < bindPoses.Length; i++) for (int j = 0; j < 16; j++) hash = hash * 31 + bindPoses[i][j].GetHashCode();
                BoneWeight[] weights = mesh.boneWeights;
                for (int i = 0; i < weights.Length; i++) {
                    BoneWeight w = weights[i];
                    hash = hash * 31 + w.boneIndex0; hash = hash * 31 + w.boneIndex1; hash = hash * 31 + w.boneIndex2; hash = hash * 31 + w.boneIndex3;
                    hash = hash * 31 + w.weight0.GetHashCode(); hash = hash * 31 + w.weight1.GetHashCode(); hash = hash * 31 + w.weight2.GetHashCode(); hash = hash * 31 + w.weight3.GetHashCode();
                }
                if (bones != null) for (int i = 0; i < bones.Length; i++) hash = hash * 31 + (bones[i] == null ? 0 : bones[i].name.GetHashCode());
                return hash.ToString("X8");
            }
        }

        private static string GetVertexFingerprint(Mesh mesh) {
            unchecked {
                int hash = 17; Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) {
                    hash = hash * 31 + vertices[i].x.GetHashCode(); hash = hash * 31 + vertices[i].y.GetHashCode(); hash = hash * 31 + vertices[i].z.GetHashCode();
                }
                return hash.ToString("X8");
            }
        }

        private SkinnedMeshRenderer FindBodyRenderer() {
            GameObject root = ChaControl.objBody != null ? ChaControl.objBody : ChaControl.gameObject;
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(x => x != null && x.sharedMesh != null && string.Equals(x.name, "o_body_a", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.sharedMesh.vertexCount).FirstOrDefault();
        }

        private static string[] GetBones(DeformationKind kind, bool left) {
            if (kind == DeformationKind.Thigh) return left ? ThighLeftBones : ThighRightBones;
            if (kind == DeformationKind.Knee || kind == DeformationKind.LowerThigh) return left ? KneeLeftBones : KneeRightBones;
            if (kind == DeformationKind.UpperArm) return left ? UpperArmLeftBones : UpperArmRightBones;
            return left ? ArmLeftBones : ArmRightBones;
        }

        private static RegionFrame AddRegion(Vector3[] output, Vector3[] vertices, BoneWeight[] weights, Matrix4x4[] bindPoses,
            Transform[] bones, string[] boneNames, DeformationEntry entry, bool garment, RegionFrame bodyFrame) {
            int bone = FindBone(bones, boneNames); if (bone < 0 || bone >= bindPoses.Length) return null;
            bool jointRegion = entry.Kind == DeformationKind.Knee || entry.Kind == DeformationKind.LowerThigh;
            bool wholeLegRegion = entry.Kind == DeformationKind.LowerThigh;
            bool leftSide = boneNames.Any(x => x.EndsWith("_L", StringComparison.OrdinalIgnoreCase));
            List<int> influenceBones = entry.Kind == DeformationKind.Arm ? new List<int>() : new List<int> { bone };
            if (jointRegion) {
                string[] names = entry.Kind == DeformationKind.LowerThigh
                    ? (leftSide ? LowerThighInfluenceLeft : LowerThighInfluenceRight)
                    : (leftSide ? KneeInfluenceLeft : KneeInfluenceRight);
                AddMatchingBones(influenceBones, bones, names);
            }
            else if (entry.Kind == DeformationKind.UpperArm) AddMatchingBones(influenceBones, bones, leftSide ? UpperArmLeftBones : UpperArmRightBones);
            else if (entry.Kind == DeformationKind.Arm) {
                AddMatchingBones(influenceBones, bones, leftSide ? ForearmInfluenceLeft : ForearmInfluenceRight);
                // Some modded meshes only retain arm00 weights. Keep a fallback so the
                // deformation remains usable, while stock meshes use the forearm helpers.
                if (influenceBones.Count == 0) influenceBones.Add(bone);
            }
            Matrix4x4 toBone = bindPoses[bone], toMesh = toBone.inverse;
            List<int> indices = new List<int>(); List<Vector3> points = new List<Vector3>();
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 primaryMin = min, primaryMax = max; int primaryCount = 0;
            for (int i = 0; i < vertices.Length; i++) {
                float primaryInfluence = GetInfluence(weights[i], bone);
                float influence = GetInfluence(weights[i], influenceBones);
                float threshold = jointRegion ? 0.025f : 0.12f;
                if (influence < threshold) continue;
                Vector3 p = toBone.MultiplyPoint3x4(vertices[i]); indices.Add(i); points.Add(p); min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                if (primaryInfluence >= 0.12f) { primaryMin = Vector3.Min(primaryMin, p); primaryMax = Vector3.Max(primaryMax, p); primaryCount++; }
            }
            if (indices.Count == 0) return null;
            bool shareBodyFrame = garment && bodyFrame != null && bones[bone] != null && string.Equals(bones[bone].name, bodyFrame.BoneName, StringComparison.OrdinalIgnoreCase);
            // Whole-leg mode deliberately uses the combined thigh-to-calf vertex extent.
            // Knee mode stays centered on leg01 and uses its local extent.
            Vector3 ranges = jointRegion && !wholeLegRegion && primaryCount > 0 ? primaryMax - primaryMin : max - min;
            int axis = shareBodyFrame ? bodyFrame.Axis : (ranges.x >= ranges.y && ranges.x >= ranges.z ? 0 : ranges.y >= ranges.z ? 1 : 2);
            float axisMin = GetAxis(jointRegion && !wholeLegRegion && primaryCount > 0 ? primaryMin : min, axis), range = GetAxis(ranges, axis); if (range < 0.0001f) return null;
            float proximalSign = shareBodyFrame ? bodyFrame.ProximalSign : GetBindPoseProximalSign(bones, bindPoses, bone, axis);
            float centerT;
            if (shareBodyFrame) centerT = bodyFrame.CenterT;
            else if (wholeLegRegion) centerT = axisMin + Mathf.Clamp01(entry.Position) * range;
            else if (jointRegion) {
                // leg01's origin is the knee. Positive position travels down the calf;
                // negative values move slightly above the knee into thigh-weighted vertices.
                float fartherEnd = Mathf.Abs(axisMin) >= Mathf.Abs(GetAxis(jointRegion && primaryCount > 0 ? primaryMax : max, axis)) ? axisMin : GetAxis(jointRegion && primaryCount > 0 ? primaryMax : max, axis);
                centerT = entry.Position * range * (Mathf.Abs(fartherEnd) < 0.0001f ? 1f : Mathf.Sign(fartherEnd));
            }
            else if (entry.Kind == DeformationKind.UpperArm || entry.Kind == DeformationKind.Arm) {
                // Position 0 is always shoulder/elbow-side and 1 is elbow/wrist-side,
                // independent of the mirrored local axis used by left and right arms.
                centerT = proximalSign >= 0f ? axisMin + range - entry.Position * range : axisMin + entry.Position * range;
            }
            else centerT = axisMin + entry.Position * range;
            float sigma = shareBodyFrame ? bodyFrame.Sigma : Mathf.Max(range * Mathf.Clamp(entry.Width, 0.015f, 0.35f), 0.0005f);
            Vector3 radialCenter = shareBodyFrame ? bodyFrame.RadialCenter : Vector3.zero;
            if (!shareBodyFrame) {
                float centerWeight = 0f;
                foreach (Vector3 raw in points) { Vector3 p = raw; float d = GetAxis(p, axis) - centerT; float w = Mathf.Exp(-(d * d) / (2f * sigma * sigma)); SetAxis(ref p, axis, 0f); radialCenter += p * w; centerWeight += w; }
                if (centerWeight > 0f) radialCenter /= centerWeight;
            }
            for (int n = 0; n < indices.Count; n++) {
                int i = indices[n]; Vector3 p = points[n]; float d = GetAxis(p, axis) - centerT; if (Mathf.Abs(d) > sigma * 3.2f) continue;
                Vector3 radial = p; SetAxis(ref radial, axis, 0f); radial -= radialCenter; if (radial.sqrMagnitude < 0.00000001f) continue;
                float pinch = Mathf.Exp(-(d * d) / (2f * sigma * sigma)); float sideSigma = sigma * 0.72f, sideOffset = sigma * 1.45f;
                float proximal = Mathf.Exp(-((d + sideOffset) * (d + sideOffset)) / (2f * sideSigma * sideSigma));
                float distal = Mathf.Exp(-((d - sideOffset) * (d - sideOffset)) / (2f * sideSigma * sideSigma));
                // Move the garment contraction peak slightly toward the torso. A symmetric
                // Gaussian makes the free edge flare outward; this shoulder produces a cuff
                // whose outer face inclines inward while keeping the lower garment smooth.
                float cuffShoulder = 0f;
                if (garment) {
                    float shifted = d - proximalSign * sigma * 0.72f;
                    cuffShoulder = 1.2f * Mathf.Exp(-(shifted * shifted) / (2f * sigma * sigma * 0.64f));
                }
                // Clothing must follow the same inward field as the body. The previous
                // garmentPinch=max(pinch,cuffShoulder) pushed the cuff farther inward than
                // the skin, which clips on joint-spanning LowerThigh regions. Keep the
                // shifted shoulder only as a wider safety envelope for outward clearance.
                float amount = -Mathf.Max(0f, entry.Depth) * pinch + Mathf.Max(0f, entry.ProximalBulge) * proximal + Mathf.Max(0f, entry.DistalBulge) * distal;
                float rawInfluence = GetInfluence(weights[i], influenceBones);
                float influence = Mathf.Clamp01(rawInfluence / 0.65f);
                Vector3 radialDirection = radial.normalized;
                Vector3 deltaInMesh = toMesh.MultiplyVector(radialDirection * amount) * influence;
                if (garment) {
                    // X/Z mean character mesh axes, not bone-local axes. Some clothing bones
                    // use Z as their longitudinal axis, which made the old Z clearance invisible.
                    Vector3 outwardInMesh = toMesh.MultiplyVector(radialDirection).normalized;
                    float envelope = Mathf.Max(Mathf.Max(pinch, cuffShoulder), Mathf.Max(proximal, distal));
                    float clearanceInfluence = Mathf.Sqrt(influence);
                    deltaInMesh += new Vector3(outwardInMesh.x * Mathf.Max(0f, entry.GarmentClearanceX),
                        outwardInMesh.y * Mathf.Max(0f, entry.GarmentClearanceY),
                        outwardInMesh.z * Mathf.Max(0f, entry.GarmentClearanceZ)) * envelope * clearanceInfluence;
                }
                output[i] += deltaInMesh;
            }
            return new RegionFrame { BoneName = bones[bone] == null ? string.Empty : bones[bone].name, Axis = axis, CenterT = centerT, Sigma = sigma, RadialCenter = radialCenter, ProximalSign = proximalSign };
        }

        private static float GetBindPoseProximalSign(Transform[] bones, Matrix4x4[] bindPoses, int bone, int axis) {
            if (bones == null || bindPoses == null || bone < 0 || bone >= bones.Length || bone >= bindPoses.Length || bones[bone] == null) return 1f;
            // Never use live Transform positions here. H animation/IK can rotate helper
            // bones far enough to reverse the apparent parent direction, even though all
            // deformation coordinates are in bind space. Find the nearest skinned ancestor
            // and express its bind-pose origin in this bone's bind coordinate system.
            Transform ancestor = bones[bone].parent; int parentIndex = -1;
            while (ancestor != null && parentIndex < 0) {
                for (int i = 0; i < bones.Length && i < bindPoses.Length; i++)
                    if (bones[i] == ancestor) { parentIndex = i; break; }
                ancestor = ancestor.parent;
            }
            if (parentIndex < 0) return 1f;
            Vector3 parentOriginInMesh = bindPoses[parentIndex].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 parentInBone = bindPoses[bone].MultiplyPoint3x4(parentOriginInMesh);
            float towardParent = GetAxis(parentInBone, axis);
            return Mathf.Abs(towardParent) < 0.0001f ? 1f : Mathf.Sign(towardParent);
        }

        private static int FindBone(Transform[] bones, string[] names) { if (bones == null) return -1; for (int n = 0; n < names.Length; n++) for (int i = 0; i < bones.Length; i++) if (bones[i] != null && string.Equals(bones[i].name, names[n], StringComparison.OrdinalIgnoreCase)) return i; return -1; }
        private static void AddMatchingBones(List<int> result, Transform[] bones, string[] names) {
            for (int n = 0; n < names.Length; n++) for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && string.Equals(bones[i].name, names[n], StringComparison.OrdinalIgnoreCase) && !result.Contains(i)) result.Add(i);
        }
        private static float GetInfluence(BoneWeight w, int bone) { float r = 0f; if (w.boneIndex0 == bone) r += w.weight0; if (w.boneIndex1 == bone) r += w.weight1; if (w.boneIndex2 == bone) r += w.weight2; if (w.boneIndex3 == bone) r += w.weight3; return r; }
        private static float GetInfluence(BoneWeight w, IList<int> bones) { float value = 0f; for (int i = 0; i < bones.Count; i++) value += GetInfluence(w, bones[i]); return value; }
        private static float GetAxis(Vector3 v, int a) { return a == 0 ? v.x : a == 1 ? v.y : v.z; }
        private static void SetAxis(ref Vector3 v, int a, float x) { if (a == 0) v.x = x; else if (a == 1) v.y = x; else v.z = x; }

        private sealed class MeshState {
            internal SkinnedMeshRenderer Renderer; internal Mesh Source; internal Mesh Generated;
            internal bool Baked;
            internal Vector3[] BakedDeltas;
            internal int RegionCount;
            internal float MaxDelta;
            internal readonly Dictionary<string, int> ShapeIndices = new Dictionary<string, int>();
        }

        private sealed class RegionFrame {
            internal string BoneName; internal int Axis; internal float CenterT; internal float Sigma; internal Vector3 RadialCenter; internal float ProximalSign;
        }

    }
}
