using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using MaximumUI;
using UnityEngine;

[assembly: System.Reflection.AssemblyFileVersion(MaximumUIGallery.MaximumUIGalleryPlugin.Version)]

namespace MaximumUIGallery {
    [BepInPlugin(GUID, "MaximumUI Gallery", Version)]
    public sealed class MaximumUIGalleryPlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.MaximumUIGallery";
        public const string Version = "0.5.0";

        private const string MountId = GUID + ".main";
        private ConfigEntry<KeyboardShortcut> shortcut;
        private GalleryView view;

        private void Awake() {
            shortcut = Config.Bind("General", "Show gallery", new KeyboardShortcut(KeyCode.F10), "Show or hide the MaximumUI Gallery.");
            bool show = Config.Bind("General", "Show on startup", false, "Open the optional Gallery when the plugin starts.").Value;
            view = new GalleryView { ShortcutText = shortcut.Value.ToString() };
            shortcut.SettingChanged += OnShortcutChanged;
            MaximumUIRuntime.EnsureHost();
            MaximumUIRuntime.Mount(
                MountId,
                view,
                new MountOptions {
                    Visible = show,
                    SortOrder = 500,
                    BuildEveryFrame = false,
                    Theme = MaximumTheme.MetroLight
                });
        }

        private void Update() {
            if (!MaximumUIRuntime.IsAnyTextFieldFocused && !MaximumUIRuntime.IsModalOpen && shortcut.Value.IsDown())
                MaximumUIRuntime.SetVisible(MountId, !MaximumUIRuntime.IsVisible(MountId));
        }

        private void OnShortcutChanged(object sender, EventArgs args) {
            view.ShortcutText = shortcut.Value.ToString();
            MaximumUIRuntime.MarkDirty(view);
        }

        private void OnDestroy() {
            if (shortcut != null) shortcut.SettingChanged -= OnShortcutChanged;
            MaximumUIRuntime.Unmount(MountId);
        }
    }

    internal sealed class GalleryState {
        public bool Enabled = true;
        public bool FoldoutOpen = true;
        public bool HeaderOpen = true;
        public bool ClosableHeaderVisible = true;
        public int ExampleTab;
        public bool ShowInlineDialog;
        public int Tab;
        public bool PreviewDisabled;
        public int LargeSelection = -1;
        public string LargeFilter = string.Empty;
        public bool ThemePending;
        public string Status = "Ready";
        public float Slider = 0.45f;
        public int IntValue = 7;
        public float FloatValue = 0.5f;
        public double DoubleValue = 3.14;
        public int DragIntValue = 3;
        public float DragFloatValue = 0.25f;
        public int SliderIntValue = 25;
        public float AngleValue = 30f;
        public float SplitRatio = 0.45f;
        public string Text = "MaximumUI";
        public string Code = "ABC123";
        public string Hex = "DEAD";
        public string Notes = "Multi-line text";
        public string Password = "secret";
        public string Mode = "Normal";
        public string ListSelection = "Third item";
        public string ListFilter = string.Empty;
        public bool EmptyScrollSamples;
        public string DropMessage = "Drop target";
        public int ClickCount;
        public int RadioIndex;
        public TableSortDirection TableSort = TableSortDirection.None;
        public string Search = string.Empty;
        public bool Selectable = true;
        public SelectionState<string> MultiSelection = new SelectionState<string>();
        public bool TreeOpen = true;
        public bool TreeSelected;
        public MuiColor Color = new MuiColor(1f, 0.25f, 0.25f, 1f);
        public ThemeTokens EditableTheme = ThemePalettes.MetroLight();
        public float FontSize = 14f;
        public string ThemeName = "Metro Light";
        public float VectorX = 1f;
        public float VectorY = 2f;
        public float VectorZ = 3f;
    }

    internal sealed class GalleryView : StatefulView<GalleryState> {
        public string ShortcutText = "F10";
        private float nextMetricsRefresh;
        private MaximumTheme ownedTheme;
        private ThemeTokens activePalette = ThemePalettes.MetroLight();
        private string[] largeItems;
        private readonly List<int> visibleItems = new List<int>();
        private string lastLargeFilter;
        private static readonly string[] pageNames = GalleryScenes.Titles;
        private float RowHeight { get { return (float)Math.Ceiling(Math.Max(32f, State.FontSize + 16f) / 4f) * 4f; } }
        private float FieldLabelWidth { get { return (float)Math.Ceiling(Math.Max(112f, State.FontSize * 7.5f) / 4f) * 4f; } }
        private readonly string[] modes = { "Normal", "Compact", "Expanded", "Debug" };
        private readonly string[] samples = {
            "First item",
            "Second item",
            "Third item",
            "Fourth item",
            "Fifth item",
            "Sixth item",
            "Seventh item",
            "Eighth item",
            "Ninth item",
            "Tenth item"
        };
        private readonly float[] plotValues = {
            0.12f, 0.32f, 0.24f, 0.58f, 0.48f, 0.82f, 0.62f, 0.74f, 0.35f, 0.56f, 0.42f, 0.90f
        };
        private readonly float[] fontSizes = { 12f, 14f, 15f, 16f, 18f, 20f };
        private readonly string[] themeNames = { "Metro Light", "Metro Dark", "Clean Light", "Expression Light" };
        private static readonly ThemeColorToken[] nativeColors = {
            ThemeColorToken.WindowBg, ThemeColorToken.PanelBg, ThemeColorToken.TitleBg,
            ThemeColorToken.DialogBg, ThemeColorToken.DropdownBg, ThemeColorToken.Backdrop,
            ThemeColorToken.Text, ThemeColorToken.TextMuted, ThemeColorToken.TextDisabled, ThemeColorToken.Border, ThemeColorToken.Separator,
            ThemeColorToken.ControlBorder, ThemeColorToken.ControlHoverBorder,
            ThemeColorToken.Accent, ThemeColorToken.AccentHover, ThemeColorToken.AccentPressed, ThemeColorToken.OnAccent, ThemeColorToken.FocusRing,
            ThemeColorToken.ButtonBg, ThemeColorToken.ButtonHoverBg, ThemeColorToken.ButtonActiveBg, ThemeColorToken.ButtonDisabledBg,
            ThemeColorToken.FieldBg, ThemeColorToken.FieldHoverBg, ThemeColorToken.FieldActiveBg,
            ThemeColorToken.SelectedBg, ThemeColorToken.InfoBg, ThemeColorToken.SuccessBg,
            ThemeColorToken.WarningBg, ThemeColorToken.ErrorBg
        };
        private static readonly ThemeScalarToken[] nativeScalars = {
            ThemeScalarToken.FontSize, ThemeScalarToken.ToggleBoxSize, ThemeScalarToken.SliderTrackHeight,
            ThemeScalarToken.SliderThumbWidth, ThemeScalarToken.SliderThumbHeight
        };


        public GalleryView()
            : base(new GalleryState()) {
        }

        public override void Update(BuildContext context) {
            if (State.Tab == 11 && Time.unscaledTime >= nextMetricsRefresh) {
                nextMetricsRefresh = Time.unscaledTime + 0.25f;
                MaximumUIRuntime.MarkDirty(this);
            }
        }

        public override void OnMount(BuildContext context) {
            activePalette = context.Theme.Tokens;
            State.FontSize = context.Theme.FontSize;
            State.ThemeName = context.Theme.Name;
            State.EditableTheme = context.Theme.Tokens.Clone();
        }

        public override void OnUnmount(BuildContext context) {
            if (ownedTheme != null) ownedTheme.Dispose();
            ownedTheme = null;
        }

        internal void ConfigureScene(int page, ThemeTokens palette, string state) {
            activePalette = palette.Clone();
            State.Tab = page;
            State.FontSize = palette.FontSize;
            State.EditableTheme = palette.Clone();
            State.ThemeName = palette.WindowBg.R > 0.5f ? "Metro Light" : "Metro Dark";
            State.PreviewDisabled = state == "disabled";
            if (state == "edited") {
                State.Text = "Portrait"; State.Mode = "Compact"; State.Slider = 0.75f;
                State.Status = "Profile updated";
            }
            if (state == "selected") {
                State.LargeSelection = 1234; State.TreeSelected = true; State.RadioIndex = 1;
                State.MultiSelection.Apply(samples, samples[1], false, false);
                State.MultiSelection.Apply(samples, samples[3], true, true);
            }
            if (state == "collapsed") {
                State.TreeOpen = false; State.HeaderOpen = false; State.FoldoutOpen = false;
            }
            if (state == "activity-tab") State.ExampleTab = 1;
            if (state == "settings-tab") State.ExampleTab = 2;
            if (state == "empty-lists") State.EmptyScrollSamples = true;
            if (state == "dialog-open") State.ShowInlineDialog = true;
            if (state == "pending") {
                State.ThemePending = true; State.EditableTheme.FontSize = 16f;
            }
            if (state == "filtered") State.LargeFilter = "0012";
            if (state == "empty") State.LargeFilter = "no matching preset";
        }

        public override Widget Build(BuildContext context) {
            RegisterShortcuts(context);
            int page = Math.Max(0, Math.Min(pageNames.Length - 1, State.Tab));
            return new Window("MaximumUI Gallery", width: 1000f, height: 760f,
                key: "gallery", minWidth: 820f, minHeight: 600f, foldable: true, closable: false) {
                FoldedHeight = Math.Max(32f, State.FontSize + 18f)
            }.Content(new VStack(gap: 12f) {
                new HStack(gap: 24f) {
                    BuildSidebar().Width(172f).ExpandHeight(),
                    new VStack(gap: 16f) {
                        new Label(pageNames[page]) { Role = LabelRole.Heading }.Height(Math.Max(36f, ThemeTypography.LineHeight(activePalette, LabelRole.Heading))),
                        BuildActivePage(context, page).ExpandHeight().ExpandWidth()
                    }.ExpandWidth().ExpandHeight()
                }.ExpandWidth().ExpandHeight(),
                new Separator(),
                new HStack(gap: 12f) {
                    new Label(State.Status) { Role = LabelRole.Caption }.ExpandWidth(),
                    new Label(ShortcutText) { Role = LabelRole.Caption }
                }.Height(24f)
            }.ExpandHeight());
        }

        private Widget BuildSidebar() {
            VStack navigation = new VStack(gap: 0f);
            for (int i = 0; i < pageNames.Length; i++) navigation.Add(NavItem(pageNames[i], i));
            navigation.Add(new Spacer());
            navigation.Add(new Label("MaximumUI " + MaximumUIGalleryPlugin.Version) { Role = LabelRole.Caption }.Height(24f));
            return navigation;
        }

        private Widget NavItem(string text, int page) {
            return new Selectable(text, State.Tab == page, delegate(bool selected) {
                SetState(delegate(GalleryState s) { s.Tab = page; s.ShowInlineDialog = false; });
            }, key: "nav-" + GalleryScenes.Ids[page]).Height(36f).ExpandWidth();
        }

        private Widget BuildActivePage(BuildContext context, int page) {
            Widget content;
            switch (page) {
                case 1: content = BuildButtonsPage(); break;
                case 2: content = BuildTextPage(); break;
                case 3: content = BuildNumericPage(); break;
                case 4: content = BuildSelectionPage(); break;
                case 5: content = BuildColorsPage(); break;
                case 6: content = BuildDrawingPage(context); break;
                case 7: content = BuildLayoutTab(context); break;
                case 8: content = BuildDialogsTab(context); break;
                case 9: return BuildStyleTab(context);
                case 10: return BuildLargeList();
                case 11: return BuildDebugTab(context);
                default: content = BuildOverview(context); break;
            }
            return new ScrollView(key: "page-scroll-" + GalleryScenes.Ids[page])
                .Content(content.Disabled(State.PreviewDisabled)).ExpandHeight().ExpandWidth();
        }

        private VStack Section(string title, params Widget[] children) {
            VStack section = new VStack(gap: 8f);
            section.Add(new Label(title) { Role = LabelRole.Subheading }.Height((float)Math.Ceiling(ThemeTypography.LineHeight(activePalette, LabelRole.Subheading) / 4f) * 4f));
            for (int i = 0; i < children.Length; i++) section.Add(children[i]);
            return section.ExpandWidth();
        }

        private Widget Field(string label, Widget input) {
            return new HStack(gap: 16f) {
                new Label(label).Width(FieldLabelWidth), input.ExpandWidth()
            }.Height(RowHeight).ExpandWidth();
        }

        private void CountClick() {
            SetState(delegate(GalleryState s) { s.ClickCount++; s.Status = "Clicks: " + s.ClickCount.ToString(); });
        }

        private Widget BuildOverview(BuildContext context) {
            return new VStack(gap: 12f) {
                Field("Name", new TextField(State.Text, delegate(string value) {
                    SetState(delegate(GalleryState s) { s.Text = value; });
                }, key: "overview-name") { Placeholder = "Profile name" }),
                Field("Mode", new ComboBox<string>(string.Empty, State.Mode, modes, delegate(string value) { return value; }, delegate(string value) {
                    SetState(delegate(GalleryState s) { s.Mode = value; });
                }, key: "overview-mode")),
                new HStack(gap: 16f) {
                    new Label("Strength").Width(FieldLabelWidth),
                    new Slider(State.Slider, 0f, 1f, delegate(float value) {
                        SetState(delegate(GalleryState s) { s.Slider = value; });
                    }, key: "overview-strength").ExpandWidth(),
                    new Label((State.Slider * 100f).ToString("0") + "%").Width(44f)
                }.Height(RowHeight),
                new ToggleSwitch("Enable profile", State.Enabled, delegate(bool value) {
                    SetState(delegate(GalleryState s) { s.Enabled = value; });
                }, key: "overview-enable").Height(RowHeight),
                new HStack(gap: 8f) {
                    new Button("Apply", delegate { SetState(delegate(GalleryState s) { s.Status = "Profile updated"; }); }, key: "overview-apply") { Variant = ButtonVariant.Primary }.Width(96f),
                    new Button("Reset", delegate {
                        SetState(delegate(GalleryState s) { s.Text = "MaximumUI"; s.Mode = "Normal"; s.Slider = 0.45f; s.Enabled = true; s.Status = "Profile reset"; });
                    }, key: "overview-reset").Width(96f), new Spacer()
                }.Height(RowHeight)
            };
        }

        private Widget BuildLargeList() {
            RefreshLargeItems();
            return new VStack(gap: 12f) {
                new SearchBox(State.LargeFilter, delegate(string value) {
                    SetState(delegate(GalleryState s) { s.LargeFilter = value; });
                }, key: "large-filter") { Placeholder = "Search presets" }.Height(RowHeight).ExpandWidth(),
                new HStack(gap: 8f) {
                    new Label(visibleItems.Count.ToString("N0") + " presets") { Role = LabelRole.Caption }.ExpandWidth(),
                    new Label(State.LargeSelection < 0 ? "" : "Selected " + State.LargeSelection.ToString("D5")) { Role = LabelRole.Caption }.Width(160f)
                }.Height(24f),
                MUI.If(visibleItems.Count > 0, delegate {
                    return new VirtualList(visibleItems.Count, RowHeight + 4f, delegate(int row) {
                        int index = visibleItems[row];
                        return new Selectable(largeItems[index], State.LargeSelection == index, delegate(bool selected) {
                            SetState(delegate(GalleryState s) { s.LargeSelection = index; });
                        }, key: "large-row-" + index.ToString()).Height(RowHeight + 4f).ExpandWidth();
                    }, key: "large-virtual-list") { Height = 320f }.ExpandHeight().ExpandWidth();
                }, delegate {
                    return new Label("No matching presets") { Role = LabelRole.Muted }.Height(RowHeight);
                })
            }.ExpandHeight();
        }

        private void RefreshLargeItems() {
            if (largeItems == null) {
                largeItems = new string[10000];
                for (int i = 0; i < largeItems.Length; i++) largeItems[i] = "Preset " + i.ToString("D5");
            }
            string filter = State.LargeFilter ?? string.Empty;
            if (string.Equals(lastLargeFilter, filter, StringComparison.Ordinal)) return;
            lastLargeFilter = filter;
            visibleItems.Clear();
            for (int i = 0; i < largeItems.Length; i++) {
                if (filter.Length == 0 || largeItems[i].IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) visibleItems.Add(i);
            }
        }

        private void RegisterShortcuts(BuildContext context) {
            if (context == null) return;
            RegisterPageShortcut(context, "gallery.controls", KeyCode.Alpha1, 1);
            RegisterPageShortcut(context, "gallery.layout", KeyCode.Alpha2, 7);
            RegisterPageShortcut(context, "gallery.dialogs", KeyCode.Alpha3, 8);
            RegisterPageShortcut(context, "gallery.style", KeyCode.Alpha4, 9);
            RegisterPageShortcut(context, "gallery.debug", KeyCode.Alpha5, 11);
            RegisterPageShortcut(context, "gallery.large", KeyCode.Alpha6, 10);
            context.RegisterShortcut("gallery.log", new ShortcutChord((int)KeyCode.L, control: true), delegate {
                DebugLog.Info("Gallery entry " + State.ClickCount.ToString()); CountClick();
            });
        }

        private void RegisterPageShortcut(BuildContext context, string id, KeyCode key, int page) {
            context.RegisterShortcut(id, new ShortcutChord((int)key, control: true), delegate {
                SetState(delegate(GalleryState s) { s.Tab = page; s.ShowInlineDialog = false; });
            });
        }

        private Widget BuildMenuBar(BuildContext context) {
            return new MenuBar(key: "gallery-menu") {
                new Menu("File") {
                    new MenuItem("Show message", delegate { context.Dialogs.Alert("MaximumUI", "The command completed."); }),
                    new MenuSeparator(), new MenuItem("Unavailable") { Enabled = false }
                },
                new Menu("View") {
                    new MenuItem("Buttons", delegate { SetState(delegate(GalleryState s) { s.Tab = 1; }); }) { Shortcut = "Ctrl+1" },
                    new MenuItem("Layout", delegate { SetState(delegate(GalleryState s) { s.Tab = 7; }); }) { Checked = State.Tab == 7, Shortcut = "Ctrl+2" },
                    new MenuItem("Dialogs", delegate { SetState(delegate(GalleryState s) { s.Tab = 8; }); }) { Shortcut = "Ctrl+3" },
                    new MenuItem("Appearance", delegate { SetState(delegate(GalleryState s) { s.Tab = 9; }); }) { Shortcut = "Ctrl+4" },
                    new Menu("More pages", key: "gallery-more-pages") {
                        new MenuItem("Selection", delegate { SetState(delegate(GalleryState s) { s.Tab = 4; }); }),
                        new MenuItem("Colors", delegate { SetState(delegate(GalleryState s) { s.Tab = 5; }); }),
                        new MenuItem("Drawing", delegate { SetState(delegate(GalleryState s) { s.Tab = 6; }); })
                    }
                },
                new Menu("Edit") {
                    new MenuItem("Increment", CountClick),
                    new MenuItem("Reset counter", delegate { SetState(delegate(GalleryState s) { s.ClickCount = 0; s.Status = "Counter reset"; }); })
                }
            }.Height(RowHeight).ExpandWidth();
        }

        private Widget BuildButtonsPage() {
            return new VStack(gap: 24f) {
                Section("Commands",
                    new HStack(gap: 8f) {
                        new Button("Primary", CountClick, key: "button-primary") { Variant = ButtonVariant.Primary }.Width(112f),
                        new Button("Standard", CountClick, key: "button-standard").Width(112f),
                        new Button("Quiet", CountClick, key: "button-quiet") { Variant = ButtonVariant.Quiet }.Width(96f),
                        new Button("Delete", CountClick, key: "button-danger") { Variant = ButtonVariant.Danger }.Width(96f)
                    }.Height(RowHeight),
                    new HStack(gap: 8f) {
                        new SmallButton("Small button", CountClick, key: "button-small").Width(Math.Max(112f, (float)Math.Ceiling((State.FontSize * 6.5f + 16f) / 4f) * 4f)).Height(Math.Max(28f, RowHeight - 8f)),
                        new ArrowButton(ArrowDirection.Left, CountClick, key: "arrow-left").Width(32f),
                        new ArrowButton(ArrowDirection.Right, CountClick, key: "arrow-right").Width(32f),
                        new Button("Disabled", null, key: "button-disabled").Width(112f).Disabled(true),
                        new Spacer()
                    }.Height(RowHeight),
                    new TextLink("View details", CountClick, key: "text-link").Height(RowHeight)),
                Section("Feedback",
                    new HStack(gap: 12f) {
                        new StatusBadge("Info", StatusBadgeKind.Info).Width(72f),
                        new StatusBadge("Success", StatusBadgeKind.Success).Width(88f),
                        new StatusBadge("Warning", StatusBadgeKind.Warning).Width(88f),
                        new StatusBadge("Error", StatusBadgeKind.Error).Width(72f),
                        new LoadingIndicator("Loading").Width(112f)
                    }.Height(RowHeight),
                    new ProgressBar(State.Slider, (State.Slider * 100f).ToString("0") + "%").Height(RowHeight).ExpandWidth(),
                    new HelpBox("Changes saved", HelpBoxType.Info),
                    new HelpBox("Some items are unavailable", HelpBoxType.Warning),
                    new HelpBox("The file could not be opened", HelpBoxType.Error)),
                Section("Context menu",
                    new Button("More actions", CountClick, key: "context-actions").Width(144f).Height(RowHeight)
                        .Tooltip("Right-click for actions")
                        .ContextMenu(new VStack(gap: 0f) {
                            new Button("Increment", CountClick).Height(RowHeight).ExpandWidth(),
                            new Button("Reset", delegate { SetState(delegate(GalleryState s) { s.ClickCount = 0; s.Status = "Ready"; }); }).Height(RowHeight).ExpandWidth()
                        }))
            };
        }

        private Widget BuildTextPage() {
            return new VStack(gap: 24f) {
                Section("Text fields",
                    Field("Name", new TextField(State.Text, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Text = value; });
                    }, key: "text") {
                        Placeholder = "Name", Flags = TextInputFlags.EscapeReverts,
                        OnSubmit = delegate(string value) { DebugLog.Info("Submit text: " + value); },
                        OnBlur = delegate(string value) { DebugLog.Info("Blur text: " + value); }
                    }),
                    Field("Code", new TextField(State.Code, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Code = value; });
                    }, key: "code") {
                        Placeholder = "Uppercase letters", Flags = TextInputFlags.Uppercase | TextInputFlags.NoBlank, MaxLength = 12
                    }),
                    Field("Hexadecimal", new TextField(State.Hex, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Hex = value; });
                    }, key: "hex") {
                        Placeholder = "Hexadecimal", Flags = TextInputFlags.Hexadecimal | TextInputFlags.Uppercase | TextInputFlags.EscapeClearsAll, MaxLength = 8
                    }),
                    Field("Read-only", new TextField("Read-only text", null, key: "readonly-text") { Flags = TextInputFlags.ReadOnly }),
                    Field("Password", new PasswordField(State.Password, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Password = value; });
                    }, key: "password") { Flags = TextInputFlags.NoBlank }),
                    Field("Search", new SearchBox(State.Search, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Search = value; });
                    }, key: "gallery-search") { Placeholder = "Search" })),
                Section("Notes",
                    new TextArea(State.Notes, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Notes = value; });
                    }, key: "notes") {
                        Placeholder = "Notes", Flags = TextInputFlags.EnterReturnsTrue,
                        OnSubmit = delegate(string value) { DebugLog.Info("Submit notes: " + value); }
                    }.Height(112f).ExpandWidth()),
                Section("Text",
                    new ValueLabel("Current name", State.Text) { LabelWidth = FieldLabelWidth }.Height(RowHeight),
                    new BulletText("List item").Height(RowHeight),
                    new SeparatorText("Divider").Height(24f))
            };
        }

        private Widget BuildNumericPage() {
            return new VStack(gap: 24f) {
                Section("Number fields",
                    Field("Integer", new InputInt(string.Empty, State.IntValue, delegate(int value) {
                        SetState(delegate(GalleryState s) { s.IntValue = value; });
                    }, key: "input-int") { HasMin = true, Min = 0, HasMax = true, Max = 99 }),
                    Field("Float", new InputFloat(string.Empty, State.FloatValue, delegate(float value) {
                        SetState(delegate(GalleryState s) { s.FloatValue = value; });
                    }, key: "input-float") { HasMin = true, Min = 0f, HasMax = true, Max = 1f }),
                    Field("Double", new InputDouble(string.Empty, State.DoubleValue, delegate(double value) {
                        SetState(delegate(GalleryState s) { s.DoubleValue = value; });
                    }, key: "input-double")),
                    Field("Drag integer", new DragInt(string.Empty, State.DragIntValue, delegate(int value) {
                        SetState(delegate(GalleryState s) { s.DragIntValue = value; });
                    }, key: "drag-int") { HasMin = true, Min = 0, HasMax = true, Max = 20 }),
                    Field("Drag float", new DragFloat(string.Empty, State.DragFloatValue, delegate(float value) {
                        SetState(delegate(GalleryState s) { s.DragFloatValue = value; });
                    }, key: "drag-float") { HasMin = true, Min = 0f, HasMax = true, Max = 1f })),
                Section("Sliders",
                    Field("Value", new Slider(State.Slider, 0f, 1f, delegate(float value) {
                        SetState(delegate(GalleryState s) { s.Slider = value; });
                    }, key: "slider")),
                    Field("Integer", new SliderInt(string.Empty, State.SliderIntValue, 0, 100, delegate(int value) {
                        SetState(delegate(GalleryState s) { s.SliderIntValue = value; });
                    }, key: "slider-int")),
                    Field("Float", new SliderFloat(string.Empty, State.FloatValue, 0f, 1f, delegate(float value) {
                        SetState(delegate(GalleryState s) { s.FloatValue = value; });
                    }, key: "slider-float")),
                    Field("Angle", new SliderAngle(string.Empty, State.AngleValue, delegate(float value) {
                        SetState(delegate(GalleryState s) { s.AngleValue = value; });
                    }, key: "slider-angle"))),
                Section("Vector",
                    new VectorField("Position", State.VectorX, State.VectorY, State.VectorZ, delegate(float x, float y, float z) {
                        SetState(delegate(GalleryState s) { s.VectorX = x; s.VectorY = y; s.VectorZ = z; });
                    }).Height(RowHeight).ExpandWidth())
            };
        }

        private Widget BuildSelectionPage() {
            return new VStack(gap: 24f) {
                Section("Options",
                    new HStack(gap: 24f) {
                        new ToggleSwitch("Enabled", State.Enabled, delegate(bool value) { SetState(delegate(GalleryState s) { s.Enabled = value; }); }, key: "toggle").Width(164f),
                        new Checkbox("Show preview", State.Enabled, delegate(bool value) { SetState(delegate(GalleryState s) { s.Enabled = value; }); }, key: "checkbox").ExpandWidth()
                    }.Height(RowHeight),
                    new HStack(gap: 24f) {
                        new RadioButton("First option", State.RadioIndex == 0, delegate { SetState(delegate(GalleryState s) { s.RadioIndex = 0; }); }, key: "radio-first").Width(164f),
                        new RadioButton("Second option", State.RadioIndex == 1, delegate { SetState(delegate(GalleryState s) { s.RadioIndex = 1; }); }, key: "radio-second").ExpandWidth()
                    }.Height(RowHeight),
                    Field("Mode", new ComboBox<string>(string.Empty, State.Mode, modes, delegate(string value) { return value; }, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Mode = value; });
                    }, key: "mode") { MaxVisibleItems = 4 }),
                    new SegmentedControl<string>(State.Mode, modes, delegate(string value) { return value; }, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.Mode = value; });
                    }, key: "mode-segment").Height(RowHeight).ExpandWidth()),
                Section("Lists",
                    new ComboBox<string>("Item", State.ListSelection, samples, delegate(string value) { return value; }, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.ListSelection = value; });
                    }, key: "samples") { MaxVisibleItems = 6 }.Height(RowHeight).ExpandWidth(),
                    new SearchableComboBox<string>("Search", State.ListSelection, samples, delegate(string value) { return value; }, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.ListSelection = value; });
                    }, key: "searchable-samples") { MaxVisibleItems = 6, FilterPlaceholder = "Search items" }.Height(RowHeight).ExpandWidth(),
                    new ListBox<string>(State.ListSelection, samples, delegate(string value) { return value; }, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.ListSelection = value; });
                    }, key: "sample-list") { VisibleItems = 4 }.Height(RowHeight * 4f + 8f).ExpandWidth(),
                    new SearchableListBox<string>(State.ListSelection, samples, delegate(string value) { return value; }, delegate(string value) {
                        SetState(delegate(GalleryState s) { s.ListSelection = value; });
                    }, key: "searchable-list") {
                        VisibleItems = 4, FilterText = State.ListFilter, FilterPlaceholder = "Search items",
                        OnFilterChange = delegate(string value) { SetState(delegate(GalleryState s) { s.ListFilter = value; }); }
                    }.Height(RowHeight * 5f + 8f).ExpandWidth()),
                Section("Multiple selection", BuildMultiSelectDemo()),
                Section("Tree",
                    new Selectable("Selectable row", State.Selectable, delegate(bool value) {
                        SetState(delegate(GalleryState s) { s.Selectable = value; });
                    }, key: "selectable-row").Height(RowHeight).ExpandWidth(),
                    new TreeNode("Folder", State.TreeOpen, delegate(bool open) { SetState(delegate(GalleryState s) { s.TreeOpen = open; }); }, key: "gallery-tree") {
                        Selected = State.TreeSelected, OpenOnArrow = true, OpenOnDoubleClick = true, DrawLines = true, Framed = false,
                        OnSelect = delegate { SetState(delegate(GalleryState s) { s.TreeSelected = !s.TreeSelected; }); }
                    }.AddFluent(new TreeLeaf("Item", delegate { SetState(delegate(GalleryState s) { s.TreeSelected = !s.TreeSelected; }); }, key: "gallery-tree-leaf") {
                        Selected = State.TreeSelected, Bullet = false
                    }.Height(RowHeight)))
            };
        }

        private Widget BuildColorsPage() {
            return new VStack(gap: 24f) {
                Section("Color fields",
                    new ColorButton("Color", State.Color, CountClick, key: "color-button").Height(RowHeight).Width(144f),
                    new ColorEdit3("RGB", State.Color, delegate(MuiColor color) { SetState(delegate(GalleryState s) { s.Color = color; }); }, key: "color-rgb") {
                        DisplayMode = ColorDisplayMode.Byte
                    }.Height(RowHeight).ExpandWidth(),
                    new ColorEdit4("RGBA", State.Color, delegate(MuiColor color) { SetState(delegate(GalleryState s) { s.Color = color; }); }, key: "color-rgba") {
                        DisplayMode = ColorDisplayMode.Hex
                    }.Height(RowHeight).ExpandWidth(),
                    new ColorEdit(State.Color, delegate(MuiColor color) { SetState(delegate(GalleryState s) { s.Color = color; }); }, key: "color-edit").Height(RowHeight).ExpandWidth()),
                Section("Color pickers",
                    new ColorPicker3("RGB", State.Color, delegate(MuiColor color) { SetState(delegate(GalleryState s) { s.Color = color; }); }, key: "picker-rgb") {
                        DisplayMode = ColorDisplayMode.Float
                    }.Height(RowHeight * 4f + 20f).ExpandWidth(),
                    new ColorPicker4("RGBA", State.Color, delegate(MuiColor color) { SetState(delegate(GalleryState s) { s.Color = color; }); }, key: "picker-rgba") {
                        DisplayMode = ColorDisplayMode.Hex
                    }.Height(RowHeight * 5f + 20f).ExpandWidth())
            };
        }

        private Widget BuildDrawingPage(BuildContext context) {
            return new VStack(gap: 24f) {
                Section("Images",
                    new HStack(gap: 16f) {
                        new Image(context.Theme == null ? null : context.Theme.ButtonActive).Width(48f).Height(32f),
                        new ImageButton(context.Theme == null ? null : context.Theme.ButtonHover, CountClick) { Text = "Image button" }.Height(RowHeight).Width(160f)
                    }.Height(RowHeight)),
                Section("Plots",
                    new PlotLines(plotValues) { OverlayText = "Line" }.Height(80f).ExpandWidth(),
                    new PlotHistogram(plotValues) { OverlayText = "Histogram" }.Height(80f).ExpandWidth()),
                Section("Custom drawing",
                    new HStack(gap: 16f) {
                        new DrawCanvas(DrawCanvasDemo, key: "draw-canvas") { CanvasWidth = 180f, CanvasHeight = 112f }.Height(112f).ExpandWidth(),
                        BuildDrawListPreview()
                    }.Height(112f)),
                Section("Drag and drop",
                    new HStack(gap: 16f) {
                        new DragSource(new DragPayload("gallery.text", State.Text, State.Text), key: "drag-source") {
                            new Label("Drag item").Height(RowHeight)
                        }.Width(144f).Height(48f),
                        new DropTarget("gallery.text", delegate(DragPayload payload) {
                            string text = payload == null || payload.Data == null ? string.Empty : payload.Data.ToString();
                            SetState(delegate(GalleryState s) { s.DropMessage = text; });
                        }, key: "drop-target") { EmptyText = State.DropMessage }.Height(48f).ExpandWidth()
                    }.Height(48f))
            };
        }

        private void DrawCanvasDemo(DrawList drawList, MuiRect rect) {
            MuiColor accent = activePalette.Text;
            MuiColor blue = new MuiColor(0.28f, 0.48f, 0.82f, 1f);
            MuiColor red = new MuiColor(0.86f, 0.26f, 0.24f, 1f);

            drawList.Line(new MuiVec2(rect.X + 10f, rect.Y + 12f), new MuiVec2(rect.Right - 10f, rect.Y + 12f), accent, 2f);
            drawList.FillCircle(new MuiVec2(rect.X + 34f, rect.Y + 46f), 14f, blue);
            drawList.StrokeCircle(new MuiVec2(rect.X + 82f, rect.Y + 46f), 18f, accent, 2f, 32);
            drawList.FillTriangle(
                new MuiVec2(rect.Right - 58f, rect.Y + 64f),
                new MuiVec2(rect.Right - 28f, rect.Y + 28f),
                new MuiVec2(rect.Right - 10f, rect.Y + 64f),
                red);
            drawList.Bezier(
                new MuiVec2(rect.X + 10f, rect.Bottom - 12f),
                new MuiVec2(rect.X + 54f, rect.Y + 20f),
                new MuiVec2(rect.Right - 54f, rect.Bottom - 4f),
                new MuiVec2(rect.Right - 10f, rect.Y + 26f),
                accent,
                2f,
                24);
        }

        private Widget BuildDrawListPreview() {
            DrawList drawList = new DrawList();
            drawList.FillRect(new MuiRect(8f, 8f, 132f, 96f), activePalette.PanelBg);
            drawList.StrokeRect(new MuiRect(8f, 8f, 132f, 96f), activePalette.Text, 1f);
            drawList.Polyline(new[] {
                new MuiVec2(18f, 86f),
                new MuiVec2(44f, 50f),
                new MuiVec2(72f, 78f),
                new MuiVec2(104f, 46f),
                new MuiVec2(132f, 70f)
            }, new MuiColor(0.18f, 0.42f, 0.58f, 1f), 2f);
            drawList.Text(new MuiRect(14f, 10f, 120f, RowHeight), "DrawList", null, activePalette.Text);

            return new DrawListWidget(drawList, key: "draw-list-widget") {
                CanvasWidth = 150f,
                CanvasHeight = 112f
            }.Height(112f).Width(150f);
        }

        private Widget BuildMultiSelectDemo() {
            VStack rows = new VStack(gap: 2f);
                        rows.Add(new Label(FormatSelection(State.MultiSelection)) { Role = LabelRole.Muted }.Height(RowHeight));

            for (int i = 0; i < 6 && i < samples.Length; i++) {
                string item = samples[i];
                bool selected = State.MultiSelection != null && State.MultiSelection.IsSelected(item);
                Selectable row = new Selectable(item, selected, null, key: "multi-select-" + i.ToString()) {
                    Value = item
                };
                row.OnSelect = delegate(SelectionRequest request) {
                    string captured = request == null || request.Value == null ? string.Empty : request.Value.ToString();
                    bool additive = request != null && request.Additive;
                    bool range = request != null && request.Range;
                    SetState(delegate(GalleryState s) {
                        if (s.MultiSelection == null) s.MultiSelection = new SelectionState<string>();
                        s.MultiSelection.Apply(samples, captured, additive, range);
                    });
                };
                rows.Add(row.Height(RowHeight).ExpandWidth());
            }

            return rows;
        }

        private static string FormatSelection(SelectionState<string> selection) {
            if (selection == null || selection.Count == 0) return "No selection";

            string text = string.Empty;
            for (int i = 0; i < selection.SelectedItems.Count; i++) {
                if (i > 0) text += ", ";
                text += selection.SelectedItems[i];
            }

            return text;
        }

        private Widget BuildScrollExamples() {
            string[] longRows = State.EmptyScrollSamples ? new string[0] : samples;
            string[] shortRows = State.EmptyScrollSamples ? new string[0] : new[] { "First item", "Second item" };
            HStack horizontal = new HStack(8f);
            for (int i = 0; i < 10; i++) {
                int column = i + 1;
                horizontal.Add(new Button("Column " + column, delegate {
                    SetState(delegate(GalleryState s) { s.Status = "Column " + column; });
                }, "horizontal-column-" + column).Width(96f).Height(RowHeight));
            }
            return new VStack(16f) {
                new HStack(16f) {
                    Section("Overflow", new ListBox<string>(State.ListSelection, longRows,
                        delegate(string value) { return value; }, delegate(string value) {
                            SetState(delegate(GalleryState s) { s.ListSelection = value; });
                        }, "scroll-overflow").Height(RowHeight * 4f + 2f).ExpandWidth()).ExpandWidth(),
                    Section("Short list", new ListBox<string>(State.ListSelection, shortRows,
                        delegate(string value) { return value; }, delegate(string value) {
                            SetState(delegate(GalleryState s) { s.ListSelection = value; });
                        }, "scroll-short").Height(RowHeight * 4f + 2f).ExpandWidth()).ExpandWidth()
                }.ExpandWidth(),
                new HStack(8f) {
                    new Button("Clear", delegate { SetState(delegate(GalleryState s) { s.EmptyScrollSamples = true; }); }, "scroll-clear").Width(96f).Height(RowHeight),
                    new Button("Restore", delegate { SetState(delegate(GalleryState s) { s.EmptyScrollSamples = false; }); }, "scroll-restore").Width(96f).Height(RowHeight)
                }.Height(RowHeight),
                Section("Horizontal scrolling", new ScrollView("horizontal-scroll") {
                    horizontal.Width(1032f).Height(RowHeight)
                }.Height(RowHeight + ScrollbarGeometry.GutterSize).ExpandWidth())
            }.ExpandWidth();
        }

        private Widget BuildLayoutTab(BuildContext context) {
            return new VStack(gap: 24f) {
                Section("Menus and tabs",
                    BuildMenuBar(context),
                    new Tabs(State.ExampleTab, delegate(int index) { SetState(delegate(GalleryState s) { s.ExampleTab = index; }); }, key: "example-tabs") {
                        new Tab("Details").Content(State.ExampleTab == 0 ? new ValueLabel("Name", State.Text).Height(RowHeight) : MUI.Empty),
                        new Tab("Activity").Content(State.ExampleTab == 1 ? new ValueLabel("Actions", State.ClickCount.ToString()).Height(RowHeight) : MUI.Empty),
                        new Tab("Settings").Content(State.ExampleTab == 2 ? new ToggleSwitch("Enabled", State.Enabled, delegate(bool value) { SetState(delegate(GalleryState s) { s.Enabled = value; }); }).Height(RowHeight) : MUI.Empty)
                    }.ExpandWidth()),
                Section("Stacks",
                    new HStack(gap: 8f) {
                        new Button("Left", CountClick).Width(96f), new MaximumUI.Space(8f),
                        new Button("Middle", CountClick).Width(96f), new Spacer(), new Button("Right", CountClick).Width(96f)
                    }.Height(RowHeight),
                    new Foldout("Details", State.FoldoutOpen, delegate(bool open) { SetState(delegate(GalleryState s) { s.FoldoutOpen = open; }); }, key: "layout-foldout") {
                        new HStack(gap: 16f) { new Label("Fixed width").Width(FieldLabelWidth), new Button("Fill width", CountClick).ExpandWidth() }.Height(RowHeight)
                    }),
                Section("Grid",
                    new Grid(3, rowGap: 8f, columnGap: 8f) {
                        new Button("One", CountClick).Height(RowHeight),
                        new Button("Two", CountClick).Height(RowHeight),
                        new Button("Three", CountClick).Height(RowHeight),
                        new Toggle("Four", State.Enabled, delegate(bool value) { SetState(delegate(GalleryState s) { s.Enabled = value; }); }).Height(RowHeight),
                        new Selectable("Five", State.Selectable, delegate(bool value) { SetState(delegate(GalleryState s) { s.Selectable = value; }); }).Height(RowHeight),
                        new ProgressBar(State.Slider).Height(RowHeight)
                    }.ExpandWidth()),
                Section("Table", BuildLayoutTable().ExpandWidth()),
                Section("Virtual list",
                    new VirtualList(10000, RowHeight, delegate(int index) {
                        return new HStack(gap: 16f, key: "virtual-row-" + index.ToString()) {
                            new Label(index.ToString("D5")).Width(64f).Height(RowHeight),
                            new Label("Item " + index.ToString()).Height(RowHeight).ExpandWidth(),
                            new SmallButton("Select", delegate { SetState(delegate(GalleryState s) { s.Status = "Selected item " + index.ToString(); }); }).Width(72f).Height(28f)
                        };
                    }, key: "virtual-list") { Height = 160f }.ExpandWidth()),
                BuildScrollExamples(),
                Section("Split view",
                    new TwoPaneLayout(State.SplitRatio, delegate(float ratio) { SetState(delegate(GalleryState s) { s.SplitRatio = ratio; }); }, key: "split-pane") {
                        MinFirst = 120f, MinSecond = 120f, SplitterSize = 8f
                    }.AddFluent(new Panel {
                        new Label("First pane").Height(RowHeight), new Button("Action", CountClick).Height(RowHeight).ExpandWidth()
                    }).AddFluent(new Panel {
                        new Label("Second pane").Height(RowHeight), new ProgressBar(State.SplitRatio).Height(RowHeight).ExpandWidth()
                    }).Height(112f).ExpandWidth()),
                Section("Collapsible sections",
                    new CollapsingHeader("Details", State.HeaderOpen, delegate(bool open) { SetState(delegate(GalleryState s) { s.HeaderOpen = open; }); }, key: "gallery-collapsing") {
                        Framed = false
                    }.AddFluent(new ValueLabel("Name", State.Text).Height(RowHeight)).ExpandWidth(),
                    new ClosableCollapsingHeader("Preview", State.HeaderOpen, delegate(bool open) { SetState(delegate(GalleryState s) { s.HeaderOpen = open; }); }, key: "gallery-closable-header") {
                        Visible = State.ClosableHeaderVisible, Framed = false,
                        OnClose = delegate { SetState(delegate(GalleryState s) { s.ClosableHeaderVisible = false; }); }
                    }.AddFluent(new ValueLabel("Mode", State.Mode).Height(RowHeight)).ExpandWidth(),
                    MUI.If(!State.ClosableHeaderVisible, delegate {
                        return new Button("Restore preview", delegate { SetState(delegate(GalleryState s) { s.ClosableHeaderVisible = true; }); }).Width(144f).Height(RowHeight);
                    }))
            };
        }

        private Widget BuildDialogsTab(BuildContext context) {
            return new VStack(gap: 24f) {
                Section("Message dialogs",
                    new HStack(gap: 8f) {
                        new Button("Show message", delegate { context.Dialogs.Alert("Saved", "Your changes have been saved."); }, key: "dialog-alert").Width(FieldLabelWidth + 32f),
                        new Button("Show confirmation", delegate {
                            context.Dialogs.Confirm("Reset counter?", "The action count will return to zero.", delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount = 0; s.Status = "Counter reset"; });
                            }, null);
                        }, key: "dialog-confirm").Width(FieldLabelWidth + 64f)
                    }.Height(RowHeight)),
                Section("Input dialog",
                    new Button("Edit profile", delegate { SetState(delegate(GalleryState s) { s.ShowInlineDialog = true; }); }, key: "open-inline-dialog").Width(144f).Height(RowHeight)),
                MUI.If(State.ShowInlineDialog, delegate {
                    return new Dialog("Edit profile", key: "inline-dialog") {
                        Width = 400f, Height = 264f, CloseOnEscape = true, CloseOnOutsideClick = false,
                        OnClose = delegate { SetState(delegate(GalleryState s) { s.ShowInlineDialog = false; }); }
                    }.AddFluent(new VStack(gap: 16f) {
                        new VStack(gap: 8f) {
                            new Label("Name").Height(Math.Max(24f, ThemeTypography.LineHeight(activePalette, LabelRole.Body))),
                            new TextField(State.Text, delegate(string value) { SetState(delegate(GalleryState s) { s.Text = value; }); }, key: "dialog-name").Height(RowHeight).ExpandWidth(),
                            new ComboBox<string>("Mode", State.Mode, modes, delegate(string value) { return value; }, delegate(string value) { SetState(delegate(GalleryState s) { s.Mode = value; }); }, key: "dialog-mode").Height(RowHeight).ExpandWidth()
                        },
                        new HStack(gap: 8f) {
                            new Spacer(), new Button("Done", delegate { SetState(delegate(GalleryState s) { s.ShowInlineDialog = false; s.Status = "Profile updated"; }); }, key: "dialog-done") { Variant = ButtonVariant.Primary }.Width(96f)
                        }.Height(RowHeight)
                    });
                })
            };
        }

        private Table BuildLayoutTable() {
            Table table = new Table(3, key: "sample-table") {
                AlternatingRows = false, ShowOuterBorder = false, ShowInnerBorders = false, HeaderHeight = RowHeight
            };
            table.Add(new TableColumn("Name") {
                Width = 144f, MinWidth = 112f, Stretch = false, Sortable = true, SortDirection = State.TableSort,
                OnSort = delegate(TableSortDirection direction) { SetState(delegate(GalleryState s) { s.TableSort = direction; }); }
            });
            table.Add(new TableColumn("Value"));
            table.Add(new TableColumn("Action") { Width = 96f, MinWidth = 96f, Stretch = false });
            TableRow strength = new TableRow {
                new TableCell { new Label("Strength").Height(RowHeight) },
                new TableCell { new ProgressBar(State.Slider).Height(RowHeight) },
                new TableCell { new Button("Reset", delegate { SetState(delegate(GalleryState s) { s.Slider = 0.5f; }); }).Height(RowHeight).ExpandWidth() }
            };
            TableRow mode = new TableRow {
                new TableCell { new Label("Mode").Height(RowHeight) },
                new TableCell { new Label(State.Mode).Height(RowHeight) },
                new TableCell { new Button("Select", delegate { SetState(delegate(GalleryState s) { s.Status = "Selected " + s.Mode; }); }).Height(RowHeight).ExpandWidth() }
            };
            if (State.TableSort == TableSortDirection.Ascending) { table.Add(mode); table.Add(strength); }
            else { table.Add(strength); table.Add(mode); }
            return table;
        }

        private Widget BuildStyleTab(BuildContext context) {
            return new VStack(gap: 12f) {
                new ScrollView(key: "style-scroll").Content(new VStack(gap: 24f) {
                    Section("Theme",
                        new ThemeSelector("Palette", State.ThemeName, themeNames, delegate(string value) {
                            SwitchTheme(context, value);
                        }, key: "theme-selector") { LabelWidth = FieldLabelWidth }.Height(RowHeight).ExpandWidth(),
                        new FontSizeSelector("Text size", State.EditableTheme.FontSize, fontSizes, delegate(float value) {
                            SetState(delegate(GalleryState s) { s.EditableTheme.FontSize = value; s.ThemePending = true; });
                        }, key: "font-size-selector") { LabelWidth = FieldLabelWidth }.Height(RowHeight).ExpandWidth()),
                    Section("Accent",
                        new ColorEdit4("Color", State.EditableTheme.Accent, delegate(MuiColor color) {
                            SetState(delegate(GalleryState s) { s.EditableTheme.Accent = color; s.ThemePending = true; });
                        }, key: "theme-accent") { DisplayMode = ColorDisplayMode.Byte }.Height(RowHeight).ExpandWidth()),
                    new ThemeEditor(State.EditableTheme, delegate(ThemeTokens tokens) {
                            SetState(delegate(GalleryState s) { s.EditableTheme = tokens; s.ThemePending = true; });
                        }, key: "theme-colors") {
                            Title = "Colors", MaxRows = 0, ScalarStep = 1f, ColorTokens = nativeColors, ScalarTokens = new ThemeScalarToken[0]
                        }.ExpandWidth(),
                    new ThemeEditor(State.EditableTheme, delegate(ThemeTokens tokens) {
                            SetState(delegate(GalleryState s) { s.EditableTheme = tokens; s.ThemePending = true; });
                        }, key: "theme-dimensions") {
                            Title = "Dimensions", MaxRows = 0, ScalarStep = 1f, ColorTokens = new ThemeColorToken[0], ScalarTokens = nativeScalars
                        }.ExpandWidth(),
                    Section("Preview",
                        new HStack(gap: 8f) {
                            new Button("Primary", CountClick) { Variant = ButtonVariant.Primary }.ExpandWidth(),
                            new Button("Standard", CountClick).ExpandWidth(),
                            new Button("Quiet", CountClick) { Variant = ButtonVariant.Quiet }.ExpandWidth(),
                            new Button("Delete", CountClick) { Variant = ButtonVariant.Danger }.ExpandWidth()
                        }.Height(RowHeight),
                        new Button("Disabled").Disabled(true).Height(RowHeight).Width(112f),
                        new HStack(gap: 8f) {
                            new StatusBadge("Info", StatusBadgeKind.Info).ExpandWidth(),
                            new StatusBadge("Success", StatusBadgeKind.Success).ExpandWidth(),
                            new StatusBadge("Warning", StatusBadgeKind.Warning).ExpandWidth(),
                            new StatusBadge("Error", StatusBadgeKind.Error).ExpandWidth()
                        }.Height(RowHeight))
                }).ExpandHeight().ExpandWidth(),
                new HStack(gap: 8f) {
                    new Button("Apply", delegate { ApplyEditedTheme(context); }, key: "theme-apply") { Variant = ButtonVariant.Primary }.Width(96f).EnabledWhen(State.ThemePending),
                    new Button("Revert", delegate {
                        SetState(delegate(GalleryState s) { s.EditableTheme = context.Theme.Tokens.Clone(); s.ThemePending = false; });
                    }, key: "theme-revert").Width(96f).EnabledWhen(State.ThemePending),
                    new Spacer(),
                    new Label(State.ThemePending ? "Unsaved changes" : string.Empty) { Role = LabelRole.Caption }.Width(144f)
                }.Height(RowHeight)
            }.ExpandHeight().ExpandWidth();
        }

        private void SwitchTheme(BuildContext context, string name) {
            MaximumTheme next = name == "Metro Light" ? MaximumTheme.MetroLight :
                name == "Clean Light" ? MaximumTheme.CleanLight :
                name == "Expression Light" ? MaximumTheme.ExpressionLight : MaximumTheme.MetroDark;
            if (!MaximumUIRuntime.SetTheme(context.MountId, next)) return;
            if (ownedTheme != null) ownedTheme.Dispose();
            ownedTheme = null;
            activePalette = next.Tokens;
            SetState(delegate(GalleryState s) {
                s.ThemeName = next.Name; s.FontSize = next.FontSize;
                s.EditableTheme = next.Tokens.Clone(); s.ThemePending = false;
                s.Status = next.Name + " applied";
            });
        }

        private void ApplyEditedTheme(BuildContext context) {
            MaximumTheme next = MaximumTheme.Create(State.EditableTheme, name: State.ThemeName + " Custom");
            try {
                if (!MaximumUIRuntime.SetTheme(context.MountId, next)) {
                    next.Dispose();
                    return;
                }
            } catch {
                next.Dispose();
                throw;
            }
            if (ownedTheme != null) ownedTheme.Dispose();
            ownedTheme = next;
            activePalette = next.Tokens;
            SetState(delegate(GalleryState s) {
                s.EditableTheme = next.Tokens.Clone(); s.FontSize = next.FontSize;
                s.ThemePending = false; s.Status = "Custom appearance applied";
            });
        }

        private Widget BuildDebugTab(BuildContext context) {
            return new ScrollView(key: "debug-scroll").Content(new VStack(gap: 16f) {
                new HStack(gap: 8f) {
                    new Button("Add log", delegate { DebugLog.Info("Gallery entry " + State.ClickCount.ToString()); CountClick(); }, key: "debug-add-log").Width(96f),
                    new Button("Warning", delegate { DebugLog.Warning("Gallery warning " + State.ClickCount.ToString()); CountClick(); }).Width(96f),
                    new Button("Clear", delegate { DebugLog.Clear(); SetState(delegate(GalleryState s) { s.Status = "Log cleared"; }); }).Width(96f),
                    new Button("Copy", delegate { DebugLog.LogToClipboard(); SetState(delegate(GalleryState s) { s.Status = "Log copied"; }); }).Width(96f)
                }.Height(RowHeight),
                new HStack(gap: 16f) {
                    new ValueLabel("Hovered", context.IsHovered("debug-add-log").ToString()).ExpandWidth(),
                    new ValueLabel("Clicked", context.IsClicked("debug-add-log").ToString()).ExpandWidth()
                }.Height(24f),
                new MetricsWindow(MaximumUIRuntime.GetMetrics(), key: "metrics") { Rows = 6 }.ExpandWidth(),
                new InputDebugger(context.Query, key: "input-debug") { Navigation = context.Navigation, Shortcuts = context.Shortcuts, Rows = 9 }.ExpandWidth(),
                new DebugLogWindow(DebugLog.Snapshot(), key: "debug-log") { Rows = 7 }.ExpandWidth(),
                new StateStoreInspector(context.StateStore, key: "state-store") { Rows = 8 }.ExpandWidth(),
                new IDStackTool(context.Query, key: "id-stack") { Rows = 8 }.ExpandWidth(),
                new ItemPicker(context.Query, key: "item-picker") { Rows = 8 }.ExpandWidth(),
                BuildDemoGallery(),
                new AboutWindow("MaximumUI Gallery", MaximumUIGalleryPlugin.Version, string.Empty, key: "about") { Rows = 3 }.ExpandWidth(),
                new UserGuideWindow(key: "user-guide") { Rows = 6 }.ExpandWidth()
            });
        }

        private Widget BuildDemoGallery() {
            DemoGallery gallery = new DemoGallery("demo-gallery") { Rows = pageNames.Length };
            for (int i = 0; i < pageNames.Length; i++) {
                gallery.Entries.Add(new DemoGalleryEntry(pageNames[i], GalleryScenes.Ids[i], State.Tab == i ? "active" : "available"));
            }
            return gallery.ExpandWidth();
        }
    }

    internal static class WidgetFluentExtensions {
        public static T AddFluent<T>(this T widget, Widget child) where T : Widget {
            widget.Add(child);
            return widget;
        }
    }
}
