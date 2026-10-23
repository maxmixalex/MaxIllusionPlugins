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
        public const string Version = "0.3.0";

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
                    Theme = MaximumTheme.MetroDark
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
        public bool ShowDebugTab = true;
        public int ExampleTab;
        public bool ShowInlineDialog;
        public int Tab = 6;
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
        public ThemeTokens EditableTheme = ThemePalettes.MetroDark();
        public string FontName = "Default";
        public float FontSize = 13f;
        public string StyleName = "Default";
        public string ThemeName = "Metro Dark";
        public float VectorX = 1f;
        public float VectorY = 2f;
        public float VectorZ = 3f;
    }

    internal sealed class GalleryView : StatefulView<GalleryState> {
        public string ShortcutText = "F10";
        private float nextMetricsRefresh;
        private MaximumTheme ownedTheme;
        private string[] largeItems;
        private readonly List<int> visibleItems = new List<int>();
        private string lastLargeFilter;
        private static readonly string[] pageNames = { "Controls", "Composition", "Dialogs", "Appearance", "Diagnostics", "Large lists", "Overview" };
        private static readonly string[] pageDescriptions = {
            "Inputs, selection and feedback with consistent spacing.",
            "Panels, tables, trees and resizable split panes.",
            "Modal flows with deliberate focus and keyboard ownership.",
            "Choose a palette, tune the scale and apply your own colors.",
            "Live input, layout and state inspection.",
            "Search 10,000 rows; only visible items create controls.",
            "An embedded workspace, designed around your content."
        };
        private float RowHeight { get { return Math.Max(26f, State.FontSize + 12f); } }
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
        private readonly float[] fontSizes = { 11f, 12f, 13f, 14f, 16f, 18f };
        private readonly string[] themeNames = { "Metro Dark", "Metro Light", "Clean Light", "Expression Light" };
        private static readonly ThemeColorToken[] nativeColors = {
            ThemeColorToken.WindowBg, ThemeColorToken.PanelBg, ThemeColorToken.TitleBg,
            ThemeColorToken.DialogBg, ThemeColorToken.DropdownBg, ThemeColorToken.Backdrop,
            ThemeColorToken.Text, ThemeColorToken.TextMuted, ThemeColorToken.Border, ThemeColorToken.Separator,
            ThemeColorToken.Accent, ThemeColorToken.AccentHover, ThemeColorToken.OnAccent, ThemeColorToken.FocusRing,
            ThemeColorToken.ButtonBg, ThemeColorToken.ButtonHoverBg, ThemeColorToken.ButtonActiveBg,
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
            if (State.Tab == 4 && Time.unscaledTime >= nextMetricsRefresh) {
                nextMetricsRefresh = Time.unscaledTime + 0.25f;
                MaximumUIRuntime.MarkDirty(this);
            }
        }

        public override void OnMount(BuildContext context) {
            State.FontSize = context.Theme.FontSize;
            State.EditableTheme = context.Theme.Tokens.Clone();
        }

        public override void OnUnmount(BuildContext context) {
            if (ownedTheme != null) ownedTheme.Dispose();
            ownedTheme = null;
        }

        public override Widget Build(BuildContext context) {
            RegisterShortcuts(context);
            int page = Math.Max(0, Math.Min(pageNames.Length - 1, State.Tab));
            return new Window(
                "MAXIMUM UI   /   INTERFACE STUDIO", width: 940f, height: 720f,
                key: "gallery", minWidth: 740f, minHeight: 480f, foldable: true, closable: false) {
                FoldedHeight = Math.Max(32f, State.FontSize + 18f)
            }.Content(new VStack(gap: 12f) {
                new HStack(gap: 14f) {
                    new Label("MaximumUI") { Role = LabelRole.Heading }.ExpandWidth(),
                    new StatusBadge("GALLERY", StatusBadgeKind.Info).Width(86f).Height(24f),
                    new Button(State.ThemeName == "Metro Light" ? "Dark" : "Light", delegate {
                        SwitchTheme(context, State.ThemeName == "Metro Light" ? "Metro Dark" : "Metro Light");
                    }) { Variant = ButtonVariant.Quiet }.Width(66f).Height(28f)
                }.Height(RowHeight + 10f),
                new Separator(),
                new HStack(gap: 20f) {
                    BuildSidebar().Width(164f).ExpandHeight(),
                    new VStack(gap: 10f) {
                        new Label(pageNames[page]) { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                        new Label(pageDescriptions[page]) { Role = LabelRole.Muted, WordWrap = true }.MinHeight(36f),
                        BuildActivePage(context, page).ExpandHeight().ExpandWidth()
                    }.ExpandWidth().ExpandHeight()
                }.ExpandWidth().ExpandHeight(),
                new Separator(),
                new HStack(gap: 12f) {
                    new Label(State.Status) { Role = LabelRole.Muted }.ExpandWidth(),
                    new Label(ShortcutText + "  show / hide     Ctrl+1..6  navigate") { Role = LabelRole.Muted }.Width(295f)
                }.Height(RowHeight)
            }.ExpandHeight());
        }

        private Widget BuildSidebar() {
            return new VStack(gap: 4f) {
                NavItem("Overview", 6),
                new MaximumUI.Space(12f),
                new Label("COMPONENTS") { Role = LabelRole.Muted }.Height(24f),
                NavItem("Controls", 0), NavItem("Composition", 1), NavItem("Dialogs", 2),
                new MaximumUI.Space(12f),
                new Label("WORKSPACE") { Role = LabelRole.Muted }.Height(24f),
                NavItem("Appearance", 3), NavItem("Large lists", 5), NavItem("Diagnostics", 4),
                new Spacer(),
                new Label("MaximumUI " + MaximumUIGalleryPlugin.Version) { Role = LabelRole.Muted }.Height(RowHeight)
            };
        }

        private Widget NavItem(string text, int page) {
            return new Selectable(text, State.Tab == page, delegate(bool selected) {
                SetState(delegate(GalleryState s) { s.Tab = page; });
            }, key: "nav-" + page.ToString()).Height(Math.Max(34f, RowHeight + 4f)).ExpandWidth();
        }

        private Widget BuildActivePage(BuildContext context, int page) {
            Widget content;
            if (page == 0) return BuildControlsTab(context);
            if (page == 3) return BuildStyleTab(context);
            if (page == 4) return BuildDebugTab(context);
            if (page == 5) return BuildLargeList();
            if (page == 1) content = BuildLayoutTab(context);
            else if (page == 2) content = BuildDialogsTab(context);
            else content = BuildOverview(context);
            return new ScrollView(key: "page-scroll-" + page.ToString()) {
                AlwaysShowVertical = true
            }.Content(content).ExpandHeight().ExpandWidth();
        }

        private Widget BuildOverview(BuildContext context) {
            return new VStack(gap: 16f) {
                new Panel {
                    new VStack(gap: 12f) {
                        new HStack(gap: 12f) {
                            new Label("Profile preview") { Role = LabelRole.Heading }.ExpandWidth(),
                            new StatusBadge(State.Enabled ? "ENABLED" : "PAUSED", State.Enabled ? StatusBadgeKind.Success : StatusBadgeKind.Warning).Width(92f).Height(24f)
                        }.Height(RowHeight + 10f),
                        new Label("A working form built from the same widgets as the rest of this gallery.") { Role = LabelRole.Muted, WordWrap = true }.MinHeight(38f),
                        new HStack(gap: 12f) {
                            new Label("Name").Width(82f),
                            new TextField(State.Text, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Text = value; });
                            }, key: "overview-name") { Placeholder = "Profile name" }.ExpandWidth()
                        }.Height(RowHeight),
                        new HStack(gap: 12f) {
                            new Label("Mode").Width(82f),
                            new SegmentedControl<string>(State.Mode, modes, delegate(string value) { return value; }, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Mode = value; });
                            }, key: "overview-mode").ExpandWidth()
                        }.Height(RowHeight),
                        new HStack(gap: 12f) {
                            new Label("Strength").Width(82f),
                            new Slider(State.Slider, 0f, 1f, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.Slider = value; });
                            }, key: "overview-strength").ExpandWidth(),
                            new Label((State.Slider * 100f).ToString("0") + "%").Width(42f)
                        }.Height(RowHeight),
                        new Toggle("Enable profile", State.Enabled, delegate(bool value) {
                            SetState(delegate(GalleryState s) { s.Enabled = value; });
                        }, key: "overview-enable").Height(RowHeight),
                        new Separator(),
                        new HStack(gap: 8f) {
                            new Button("Apply preview", delegate {
                                SetState(delegate(GalleryState s) { s.Status = "Preview applied: " + s.Text; });
                                context.Dialogs.Alert("Preview applied", "The gallery profile was updated. This demonstration does not change any game characters.");
                            }) { Variant = ButtonVariant.Primary }.Width(130f),
                            new Button("Reset", delegate {
                                SetState(delegate(GalleryState s) { s.Text = "MaximumUI"; s.Mode = "Normal"; s.Slider = 0.45f; s.Enabled = true; s.Status = "Profile reset"; });
                            }) { Variant = ButtonVariant.Quiet }.Width(76f),
                            new Spacer()
                        }.Height(RowHeight)
                    }
                },
                new Grid(2, rowGap: 10f, columnGap: 12f) {
                    new Panel {
                        new VStack(gap: 10f) {
                            new Label("A quieter interface") { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                            new Label("Flat surfaces, clear type and one accent. Focus, selection and disabled states remain distinct.") { WordWrap = true, Role = LabelRole.Muted }.MinHeight(64f),
                            new Button("Choose appearance", delegate { SetState(delegate(GalleryState s) { s.Tab = 3; }); }).Height(RowHeight).ExpandWidth()
                        }
                    },
                    new Panel {
                        new VStack(gap: 10f) {
                            new Label("Room for large data") { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                            new Label("Explore 10,000 searchable rows. Scrolling creates controls for the visible range.") { WordWrap = true, Role = LabelRole.Muted }.MinHeight(64f),
                            new Button("Explore large lists", delegate { SetState(delegate(GalleryState s) { s.Tab = 5; }); }).Height(RowHeight).ExpandWidth()
                        }
                    }
                }
            };
        }

        private Widget BuildLargeList() {
            RefreshLargeItems();
            return new VStack(gap: 10f) {
                new SearchBox(State.LargeFilter, delegate(string value) {
                    SetState(delegate(GalleryState s) { s.LargeFilter = value; });
                }, key: "large-filter") { Placeholder = "Search by name or number" }.Height(RowHeight).ExpandWidth(),
                new HStack(gap: 8f) {
                    new Label(visibleItems.Count.ToString("N0") + " of 10,000 entries") { Role = LabelRole.Muted }.ExpandWidth(),
                    new Label(State.LargeSelection < 0 ? "No selection" : "Selected #" + State.LargeSelection.ToString()) { Role = LabelRole.Muted }.Width(160f)
                }.Height(RowHeight),
                new VirtualList(visibleItems.Count, RowHeight + 2f, delegate(int row) {
                    int index = visibleItems[row];
                    return new Selectable(largeItems[index], State.LargeSelection == index, delegate(bool selected) {
                        SetState(delegate(GalleryState s) { s.LargeSelection = index; });
                    }, key: "large-row-" + index.ToString()).Height(RowHeight).ExpandWidth();
                }, key: "large-virtual-list") { Height = 300f }.ExpandHeight().ExpandWidth(),
                new Label(visibleItems.Count == 0 ? "No matching entries. Clear or change the search to continue." : "Use the wheel to scroll. Selection survives filtering.") { Role = LabelRole.Muted, WordWrap = true }.MinHeight(32f)
            }.ExpandHeight();
        }

        private void RefreshLargeItems() {
            if (largeItems == null) {
                largeItems = new string[10000];
                for (int i = 0; i < largeItems.Length; i++) largeItems[i] = "Preset " + i.ToString("D5") + "   /   Profile " + (i % 12 + 1).ToString("D2");
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

            context.RegisterShortcut("gallery.controls", new ShortcutChord((int)KeyCode.Alpha1, control: true), delegate {
                SetState(delegate(GalleryState s) { s.Tab = 0; });
            });
            context.RegisterShortcut("gallery.layout", new ShortcutChord((int)KeyCode.Alpha2, control: true), delegate {
                SetState(delegate(GalleryState s) { s.Tab = 1; });
            });
            context.RegisterShortcut("gallery.dialogs", new ShortcutChord((int)KeyCode.Alpha3, control: true), delegate {
                SetState(delegate(GalleryState s) { s.Tab = 2; });
            });
            context.RegisterShortcut("gallery.style", new ShortcutChord((int)KeyCode.Alpha4, control: true), delegate {
                SetState(delegate(GalleryState s) { s.Tab = 3; });
            });
            context.RegisterShortcut("gallery.debug", new ShortcutChord((int)KeyCode.Alpha5, control: true), delegate {
                SetState(delegate(GalleryState s) { s.Tab = 4; });
            });
            context.RegisterShortcut("gallery.large", new ShortcutChord((int)KeyCode.Alpha6, control: true), delegate {
                SetState(delegate(GalleryState s) { s.Tab = 5; });
            });
            context.RegisterShortcut("gallery.log", new ShortcutChord((int)KeyCode.L, control: true), delegate {
                DebugLog.Info("Shortcut log entry " + State.ClickCount.ToString());
                SetState(delegate(GalleryState s) { s.ClickCount++; });
            });
        }

        private Widget BuildMenuBar(BuildContext context) {
            return new MenuBar(key: "gallery-menu") {
                new Menu("File") {
                    new MenuItem("Alert", delegate {
                        context.Dialogs.Alert("MaximumUI", "Menu item invoked Alert.");
                    }) {
                        Shortcut = "F1"
                    },
                    new MenuSeparator(),
                    new MenuItem("Disabled") {
                        Enabled = false
                    }
                },
                new Menu("View") {
                    new MenuItem("Controls", delegate {
                        SetState(delegate(GalleryState s) { s.Tab = 0; });
                    }) {
                        Checked = State.Tab == 0
                    },
                    new MenuItem("Layout", delegate {
                        SetState(delegate(GalleryState s) { s.Tab = 1; });
                    }) {
                        Checked = State.Tab == 1
                    },
                    new MenuItem("Dialogs", delegate {
                        SetState(delegate(GalleryState s) { s.Tab = 2; });
                    }) {
                        Checked = State.Tab == 2
                    },
                    new MenuItem("Style", delegate {
                        SetState(delegate(GalleryState s) { s.Tab = 3; });
                    }) {
                        Checked = State.Tab == 3
                    },
                    new MenuItem("Debug", delegate {
                        SetState(delegate(GalleryState s) { s.Tab = 4; });
                    }) {
                        Checked = State.Tab == 4
                    }
                },
                new Menu("Help") {
                    new MenuSeparator("About"),
                    new MenuItem("Increment clicks", delegate {
                        SetState(delegate(GalleryState s) { s.ClickCount++; });
                    })
                }
            }.Height(RowHeight).ExpandWidth();
        }

        private Widget BuildHeader(BuildContext context) {
            return new HStack(gap: 6f) {
                new Label("Runtime").Width(72f),
                new Toggle("Enabled", State.Enabled, delegate(bool value) {
                    SetState(delegate(GalleryState s) { s.Enabled = value; });
                }).Width(120f),
                new Spacer(),
                new Button("Alert", delegate {
                    context.Dialogs.Alert("MaximumUI", "DialogService.Alert is working.");
                }).Width(72f)
            };
        }

        private Widget BuildControlsTab(BuildContext context) {
            return new ScrollView(key: "controls-scroll") {
                AlwaysShowVertical = true
            } .AddFluent(new VStack(gap: 8f) {
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Basic widgets"),
                        new HStack(gap: 6f) {
                            new Button("Button", delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Width(96f),
                            new SmallButton("Small", delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Width(72f),
                            new ArrowButton(ArrowDirection.Right, delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Width(24f),
                            new Label("Clicks: " + State.ClickCount).Width(100f)
                        },
                        new HStack(gap: 6f) {
                            new Toggle("Toggle", State.Enabled, delegate(bool value) {
                                SetState(delegate(GalleryState s) { s.Enabled = value; });
                            }).Width(120f),
                            new Checkbox("Check", State.Enabled, delegate(bool value) {
                                SetState(delegate(GalleryState s) { s.Enabled = value; });
                            }).Width(96f)
                        },
                        new HStack(gap: 6f) {
                            new TextLink("TextLink", delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Width(72f),
                            new BulletText("BulletText").Width(110f),
                            new ValueLabel("Value", State.Mode).Width(150f),
                            new StatusBadge("OK", StatusBadgeKind.Success).Width(48f),
                            new LoadingIndicator("Loading").Width(96f)
                        },
                        new HStack(gap: 6f) {
                            new Label("Slider").Width(72f),
                            new Slider(State.Slider, 0f, 1f, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.Slider = value; });
                            }).Height(RowHeight).ExpandWidth(),
                            new Label(((int)(State.Slider * 100f)).ToString() + "%").Width(48f)
                        },
                        new HStack(gap: 6f) {
                            new InputInt("Int", State.IntValue, delegate(int value) {
                                SetState(delegate(GalleryState s) { s.IntValue = value; });
                            }, key: "input-int") {
                                HasMin = true,
                                Min = 0,
                                HasMax = true,
                                Max = 99
                            }.Height(RowHeight).ExpandWidth(),
                            new InputFloat("Float", State.FloatValue, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.FloatValue = value; });
                            }, key: "input-float") {
                                HasMin = true,
                                Min = 0f,
                                HasMax = true,
                                Max = 1f
                            }.Height(RowHeight).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new InputDouble("Double", State.DoubleValue, delegate(double value) {
                                SetState(delegate(GalleryState s) { s.DoubleValue = value; });
                            }, key: "input-double").Height(RowHeight).ExpandWidth(),
                            new DragInt("DragInt", State.DragIntValue, delegate(int value) {
                                SetState(delegate(GalleryState s) { s.DragIntValue = value; });
                            }, key: "drag-int") {
                                HasMin = true,
                                Min = 0,
                                HasMax = true,
                                Max = 20
                            }.Height(RowHeight).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new DragFloat("Drag", State.DragFloatValue, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.DragFloatValue = value; });
                            }, key: "drag-float") {
                                HasMin = true,
                                Min = 0f,
                                HasMax = true,
                                Max = 1f
                            }.Height(RowHeight).ExpandWidth(),
                            new SliderInt("SliderInt", State.SliderIntValue, 0, 100, delegate(int value) {
                                SetState(delegate(GalleryState s) { s.SliderIntValue = value; });
                            }, key: "slider-int").Height(RowHeight).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new SliderFloat("SliderFloat", State.FloatValue, 0f, 1f, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.FloatValue = value; });
                            }, key: "slider-float").Height(RowHeight).ExpandWidth(),
                            new SliderAngle("Angle", State.AngleValue, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.AngleValue = value; });
                            }, key: "slider-angle").Height(RowHeight).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new Label("Text").Width(72f),
                            new TextField(State.Text, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Text = value; });
                            }, key: "text") {
                                Placeholder = "Escape reverts",
                                Flags = TextInputFlags.EscapeReverts,
                                OnSubmit = delegate(string value) { DebugLog.Info("Submit text: " + value); },
                                OnBlur = delegate(string value) { DebugLog.Info("Blur text: " + value); }
                            }.ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new Label("Filtered").Width(72f),
                            new TextField(State.Code, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Code = value; });
                            }, key: "code") {
                                Placeholder = "uppercase no blanks",
                                Flags = TextInputFlags.Uppercase | TextInputFlags.NoBlank,
                                MaxLength = 12
                            }.Height(RowHeight).ExpandWidth(),
                            new TextField(State.Hex, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Hex = value; });
                            }, key: "hex") {
                                Placeholder = "HEX",
                                Flags = TextInputFlags.Hexadecimal | TextInputFlags.Uppercase | TextInputFlags.EscapeClearsAll,
                                MaxLength = 8
                            }.Height(RowHeight).Width(96f)
                        },
                        new HStack(gap: 6f) {
                            new Label("ReadOnly").Width(72f),
                            new TextField("Stable readonly text", null, key: "readonly-text") {
                                Flags = TextInputFlags.ReadOnly
                            }.Height(RowHeight).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new Label("Password").Width(72f),
                            new PasswordField(State.Password, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Password = value; });
                            }, key: "password") {
                                Placeholder = "Password",
                                Flags = TextInputFlags.NoBlank
                            }.Height(RowHeight).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new Label("TextArea").Width(72f),
                            new TextArea(State.Notes, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Notes = value; });
                            }, key: "notes") {
                                Placeholder = "Notes",
                                Flags = TextInputFlags.EnterReturnsTrue,
                                OnSubmit = delegate(string value) { DebugLog.Info("Submit notes: " + value); }
                            }.Height(58f).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new Label("Combo").Width(72f),
                            new ComboBox<string>(
                                string.Empty,
                                State.Mode,
                                modes,
                                delegate(string value) { return value; },
                                delegate(string value) {
                                    SetState(delegate(GalleryState s) { s.Mode = value; });
                                },
                                key: "mode") {
                                MaxVisibleItems = 4
                            }.ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new Label("Segment").Width(72f),
                            new SegmentedControl<string>(
                                State.Mode,
                                modes,
                                delegate(string value) { return value; },
                                delegate(string value) {
                                    SetState(delegate(GalleryState s) { s.Mode = value; });
                                },
                                key: "mode-segment").Height(RowHeight).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Scrollable ComboBox"),
                        new SeparatorText("Selection"),
                        new ComboBox<string>(
                            string.Empty,
                            State.ListSelection,
                            samples,
                            delegate(string value) { return value; },
                            delegate(string value) {
                                SetState(delegate(GalleryState s) { s.ListSelection = value; });
                            },
                            key: "samples") {
                            MaxVisibleItems = 6
                        }.ExpandWidth(),
                        new SearchableComboBox<string>(
                            "Search Combo",
                            State.ListSelection,
                            samples,
                            delegate(string value) { return value; },
                            delegate(string value) {
                                SetState(delegate(GalleryState s) { s.ListSelection = value; });
                            },
                            key: "searchable-samples") {
                            MaxVisibleItems = 6,
                            FilterPlaceholder = "Filter samples"
                        }.Height(RowHeight).ExpandWidth(),
                        new ListBox<string>(
                            State.ListSelection,
                            samples,
                            delegate(string value) { return value; },
                            delegate(string value) {
                                SetState(delegate(GalleryState s) { s.ListSelection = value; });
                            },
                            key: "sample-list") {
                            VisibleItems = 4
                        }.Height(88f).ExpandWidth(),
                        new SearchableListBox<string>(
                            State.ListSelection,
                            samples,
                            delegate(string value) { return value; },
                            delegate(string value) {
                                SetState(delegate(GalleryState s) { s.ListSelection = value; });
                            },
                            key: "searchable-list") {
                            VisibleItems = 4,
                            FilterText = State.ListFilter,
                            FilterPlaceholder = "Filter list",
                            OnFilterChange = delegate(string value) {
                                SetState(delegate(GalleryState s) { s.ListFilter = value; });
                            }
                        }.Height(114f).ExpandWidth(),
                        BuildMultiSelectDemo(),
                        new HelpBox("HelpBox supports informational messages.", HelpBoxType.Info),
                        new HelpBox("Warning and error states are rendered with the theme box style.", HelpBoxType.Warning)
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Overlay"),
                        new HStack(gap: 6f) {
                            new Button("Hover / right-click", delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            })
                                .Width(150f)
                                .Tooltip("Tooltip rendered by MaximumUI overlay.")
                                .ContextMenu(new VStack(gap: 4f) {
                                    new Button("Increment", delegate {
                                        SetState(delegate(GalleryState s) { s.ClickCount++; });
                                    }).Height(RowHeight).ExpandWidth(),
                                    new Button("Reset text", delegate {
                                        SetState(delegate(GalleryState s) { s.Text = "MaximumUI"; });
                                    }).Height(RowHeight).ExpandWidth()
                                }),
                            new Label("Right-click opens a context menu.").Height(RowHeight).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Drag and drop"),
                        new HStack(gap: 6f) {
                            new DragSource(new DragPayload("gallery.text", State.Text, "Dragging: " + State.Text), key: "drag-source") {
                                new Label("Drag me").Height(RowHeight)
                            }.Width(120f).Height(34f),
                            new DropTarget("gallery.text", delegate(DragPayload payload) {
                                string text = payload == null || payload.Data == null ? string.Empty : payload.Data.ToString();
                                SetState(delegate(GalleryState s) { s.DropMessage = "Dropped " + text; });
                            }, key: "drop-target") {
                                EmptyText = State.DropMessage
                            }.Height(34f).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Advanced controls"),
                        new HStack(gap: 6f) {
                            new RadioButton("Alpha", State.RadioIndex == 0, delegate {
                                SetState(delegate(GalleryState s) { s.RadioIndex = 0; });
                            }).Width(90f),
                            new RadioButton("Beta", State.RadioIndex == 1, delegate {
                                SetState(delegate(GalleryState s) { s.RadioIndex = 1; });
                            }).Width(90f),
                            new ProgressBar(State.Slider, ((int)(State.Slider * 100f)).ToString() + "%").Height(RowHeight).ExpandWidth()
                        },
                        new SearchBox(State.Search, delegate(string value) {
                            SetState(delegate(GalleryState s) { s.Search = value; });
                        }, key: "gallery-search") {
                            Placeholder = "Search controls"
                        }.Height(RowHeight).ExpandWidth(),
                        new Selectable("Selectable row", State.Selectable, delegate(bool value) {
                            SetState(delegate(GalleryState s) { s.Selectable = value; });
                        }).Height(RowHeight).ExpandWidth(),
                        new CollapsingHeader("Collapsing header", State.HeaderOpen, delegate(bool open) {
                            SetState(delegate(GalleryState s) { s.HeaderOpen = open; });
                        }, key: "gallery-collapsing") {
                            new Label("Header content").Height(RowHeight)
                        }.Height(State.HeaderOpen ? RowHeight * 2f : RowHeight).ExpandWidth(),
                        new ClosableCollapsingHeader("Closable header", State.HeaderOpen, delegate(bool open) {
                            SetState(delegate(GalleryState s) { s.HeaderOpen = open; });
                        }, key: "gallery-closable-header") {
                            Visible = State.ClosableHeaderVisible,
                            OnClose = delegate {
                                SetState(delegate(GalleryState s) { s.ClosableHeaderVisible = false; });
                            }
                        }.AddFluent(new Label("Closable content").Height(RowHeight))
                            .Height(State.ClosableHeaderVisible ? (State.HeaderOpen ? RowHeight * 2f : RowHeight) : 0f)
                            .ExpandWidth(),
                        new TreeNode("Tree node", State.TreeOpen, delegate(bool open) {
                            SetState(delegate(GalleryState s) { s.TreeOpen = open; });
                        }, key: "gallery-tree") {
                            Selected = State.TreeSelected,
                            OpenOnArrow = true,
                            OpenOnDoubleClick = true,
                            DrawLines = true,
                            Framed = true,
                            OnSelect = delegate {
                                SetState(delegate(GalleryState s) { s.TreeSelected = !s.TreeSelected; });
                            }
                        }.AddFluent(new TreeLeaf("Tree leaf", delegate {
                            SetState(delegate(GalleryState s) { s.TreeSelected = !s.TreeSelected; });
                        }) {
                            Selected = State.TreeSelected,
                            Bullet = true
                        }.Height(RowHeight)),
                        new HStack(gap: 6f) {
                            new ColorButton("ColorButton", State.Color, delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Height(RowHeight).Width(126f),
                            new ColorEdit3("Edit3", State.Color, delegate(MuiColor color) {
                                SetState(delegate(GalleryState s) { s.Color = color; });
                            }) {
                                DisplayMode = ColorDisplayMode.Byte
                            }.Height(RowHeight).ExpandWidth()
                        },
                        new ColorEdit4("Edit4", State.Color, delegate(MuiColor color) {
                            SetState(delegate(GalleryState s) { s.Color = color; });
                        }) {
                            DisplayMode = ColorDisplayMode.Hex
                        }.Height(RowHeight).ExpandWidth(),
                        new HStack(gap: 6f) {
                            new ColorPicker3("Picker3 RGB", State.Color, delegate(MuiColor color) {
                                SetState(delegate(GalleryState s) { s.Color = color; });
                            }) {
                                DisplayMode = ColorDisplayMode.Float
                            }.Height(94f).ExpandWidth(),
                            new ColorPicker4("Picker4 RGBA", State.Color, delegate(MuiColor color) {
                                SetState(delegate(GalleryState s) { s.Color = color; });
                            }) {
                                DisplayMode = ColorDisplayMode.Hex
                            }.Height(118f).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new ColorEdit(State.Color, delegate(MuiColor color) {
                                SetState(delegate(GalleryState s) { s.Color = color; });
                            }).Height(RowHeight).ExpandWidth(),
                            new VectorField("Vec", State.VectorX, State.VectorY, State.VectorZ, delegate(float x, float y, float z) {
                                SetState(delegate(GalleryState s) {
                                    s.VectorX = x;
                                    s.VectorY = y;
                                    s.VectorZ = z;
                                });
                            }).Height(RowHeight).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Images and plots"),
                        new HStack(gap: 6f) {
                            new Image(context.Theme.ButtonActive).Width(48f).Height(32f),
                            new ImageButton(context.Theme.ButtonHover, delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }) {
                                Text = "ImageButton"
                            }.Height(28f).ExpandWidth()
                        },
                        new PlotLines(plotValues) {
                            OverlayText = "PlotLines"
                        }.Height(56f).ExpandWidth(),
                        new PlotHistogram(plotValues) {
                            OverlayText = "PlotHistogram"
                        }.Height(56f).ExpandWidth(),
                        new HStack(gap: 6f) {
                            new DrawCanvas(DrawCanvasDemo, key: "draw-canvas") {
                                CanvasWidth = 180f,
                                CanvasHeight = 86f
                            }.Height(86f).ExpandWidth(),
                            BuildDrawListPreview()
                        }
                    }
                }
            });
        }

        private static void DrawCanvasDemo(DrawList drawList, MuiRect rect) {
            MuiColor accent = new MuiColor(0.12f, 0.12f, 0.12f, 1f);
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

        private static Widget BuildDrawListPreview() {
            DrawList drawList = new DrawList();
            drawList.FillRect(new MuiRect(8f, 8f, 132f, 70f), new MuiColor(0.92f, 0.92f, 0.92f, 1f));
            drawList.StrokeRect(new MuiRect(8f, 8f, 132f, 70f), new MuiColor(0f, 0f, 0f, 1f), 1f);
            drawList.Polyline(new[] {
                new MuiVec2(18f, 62f),
                new MuiVec2(44f, 24f),
                new MuiVec2(72f, 54f),
                new MuiVec2(104f, 18f),
                new MuiVec2(132f, 46f)
            }, new MuiColor(0.18f, 0.42f, 0.58f, 1f), 2f);
            drawList.Text(new MuiRect(14f, 10f, 120f, 20f), "DrawListWidget", null, new MuiColor(0f, 0f, 0f, 1f));

            return new DrawListWidget(drawList, key: "draw-list-widget") {
                CanvasWidth = 150f,
                CanvasHeight = 86f
            }.Height(86f).Width(150f);
        }

        private Widget BuildMultiSelectDemo() {
            VStack rows = new VStack(gap: 2f);
            rows.Add(new Label("Multi-select rows").Height(RowHeight));
            rows.Add(new Label("Selected: " + FormatSelection(State.MultiSelection)).Height(RowHeight));

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
                rows.Add(row.Height(22f).ExpandWidth());
            }

            return rows;
        }

        private static string FormatSelection(SelectionState<string> selection) {
            if (selection == null || selection.Count == 0) return "none";

            string text = string.Empty;
            for (int i = 0; i < selection.SelectedItems.Count; i++) {
                if (i > 0) text += ", ";
                text += selection.SelectedItems[i];
            }

            return text;
        }

        private Widget BuildLayoutTab(BuildContext context) {
            return new VStack(gap: 12f) {
                new Panel {
                    new VStack(gap: 8f) {
                        new Label("Menus and tabs") { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                        BuildMenuBar(context),
                        new Tabs(State.ExampleTab, delegate(int index) { SetState(delegate(GalleryState s) { s.ExampleTab = index; }); }, key: "example-tabs") {
                            new Tab("Details").Content(State.ExampleTab == 0 ? new Label("One active tab contributes its content.").Height(RowHeight) : MUI.Empty),
                            new Tab("Activity").Content(State.ExampleTab == 1 ? new Label("Activity is selected.").Height(RowHeight) : MUI.Empty),
                            new Tab("Settings").Content(State.ExampleTab == 2 ? new Toggle("Enabled", State.Enabled, delegate(bool value) { SetState(delegate(GalleryState s) { s.Enabled = value; }); }).Height(RowHeight) : MUI.Empty)
                        }.ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("VStack, HStack, Space and Spacer"),
                        new HStack(gap: 6f) {
                            new Button("Left").Width(80f),
                            new MaximumUI.Space(8f),
                            new Button("Middle").Width(90f),
                            new Spacer(),
                            new Button("Right").Width(80f)
                        },
                        new Separator(),
                        new Foldout("Foldout section", State.FoldoutOpen, delegate(bool value) {
                            SetState(delegate(GalleryState s) { s.FoldoutOpen = value; });
                        }, key: "foldout") {
                            new Label("Foldout content"),
                            new HStack(gap: 6f) {
                                new Label("Fixed").Width(72f),
                                new Button("Expand width").ExpandWidth()
                            }
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Declarative composition"),
                        new HStack(gap: 6f) {
                            new Button("A").Width(44f),
                            new Button("B").Width(44f),
                            new Button("C").Width(44f),
                            new Label("All gallery elements are MaximumUI widgets.").Height(RowHeight).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Grid"),
                        new Grid(3, rowGap: 4f, columnGap: 4f) {
                            new Button("One").Height(RowHeight),
                            new Button("Two").Height(RowHeight),
                            new Button("Three").Height(RowHeight),
                            new Toggle("Four", State.Enabled, delegate(bool value) {
                                SetState(delegate(GalleryState s) { s.Enabled = value; });
                            }).Height(RowHeight),
                            new Selectable("Five", State.Selectable, delegate(bool value) {
                                SetState(delegate(GalleryState s) { s.Selectable = value; });
                            }).Height(RowHeight),
                            new ProgressBar(State.Slider).Height(RowHeight)
                        }.Height(RowHeight * 2f + 4f).ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Table"),
                        BuildLayoutTable().ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("VirtualList"),
                        new VirtualList(10000, RowHeight, delegate(int index) {
                            return new HStack(gap: 6f, key: "virtual-row-" + index.ToString()) {
                                new Label("#" + index.ToString()).Width(54f).Height(RowHeight),
                                new Label("Only visible rows are built").Height(RowHeight).ExpandWidth(),
                                new SmallButton("+", delegate {
                                    SetState(delegate(GalleryState s) { s.ClickCount++; });
                                }).Width(32f).Height(RowHeight)
                            };
                        }, key: "virtual-list") {
                            Height = 132f
                        }.ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("TwoPaneLayout"),
                        new TwoPaneLayout(State.SplitRatio, delegate(float ratio) {
                            SetState(delegate(GalleryState s) { s.SplitRatio = ratio; });
                        }, key: "split-pane") {
                            MinFirst = 80f,
                            MinSecond = 80f
                        }.AddFluent(new Panel {
                            new Label("Left pane").Height(RowHeight),
                            new Button("Action").Height(RowHeight).ExpandWidth()
                        }).AddFluent(new Panel {
                            new Label("Right pane").Height(RowHeight),
                            new ProgressBar(State.SplitRatio).Height(RowHeight).ExpandWidth()
                        }).Height(96f).ExpandWidth()
                    }
                }
            };
        }

        private Widget BuildDialogsTab(BuildContext context) {
            return new VStack(gap: 8f) {
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("DialogService"),
                        new HStack(gap: 6f) {
                            new Button("Alert", delegate {
                                context.Dialogs.Alert("Alert", "This dialog was opened by DialogService.");
                            }).Width(90f),
                            new Button("Confirm", delegate {
                                context.Dialogs.Confirm(
                                    "Confirm",
                                    "Increment click counter?",
                                    delegate {
                                        SetState(delegate(GalleryState s) { s.ClickCount++; });
                                    },
                                    null);
                            }).Width(90f)
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Inline Dialog widget"),
                        new Button("Open inline dialog", delegate {
                            SetState(delegate(GalleryState s) { s.ShowInlineDialog = true; });
                        }).Width(150f),
                        MUI.If(State.ShowInlineDialog, delegate {
                            return new Dialog("Inline Dialog", key: "inline-dialog") {
                                Width = 340f,
                                Height = 240f,
                                CloseOnEscape = true,
                                CloseOnOutsideClick = true,
                                OnClose = delegate {
                                    SetState(delegate(GalleryState s) { s.ShowInlineDialog = false; });
                                }
                            }.AddFluent(new VStack(gap: 8f) {
                                new Label("Edit a name or choose a mode inside the modal.") { WordWrap = true }.MinHeight(36f),
                                new TextField(State.Text, delegate(string value) { SetState(delegate(GalleryState s) { s.Text = value; }); }, key: "dialog-name").Height(RowHeight).ExpandWidth(),
                                new ComboBox<string>("Mode", State.Mode, modes, delegate(string value) { return value; }, delegate(string value) { SetState(delegate(GalleryState s) { s.Mode = value; }); }, key: "dialog-mode").Height(RowHeight).ExpandWidth(),
                                new Separator(),
                                new HStack(gap: 6f) {
                                    new Spacer(),
                                    new Button("Close", delegate {
                                        SetState(delegate(GalleryState s) { s.ShowInlineDialog = false; });
                                    }).Width(80f)
                                }
                            });
                        })
                    }
                }
            };
        }

        private Table BuildLayoutTable() {
            Table table = new Table(3);
            table.AlternatingRows = true;
            table.HeaderHeight = RowHeight;

            table.Add(new TableColumn("Name") {
                Width = 90f,
                MinWidth = 72f,
                Stretch = false,
                Sortable = true,
                SortDirection = State.TableSort,
                OnSort = delegate(TableSortDirection direction) {
                    SetState(delegate(GalleryState s) { s.TableSort = direction; });
                }
            });
            table.Add(new TableColumn("State") {
                Visible = false
            });
            table.Add(new TableColumn("Action") {
                Width = 80f,
                MinWidth = 70f,
                MaxWidth = 96f,
                Stretch = false
            });

            table.Add(new TableRow {
                new TableCell { new Label("Slider").Height(RowHeight) },
                new TableCell { new ProgressBar(State.Slider).Height(RowHeight) },
                new TableCell {
                    new Button("Reset", delegate {
                        SetState(delegate(GalleryState s) { s.Slider = 0.5f; });
                    }).Height(RowHeight).ExpandWidth()
                }
            });
            table.Add(new TableRow {
                new TableCell { new Label("Mode").Height(RowHeight) },
                new TableCell { new Label(State.Mode).Height(RowHeight) },
                new TableCell {
                    new Button("Click", delegate {
                        SetState(delegate(GalleryState s) { s.ClickCount++; });
                    }).Height(RowHeight).ExpandWidth()
                }
            });

            return table;
        }

        private Widget BuildStyleTab(BuildContext context) {
            return new ScrollView(key: "style-scroll") { AlwaysShowVertical = true }.Content(new VStack(gap: 14f) {
                new Panel {
                    new VStack(gap: 10f) {
                        new Label("Palette and scale") { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                        new ThemeSelector("Palette", State.ThemeName, themeNames, delegate(string value) {
                            SwitchTheme(context, value);
                        }, key: "theme-selector").Height(RowHeight).ExpandWidth(),
                        new FontSizeSelector("Text size", State.EditableTheme.FontSize, fontSizes, delegate(float value) {
                            SetState(delegate(GalleryState s) { s.EditableTheme.FontSize = value; s.ThemePending = true; });
                        }, key: "font-size-selector").Height(RowHeight).ExpandWidth(),
                        new Label("Palette changes apply immediately. Edited colors and scale apply together when you choose Apply.") { Role = LabelRole.Muted, WordWrap = true }.MinHeight(42f),
                        new HStack(gap: 8f) {
                            new Button("Apply changes", delegate { ApplyEditedTheme(context); }) { Variant = ButtonVariant.Primary }.Width(140f).EnabledWhen(State.ThemePending),
                            new Button("Revert edits", delegate {
                                SetState(delegate(GalleryState s) { s.EditableTheme = context.Theme.Tokens.Clone(); s.ThemePending = false; });
                            }) { Variant = ButtonVariant.Quiet }.Width(110f).EnabledWhen(State.ThemePending),
                            new Spacer(),
                            new StatusBadge(State.ThemePending ? "UNAPPLIED" : "APPLIED", State.ThemePending ? StatusBadgeKind.Warning : StatusBadgeKind.Success).Width(96f)
                        }.Height(RowHeight)
                    }
                },
                new Panel {
                    new VStack(gap: 10f) {
                        new Label("Accent color") { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                        new ColorEdit4("Accent", State.EditableTheme.Accent, delegate(MuiColor color) {
                            SetState(delegate(GalleryState s) { s.EditableTheme.Accent = color; s.ThemePending = true; });
                        }, key: "theme-accent") { DisplayMode = ColorDisplayMode.Byte }.Height(RowHeight).ExpandWidth(),
                        new Label("Preview your foreground and accent together; keep labels readable after changing a palette.") { Role = LabelRole.Muted, WordWrap = true }.MinHeight(40f)
                    }
                },
                new Panel {
                    new VStack(gap: 10f) {
                        new Label("Surface and control tokens") { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                        new ThemeEditor(State.EditableTheme, delegate(ThemeTokens tokens) {
                            SetState(delegate(GalleryState s) { s.EditableTheme = tokens; s.ThemePending = true; });
                        }, key: "theme-editor") { MaxRows = 10, ScalarStep = 1f, ColorTokens = nativeColors, ScalarTokens = nativeScalars }.Height(300f).ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 10f) {
                        new Label("States") { Role = LabelRole.Heading }.Height(RowHeight + 10f),
                        new HStack(gap: 8f) {
                            new Button("Primary") { Variant = ButtonVariant.Primary }.ExpandWidth(),
                            new Button("Default").ExpandWidth(),
                            new Button("Quiet") { Variant = ButtonVariant.Quiet }.ExpandWidth(),
                            new Button("Danger") { Variant = ButtonVariant.Danger }.ExpandWidth()
                        }.Height(RowHeight),
                        new Button("Disabled action") { Variant = ButtonVariant.Primary }.Disabled(true).Height(RowHeight).ExpandWidth(),
                        new HStack(gap: 8f) {
                            new StatusBadge("Info", StatusBadgeKind.Info).ExpandWidth(),
                            new StatusBadge("Success", StatusBadgeKind.Success).ExpandWidth(),
                            new StatusBadge("Warning", StatusBadgeKind.Warning).ExpandWidth(),
                            new StatusBadge("Error", StatusBadgeKind.Error).ExpandWidth()
                        }.Height(RowHeight),
                        new Label("Rendering uses the Unity/game font. No font atlas statistics are synthesized.") { Role = LabelRole.Muted, WordWrap = true }.MinHeight(40f)
                    }
                }
            });
        }

        private void SwitchTheme(BuildContext context, string name) {
            MaximumTheme next = name == "Metro Light" ? MaximumTheme.MetroLight :
                name == "Clean Light" ? MaximumTheme.CleanLight :
                name == "Expression Light" ? MaximumTheme.ExpressionLight : MaximumTheme.MetroDark;
            if (!MaximumUIRuntime.SetTheme(context.MountId, next)) return;
            if (ownedTheme != null) ownedTheme.Dispose();
            ownedTheme = null;
            SetState(delegate(GalleryState s) {
                s.ThemeName = next.Name; s.FontSize = next.FontSize;
                s.EditableTheme = next.Tokens.Clone(); s.ThemePending = false;
                s.Status = next.Name + " applied";
            });
        }

        private void ApplyEditedTheme(BuildContext context) {
            MaximumTheme next = MaximumTheme.Create(State.EditableTheme, name: State.ThemeName + " / Custom");
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
            SetState(delegate(GalleryState s) {
                s.EditableTheme = next.Tokens.Clone(); s.FontSize = next.FontSize;
                s.ThemePending = false; s.Status = "Custom appearance applied";
            });
        }

        private Widget BuildDebugTab(BuildContext context) {
            return new ScrollView(key: "debug-scroll") {
                AlwaysShowVertical = true
            }.AddFluent(new VStack(gap: 8f) {
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Debug tools"),
                        new HStack(gap: 6f) {
                            new Button("Add log", delegate {
                                DebugLog.Info("Gallery log entry " + State.ClickCount.ToString());
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }, key: "debug-add-log").Width(90f),
                            new Button("Warn", delegate {
                                DebugLog.Warning("Gallery warning " + State.ClickCount.ToString());
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Width(90f),
                            new Button("Clear", delegate {
                                DebugLog.Clear();
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Width(90f),
                            new Button("Copy log", delegate {
                                DebugLog.LogToClipboard();
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Width(90f)
                        },
                        new Label("Add log hovered: " + context.IsHovered("debug-add-log").ToString() +
                            " clicked: " + context.IsClicked("debug-add-log").ToString()).Height(RowHeight)
                    }
                },
                new MetricsWindow(MaximumUIRuntime.GetMetrics(), key: "metrics").Height(154f).ExpandWidth(),
                new InputDebugger(context.Query, key: "input-debug") {
                    Navigation = context.Navigation,
                    Shortcuts = context.Shortcuts,
                    Rows = 9
                }.Height(220f).ExpandWidth(),
                new DebugLogWindow(DebugLog.Snapshot(), key: "debug-log") {
                    Rows = 7
                }.Height(176f).ExpandWidth(),
                new StateStoreInspector(context.StateStore, key: "state-store") {
                    Rows = 8
                }.Height(198f).ExpandWidth(),
                new IDStackTool(context.Query, key: "id-stack") {
                    Rows = 8
                }.Height(198f).ExpandWidth(),
                new ItemPicker(context.Query, key: "item-picker") {
                    Rows = 8
                }.Height(198f).ExpandWidth(),
                BuildDemoGallery(),
                new AboutWindow("MaximumUI Gallery", MaximumUIGalleryPlugin.Version, "Runtime verification surface for MaximumUI.", key: "about") {
                    Rows = 5
                }.Height(132f).ExpandWidth(),
                new UserGuideWindow(key: "user-guide") {
                    Rows = 6
                }.Height(154f).ExpandWidth()
            });
        }

        private Widget BuildDemoGallery() {
            DemoGallery gallery = new DemoGallery("demo-gallery") {
                Rows = 8
            };
            gallery.Entries.Add(new DemoGalleryEntry("Controls", "Buttons, toggles, sliders, text input", "visible"));
            gallery.Entries.Add(new DemoGalleryEntry("Controls", "ComboBox, ListBox, Selectable", "visible"));
            gallery.Entries.Add(new DemoGalleryEntry("Layout", "Grid, Table, VirtualList, TwoPaneLayout", "visible"));
            gallery.Entries.Add(new DemoGalleryEntry("Dialogs", "Alert, Confirm and inline input dialog", "visible"));
            gallery.Entries.Add(new DemoGalleryEntry("Style", "ThemeEditor and token preview", "visible"));
            gallery.Entries.Add(new DemoGalleryEntry("Debug", "Metrics, input, log, state, IDs and item picker", "visible"));
            return gallery.Height(198f).ExpandWidth();
        }
    }

    internal static class WidgetFluentExtensions {
        public static T AddFluent<T>(this T widget, Widget child) where T : Widget {
            widget.Add(child);
            return widget;
        }
    }
}
