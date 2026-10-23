using System;
using System.Collections.Generic;
using BepInEx;
using MaximumUI;
using UnityEngine;

[assembly: System.Reflection.AssemblyFileVersion(MaximumUIGallery.MaximumUIGalleryPlugin.Version)]

namespace MaximumUIGallery {
    [BepInPlugin(GUID, "MaximumUI Gallery", Version)]
    public sealed class MaximumUIGalleryPlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.MaximumUIGallery";
        public const string Version = "0.1.0";

        private const string MountId = GUID + ".main";

        private void Awake() {
            MaximumUIRuntime.EnsureHost();
            MaximumUIRuntime.Mount(
                MountId,
                new GalleryView(),
                new MountOptions {
                    Visible = true,
                    HasToggleKey = true,
                    ToggleKey = KeyCode.F8,
                    SortOrder = 500
                });
        }

        private void OnDestroy() {
            MaximumUIRuntime.Unmount(MountId);
        }
    }

    internal sealed class GalleryState {
        public bool Enabled = true;
        public bool FoldoutOpen = true;
        public bool HeaderOpen = true;
        public bool ClosableHeaderVisible = true;
        public bool ShowDebugTab = true;
        public bool ShowInlineDialog;
        public int Tab;
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
        public ThemeTokens EditableTheme = new ThemeTokens();
        public string FontName = "Default";
        public float FontSize = 12f;
        public string StyleName = "Default";
        public string ThemeName = "Expression Light";
        public float VectorX = 1f;
        public float VectorY = 2f;
        public float VectorZ = 3f;
    }

    internal sealed class GalleryView : StatefulView<GalleryState> {
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
        private readonly string[] fontNames = { "Default", "Monospace", "Small", "Large" };
        private readonly float[] fontSizes = { 10f, 11f, 12f, 14f, 16f, 18f, 20f };
        private readonly string[] styleNames = { "Default", "Compact", "High Contrast" };
        private readonly string[] themeNames = { "Expression Light", "Classic", "Debug Contrast" };

        public GalleryView()
            : base(new GalleryState()) {
        }

        public override Widget Build(BuildContext context) {
            RegisterShortcuts(context);

            return new Window(
                "MaximumUI Gallery 0.1.0",
                width: 520f,
                height: 620f,
                key: "gallery",
                minWidth: 430f,
                minHeight: 420f,
                foldable: true,
                closable: false)
            {
                new VStack(gap: 6f) {
                    BuildHeader(context),
                    BuildMenuBar(context),
                    new Separator(),
                    new Tabs(State.Tab, delegate(int tab) {
                        SetState(delegate(GalleryState s) { s.Tab = tab; });
                    }, key: "tabs") {
                        Reorderable = true,
                        OnReorder = delegate(int from, int to) {
                            DebugLog.Info("Tab reorder requested " + from.ToString() + " -> " + to.ToString());
                        }
                    }.Content(new LeadingTabButton("+", delegate {
                        SetState(delegate(GalleryState s) { s.ShowDebugTab = true; });
                    }, key: "show-debug-tab") {
                        ButtonWidth = 24f
                    })
                    .Content(new Tab("Controls").Content(BuildControlsTab(context)))
                    .Content(new Tab("Layout").Content(BuildLayoutTab()))
                    .Content(new Tab("Dialogs").Content(BuildDialogsTab(context)))
                    .Content(new Tab("Style") {
                        Unsaved = true
                    }.Content(BuildStyleTab()))
                    .Content(new Tab("Debug") {
                        Closable = true,
                        Visible = State.ShowDebugTab,
                        OnClose = delegate {
                            SetState(delegate(GalleryState s) {
                                s.ShowDebugTab = false;
                                if (s.Tab > 3) s.Tab = 3;
                            });
                        }
                    }.Content(BuildDebugTab(context)))
                    .Content(new TrailingTabButton("Log", delegate {
                        DebugLog.Info("Trailing tab button " + State.ClickCount.ToString());
                        SetState(delegate(GalleryState s) { s.ClickCount++; });
                    }, key: "tab-log") {
                        ButtonWidth = 42f
                    }).ExpandWidth(),
                    new Separator(),
                    new Label("F8 toggles this gallery.").Height(20f)
                }
            };
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
            }.Height(22f).ExpandWidth();
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
                Height = 440f
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
                            new Label("Clicks: " + State.ClickCount).Width(100f),
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
                            }).Height(22f).ExpandWidth(),
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
                            }.Height(22f).ExpandWidth(),
                            new InputFloat("Float", State.FloatValue, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.FloatValue = value; });
                            }, key: "input-float") {
                                HasMin = true,
                                Min = 0f,
                                HasMax = true,
                                Max = 1f
                            }.Height(22f).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new InputDouble("Double", State.DoubleValue, delegate(double value) {
                                SetState(delegate(GalleryState s) { s.DoubleValue = value; });
                            }, key: "input-double").Height(22f).ExpandWidth(),
                            new DragInt("DragInt", State.DragIntValue, delegate(int value) {
                                SetState(delegate(GalleryState s) { s.DragIntValue = value; });
                            }, key: "drag-int") {
                                HasMin = true,
                                Min = 0,
                                HasMax = true,
                                Max = 20
                            }.Height(22f).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new DragFloat("Drag", State.DragFloatValue, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.DragFloatValue = value; });
                            }, key: "drag-float") {
                                HasMin = true,
                                Min = 0f,
                                HasMax = true,
                                Max = 1f
                            }.Height(22f).ExpandWidth(),
                            new SliderInt("SliderInt", State.SliderIntValue, 0, 100, delegate(int value) {
                                SetState(delegate(GalleryState s) { s.SliderIntValue = value; });
                            }, key: "slider-int").Height(22f).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new SliderFloat("SliderFloat", State.FloatValue, 0f, 1f, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.FloatValue = value; });
                            }, key: "slider-float").Height(22f).ExpandWidth(),
                            new SliderAngle("Angle", State.AngleValue, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.AngleValue = value; });
                            }, key: "slider-angle").Height(22f).ExpandWidth()
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
                            }.Height(22f).ExpandWidth(),
                            new TextField(State.Hex, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Hex = value; });
                            }, key: "hex") {
                                Placeholder = "HEX",
                                Flags = TextInputFlags.Hexadecimal | TextInputFlags.Uppercase | TextInputFlags.EscapeClearsAll,
                                MaxLength = 8
                            }.Height(22f).Width(96f)
                        },
                        new HStack(gap: 6f) {
                            new Label("ReadOnly").Width(72f),
                            new TextField("Stable readonly text", null, key: "readonly-text") {
                                Flags = TextInputFlags.ReadOnly
                            }.Height(22f).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new Label("Password").Width(72f),
                            new PasswordField(State.Password, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Password = value; });
                            }, key: "password") {
                                Placeholder = "Password",
                                Flags = TextInputFlags.NoBlank
                            }.Height(22f).ExpandWidth()
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
                                key: "mode-segment").Height(22f).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Scrollable ComboBox"),
                        new SeparatorText("Selection"),
                        new ComboBox<string>(
                            string.Empty,
                            samples[0],
                            samples,
                            delegate(string value) { return value; },
                            delegate(string value) {
                                SetState(delegate(GalleryState s) { s.Text = value; });
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
                        }.Height(22f).ExpandWidth(),
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
                                    }).Height(22f).ExpandWidth(),
                                    new Button("Reset text", delegate {
                                        SetState(delegate(GalleryState s) { s.Text = "MaximumUI"; });
                                    }).Height(22f).ExpandWidth()
                                }),
                            new Label("Right-click opens a context menu.").Height(22f).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Drag and drop"),
                        new HStack(gap: 6f) {
                            new DragSource(new DragPayload("gallery.text", State.Text, "Dragging: " + State.Text), key: "drag-source") {
                                new Label("Drag me").Height(22f)
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
                            new ProgressBar(State.Slider, ((int)(State.Slider * 100f)).ToString() + "%").Height(20f).ExpandWidth()
                        },
                        new SearchBox(State.Search, delegate(string value) {
                            SetState(delegate(GalleryState s) { s.Search = value; });
                        }, key: "gallery-search") {
                            Placeholder = "Search controls"
                        }.Height(22f).ExpandWidth(),
                        new Selectable("Selectable row", State.Selectable, delegate(bool value) {
                            SetState(delegate(GalleryState s) { s.Selectable = value; });
                        }).Height(22f).ExpandWidth(),
                        new CollapsingHeader("Collapsing header", State.HeaderOpen, delegate(bool open) {
                            SetState(delegate(GalleryState s) { s.HeaderOpen = open; });
                        }, key: "gallery-collapsing") {
                            new Label("Header content").Height(20f)
                        }.Height(State.HeaderOpen ? 44f : 22f).ExpandWidth(),
                        new ClosableCollapsingHeader("Closable header", State.HeaderOpen, delegate(bool open) {
                            SetState(delegate(GalleryState s) { s.HeaderOpen = open; });
                        }, key: "gallery-closable-header") {
                            Visible = State.ClosableHeaderVisible,
                            OnClose = delegate {
                                SetState(delegate(GalleryState s) { s.ClosableHeaderVisible = false; });
                            }
                        }.AddFluent(new Label("Closable content").Height(20f))
                            .Height(State.ClosableHeaderVisible ? (State.HeaderOpen ? 44f : 22f) : 0f)
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
                        }.Height(20f)),
                        new HStack(gap: 6f) {
                            new ColorButton("ColorButton", State.Color, delegate {
                                SetState(delegate(GalleryState s) { s.ClickCount++; });
                            }).Height(22f).Width(126f),
                            new ColorEdit3("Edit3", State.Color, delegate(MuiColor color) {
                                SetState(delegate(GalleryState s) { s.Color = color; });
                            }) {
                                DisplayMode = ColorDisplayMode.Byte
                            }.Height(22f).ExpandWidth()
                        },
                        new ColorEdit4("Edit4", State.Color, delegate(MuiColor color) {
                            SetState(delegate(GalleryState s) { s.Color = color; });
                        }) {
                            DisplayMode = ColorDisplayMode.Hex
                        }.Height(22f).ExpandWidth(),
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
                            }).Height(22f).ExpandWidth(),
                            new VectorField("Vec", State.VectorX, State.VectorY, State.VectorZ, delegate(float x, float y, float z) {
                                SetState(delegate(GalleryState s) {
                                    s.VectorX = x;
                                    s.VectorY = y;
                                    s.VectorZ = z;
                                });
                            }).Height(22f).ExpandWidth()
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
            rows.Add(new Label("Multi-select rows").Height(20f));
            rows.Add(new Label("Selected: " + FormatSelection(State.MultiSelection)).Height(20f));

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

        private Widget BuildLayoutTab() {
            return new VStack(gap: 8f) {
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
                            new Label("All gallery elements are MaximumUI widgets.").Height(22f).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Grid"),
                        new Grid(3, rowGap: 4f, columnGap: 4f) {
                            new Button("One").Height(22f),
                            new Button("Two").Height(22f),
                            new Button("Three").Height(22f),
                            new Toggle("Four", State.Enabled, delegate(bool value) {
                                SetState(delegate(GalleryState s) { s.Enabled = value; });
                            }).Height(22f),
                            new Selectable("Five", State.Selectable, delegate(bool value) {
                                SetState(delegate(GalleryState s) { s.Selectable = value; });
                            }).Height(22f),
                            new ProgressBar(State.Slider).Height(22f)
                        }.Height(48f).ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Table"),
                        BuildLayoutTable().Height(96f).ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("VirtualList"),
                        new VirtualList(1000, 22f, delegate(int index) {
                            return new HStack(gap: 6f, key: "virtual-row-" + index.ToString()) {
                                new Label("#" + index.ToString()).Width(54f).Height(22f),
                                new Label("Only visible rows are built").Height(22f).ExpandWidth(),
                                new SmallButton("+", delegate {
                                    SetState(delegate(GalleryState s) { s.ClickCount++; });
                                }).Width(32f).Height(20f)
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
                            new Label("Left pane").Height(22f),
                            new Button("Action").Height(22f).ExpandWidth()
                        }).AddFluent(new Panel {
                            new Label("Right pane").Height(22f),
                            new ProgressBar(State.SplitRatio).Height(22f).ExpandWidth()
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
                                Height = 150f,
                                CloseOnEscape = true,
                                CloseOnOutsideClick = true,
                                OnClose = delegate {
                                    SetState(delegate(GalleryState s) { s.ShowInlineDialog = false; });
                                }
                            }.AddFluent(new VStack(gap: 8f) {
                                new Label("This is a MaximumUI Dialog widget."),
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
            table.HeaderHeight = 24f;

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
                new TableCell { new Label("Slider").Height(22f) },
                new TableCell { new ProgressBar(State.Slider).Height(22f) },
                new TableCell {
                    new Button("Reset", delegate {
                        SetState(delegate(GalleryState s) { s.Slider = 0.5f; });
                    }).Height(22f).ExpandWidth()
                }
            });
            table.Add(new TableRow {
                new TableCell { new Label("Mode").Height(22f) },
                new TableCell { new Label(State.Mode).Height(22f) },
                new TableCell {
                    new Button("Click", delegate {
                        SetState(delegate(GalleryState s) { s.ClickCount++; });
                    }).Height(22f).ExpandWidth()
                }
            });

            return table;
        }

        private Widget BuildStyleTab() {
            return new ScrollView(key: "style-scroll") {
                Height = 440f
            }.AddFluent(new VStack(gap: 8f) {
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Selectors"),
                        new HStack(gap: 6f) {
                            new FontSelector("Font", State.FontName, fontNames, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.FontName = value; });
                            }, key: "font-selector").Height(22f).ExpandWidth(),
                            new FontSizeSelector("Size", State.FontSize, fontSizes, delegate(float value) {
                                SetState(delegate(GalleryState s) { s.FontSize = value; });
                            }, key: "font-size-selector").Height(22f).Width(150f)
                        },
                        new HStack(gap: 6f) {
                            new StyleSelector("Style", State.StyleName, styleNames, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.StyleName = value; });
                            }, key: "style-selector").Height(22f).ExpandWidth(),
                            new ThemeSelector("Theme", State.ThemeName, themeNames, delegate(string value) {
                                SetState(delegate(GalleryState s) { s.ThemeName = value; });
                            }, key: "theme-selector").Height(22f).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Theme tokens"),
                        new ThemeEditor(State.EditableTheme, delegate(ThemeTokens tokens) {
                            SetState(delegate(GalleryState s) {
                                s.EditableTheme = tokens;
                            });
                        }, key: "theme-editor") {
                            MaxRows = 12,
                            ScalarStep = 1f
                        }.Height(292f).ExpandWidth()
                    }
                },
                new Panel {
                    new VStack(gap: 6f) {
                        new Label("Token preview"),
                        new HStack(gap: 6f) {
                            new ColorEdit(State.EditableTheme.Accent, delegate(MuiColor color) {
                                SetState(delegate(GalleryState s) { s.EditableTheme.SetColor(ThemeColorToken.Accent, color); });
                            }).Height(22f).ExpandWidth(),
                            new ProgressBar(State.Slider).Height(22f).ExpandWidth()
                        },
                        new HStack(gap: 6f) {
                            new StatusBadge("Info", StatusBadgeKind.Info).Width(64f),
                            new StatusBadge("Warn", StatusBadgeKind.Warning).Width(64f),
                            new StatusBadge("Error", StatusBadgeKind.Error).Width(64f),
                            new Label("Font " + State.EditableTheme.FontSize.ToString("0")).Height(22f).ExpandWidth()
                        }
                    }
                },
                new Panel {
                    new HStack(gap: 6f) {
                        new FontAtlasViewer(key: "font-atlas") {
                            FontName = State.FontName,
                            TextureWidth = 256,
                            TextureHeight = 256,
                            GlyphCount = 128,
                            SampleText = "Preview " + State.FontName + " " + State.FontSize.ToString("0"),
                            Rows = 6
                        }.Height(154f).ExpandWidth(),
                        new FallbackGlyphDiagnostics(key: "fallback-glyphs") {
                            FontName = State.FontName,
                            SampleText = "ABC 123",
                            Rows = 6
                        }.Height(154f).ExpandWidth()
                    }
                }
            });
        }

        private Widget BuildDebugTab(BuildContext context) {
            return new ScrollView(key: "debug-scroll") {
                Height = 440f
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
                            " clicked: " + context.IsClicked("debug-add-log").ToString()).Height(22f)
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
                new AboutWindow("MaximumUI Gallery", "0.1.0", "Runtime verification surface for MaximumUI.", key: "about") {
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
            gallery.Entries.Add(new DemoGalleryEntry("Dialogs", "Alert, Confirm, Prompt and inline dialog", "visible"));
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
