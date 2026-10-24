using System;
using System.Linq;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Studio;
using MaximumUI;
using UnityEngine;

namespace ClothingCompression {
    /// <summary>Connects the retained editor to the game's selected character.</summary>
    internal sealed class CompressionGui : IDisposable {
        private const string MountId = ClothingCompressionPlugin.Guid + ".editor";
        private readonly CompressionEditorView view;
        private readonly CompressionEditorData data = new CompressionEditorData();
        private readonly Func<CompressionController> findController;
        private readonly MountOptions options = new MountOptions {
            Visible = false, BuildEveryFrame = false, SortOrder = 120,
            SoftMouseCapture = true, SoftKeyboardCapture = true
        };
        private bool mounted;
        private bool refresh = true;
        private float nextSelectionPoll;
        private int coordinate = -1, selected = -1, count = -1;
        private bool dirty;
        private string status;

        internal CompressionGui(Func<CompressionController> findController = null) {
            this.findController = findController ?? FindController;
            view = new CompressionEditorView(ClothingCompressionPlugin.Name + " " + ClothingCompressionPlugin.Version) {
                OnInvalidated = Invalidate,
                OnVisibleChange = value => Visible = value,
                IsCurrentController = candidate => ReferenceEquals(this.findController(), candidate)
            };
        }

        internal CompressionEditorView View { get { return view; } }
        internal CompressionEditorData Data { get { return data; } }

        internal bool Visible {
            get { return options.Visible; }
            set {
                if (options.Visible == value) return;
                if (mounted) MaximumUIRuntime.SetVisible(MountId, value);
                else options.Visible = value;
                Invalidate();
                nextSelectionPoll = 0f;
                if (value && mounted) MaximumUIRuntime.Focus(MountId);
            }
        }

        internal void Invalidate() {
            refresh = true;
            MaximumUIRuntime.MarkDirty(view);
        }

        internal void Update() {
            if (!Visible) return;
            // Maker callbacks invalidate immediately. Studio/external selection is
            // polled separately so idle repaints do not scan controllers or items.
            if (Time.realtimeSinceStartup >= nextSelectionPoll) {
                nextSelectionPoll = Time.realtimeSinceStartup + 0.2f;
                CompressionController current = findController();
                if (!ReferenceEquals(view.Controller, current)) {
                    view.Controller = current;
                    Invalidate();
                }
            }
            // Refresh checks only identities/revisions when the source is unchanged.
            // Numeric edits and status messages never rescan equipment or clone imports.
            RefreshModel();
        }

        // Called from OnGUI: initialize native theme resources in a valid IMGUI
        // context. The shared MaximumUI host owns all actual drawing.
        internal void Draw() {
            if (!Visible) return;
            if (refresh) RefreshModel();
            if (mounted) return;
            MaximumUIRuntime.EnsureHost();
            MaximumUIRuntime.Mount(MountId, view, options);
            mounted = true;
            MaximumUIRuntime.Focus(MountId);
        }

        private void RefreshModel() {
            CompressionController controller = view.Controller;
            view.ValidateImportCoordinate();
            bool changed = data.Refresh(controller, view.ImportCoordinate);
            if (changed) {
                view.BindingItems = data.BindingItems;
                view.ImportEntries = data.ImportEntries;
            }
            if (controller != null && controller.ChaControl != null) {
                changed |= coordinate != controller.CoordinateIndex || selected != controller.SelectedIndex ||
                    count != controller.Entries.Count || dirty != controller.Dirty || status != controller.Status;
                coordinate = controller.CoordinateIndex;
                selected = controller.SelectedIndex;
                count = controller.Entries.Count;
                dirty = controller.Dirty;
                status = controller.Status;
            } else {
                changed |= coordinate != -1;
                coordinate = selected = count = -1;
                dirty = false;
                status = null;
            }
            refresh = false;
            if (changed) MaximumUIRuntime.MarkDirty(view);
        }

        public void Dispose() {
            options.Visible = false;
            if (mounted) MaximumUIRuntime.Unmount(MountId);
            mounted = false;
            view.Controller = null;
            data.Clear();
            view.BindingItems = data.BindingItems;
            view.ImportEntries = data.ImportEntries;
        }

        private static CompressionController FindController() {
            if (MakerAPI.InsideMaker) {
                ChaControl maker = MakerAPI.GetCharacterControl();
                return maker == null ? null : maker.GetComponent<CompressionController>();
            }
            if (StudioAPI.InsideStudio) {
                return StudioAPI.GetSelectedCharacters().Select(x => x.GetChaControl()).Where(x => x != null)
                    .Select(x => x.GetComponent<CompressionController>()).FirstOrDefault(x => x != null);
            }
            CharacterApi.ControllerRegistration registration = CharacterApi.GetRegisteredBehaviour(ClothingCompressionPlugin.Guid);
            return registration == null ? null : registration.Instances.OfType<CompressionController>().FirstOrDefault(x => x.ChaControl != null);
        }
    }
}
