# MaximumUI 设计文档

> 当前实现为 0.2.0。Metro 主题、缓存失效、输入归属、生命周期与已实现 API 请优先阅读 [库 README](src/MaximumUI/README.md) 和 [后续修复记录](docs/Followup-2026-10-23.md)。本文保留设计说明，规划内容不等同于所有后端已经实现。

## 1. 概述

MaximumUI 是一个面向 Koikatsu 插件开发的 C# 声明式 IMGUI UI 库。

目标运行环境：

```text
Game: Koikatsu / Koikatsu Party
Mod loader: BepInEx 5
Unity: 5.6
Target framework: net35
Rendering backend: Unity OnGUI platform entry + MaximumUI layout/input/painter + Unity low-level drawing
```

MaximumUI 的目标是为 KK 插件提供一套简洁、类型安全、可组合、可维护的 UI 编写方式。它不尝试实现真正的 JSX 语法，也不依赖 uGUI、UI Toolkit、XAML、Source Generator、Dear ImGui native backend 或运行时代码编译。UI 以普通 C# 对象树的形式声明，默认后端使用 Unity `OnGUI` 作为平台入口，但控件布局、命中测试、输入派发、状态管理和视觉绘制由 MaximumUI 自己控制。

核心风格：

```csharp
return new Window("Material Tool", width: 460f, height: 360f, key: "material-tool")
{
    new VStack(gap: 8f)
    {
        new Label("Material Tool"),

        new Toggle("Live preview", State.LivePreview,
            v => SetState(s => s.LivePreview = v)),

        new HStack(gap: 6f)
        {
            new Label("Intensity").Width(80f),

            new Slider(State.Intensity, 0f, 1f,
                v => SetState(s => s.Intensity = v))
                .ExpandWidth()
        },

        new Separator(),

        new Button("Apply", Apply)
    }
};
```

MaximumUI 的核心定位：

```text
Flutter-like C# object tree
+ React-like state-driven rendering
+ Dear ImGui-inspired tool widget richness
+ MaximumUI-owned layout / input / painter
+ Unity OnGUI low-level backend
+ net35-compatible public API
+ Koikatsu plugin-oriented runtime behavior
```

---

## 2. 设计目标

MaximumUI 需要满足以下目标：

```text
1. 支持 C# 内嵌声明式 UI。
2. 支持 collection initializer 表达 children。
3. 支持 named arguments / optional arguments 表达常用 props。
4. 支持 View / StatefulView 管理插件 UI 状态。
5. 支持 Window、Panel、Label、Button、Toggle、Slider、TextField、ComboBox、ScrollView、Dialog 等常用控件。
6. 以 Dear ImGui Explorer / `ShowDemoWindow()` 展示的 Dear ImGui core demo 为工具控件丰富度基准；目标控件全集一次性纳入实现范围，不把 ImPlot、Node Editor、Markdown、File Dialog 等 Dear ImGui Bundle / 第三方扩展作为 MaximumUI 核心目标。
7. 支持 keyed state store，保证窗口位置、滚动位置、下拉框展开状态、文本输入状态稳定。
8. 支持 Dialog、Dropdown、Tooltip、ContextMenu 等 overlay layer。
9. 支持自有布局引擎，提供 Width / Height / ExpandWidth / ExpandHeight 等布局提示，不依赖 GUILayout 决定布局行为。
10. 支持 Widget.Content(...) 等流式 API 表达单一子节点。
11. 支持 Widget 级 Enabled / Disabled 语义，使容器禁用能影响子树交互。
12. 支持 Theme token、文本 GUIStyle cache 和自绘控件 painter。
13. 支持保守的 IMGUI event consumption 和 soft input capture。
14. 支持脱离 Unity / Koikatsu 的无头核心测试。
15. 避免依赖 net35 不可用的 BCL API。
16. 避免在公开 API 中使用 modern C# / modern .NET 专用类型。
```

MaximumUI 不承担以下职责：

```text
1. 不负责替换 Koikatsu 原生 UI。
2. 不负责复杂动画系统。
3. 不负责高美术质量界面。
4. 不负责 uGUI prefab 管理。
5. 不负责真正的游戏输入拦截。
6. 不负责完整 CSS / Flexbox / selector 系统。
7. 不负责跨现代 Unity 版本抽象。
8. 不负责兼容既有插件作者的旧 UI API。
9. 不提供 `Gui`、`WindowHost`、旧 `ComboBox` 包装器等兼容层。
10. 不在核心库中放入插件业务控件，例如“命名输入对话框”；业务 UI 必须由通用 Widget 组合。
11. 不向插件层暴露直接 IMGUI 绘制逃逸口；直接 IMGUI 只属于 MaximumUI backend。
12. 不把 Dear ImGui 作为默认渲染后端，也不引入 native bridge、renderer hook 或 Dear ImGui .NET binding 作为核心依赖。
```

---

## 3. 目标框架与语言特性

MaximumUI 目标框架为 `net35`。这限制的是可用的 BCL API 和运行时能力，不等同于限制 C# 语法本身。

只要当前编译器支持，并且编译产物不依赖 net35 缺失的类型或运行时能力，就可以使用对应语法。

允许使用：

```text
named arguments
optional arguments
object initializer
collection initializer
lambda
anonymous delegate
extension methods
generic types
Nullable<T>
LINQ，但热路径慎用
```

建议避免：

```text
init
record
ValueTuple / tuple syntax
async / await
Task
dynamic
Span<T>
IReadOnlyList<T>
IReadOnlyDictionary<TKey,TValue>
nullable reference types
string.IsNullOrWhiteSpace
Enum.TryParse
modern pattern matching in public examples
source generator
```

公开 API 应保持 net35 友好。例如：

不使用：

```csharp
public IReadOnlyList<Widget> Children { get; }
public Action? OnClick { get; set; }
public float Width { get; init; }
```

使用：

```csharp
public IList<Widget> Children
{
    get { return _children; }
}

public Action OnClick;

public float Width;
```

`optional arguments` 的默认值会被调用方编译进插件程序集，因此公共 API 中的默认值应保持长期稳定。

### 3.1 代码风格约定

MaximumUI 的公开 API 应优先保持稳定、直观和 net35 友好；示例代码可以使用较新的 C# 语法，但不应依赖 net35 缺失的运行时类型。

推荐风格：

```text
1. 简单 callback 优先使用 lambda。
2. 多行状态更新可以使用 lambda block，也可以使用 anonymous delegate。
3. 示例代码不应为了压缩行数而写成难读的嵌套表达式。
4. 受控组件的 Value 必须来自 State，OnChange 必须更新同一个 State 字段或明确更新另一个有意义的字段。
5. TextField、ComboBox、ScrollView、Window、Dialog、动态列表项必须使用稳定 key。
6. State 类型可以使用 public field 作为内部 mutable state bag；是否改用属性由项目代码规范决定，不作为框架要求。
7. 公开 API 不使用 nullable reference type、record、init-only property、ValueTuple 等现代运行时或语言特性。
8. 示例中的 Enabled / Disabled 状态必须产生真实 UI 行为，不应只是无效果的展示字段。
9. 版本号常量应避免重复硬编码；插件版本、程序集文件版本和信息版本应按各自语义维护。
```

推荐写法：

```csharp
new Toggle("Enabled", State.Enabled, value =>
{
    SetState(s =>
    {
        s.Enabled = value;
    });
})
```

简单场景可以压缩为一行：

```csharp
new Toggle("Enabled", State.Enabled,
    value => SetState(s => s.Enabled = value))
```

需要兼容更旧编译器、或团队希望保持显式写法时，也可以使用 anonymous delegate：

```csharp
new Toggle("Enabled", State.Enabled, delegate(bool value)
{
    SetState(delegate(SettingsState s)
    {
        s.Enabled = value;
    });
})
```

不建议在示例中写成过度压缩的嵌套表达式：

```csharp
new Toggle("Enabled", State.Enabled, v => SetState(s => s.Enabled = v)).Width(120f)
```

该写法可用，但当回调逻辑变长时不利于阅读和断点调试。

受控组件示例：

```csharp
new ComboBox<string>(
    "Mode",
    State.Mode,
    State.Modes,
    x => x,
    value => SetState(s => s.Mode = value),
    key: "mode")
```

避免把当前值写成常量：

```csharp
new ComboBox<string>(
    "Mode",
    State.Modes[0],
    State.Modes,
    x => x,
    value => SetState(s => s.Mode = value),
    key: "mode")
```

上面的错误写法会导致用户选择后控件又回到第一个选项，违背 state-driven UI 的基本原则。

程序集版本建议：

```csharp
public const string Version = "0.1.0";
public const string FileVersion = "0.1.0.0";

[assembly: System.Reflection.AssemblyFileVersion(FileVersion)]
[assembly: System.Reflection.AssemblyInformationalVersion(Version)]
```

`AssemblyFileVersion` 应使用 Windows 文件版本可接受的数字格式；带 prerelease 标签的 SemVer 应放入 `AssemblyInformationalVersion`。


---

## 4. 编程模型

MaximumUI 使用三类核心对象：

```text
View
    挂载到 MaximumUIRuntime 的 UI 入口。
    可拥有生命周期。
    可持有状态。

Widget
    UI 描述节点。
    每次 Build 返回一棵 Widget 树。
    通过 collection initializer 表达 children。

StateStore
    保存 IMGUI 无法自然持久化的运行时状态。
    例如窗口位置、滚动位置、ComboBox 展开状态、TextField 输入缓冲。
```

典型结构：

```csharp
public sealed class SettingsState
{
    public bool Enabled = true;
    public float Opacity = 0.8f;
}

public sealed class SettingsView : StatefulView<SettingsState>
{
    public SettingsView()
        : base(new SettingsState())
    {
    }

    public override Widget Build(BuildContext context)
    {
        return new Window("Settings", width: 420f, height: 260f, key: "settings")
        {
            new VStack(gap: 8f)
            {
                new Toggle("Enabled", State.Enabled,
                    v => SetState(s => s.Enabled = v)),

                new Slider(State.Opacity, 0f, 1f,
                    v => SetState(s => s.Opacity = v)),

                new Button("Apply", Apply)
            }
        };
    }

    private void Apply()
    {
    }
}
```

MaximumUI 的 UI 更新原则：

```text
1. Build() 根据当前 State 返回 Widget 树。
2. 用户交互触发 callback。
3. callback 调用 SetState。
4. SetState 修改 State 并标记 View dirty。
5. OnGUI 重新 Build 并绘制。
6. 内部 IMGUI 状态由 StateStore 根据 key / path 保持。
```

---

## 5. Runtime 架构

整体模块：

```text
MaximumUI
├── MaximumUIRuntime
├── MaximumUIHost : MonoBehaviour
├── Mount
├── MountOptions
├── BuildContext
├── View / StatefulView<TState>
├── Widget / Fragment / EmptyWidget
├── UiStateStore
├── LayoutEngine
├── InputDispatcher
├── DrawList
├── UnityOnGuiBackend
├── UnityPainter
├── RenderContext
├── OverlayLayer
├── DialogService
├── Theme
└── Widgets
    ├── Window
    ├── Panel
    ├── Label
    ├── Button
    ├── Toggle
    ├── Slider
    ├── TextField
    ├── ComboBox<T>
    ├── Separator
    ├── Space
    ├── Spacer
    ├── VStack
    ├── HStack
    ├── ScrollView
    ├── Foldout
    ├── Tabs
    ├── Tab
    ├── Dialog
    └── HelpBox
```

BepInEx 插件挂载示例：

```csharp
[BepInPlugin("com.example.maximumui.demo", "MaximumUI Demo", "1.0.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        MaximumUIRuntime.EnsureHost();

        MaximumUIRuntime.Mount(
            "demo.settings",
            new SettingsView(),
            new MountOptions
            {
                Visible = true,
                ToggleKey = KeyCode.F8,
                HasToggleKey = true
            });
    }

    private void OnDestroy()
    {
        MaximumUIRuntime.Unmount("demo.settings");
    }
}
```

Host：

```csharp
public sealed class MaximumUIHost : MonoBehaviour
{
    private void Update()
    {
        MaximumUIRuntime.Update();
    }

    private void OnGUI()
    {
        MaximumUIRuntime.OnGUI();
    }
}
```

Runtime 主要职责：

```text
1. 创建并维护 DontDestroyOnLoad 的 MaximumUIHost。
2. 管理 mount / unmount。
3. 管理快捷键显示隐藏。
4. 调用 View.Update。
5. 调用 View.Build。
6. 调用布局、输入派发和绘制后端。
7. 维护 callback queue。
8. 维护 overlay layer。
9. 维护 dialog layer。
10. 维护 soft input capture 状态。
```

---

## 6. MountOptions

```csharp
public sealed class MountOptions
{
    public bool Visible;

    public KeyCode ToggleKey;
    public bool HasToggleKey;

    public bool BuildEveryOnGUI;

    public bool SoftMouseCapture;
    public bool SoftKeyboardCapture;

    public int SortOrder;

    public Theme Theme;

    public MountOptions()
    {
        Visible = true;
        HasToggleKey = false;
        BuildEveryOnGUI = true;
        SoftMouseCapture = true;
        SoftKeyboardCapture = false;
        SortOrder = 0;
        Theme = null;
    }
}
```

字段含义：

```text
Visible:
    当前 mount 是否可见。

ToggleKey / HasToggleKey:
    可选显示隐藏快捷键。

BuildEveryOnGUI:
    是否每次 OnGUI 都重新 Build。
    IMGUI 环境中保持 true 可以让外部状态变化自然反映到 UI。

SoftMouseCapture:
    鼠标位于 MaximumUI 窗口或 overlay 上时，MaximumUI 消费当前 IMGUI mouse event。

SoftKeyboardCapture:
    MaximumUI 在 TextField 聚焦或 Dialog 打开时消费部分 IMGUI keyboard event。

SortOrder:
    多个 mount 的绘制顺序。

Theme:
    当前 mount 使用的主题；为空时使用全局默认主题。
```

---

## 7. View 与状态

### 7.1 View

```csharp
public abstract class View
{
    public virtual void OnMount(BuildContext context)
    {
    }

    public virtual void OnUnmount(BuildContext context)
    {
    }

    public virtual void Update(BuildContext context)
    {
    }

    public abstract Widget Build(BuildContext context);
}
```

### 7.2 StatefulView

```csharp
public abstract class StatefulView<TState> : View
{
    protected TState State;

    protected StatefulView(TState initialState)
    {
        State = initialState;
    }

    protected void SetState(Action<TState> update)
    {
        if (update != null)
            update(State);

        MaximumUIRuntime.MarkDirty(this);
    }

    protected void ReplaceState(TState state)
    {
        State = state;
        MaximumUIRuntime.MarkDirty(this);
    }
}
```

使用约定：

```text
1. View 是 mount root。
2. View 可以持有插件 UI 状态。
3. Widget 是 UI 描述，不应长期保存并在外部修改。
4. 业务状态变化通过 SetState 或外部 MarkDirty 反映到 UI。
5. Build 中应避免扫描场景、读写文件、反射遍历等重操作。
```

---

## 8. BuildContext

```csharp
public sealed class BuildContext
{
    public string MountId;
    public Theme Theme;
    public DialogService Dialogs;
    public UiStateStore StateStore;
    public UiQueryStore Query;
    public UiNavigationStore Navigation;
    public UiShortcutRouter Shortcuts;
    public bool IsModalOpen;
    public bool IsMouseOverMaximumUI;
}
```

用途：

```text
MountId:
    当前 mount 标识。

Theme:
    当前主题。

Dialogs:
    Dialog 服务。

StateStore:
    当前 mount 的内部状态存储。

Query:
    当前 mount 的 item / window / input capture 查询快照。
    Build 中读取上一帧渲染结果。

Navigation:
    当前 mount 的键盘导航与焦点顺序状态。

Shortcuts:
    当前 mount 的快捷键路由器。
    View 可在 Build 中注册快捷键，runtime 在 KeyDown 事件中统一路由。

IsModalOpen:
    是否存在 modal dialog。

IsMouseOverMaximumUI:
    鼠标是否位于 MaximumUI 控件区域。
```

---

## 9. Widget 基类

```csharp
using System.Collections;
using System.Collections.Generic;

public sealed class LayoutHints
{
    public bool HasWidth;
    public float Width;

    public bool HasHeight;
    public float Height;

    public bool HasMinWidth;
    public float MinWidth;

    public bool HasMaxWidth;
    public float MaxWidth;

    public bool HasMinHeight;
    public float MinHeight;

    public bool HasMaxHeight;
    public float MaxHeight;

    public bool ExpandWidth;
    public bool ExpandHeight;
}

public abstract class Widget : IEnumerable
{
    private readonly List<Widget> _children = new List<Widget>();

    public object Key;
    public bool Enabled;
    public bool Focusable;
    public bool HasDefaultFocus;
    public bool RequestFocus;
    public readonly LayoutHints Layout = new LayoutHints();
    public string TooltipText;
    public Widget ContextMenuContent;

    public IList<Widget> Children
    {
        get { return _children; }
    }

    protected Widget()
        : this(null)
    {
    }

    protected Widget(object key)
    {
        Key = key;
        Enabled = true;
    }

    public void Add(Widget child)
    {
        if (child == null)
            return;

        if (child is EmptyWidget)
            return;

        Fragment fragment = child as Fragment;
        if (fragment != null)
        {
            for (int i = 0; i < fragment.Children.Count; i++)
                Add(fragment.Children[i]);

            return;
        }

        _children.Add(child);
    }

    public IEnumerator GetEnumerator()
    {
        return _children.GetEnumerator();
    }
}
```

说明：

```text
1. collection initializer 依赖 Add(Widget)。
2. Widget 同时允许作为 container 和 leaf。
3. 不引入 IReadOnlyList<T>。
4. LayoutHints 用于 Width / Height / MinWidth / MaxWidth / MinHeight / MaxHeight / ExpandWidth / ExpandHeight 等自有布局提示。
5. Enabled 是子树级启用状态；Renderer 渲染子节点时应与父级 Enabled 状态组合。
6. Focusable / HasDefaultFocus / RequestFocus 描述键盘导航语义；实际焦点顺序由 mount 级 UiNavigationStore 维护。
7. TooltipText / ContextMenuContent 是通用 overlay 入口，不属于具体控件兼容层。
8. Widget 不保存 GUILayoutOption，布局结果完全由 MaximumUI LayoutEngine 计算。
```

---

## 10. Fragment 与 EmptyWidget

```csharp
public sealed class EmptyWidget : Widget
{
    public static readonly EmptyWidget Instance = new EmptyWidget();

    private EmptyWidget()
    {
    }
}

public sealed class Fragment : Widget
{
    public Fragment(object key = null)
        : base(key)
    {
    }
}
```

辅助 API：

```csharp
public static class MUI
{
    public static readonly Widget Empty = EmptyWidget.Instance;

    public static Widget If(bool condition, Widget widget)
    {
        return condition ? widget : Empty;
    }

    public static Widget If(bool condition, Func<Widget> build)
    {
        if (!condition || build == null)
            return Empty;

        return build();
    }

    public static Widget If(
        bool condition,
        Func<Widget> thenBuild,
        Func<Widget> elseBuild)
    {
        if (condition)
            return thenBuild != null ? thenBuild() : Empty;

        return elseBuild != null ? elseBuild() : Empty;
    }

    public static Widget ForEach<T>(
        IEnumerable<T> items,
        Func<T, Widget> build)
    {
        Fragment fragment = new Fragment();

        if (items == null || build == null)
            return fragment;

        foreach (T item in items)
            fragment.Add(build(item));

        return fragment;
    }

    public static Widget ForEach<T>(
        IEnumerable<T> items,
        Func<T, int, Widget> build)
    {
        Fragment fragment = new Fragment();

        if (items == null || build == null)
            return fragment;

        int index = 0;

        foreach (T item in items)
        {
            fragment.Add(build(item, index));
            index++;
        }

        return fragment;
    }
}
```

示例：

```csharp
new VStack
{
    new Label("Basic"),

    MUI.If(State.Advanced, () =>
        new Label("Advanced mode enabled")),

    MUI.ForEach(State.Items, item =>
        new Button(item.Name, () => Select(item), key: item.Id))
}
```

---

## 11. Key 与 StateStore

Unity IMGUI 自身不会保存复杂控件状态。MaximumUI 使用 StateStore 保存内部状态。

需要保存的状态包括：

```text
Window rect
ScrollView position
ComboBox open state
ComboBox scroll position
TextField edit buffer
Foldout uncontrolled state
Tabs uncontrolled state
Dialog stack
Overlay focus state
```

StateStore：

```csharp
public sealed class UiStateStore
{
    private readonly Dictionary<string, object> _states =
        new Dictionary<string, object>();

    public T GetOrCreate<T>(string id)
        where T : new()
    {
        object value;

        if (_states.TryGetValue(id, out value))
            return (T)value;

        T created = new T();
        _states[id] = created;
        return created;
    }

    public void Remove(string id)
    {
        _states.Remove(id);
    }

    public void Clear()
    {
        _states.Clear();
    }
}
```

State id 生成规则：

```text
mount id + render path + widget type + explicit key
```

Key 使用规则：

```text
1. 动态列表项必须提供稳定 key。
2. Window 应提供 key。
3. ScrollView 应提供 key。
4. ComboBox 应提供 key。
5. TextField 应提供 key。
6. 静态 UI 可省略 key，MaximumUI 将按 child index path 生成内部 id。
```

示例：

```csharp
new Window("Material Tool", key: "material-tool")
{
    new ScrollView(key: "material-list")
    {
        MUI.ForEach(State.Materials, material =>
            new HStack(key: material.Name)
            {
                new Label(material.Name).ExpandWidth(),
                new Button("Select", () => Select(material))
            })
    }
}
```

---

## 12. 默认后端、布局引擎与 DrawList

MaximumUI 的默认后端使用 Unity `OnGUI` 作为平台入口，但不应长期依赖 `GUILayout.Button`、`GUILayout.Toggle`、`GUILayout.Window` 等高级 IMGUI 控件来定义控件行为。Unity 在默认后端中主要承担以下职责：

```text
1. 提供 OnGUI 调用入口。
2. 提供 Event.current 输入事件来源。
3. 提供 GUIUtility.hotControl / keyboardControl 等 IMGUI 控制状态。
4. 提供 GUIStyle 文本测量与文本绘制能力。
5. 提供 GUI.DrawTexture、GUIStyle.Draw、GUI.Label、GUI.BeginClip 等低级绘制能力。
```

MaximumUI 自己负责：

```text
1. Widget tree 遍历。
2. key / path 解析。
3. Measure / Arrange 布局。
4. hit test。
5. hover / active / focus 状态机。
6. mouse / keyboard / scroll 输入派发。
7. Window / Dialog / Dropdown / Tooltip / ContextMenu overlay 顺序。
8. ScrollView clip 和滚动条行为。
9. Button / Toggle / Slider / ComboBox / Tabs 等控件视觉状态绘制。
10. DrawList 生成。
```

渲染主流程：

```text
1. Begin mount frame。
2. View.Build 生成 Widget tree。
3. 解析 key path，并从 StateStore 取得内部状态。
4. LayoutEngine 执行 Measure。
5. LayoutEngine 执行 Arrange，生成布局树。
6. InputDispatcher 根据当前 MuiInputEvent 和布局树派发输入。
7. 控件根据状态生成 DrawList。
8. OverlayLayer 追加 dropdown、tooltip、context menu、dialog 等 DrawCommand。
9. UnityPainter 将 DrawList 翻译为 Unity 低级绘制调用。
10. 计算 soft capture 状态。
11. End mount frame。
```

基础几何与输入类型应尽量放在 MaximumUI.Core 中，不直接依赖 UnityEngine：

```csharp
public struct MuiVec2
{
    public float X;
    public float Y;
}

public struct MuiRect
{
    public float X;
    public float Y;
    public float Width;
    public float Height;

    public bool Contains(MuiVec2 point)
    {
        return point.X >= X && point.X < X + Width &&
               point.Y >= Y && point.Y < Y + Height;
    }
}

public struct MuiColor
{
    public float R;
    public float G;
    public float B;
    public float A;
}

public enum MuiEventType
{
    None,
    MouseMove,
    MouseDown,
    MouseUp,
    MouseDrag,
    ScrollWheel,
    KeyDown,
    KeyUp,
    TextInput,
    Repaint
}

public sealed class MuiInputEvent
{
    public MuiEventType Type;
    public MuiVec2 MousePosition;
    public int MouseButton;
    public int KeyCode;
    public char Character;
    public float ScrollDeltaY;
}
```

DrawList 是控件自绘和无头测试的核心中间层：

```csharp
public enum DrawCommandType
{
    PushClip,
    PopClip,
    FillRect,
    StrokeRect,
    Line,
    Polyline,
    FillTriangle,
    StrokeTriangle,
    FillCircle,
    StrokeCircle,
    Bezier,
    Text,
    Image
}

public sealed class DrawCommand
{
    public DrawCommandType Type;
    public MuiRect Rect;
    public MuiColor Color;
    public MuiVec2 P1;
    public MuiVec2 P2;
    public MuiVec2 P3;
    public MuiVec2 P4;
    public MuiVec2[] Points;
    public string Text;
    public object Resource;
    public float Thickness;
    public float Radius;
    public int Segments;
}

public sealed class DrawList
{
    private readonly List<DrawCommand> _commands = new List<DrawCommand>();

    public IList<DrawCommand> Commands
    {
        get { return _commands; }
    }

    public void PushClip(MuiRect rect);
    public void PopClip();
    public void FillRect(MuiRect rect, MuiColor color);
    public void StrokeRect(MuiRect rect, MuiColor color, float thickness);
    public void Line(MuiVec2 start, MuiVec2 end, MuiColor color, float thickness);
    public void Polyline(IList<MuiVec2> points, MuiColor color, float thickness);
    public void Polyline(IList<MuiVec2> points, MuiColor color, float thickness, bool closed);
    public void FillTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color);
    public void StrokeTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color, float thickness);
    public void FillCircle(MuiVec2 center, float radius, MuiColor color);
    public void StrokeCircle(MuiVec2 center, float radius, MuiColor color, float thickness, int segments);
    public void Bezier(MuiVec2 p1, MuiVec2 p2, MuiVec2 p3, MuiVec2 p4, MuiColor color, float thickness, int segments);
    public void Text(MuiRect rect, string text, object fontOrStyle, MuiColor color);
    public void Image(MuiRect rect, object texture, MuiColor color);
    public void Append(DrawList source);
    public void Append(DrawList source, MuiVec2 offset);
}
```

UnityPainter 只负责执行 DrawList，不负责决定控件行为：

```text
FillRect      -> GUI.DrawTexture 或 GUIStyle.Draw
StrokeRect    -> 细矩形贴图绘制四边
Line/Polyline -> 旋转 1x1 texture 或等效线段绘制
Triangle      -> scanline fill 或线段描边
Circle        -> polyline 描边或 scanline fill
Bezier        -> 分段 polyline
Text          -> GUI.Label / GUIStyle.Draw
Image         -> GUI.DrawTexture
PushClip      -> GUI.BeginClip 或等效裁剪实现；DrawList 命令保持绝对坐标，由 painter 处理 clip origin
PopClip       -> GUI.EndClip
```

TextField 是特殊控件。为了保证中文 / 日文输入法、复制粘贴、caret、selection 等基础体验，TextField 允许使用 Unity `GUI.TextField` 或 Unity `TextEditor` 作为兼容后端策略。除 TextField 以外，核心控件由 MaximumUI 自有 LayoutEngine / InputDispatcher / Painter 实现，不依赖 Unity 高级 IMGUI 控件定义行为。

---

## 13. Callback Queue

IMGUI 的 `OnGUI` 可能在一帧内被多次调用，事件类型包括 Layout、Repaint、MouseDown、MouseUp、KeyDown 等。控件事件中立即修改 UI 树可能导致 Layout / Repaint 调用序列不一致。

MaximumUI 采用 callback queue：

```csharp
private static readonly List<Action> _pendingCallbacks =
    new List<Action>();

internal static void EnqueueCallback(Action callback)
{
    if (callback != null)
        _pendingCallbacks.Add(callback);
}

private static void FlushCallbacks()
{
    if (_pendingCallbacks.Count == 0)
        return;

    Action[] callbacks = _pendingCallbacks.ToArray();
    _pendingCallbacks.Clear();

    for (int i = 0; i < callbacks.Length; i++)
    {
        try
        {
            callbacks[i]();
        }
        catch (Exception ex)
        {
            LogException(ex);
        }
    }
}
```

执行规则：

```text
1. Button、Toggle、Slider、ComboBox 等控件回调进入队列。
2. 队列在 Runtime.Update 中执行。
3. TextField 可使用内部 edit buffer 保证输入手感。
4. DialogService 的 Show / Close 也进入队列或在 Runtime 安全阶段执行。
```

---

## 14. 输入与 Soft Capture

MaximumUI 的输入处理基于 Unity IMGUI event consumption。它可以消费 `Event.current`，但不能保证阻止 Koikatsu 或其他插件在 `Update()` 中直接读取 `Input.GetKey` / `Input.GetMouseButton`。

因此 MaximumUI 提供的是 soft capture：

```csharp
public static bool IsMouseOverAnyWindow;
public static bool IsAnyTextFieldFocused;
public static bool IsModalOpen;
```

Soft capture 行为：

```text
1. 鼠标位于 MaximumUI Window / Dialog / Dropdown 上时，可以 Event.current.Use() 当前 mouse event。
2. Dialog 打开时，MaximumUI 自身下层控件不响应输入。
3. TextField 聚焦时，MaximumUI 可消费部分 keyboard event。
4. Escape 可关闭顶层 Dialog 或 Dropdown。
5. 点击 Dropdown 外部关闭 Dropdown。
```

不应承诺：

```text
1. 完全阻止 Studio 相机移动。
2. 完全阻止 Koikatsu 快捷键。
3. 完全阻止其他插件热键。
4. 完全阻止游戏 Update 读取输入。
```

需要完整输入拦截时，应单独设计 `MaximumUI.KoikatsuInput`，通过 Harmony patch 特定 Koikatsu 输入路径实现，不放在核心 IMGUI runtime 中。

---

## 15. Theme、设计 Token 与文本样式

MaximumUI 的自绘控件不应只依赖 Unity 默认 skin。Theme 应以设计 token 为主，`GUIStyle` 主要用于文本测量和文本绘制。

Theme：

```csharp
public sealed class Theme
{
    public MuiColor WindowBg;
    public MuiColor PanelBg;
    public MuiColor DialogBg;
    public MuiColor DropdownBg;
    public MuiColor Backdrop;

    public MuiColor Text;
    public MuiColor TextDisabled;
    public MuiColor Border;
    public MuiColor Separator;
    public MuiColor Accent;

    public MuiColor ButtonBg;
    public MuiColor ButtonHoverBg;
    public MuiColor ButtonActiveBg;
    public MuiColor ButtonDisabledBg;

    public MuiColor FieldBg;
    public MuiColor FieldHoverBg;
    public MuiColor FieldActiveBg;

    public MuiColor SelectedBg;
    public MuiColor WarningBg;
    public MuiColor ErrorBg;

    public float BorderWidth;
    public float Radius;
    public float FontSize;

    public RectOffset WindowContentPadding;
    public RectOffset PanelContentPadding;
    public RectOffset DialogContentPadding;
    public RectOffset DropdownContentPadding;
    public RectOffset ButtonPadding;
    public RectOffset FieldPadding;

    public GUIStyle LabelText;
    public GUIStyle ButtonText;
    public GUIStyle FieldText;
    public GUIStyle HelpBoxText;
    public GUIStyle TabText;
}
```

ThemeManager 负责：

```text
1. 初始化默认 Theme。
2. 基于 GUI.skin 创建文本 GUIStyle 副本。
3. 缓存常用文本 GUIStyle 和 1x1 绘制纹理。
4. 支持每个 mount 覆盖 Theme。
5. 支持 Widget 局部覆盖文本 GUIStyle 或状态颜色。
6. Toggle / Slider / Button / Tabs / ComboBox 必须使用 MaximumUI painter 显式绘制所有状态，不依赖 Unity 默认皮肤是否提供可见纹理。
7. Window / Dialog / Dropdown 的内容 inset 来自 Theme，不要求插件手动留边。
8. Theme token 应足够表达 normal / hover / active / selected / disabled / warning / error 等工具 UI 状态。
```

Widget 局部 style 示例：

```csharp
new Label("Warning")
{
    Style = MyStyles.WarningLabel
}
```

布局相关不放进 Theme，而使用 `LayoutHints`：

```csharp
new Label("Name").Width(80f)
new Slider(value, min, max, onChange).ExpandWidth()
```

---

## 16. Layout 扩展方法

```csharp
public static class WidgetExtensions
{
    public static T Width<T>(this T widget, float width)
        where T : Widget
    {
        widget.Layout.HasWidth = true;
        widget.Layout.Width = width;
        return widget;
    }

    public static T Height<T>(this T widget, float height)
        where T : Widget
    {
        widget.Layout.HasHeight = true;
        widget.Layout.Height = height;
        return widget;
    }

    public static T MinWidth<T>(this T widget, float width)
        where T : Widget
    {
        widget.Layout.HasMinWidth = true;
        widget.Layout.MinWidth = width;
        return widget;
    }

    public static T MaxWidth<T>(this T widget, float width)
        where T : Widget
    {
        widget.Layout.HasMaxWidth = true;
        widget.Layout.MaxWidth = width;
        return widget;
    }

    public static T MinHeight<T>(this T widget, float height)
        where T : Widget
    {
        widget.Layout.HasMinHeight = true;
        widget.Layout.MinHeight = height;
        return widget;
    }

    public static T MaxHeight<T>(this T widget, float height)
        where T : Widget
    {
        widget.Layout.HasMaxHeight = true;
        widget.Layout.MaxHeight = height;
        return widget;
    }

    public static T ExpandWidth<T>(this T widget)
        where T : Widget
    {
        widget.Layout.ExpandWidth = true;
        return widget;
    }

    public static T ExpandHeight<T>(this T widget)
        where T : Widget
    {
        widget.Layout.ExpandHeight = true;
        return widget;
    }

    public static T Enabled<T>(this T widget, bool enabled)
        where T : Widget
    {
        widget.Enabled = enabled;
        return widget;
    }

    public static T Content<T>(this T widget, Widget child)
        where T : Widget
    {
        widget.Add(child);
        return widget;
    }

    public static T Disabled<T>(this T widget, bool disabled)
        where T : Widget
    {
        widget.Enabled = !disabled;
        return widget;
    }

    public static T EnabledWhen<T>(this T widget, bool enabled)
        where T : Widget
    {
        widget.Enabled = enabled;
        return widget;
    }

    public static T Tooltip<T>(this T widget, string text)
        where T : Widget
    {
        widget.TooltipText = text ?? string.Empty;
        return widget;
    }

    public static T ContextMenu<T>(this T widget, Widget content)
        where T : Widget
    {
        widget.ContextMenuContent = content;
        return widget;
    }

    public static T Focusable<T>(this T widget, bool focusable)
        where T : Widget
    {
        widget.Focusable = focusable;
        return widget;
    }

    public static T DefaultFocus<T>(this T widget)
        where T : Widget
    {
        widget.HasDefaultFocus = true;
        return widget;
    }

    public static T FocusHere<T>(this T widget)
        where T : Widget
    {
        widget.RequestFocus = true;
        return widget;
    }
}
```

使用：

```csharp
new HStack(gap: 6f)
{
    new Label("Opacity").Width(80f),

    new Slider(State.Opacity, 0f, 1f,
        v => SetState(s => s.Opacity = v))
        .ExpandWidth(),

    new Label(State.Opacity.ToString("0.00")).Width(48f)
}
```

性能注意：

```text
Width / Height / ExpandWidth / ExpandHeight 只修改 Widget.Layout，不创建 GUILayoutOption。
布局性能主要取决于 Widget 数量、Measure / Arrange 次数、文本测量次数和动态列表规模。
大规模动态列表中应缓存过滤结果、分页或使用 VirtualList / ListClipper 控件。
```

---

# 17. 常用组件 API

所有 Widget 继承 `Enabled` 字段。Renderer 应将父子 Enabled 状态组合；因此禁用 Panel、VStack、HStack、ScrollView 等容器时，其子树中的交互控件也应不可操作。单个 Button、Toggle、Slider、TextField、ComboBox 也可以直接设置 `Enabled = false`。

控件丰富度以 Dear ImGui Explorer / `ShowDemoWindow()` 中的 Dear ImGui core demo 为功能基准。MaximumUI 不引入 Dear ImGui native backend，也不复制 Dear ImGui 的 C++ immediate-mode API；它提供 C# 声明式 Widget API，但核心控件、布局、窗口、表格、输入、弹窗、调试和示例应用能力必须覆盖以下目标全集。

不纳入核心目标的内容：ImPlot / ImPlot3D、Node Editor、Markdown、File Dialog、Knob、第三方 gizmo、第三方文件浏览器等 Dear ImGui Bundle 或第三方扩展。MaximumUI 可以在扩展包中另行提供这些能力，但核心库不以它们作为验收范围。

## 17.0 Dear ImGui core feature 目标全集

### 17.0.1 窗口、应用结构与窗口标志

```text
Window:
    普通窗口、可关闭窗口、可折叠窗口、可拖动窗口、可调整尺寸窗口、自动尺寸窗口、受约束尺寸窗口、全屏窗口、简单覆盖层窗口、无标题栏窗口、无背景窗口、无滚动条窗口、不可移动窗口、不可调整尺寸窗口、不可折叠窗口、不可获得导航焦点窗口、不可 bring-to-front 窗口、带未保存状态标记的窗口。

Window position / size:
    首次显示位置、首次显示尺寸、下一帧位置、下一帧尺寸、最小/最大尺寸约束、宽高比约束、固定步进约束、窗口 padding 控制。

Document UI:
    文档标签、可关闭文档、未保存文档标记、文档列表、文档切换、文档关闭确认、文档菜单。

Application examples:
    MainMenuBar、DebugConsole、DebugLog、SimpleLayout、PropertyEditor、LongTextDisplay、AutoResizeWindow、ConstrainedResizeWindow、SimpleOverlay、FullscreenWindow、WindowTitleManipulation、CustomRenderingDemo、DocumentsDemo、AssetsBrowserDemo。

Docking-like layout:
    DockSpace、DockPanel、DockNode、可拆分停靠区域、标签式停靠区域。该能力在 MaximumUI 自有窗口系统内部实现，不创建操作系统原生多窗口，也不依赖 Dear ImGui native docking backend。

Viewport-like behavior:
    Dear ImGui multi-viewport 的 OS 原生多窗口语义不作为 KK 插件目标；MaximumUI 对应能力是在 Unity game view 内部提供 work area、fullscreen window、overlay window 和内部 viewport clipping。
```

### 17.0.2 菜单系统

```text
Menu bar:
    MenuBar、MainMenuBar、WindowMenuBar、BeginMenuBar / EndMenuBar 语义、窗口内菜单栏。

Menu:
    Menu、nested menu、MenuItem、checkable menu item、disabled menu item、shortcut display、separator、separator text、menu section title。

Common menus:
    File menu、Edit menu、contextual tool menu、debug tools menu、examples menu。
```

### 17.0.3 文本、标签、链接和帮助

```text
Text:
    Text、TextUnformatted、TextColored、TextDisabled、TextWrapped、TextWrapped with explicit wrap width、LabelText、ValueLabel、PrefixValueLabel、MonospaceLabel、CodeLabel、EllipsisLabel。

Bullets:
    Bullet、BulletText、multi-line BulletText、bullet with following custom item。

Separators:
    Separator、SeparatorText、section separator、horizontal separator、menu separator。

Links and help:
    TextLink、UrlLink、HelpMarker、InlineHelp、ItemHelp、CopyableText、clipboard logging text。

Baseline / alignment:
    AlignTextToFramePadding、text baseline alignment with framed widgets、multi-line text alignment。
```

### 17.0.4 基础交互控件

```text
Buttons:
    Button、SmallButton、ColoredButton、RepeatButton、ArrowButton、InvisibleButton、ImageButton、TexturedButton。

Boolean controls:
    Toggle、Checkbox、CheckboxFlags、RadioButton、SegmentedControl。

Status controls:
    ProgressBar、indeterminate progress bar、StatusBadge、LoadingIndicator。

Disabled controls:
    BeginDisabled / EndDisabled 语义、容器禁用、单项禁用、禁用项 hover 查询、禁用项 tooltip。

Hyperlink-like controls:
    TextLink、TextLinkOpenURL、small help link。
```

### 17.0.5 文本输入

```text
Text fields:
    TextField、InputText、InputTextWithHint、InputTextMultiline、TextArea、PasswordField、SearchBox、FilterBox。

Text input behavior:
    placeholder、max length、read only、password masking、auto focus、default focus、submit、blur、caret、selection、copy、paste、cut、select all、word jump、double click select、escape revert、history callback、completion callback、edit callback、resize callback。

Text input flags:
    decimal only、hex only、scientific notation、uppercase、no blank、chars filter、callback always、callback char filter、enter returns true、escape clears all、read only、password、parse empty ref value、display empty ref value。

Compatibility:
    TextField 可以使用 Unity GUI.TextField / TextEditor 作为兼容后端策略；MaximumUI 仍负责外层 key、focus、submit、blur、layout、enabled、tooltip 和 validation 语义。
```

当前实现先提供统一的 `TextInputFlags` 和 `TextInputFilter`，供 TextField、TextArea、PasswordField、SearchBox 共享 read-only、字符过滤、提交、失焦和 Escape 行为。

### 17.0.6 数值输入、Scalar、Drag 和 Slider

```text
Scalar input:
    InputInt、InputInt2、InputInt3、InputInt4、InputFloat、InputFloat2、InputFloat3、InputFloat4、InputDouble、InputScalar、InputScalarN。

Scalar data types:
    signed / unsigned 8-bit、16-bit、32-bit、64-bit、float、double、hex display、decimal display、scientific notation、format string。

Drag controls:
    DragInt、DragInt2、DragInt3、DragInt4、DragIntRange2、DragFloat、DragFloat2、DragFloat3、DragFloat4、DragFloatRange2、DragScalar、DragScalarN。

Slider controls:
    SliderInt、SliderInt2、SliderInt3、SliderInt4、SliderFloat、SliderFloat2、SliderFloat3、SliderFloat4、SliderAngle、SliderScalar、SliderScalarN。

Vertical sliders:
    VSliderInt、VSliderFloat、VSliderScalar。

Drag / Slider behavior:
    drag speed、min/max clamp、always clamp、clamp on text input、zero-range clamp、logarithmic scale、no round to format、no direct text input、modifier-based speed tweak、wrap around、format suffix/prefix、Ctrl/click 转文本输入、deactivate-after-edit commit。

Vector fields:
    Vector2Field、Vector3Field、Vector4Field、Int2Field、Int3Field、Int4Field、RectField。
```

当前实现先提供第一批受控数值控件。Input 控件提供格式化显示和 +/- step；Drag 控件通过横向拖动增减；SliderInt / SliderFloat / SliderAngle 复用 MaximumUI slider 命中测试和绘制。

```csharp
public sealed class InputInt : Widget;
public sealed class InputFloat : Widget;
public sealed class InputDouble : Widget;
public sealed class DragInt : Widget;
public sealed class DragFloat : Widget;
public sealed class SliderInt : Widget;
public sealed class SliderFloat : Widget;
public sealed class SliderAngle : Widget;
```

公共字段语义：

```text
Label:
    可选左侧标签。

Value:
    当前受控值。

Step / StepFast:
    InputInt / InputFloat / InputDouble 的 +/- 步进。
    Shift 点击使用 StepFast。

Speed:
    DragInt / DragFloat 的横向拖动速度。
    Shift 加速，Alt 减速。

Min / Max + HasMin / HasMax:
    可选 clamp 范围。

Format:
    显示格式。int 默认 "{0}"，float / double 默认 "0.###"。

OnChange:
    用户操作后进入 callback queue；调用方负责更新受控 state。
```

使用示例：

```csharp
new HStack(gap: 6f)
{
    new InputInt("Count", State.Count,
        value => SetState(s => s.Count = value))
    {
        HasMin = true,
        Min = 0,
        HasMax = true,
        Max = 99
    }.ExpandWidth(),

    new DragFloat("Speed", State.Speed,
        value => SetState(s => s.Speed = value))
    {
        Speed = 0.01f,
        HasMin = true,
        Min = 0f,
        HasMax = true,
        Max = 1f
    }.ExpandWidth()
}

new SliderAngle("Angle", State.Angle,
    value => SetState(s => s.Angle = value))
```

实现约束：

```text
1. 数值控件是受控组件，Value 必须来自调用方 state。
2. clamp 在 renderer callback 前执行；调用方仍可在业务层再次校正。
3. Input 控件当前提供 stepper 行为；后续 Ctrl/click 转文本输入应复用 TextInputFlags 和同一 edit buffer 语义。
4. Drag 控件使用 MouseDrag delta，不持久保存业务值。
5. SliderInt 对 float slider 结果 round 并 clamp。
6. SliderAngle 当前以 degree 为公开 Value，显示 "deg" 后缀。
7. 所有数值控件必须走 LayoutEngine / painter / renderer，不调用 GUILayout。
```

### 17.0.7 Color 与颜色选择器

```text
Color edit:
    ColorButton、ColorEdit3、ColorEdit4、ColorPicker3、ColorPicker4。

Color modes:
    RGB input、HSV input、HEX input、float display、uint8 display、HDR value、InputHSV、DisplayRGB、DisplayHSV、DisplayHex。

Alpha and preview:
    no alpha、alpha opaque、alpha no background、alpha preview、alpha preview half、alpha bar、reference color、side preview、small preview。

Picker variants:
    hue bar picker、hue wheel picker、custom picker popup、palette grid、current/previous color、drag/drop color payload、right-click options popup、default color edit options。
```

当前实现提供第一批受控颜色控件。ColorButton 负责通用颜色按钮点击语义；ColorEdit3 / ColorEdit4 提供单行 RGB / RGBA 通道编辑；ColorPicker3 / ColorPicker4 提供多行通道滑条式颜色选择；ColorEdit 作为通用紧凑颜色编辑器继续存在，但新代码应优先选择 3/4 通道语义明确的类型。

```csharp
public enum ColorDisplayMode
{
    Float,
    Byte,
    Hex
}

public sealed class ColorButton : Widget;
public sealed class ColorEdit3 : Widget;
public sealed class ColorEdit4 : Widget;
public sealed class ColorPicker3 : Widget;
public sealed class ColorPicker4 : Widget;
public sealed class ColorEdit : Widget;
```

公共字段语义：

```text
Label:
    可选左侧标签或按钮文本。

Value:
    当前受控颜色。ColorEdit3 / ColorPicker3 会固定 A=1。

DisplayMode:
    通道文本显示模式。Float 显示 0..1，Byte 显示 0..255，Hex 显示两位十六进制。

ShowPreview:
    ColorEdit3 / ColorEdit4 / ColorPicker3 / ColorPicker4 是否显示颜色预览色块。

ShowAlpha:
    ColorButton / ColorEdit 是否在文本摘要中包含 Alpha。

ShowCheckerboard:
    ColorButton 的透明背景棋盘预览。

OnChange:
    用户修改通道后进入 callback queue；调用方负责更新受控 state。
```

使用示例：

```csharp
new ColorEdit4("Tint", State.Tint,
    value => SetState(s => s.Tint = value))
{
    DisplayMode = ColorDisplayMode.Hex
}.ExpandWidth()

new ColorPicker4("Picker", State.Tint,
    value => SetState(s => s.Tint = value))
{
    DisplayMode = ColorDisplayMode.Float
}.Height(118f).ExpandWidth()
```

实现约束：

```text
1. 颜色控件是受控组件，Value 必须来自调用方 state。
2. RGB / A 通道写回前 clamp 到 0..1。
3. ColorEdit3 / ColorPicker3 不暴露 alpha 编辑，写回 A=1。
4. 当前 ColorPicker 使用 RGB / RGBA 组件滑条；HSV、HEX 直接输入、调色板、右键 options popup 和 drag/drop color payload 属于后续增强。
5. 所有颜色控件必须走 LayoutEngine / painter / renderer，不调用 GUILayout。
```

### 17.0.8 Combo、ListBox、Selectable 和选择状态

```text
Combo:
    ComboBox<T>、Combo one-liner、array combo、accessor function combo、BeginCombo-style CustomCombo、searchable combo、filtered combo、enum combo、flags combo、grouped combo。

Combo behavior:
    preview value、selected item default focus、popup align left、no arrow button、no preview、width fit preview、height small/regular/large/largest、keyboard navigation、scroll to selected item。

ListBox:
    ListBox<T>、BeginListBox-style CustomListBox、VirtualListBox、SearchableListBox。

Selectable:
    Selectable、SelectableRow、SelectableTreeRow、double-click selectable、disabled selectable、same-line selectable with extra buttons、selectable in table、span-all-columns selectable、grid selectable、alignment override、allow overlap。

Selection:
    SingleSelect、MultiSelect、RangeSelect、Ctrl/Shift selection、keyboard selection、box select 1D/2D、selection storage、selection request apply、delete selected items、dual list box、selection in clipped lists、selection in tables、selection in trees。
```

Selection API 使用受控状态模型。控件只负责报告用户意图，业务列表如何排序、过滤、删除和保存选择由插件 state 决定：

```csharp
public sealed class SelectionRequest
{
    public object Value;
    public bool Selected;
    public bool Additive;
    public bool Range;
    public bool DoubleClick;
}

public sealed class SelectionState<T>
{
    public IList<T> SelectedItems { get; }
    public T Anchor;
    public bool HasAnchor;

    public bool IsSelected(T item);
    public void Clear();
    public void SetSingle(T item);
    public void SetSelected(T item, bool selected);
    public void Toggle(T item);
    public void Apply(IList<T> orderedItems, T item, bool additive, bool range);
    public IList<T> Snapshot();
}
```

`Selectable` 必须支持将 `SelectionRequest` 上报给调用方：

```csharp
new Selectable(item.Name, State.Selection.IsSelected(item.Id), key: item.Id)
{
    Value = item.Id,
    OnSelect = request =>
    {
        SetState(s =>
        {
            s.Selection.Apply(s.VisibleIds, (int)request.Value,
                request.Additive, request.Range);
        });
    }
}
```

实现约束：

```text
1. `SelectionState<T>` 是通用状态容器，不依赖具体 Widget。
2. Ctrl / Command 产生 additive selection request。
3. Shift 产生 range selection request。
4. Range selection 使用稳定的 orderedItems 和 Anchor。
5. Selectable 仍是受控组件；Selected 必须来自调用方 state。
6. ListBox、Tree、Table 行等后续选择控件应复用 SelectionRequest / SelectionState，而不是各自实现独立选择规则。
```

当前实现新增第一批过滤式选择控件：

```csharp
public sealed class SearchableComboBox<T> : Widget;
public sealed class SearchableListBox<T> : Widget;
```

`SearchableComboBox<T>` 复用 ComboBox renderer 和 DropdownOverlay；展开后在 dropdown 顶部显示过滤输入，过滤文本保存在稳定 key 对应的 ComboBoxState 中。选择回调仍然返回原始 item，而不是过滤后的索引。

`SearchableListBox<T>` 在列表顶部显示过滤输入。`FilterText` 是受控文本；`OnFilterChange` 进入 callback queue，调用方负责写回 state。列表只渲染 `GetText(item)` 包含过滤文本的项。

使用示例：

```csharp
new SearchableComboBox<string>(
    "Preset",
    State.Preset,
    State.Presets,
    x => x,
    value => SetState(s => s.Preset = value),
    key: "preset-combo")
{
    FilterPlaceholder = "Filter presets"
}

new SearchableListBox<string>(
    State.SelectedItem,
    State.Items,
    x => x,
    value => SetState(s => s.SelectedItem = value),
    key: "item-list")
{
    FilterText = State.Filter,
    OnFilterChange = value => SetState(s => s.Filter = value)
}
```

实现约束：

```text
1. SearchableComboBox / SearchableListBox 的选择值仍然是受控 Value。
2. 搜索过滤只影响可见项，不改变调用方传入的 Items。
3. SearchableComboBox 的过滤文本属于控件内部 UI 状态，依赖稳定 key 保持。
4. SearchableListBox 的过滤文本由调用方 state 控制，便于和外部 SearchBox、快捷键或 preset 共享。
5. enum combo、flags combo、grouped combo、custom BeginCombo-style 内容仍属于后续增强。
```

### 17.0.9 Tree、Foldout 和层级数据

```text
Tree:
    TreeNode、TreeNodeEx、TreeLeaf、TreeRow、TreeTable、TreePush / TreePop equivalent、TreeNodeSetOpen / TreeNodeGetOpen equivalent。

Headers:
    Foldout、CollapsingHeader、ClosableCollapsingHeader。

Tree flags / behavior:
    default open、open on arrow、open on double click、leaf、bullet、framed node、selected node、span full width、span label width、span all columns、label span all columns、draw hierarchy lines、nested tree、filterable tree、virtualized tree、tree in table、tree multi-select。
```

当前实现提供第一批层级控件和 header 变体：

```csharp
public sealed class Foldout : Widget;
public sealed class CollapsingHeader : Widget;
public sealed class ClosableCollapsingHeader : Widget;
public sealed class TreeNode : Widget;
public sealed class TreeLeaf : Widget;
```

新增字段语义：

```text
Foldout.Framed / CollapsingHeader.Framed:
    是否绘制 framed header 背景和边框。

ClosableCollapsingHeader.Visible:
    false 时不渲染、不测量内容；关闭动作由调用方 state 控制。

TreeNode.OpenOnArrow:
    true 时仅箭头区域切换展开；label 区域只触发 OnSelect。

TreeNode.OpenOnDoubleClick:
    true 时双击标题区域也可切换展开。

TreeNode.Framed:
    绘制 framed tree row。

TreeNode.DrawLines:
    展开时绘制简单层级辅助线。

TreeNode.Bullet / TreeLeaf.Bullet:
    使用 bullet-like 前缀绘制 leaf。

TreeNode.Selected / TreeLeaf.Selected:
    由调用方 state 控制选中视觉状态。
```

使用示例：

```csharp
new CollapsingHeader("Advanced", State.AdvancedOpen,
    value => SetState(s => s.AdvancedOpen = value),
    key: "advanced")
{
    new Toggle("Enabled", State.Enabled,
        value => SetState(s => s.Enabled = value))
}

new TreeNode("Root", State.RootOpen,
    value => SetState(s => s.RootOpen = value),
    key: "root")
{
    OpenOnArrow = true,
    OpenOnDoubleClick = true,
    DrawLines = true
}
.Content(new TreeLeaf("Leaf", SelectLeaf)
{
    Selected = State.SelectedLeaf
})
```

实现约束：

```text
1. TreeNode / TreeLeaf 的 Open / Selected 均为受控状态。
2. TreeNode 只上报 OnOpenChange / OnSelect，不保存业务树数据。
3. ClosableCollapsingHeader 的关闭只调用 OnClose；调用方负责设置 Visible=false。
4. 当前实现覆盖 nested tree、framed/selected/leaf/bullet/open-on-arrow/open-on-double-click/simple hierarchy line。
5. TreeTable、tree multi-select、virtualized tree、span all columns、TreePush / TreePop equivalent 属于后续增强。
```

### 17.0.10 Tabs 和 TabBar

```text
Tabs:
    Tabs、TabBar、TabItem、Tab、ClosableTab、ReorderableTab、UnsavedTab、LeadingTabButton、TrailingTabButton、TabItemButton。

Tab behavior:
    selected tab、close request、unsaved marker、reorder by drag、tab list popup、fitting policy resize down、fitting policy scroll、no close with middle mouse button、no tooltip、no reorder、set selected、set visible、tab bar flags。
```

当前 Tabs 以受控 `SelectedIndex` 为核心；close / reorder 都只上报用户意图，调用方负责更新自己的 tab 列表、隐藏状态或顺序。

### 17.0.11 Tables 和 Legacy Columns

```text
Table core:
    Table、TableColumn、TableRow、TableCell、TableHeaderRow、TableHeaders、TableNextRow、TableNextColumn、TableSetColumnIndex。

Column sizing:
    fixed column、stretch column、mixed fixed/stretch columns、resizable column、auto-fit column、width fixed、width stretch、precise widths、no host extend X/Y、outer size、inner width。

Column features:
    reorderable column、hideable column、sortable column、multi-column sort、tri-state sort、default sort、column context menu、column header context menu、highlight hovered column、angled header、header labels。

Scrolling and freezing:
    horizontal scroll、vertical scroll、inner scroll、frozen rows、frozen columns、scrollbar handling、clipped rows、ListClipper integration。

Decorations:
    row background、alternating row background、cell background、column background、borders outer/inner H/V、no borders in body、padding outer/inner、cell padding、row min height。

Advanced contents:
    text cells、button cells、small button cells、fill button cells、selectable cells、selectable spanning row、tree view in table、nested table、custom headers、table item width。

Legacy Columns:
    ColumnsLegacy / old Columns API compatibility surface for simple legacy-style split columns; new code should prefer Table。
```

当前实现提供第一批 Table 能力：显式列定义、可见列过滤、固定/拉伸列宽、MinWidth / MaxWidth 约束、sortable header 回调、自动 header、显式 TableHeaderRow、交替行背景、行/单元格背景、基础边框控制。

新增字段语义：

```text
Table.ShowOuterBorder:
    是否绘制表格外边框。

Table.ShowInnerBorders:
    是否绘制基础内部分隔线。

Table.AlternatingRows:
    是否为奇数数据行绘制轻量交替背景。

Table.HeaderHeight:
    自动 header 高度；0 表示使用默认行高。

TableColumn.Visible:
    false 时该显式列不参与布局、header 和宽度计算；对应行单元格会保留路径但布局宽度为 0。

TableColumn.MinWidth / MaxWidth:
    对固定列和拉伸列结果统一生效。

TableRow.HasBackground / Background:
    行级背景色。

TableCell.HasBackground / Background / ShowBorder:
    单元格级背景与边框显示。
```

使用示例：

```csharp
Table table = new Table(3);
table.AlternatingRows = true;
table.Add(new TableColumn("Name")
{
    Width = 96f,
    Stretch = false,
    Sortable = true,
    SortDirection = State.Sort,
    OnSort = value => SetState(s => s.Sort = value)
});
table.Add(new TableColumn("Internal")
{
    Visible = false
});
table.Add(new TableColumn("Action")
{
    Width = 80f,
    MinWidth = 64f,
    MaxWidth = 120f,
    Stretch = false
});
table.Add(new TableRow
{
    new TableCell { new Label("Alpha") },
    new TableCell { new Label("Hidden metadata") },
    new TableCell { new Button("Run", Run) }
});
return table;
```

实现约束：

```text
1. TableColumn.Visible 只隐藏显式列；没有显式列时仍使用 Table.Columns 和行 cell 数推断列数。
2. 隐藏列对应的 TableCell 保留原始 path，便于 state/query 稳定，但 Rect.Width=0。
3. SortDirection 是受控状态；header 点击只通过 OnSort 上报下一方向。
4. 当前实现不提供列拖拽重排、运行时调整列宽、冻结行/列、列上下文菜单、multi-column sort、horizontal scroll。
5. Legacy Columns compatibility surface 仍属于后续增强；新代码继续使用 Table。
```

### 17.0.12 Layout、滚动、裁剪和虚拟化

```text
Basic layout:
    VStack、HStack、Inline、SameLine、Group、BeginGroup / EndGroup equivalent、Indent、Unindent、Space、Spacing、Dummy、NewLine、Separator、SeparatorText。

Size and item width:
    Width、Height、MinWidth、MinHeight、MaxWidth、MaxHeight、ExpandWidth、ExpandHeight、ItemWidth、PushItemWidth / PopItemWidth equivalent、SetNextItemWidth equivalent、ContentRegionAvailable、FitContent、AutoSize、Stretch、Fixed。

Child regions:
    ChildPanel、ChildWindow、framed child、bordered child、auto-resize child、resizable child、nested child、child with independent scroll。

Scrolling:
    ScrollView、HorizontalScrollView、VerticalScrollView、ScrollToItem、ScrollToX、ScrollToY、ScrollHereX、ScrollHereY、mouse wheel、scrollbar、horizontal scrollbar。

Clipping:
    ClipRect、PushClip、PopClip、clip children、clip table columns、clip scroll content。

Virtualization:
    ListClipper、VirtualList、VirtualTableRows，用于大列表、大表格和搜索结果。

Alignment:
    text baseline alignment、frame padding alignment、selectable text alignment、minor-axis alignment、same-line spacing、group bounding box capture。

Splitters:
    Splitter、ResizableSplitPane、TwoPaneLayout、ThreePaneLayout。
```

### 17.0.13 Popups、Context Menu、Modal 和 Dialog

```text
Popup:
    Popup、PopupMenu、DropdownPopup、OpenPopup、BeginPopup / EndPopup equivalent、CloseCurrentPopup、stacked popup、popup with menu bar、popup positioning。

Context menu:
    ContextMenu、ItemContextMenu、WindowContextMenu、VoidContextMenu、title-bar context menu。

Modal:
    Modal、PopupModal、Dialog、Alert、Confirm、Prompt、custom modal content、dim background、centered first show、draggable modal、stacked modal。

Close behavior:
    outside click close、Escape close、modal blocks outside interaction、popup blocks normal hover unless explicitly overridden。
```

### 17.0.14 Tooltip 和 Hover 延迟

```text
Tooltip:
    Tooltip、ItemTooltip、SetItemTooltip equivalent、BeginTooltip / EndTooltip equivalent、RichTooltip、DisabledItemTooltip。

Hover delay:
    DelayedTooltip、short delay、normal delay、stationary hover、tooltip hover policy、allow when blocked by popup、allow when blocked by active item、allow when overlapped、allow when disabled。
```

### 17.0.15 Drag & Drop

```text
Drag & Drop:
    DragSource、DropTarget、typed payload、payload preview、accept payload、accept-before-delivery、accept no preview tooltip、drag/drop color payload、copy payload、move payload、swap payload、drop target tooltip、drag to reorder list items、drag into list/tree/table。
```

### 17.0.16 Plotting、Image 和自定义绘制

```text
Core plotting:
    PlotLines、PlotHistogram、overlay text、animated plot values、function-backed plot values。该范围只覆盖 Dear ImGui core demo 的轻量 plot，不包含 ImPlot。

Image:
    Image、ImageWithBackground、ImageButton、TexturedButton、TexturePreview、TextureInspectorEntry。

Image viewer:
    zoom、pan、fit、pixel grid、nearest/linear sampler callback abstraction、UV display、checkerboard / image background、clip image canvas。

Custom Draw:
    DrawCanvas、DrawListWidget、line、polyline、rect、filled rect、rounded rect、circle、filled circle、ngon、triangle、filled triangle、bezier curve、convex polygon、path、text、image、clipping、draw layers、channel split / merge equivalent。
```

当前自定义绘制先以 DrawList 为核心抽象。DrawCanvas 给调用方一个已布局的 rect，并允许在 callback 中写入 DrawList；DrawListWidget 用于回放已有 DrawList，默认把已有命令视为组件本地坐标。

```csharp
public sealed class DrawCanvas : Widget
{
    public float CanvasWidth;
    public float CanvasHeight;
    public bool Clip;
    public bool DrawBackground;
    public Action<DrawList, MuiRect> OnDraw;
}

public sealed class DrawListWidget : Widget
{
    public DrawList Source;
    public float CanvasWidth;
    public float CanvasHeight;
    public bool Clip;
    public bool DrawBackground;
    public bool LocalCoordinates;
}
```

使用示例：

```csharp
new DrawCanvas((draw, rect) =>
{
    draw.Line(
        new MuiVec2(rect.X + 8f, rect.Y + 8f),
        new MuiVec2(rect.Right - 8f, rect.Bottom - 8f),
        MuiColor.FromRgb(0f, 0f, 0f),
        2f);

    draw.FillCircle(
        new MuiVec2(rect.X + 32f, rect.Y + 32f),
        12f,
        MuiColor.FromRgb(0.2f, 0.4f, 0.8f));
}).Height(80f).ExpandWidth()
```

实现约束：

```text
1. DrawList 是无头测试可检查的中间层，不依赖 UnityEngine 类型。
2. DrawCanvas callback 不应直接调用 Unity GUI；只能写入 DrawList。
3. DrawList 命令使用绝对坐标；DrawListWidget.LocalCoordinates=true 时由 WidgetPainter / renderer 对 Source 做 rect offset。
4. PushClip / PopClip 必须支持嵌套，且不改变调用方看到的 DrawList 坐标语义。
5. UnityPainter 可以用贴图线段、scanline、polyline approximation 执行基础图元；复杂矢量质量后续可替换后端优化。
6. ImPlot 不属于核心目标；PlotLines / PlotHistogram 仍只覆盖 Dear ImGui core demo 的轻量 plot。
```

### 17.0.17 Font、Style 和 Theme Editor

```text
Fonts:
    FontSelector、FontAtlasViewer、font preview、font size selector、monospace text style、fallback glyph diagnostics、texture atlas preview。

Style:
    StyleEditor、ThemeEditor、StyleSelector、ThemeSelector、color token editor、spacing editor、rounding editor、border editor、padding editor、alpha editor、disabled alpha editor。

Style stack:
    PushStyleColor / PopStyleColor equivalent、PushStyleVar / PopStyleVar equivalent、temporary style scope、per-widget style override、theme token override。
```

当前 Theme / Style 编辑先以 `ThemeTokens` 为核心数据结构。运行时主题可以继续通过 `MaximumTheme` 提供 Unity GUIStyle / texture 资源，但自有 painter、无头测试和 ThemeEditor 必须以 token 为中心，避免把可编辑样式硬编码在具体控件里。

```csharp
public enum ThemeColorToken
{
    WindowBg,
    PanelBg,
    DialogBg,
    DropdownBg,
    Backdrop,
    Text,
    TextDisabled,
    Border,
    Separator,
    Accent,
    ButtonBg,
    ButtonHoverBg,
    ButtonActiveBg,
    ButtonDisabledBg,
    FieldBg,
    FieldHoverBg,
    FieldActiveBg,
    SelectedBg,
    InfoBg,
    SuccessBg,
    WarningBg,
    ErrorBg
}

public enum ThemeScalarToken
{
    BorderWidth,
    Radius,
    FontSize,
    ToggleBoxSize,
    SliderTrackHeight,
    SliderThumbWidth,
    SliderThumbHeight
}

public sealed class ThemeTokens
{
    public ThemeTokens Clone();
    public void CopyFrom(ThemeTokens source);
    public MuiColor GetColor(ThemeColorToken token);
    public void SetColor(ThemeColorToken token, MuiColor color);
    public float GetScalar(ThemeScalarToken token);
    public void SetScalar(ThemeScalarToken token, float value);
}

public class ThemeEditor : Widget
{
    public ThemeTokens Tokens;
    public Action<ThemeTokens> OnChange;
    public bool ShowColors;
    public bool ShowScalars;
    public int MaxRows;
    public float ScalarStep;
}

public sealed class StyleEditorWindow : ThemeEditor;

public sealed class FontSelector : Widget;
public sealed class FontSizeSelector : Widget;
public sealed class StyleSelector : Widget;
public sealed class ThemeSelector : Widget;
public sealed class FontAtlasViewer : Widget;
public sealed class FallbackGlyphDiagnostics : Widget;
```

使用示例：

```csharp
new ThemeEditor(State.Theme, tokens =>
{
    SetState(s =>
    {
        s.Theme = tokens;
    });
})

new HStack(gap: 6f)
{
    new FontSelector("Font", State.FontName, State.Fonts,
        value => SetState(s => s.FontName = value)).ExpandWidth(),
    new FontSizeSelector("Size", State.FontSize, State.FontSizes,
        value => SetState(s => s.FontSize = value)).Width(150f)
}

new HStack(gap: 6f)
{
    new StyleSelector("Style", State.StyleName, State.Styles,
        value => SetState(s => s.StyleName = value)).ExpandWidth(),
    new ThemeSelector("Theme", State.ThemeName, State.Themes,
        value => SetState(s => s.ThemeName = value)).ExpandWidth()
}

new FontAtlasViewer
{
    FontName = State.FontName,
    TextureWidth = 256,
    TextureHeight = 256,
    GlyphCount = 128
}
```

实现约束：

```text
1. ThemeEditor 是受控组件；推荐通过 OnChange 返回新的 ThemeTokens 快照。
2. 未提供 OnChange 时，ThemeEditor 可以直接修改传入的 Tokens，作为轻量内部工具使用。
3. ThemeEditor 必须通过 MaximumUI LayoutEngine / painter / renderer 绘制，不得调用 GUILayout。
4. Theme token 读写必须走 ThemeColorToken / ThemeScalarToken，不让调用方依赖字段反射。
5. StyleEditorWindow 是通用样式工具入口，不包含任何插件业务控件。
6. FontSelector / FontSizeSelector / StyleSelector / ThemeSelector 是受控选择器，复用 ComboBox renderer，不引入单独兼容路径。
7. FontAtlasViewer / FallbackGlyphDiagnostics 是通用诊断 Widget，调用方提供 atlas / glyph 数据，MaximumUI 负责展示和布局。
8. 后续 PushStyleColor / PushStyleVar 应基于 ThemeTokens overlay，而不是绕过 token 系统直接改 GUIStyle。
```

### 17.0.18 Item Query、Window Query、输入和快捷键

```text
Item query:
    IsHovered、IsActive、IsFocused、IsClicked、IsVisible、IsEdited、IsActivated、IsDeactivated、IsDeactivatedAfterEdit、IsToggledOpen、item rect min/max/size、item id、item flags、return value query。

Window query:
    IsWindowFocused、IsWindowHovered、root/child hierarchy flags、popup hierarchy flags、dock hierarchy equivalent、any-window query、title-bar hovered/active query。

Input:
    keyboard navigation、mouse navigation、shortcut routing、shortcut ownership、default focus、focus here、key repeat、double click、drag threshold、mouse wheel、character input queue、modifier state、mouse cursor request。

Capture:
    WantCaptureMouse、WantCaptureKeyboard、WantTextInput、WantSetMousePos、NavActive、NavVisible、next-frame capture override。
```

当前查询系统以 mount 级 `UiQueryStore` 保存上一帧渲染结果。由于 MaximumUI 是声明式 UI，`Build()` 中查询的是上一帧状态；本帧输入在 renderer 阶段写入，下一次 Build 可见。

```csharp
public sealed class ItemQuery
{
    public string Id;
    public string Key;
    public string Path;
    public string TypeName;
    public MuiRect Rect;
    public bool Visible;
    public bool Hovered;
    public bool Active;
    public bool Focused;
    public bool Clicked;
    public bool Edited;
    public bool Activated;
    public bool Deactivated;
    public bool DeactivatedAfterEdit;
    public bool ToggledOpen;
}

public sealed class WindowQuery
{
    public string Id;
    public string Key;
    public string Title;
    public MuiRect Rect;
    public MuiRect TitleRect;
    public bool Visible;
    public bool Hovered;
    public bool Focused;
    public bool TitleHovered;
    public bool TitleActive;
}

public sealed class InputCaptureState
{
    public bool WantCaptureMouse;
    public bool WantCaptureKeyboard;
    public bool WantTextInput;
    public bool WantSetMousePos;
    public bool NavActive;
    public bool NavVisible;
    public bool MouseDownOnUi;
    public MuiVec2 MousePosition;
    public int KeyCode;
    public char Character;
    public bool Control;
    public bool Shift;
    public bool Alt;
    public bool Command;
}

public sealed class UiQueryStore
{
    public InputCaptureState Capture { get; }
    public int ItemCount { get; }
    public int WindowCount { get; }

    public ItemQuery GetItem(object key);
    public ItemQuery GetItemByPath(string path);
    public WindowQuery GetWindow(object key);

    public bool IsHovered(object key);
    public bool IsActive(object key);
    public bool IsFocused(object key);
    public bool IsClicked(object key);
    public bool IsVisible(object key);
    public bool IsWindowHovered(object key);

    public IList<ItemQuery> SnapshotItems();
    public IList<WindowQuery> SnapshotWindows();
}
```

`BuildContext` 暴露便捷查询：

```csharp
context.IsHovered("apply-button")
context.IsClicked("apply-button")
context.IsWindowHovered("settings-window")
context.Query.Capture.WantCaptureMouse
```

键盘导航使用 mount 级 `UiNavigationStore`。Renderer 每帧按布局遍历顺序注册可聚焦控件；runtime 在 Unity `KeyDown` 的 Tab / Shift+Tab 事件中切换焦点，并在鼠标按下可聚焦控件时同步焦点。`Build()` 中读取的是上一帧焦点状态，控件可通过稳定 key 或 path 被显式聚焦。

```csharp
public sealed class NavigationItem
{
    public string Id;
    public string Key;
    public string Path;
    public string TypeName;
    public MuiRect Rect;
    public bool Enabled;
}

public sealed class UiNavigationStore
{
    public string FocusedId { get; }
    public int Count { get; }
    public bool HasFocus { get; }

    public void BeginFrame();
    public void Register(Widget widget, string path, MuiRect rect, bool enabled);
    public void EndFrame();

    public bool Focus(object key);
    public bool FocusPath(string path);
    public bool FocusNext();
    public bool FocusPrevious();
    public bool IsFocused(Widget widget, string path);
    public NavigationItem FocusedItem();
    public IList<NavigationItem> Snapshot();
}
```

使用示例：

```csharp
new TextField(State.Name, value => SetState(s => s.Name = value), key: "name")
    .DefaultFocus()

new Button("Advanced", ToggleAdvanced, key: "advanced")
    .Focusable(false)

new Button("Focus name", () => context.Focus("name"))
```

键盘导航实现约束：

```text
1. Renderer 每帧 BeginFrame / Register / EndFrame 重建焦点顺序，不能依赖旧树中已消失的控件。
2. Widget.Focusable=false 或 Enabled=false 的控件不进入焦点顺序。
3. HasDefaultFocus 用于当前焦点丢失后的默认焦点；RequestFocus / FocusHere 只请求当前帧聚焦。
4. context.Focus(key) 和 context.FocusPath(path) 在 Build 中通常作用于上一帧已注册的控件；本帧新增控件应使用 .FocusHere() 请求聚焦。
5. Tab / Shift+Tab 先于普通快捷键路由，并在处理后 Event.Use() 与 Input.ResetInputAxes()。
6. 鼠标点击可聚焦控件时，ItemQuery.Focused 应在下一帧反映新的焦点。
7. UiQueryStore.Capture.NavActive / NavVisible 来自导航状态，供插件判断是否需要捕获键盘。
```

快捷键路由使用 mount 级 `UiShortcutRouter`。View 在 Build 中注册快捷键，runtime 在 Unity `KeyDown` 事件中按 mount sort order 反向解析；后注册的快捷键优先级更高。触发动作进入 MaximumUI callback queue，不在 Build 中立即执行。

```csharp
public sealed class ShortcutChord
{
    public int KeyCode;
    public bool Control;
    public bool Shift;
    public bool Alt;
    public bool Command;

    public ShortcutChord();
    public ShortcutChord(
        int keyCode,
        bool control = false,
        bool shift = false,
        bool alt = false,
        bool command = false);

    public bool Matches(MuiInputEvent input);
    public string DisplayText { get; }
}

public sealed class ShortcutBinding
{
    public string Id;
    public ShortcutChord Chord;
    public Action Action;
    public bool Enabled;
    public bool AllowWhenTextInput;
    public object Owner;
}

public sealed class UiShortcutRouter
{
    public string LastTriggeredId;
    public ShortcutChord LastTriggeredChord;
    public int TriggerCount;
    public int Count { get; }

    public void BeginBuild();
    public ShortcutBinding Register(
        string id,
        ShortcutChord chord,
        Action action,
        bool enabled = true,
        bool allowWhenTextInput = false,
        object owner = null);
    public ShortcutBinding Resolve(MuiInputEvent input, bool textInputActive);
    public IList<ShortcutBinding> Snapshot();
}
```

使用示例：

```csharp
context.RegisterShortcut(
    "save",
    new ShortcutChord((int)KeyCode.S, control: true),
    Save);
```

快捷键实现约束：

```text
1. ShortcutChord 使用 int KeyCode，保持核心和无头测试不依赖 Unity 类型。
2. Shortcut 注册发生在 Build 中；BuildEveryOnGUI=false 时保留上次注册表。
3. Resolve 从后往前查找，允许局部/后注册快捷键覆盖先注册项。
4. TextField 聚焦时默认屏蔽快捷键，除非 AllowWhenTextInput=true。
5. runtime 触发快捷键后必须 Event.Use() 并 Input.ResetInputAxes()。
6. MenuItem.Shortcut 可以继续作为显示文本；真实快捷键应通过 UiShortcutRouter 注册。
```

实现约束：

```text
1. 查询必须基于稳定 key 或 path；动态列表项应优先提供 key。
2. Build 中查询的是上一帧状态，不能要求同一次 Build 内立即看到本帧鼠标事件。
3. UiQueryStore 是只读查询入口；控件行为仍由受控 props / callback 决定。
4. Renderer 负责记录真实运行时 item / window rect、hover、active、click 和 capture。
5. 无头测试可通过 LayoutNode + InputDispatcherResult 写入 QueryStore，不依赖 Unity。
6. Edited / Activated / Deactivated / ToggledOpen 字段保留给后续控件逐步精确上报。
```

### 17.0.19 Debug、日志和开发工具

```text
Debug Tools:
    MetricsWindow、DebugLogWindow、IDStackTool、StyleEditorWindow、AboutWindow、UserGuideWindow、DemoGallery、ItemPicker、LayoutDebugger、DrawListDebugger、StateStoreInspector、InputDebugger。

Logging / Capture:
    LogToClipboard、LogToFile、LogToMemory、copy focused window text、debug export、LogText equivalent。

Error / diagnostics:
    ID conflict highlight、error recovery assertion option、layout mismatch diagnostics、draw command inspection、state store inspection。
```

当前内置调试工具先提供可组合的基础诊断 Widget：

```csharp
public enum DebugLogLevel
{
    Info,
    Warning,
    Error
}

public sealed class DebugLogEntry
{
    public DateTime Time;
    public DebugLogLevel Level;
    public string Message;
}

public static class DebugLog
{
    public static int MaxEntries;

    public static void Info(string message);
    public static void Warning(string message);
    public static void Error(string message);
    public static void Add(DebugLogLevel level, string message);
    public static void Clear();
    public static IList<DebugLogEntry> Snapshot();
    public static string LogToMemory();
    public static void LogToFile(string path);
    public static void LogToClipboard();
    public static string FormatEntries(IEnumerable<DebugLogEntry> source);
}

public sealed class RuntimeMetrics
{
    public int FrameCount;
    public int MountCount;
    public int VisibleMountCount;
    public int PendingCallbackCount;
    public bool IsMouseOverAnyWindow;
    public bool IsAnyTextFieldFocused;
    public bool IsModalOpen;
}

public sealed class MetricsWindow : Widget;
public sealed class DebugLogWindow : Widget;
public sealed class StateStoreInspector : Widget;
public sealed class LayoutDebugger : Widget;
public sealed class DrawListDebugger : Widget;
public sealed class InputDebugger : Widget;
public sealed class IDStackTool : Widget;
public sealed class ItemPicker : Widget;
public sealed class AboutWindow : Widget;
public sealed class UserGuideWindow : Widget;
public sealed class DemoGalleryEntry;
public sealed class DemoGallery : Widget;
```

使用示例：

```csharp
new VStack(gap: 6f)
{
    new MetricsWindow(MaximumUIRuntime.GetMetrics()).Height(154f),
    new InputDebugger(context.Query)
    {
        Navigation = context.Navigation,
        Shortcuts = context.Shortcuts
    }.Height(220f),
    new IDStackTool(context.Query).Height(198f),
    new ItemPicker(context.Query).Height(198f),
    new DebugLogWindow(DebugLog.Snapshot()).Height(176f),
    new StateStoreInspector(context.StateStore).Height(198f),
    new DemoGallery().Height(198f),
    new AboutWindow().Height(132f),
    new UserGuideWindow().Height(154f)
}
```

实现约束：

```text
1. DebugLog 是内存 ring buffer，默认用于开发诊断，不替代插件自己的持久日志。
2. StateStoreInspector 只读取 `UiStateStore.Snapshot()`，不得直接暴露内部 Dictionary。
3. MetricsWindow 读取 MaximumUIRuntime.GetMetrics() 的快照，不在 Build 中做昂贵采样。
4. LayoutDebugger / DrawListDebugger 接收调用方传入的 LayoutNode / DrawList，用于无头测试和开发工具。
5. InputDebugger 读取 `UiQueryStore`、`UiNavigationStore`、`UiShortcutRouter`，展示 capture、modifier、hovered / active / focused / clicked item、焦点数量和快捷键状态。
6. IDStackTool / ItemPicker 只读取 `UiQueryStore.SnapshotItems()` / `SnapshotWindows()`，用于检查稳定 ID、path、rect 和 hover / active / focused / clicked 状态。
7. AboutWindow / UserGuideWindow / DemoGallery 是通用开发工具入口，不包含任何具体插件业务 UI。
8. LogToMemory / LogToFile / LogToClipboard 是调试导出工具；持久业务日志仍由插件自己决定。
9. Debug 工具必须走 MaximumUI 自有 LayoutEngine / painter / renderer，不能回退到 GUILayout。
```

### 17.0.20 Gallery 验收要求

MaximumUI Gallery 必须完整展示以上目标控件全集。Gallery 不是装饰性示例，而是框架功能验收入口。每个控件至少需要展示 normal、hover、active、focused、selected、disabled、error / warning 相关状态中的适用状态；每个涉及内部状态的控件必须展示稳定 key、受控 value、OnChange 更新和重建后的状态保持。

## 17.1 Label

用途：显示文本。

```csharp
public sealed class Label : Widget
{
    public string Text;
    public bool WordWrap;
    public GUIStyle Style;

    public Label(string text, object key = null)
        : base(key)
    {
        Text = text ?? string.Empty;
        WordWrap = false;
        Style = null;
    }
}
```

使用：

```csharp
new Label("Settings")
new Label("This operation cannot be undone.") { WordWrap = true }
```

---

## 17.2 Button

用途：普通按钮。

```csharp
public sealed class Button : Widget
{
    public string Text;
    public Action OnClick;
    public GUIStyle Style;

    public Button(string text, Action onClick = null, object key = null)
        : base(key)
    {
        Text = text ?? string.Empty;
        OnClick = onClick;
        Enabled = true;
        Style = null;
    }
}
```

使用：

```csharp
new Button("Apply", Apply)

new Button("Reset", () =>
    SetState(s => s.Value = 0))
```

渲染行为：

```text
1. LayoutEngine 为 Button 分配 Rect。
2. InputDispatcher 根据 Rect 判断 hover / active / clicked。
3. Painter 根据 normal / hover / active / disabled 状态绘制背景、边框和文本。
4. clicked 且 Button 有效时，将 OnClick 放入 callback queue。
```

示意：

```csharp
private void RenderButton(Button button, LayoutNode node, RenderContext context)
{
    ControlState state = context.Input.GetControlState(node.Id, node.Rect, button.Enabled);

    context.Draw.FillRect(
        node.Rect,
        context.Theme.GetButtonColor(state));

    context.Draw.StrokeRect(
        node.Rect,
        context.Theme.Border,
        context.Theme.BorderWidth);

    context.Draw.Text(
        node.Rect,
        button.Text,
        context.Theme.ButtonText,
        button.Enabled ? context.Theme.Text : context.Theme.TextDisabled);

    if (state.Clicked && button.OnClick != null)
        MaximumUIRuntime.EnqueueCallback(button.OnClick);
}
```

---

## 17.3 Toggle

用途：布尔开关。

```csharp
public sealed class Toggle : Widget
{
    public string Text;
    public bool Value;
    public Action<bool> OnChange;
    public GUIStyle Style;

    public Toggle(
        string text,
        bool value,
        Action<bool> onChange = null,
        object key = null)
        : base(key)
    {
        Text = text ?? string.Empty;
        Value = value;
        OnChange = onChange;
        Enabled = true;
        Style = null;
    }
}
```

使用：

```csharp
new Toggle("Enabled", State.Enabled,
    v => SetState(s => s.Enabled = v))
```

渲染规则：

```text
1. Toggle 使用 MaximumUI 自绘 checkbox 和文本。
2. checkbox 在未选中、hover、active、checked 状态都必须可见。
3. 文本为空时仍绘制 checkbox，适合表格或锁定列。
```

---

## 17.4 Slider

用途：浮点滑条。

```csharp
public sealed class Slider : Widget
{
    public float Value;
    public float Min;
    public float Max;
    public Action<float> OnChange;

    public Slider(
        float value,
        float min,
        float max,
        Action<float> onChange = null,
        object key = null)
        : base(key)
    {
        Value = value;
        Min = min;
        Max = max;
        OnChange = onChange;
    }
}
```

使用：

```csharp
new Slider(State.Intensity, 0f, 1f,
    v => SetState(s => s.Intensity = v))
```

渲染规则：

```text
1. Slider track 和 thumb 使用 MaximumUI painter 根据 Theme token 自绘。
2. Slider 在固定行高中必须垂直居中。
3. Slider 不依赖 Unity 默认 skin 的可见纹理。
```

---

## 17.5 TextField

用途：单行文本输入。

```csharp
[Flags]
public enum TextInputFlags
{
    None = 0,
    ReadOnly = 1,
    Decimal = 2,
    Hexadecimal = 4,
    Scientific = 8,
    Uppercase = 16,
    NoBlank = 32,
    EnterReturnsTrue = 64,
    EscapeClearsAll = 128,
    EscapeReverts = 256
}

public static class TextInputFilter
{
    public static string Apply(
        string value,
        TextInputFlags flags,
        Func<char, bool> charFilter = null,
        int maxLength = 0);
}

public sealed class TextField : Widget
{
    public string Value;
    public Action<string> OnChange;
    public string Placeholder;
    public int MaxLength;
    public GUIStyle Style;
    public Action<string> OnSubmit;
    public Action<string> OnBlur;
    public Action<string> OnEdit;
    public Func<char, bool> CharFilter;
    public TextInputFlags Flags;
    public bool AutoFocus;

    public TextField(
        string value,
        Action<string> onChange = null,
        object key = null)
        : base(key)
    {
        Value = value ?? string.Empty;
        OnChange = onChange;
        Placeholder = string.Empty;
        MaxLength = 0;
        Style = null;
        OnSubmit = null;
        OnBlur = null;
        OnEdit = null;
        CharFilter = null;
        Flags = TextInputFlags.None;
        AutoFocus = false;
    }
}

public sealed class TextArea : Widget
{
    public string Value;
    public Action<string> OnChange;
    public string Placeholder;
    public int MaxLength;
    public GUIStyle Style;
    public Action<string> OnSubmit;
    public Action<string> OnBlur;
    public Action<string> OnEdit;
    public Func<char, bool> CharFilter;
    public TextInputFlags Flags;
}

public sealed class PasswordField : Widget
{
    public string Value;
    public Action<string> OnChange;
    public string Placeholder;
    public int MaxLength;
    public char MaskChar;
    public GUIStyle Style;
    public Action<string> OnSubmit;
    public Action<string> OnBlur;
    public Action<string> OnEdit;
    public Func<char, bool> CharFilter;
    public TextInputFlags Flags;
}

public sealed class SearchBox : Widget
{
    public string Value;
    public Action<string> OnChange;
    public string Placeholder;
    public int MaxLength;
    public Action<string> OnSubmit;
    public Action<string> OnBlur;
    public Action<string> OnEdit;
    public Func<char, bool> CharFilter;
    public TextInputFlags Flags;
}
```

内部状态：

```csharp
public sealed class TextFieldState
{
    public string EditingValue = string.Empty;
    public bool HasEditingValue;
    public bool Focused;
    public bool WasFocused;
    public string ControlName;
    public bool AutoFocusApplied;
    public string FocusStartValue;
    public bool HasFocusStartValue;
}
```

使用：

```csharp
new TextField(State.Search,
    v => SetState(s => s.Search = v),
    key: "search")
{
    Placeholder = "Search..."
}
```

带自动聚焦、回车提交和失焦校正：

```csharp
new TextField(State.Name,
    v => SetState(s => s.Name = v),
    key: "name-input")
{
    AutoFocus = true,
    OnSubmit = Save,
    OnBlur = NormalizeName,
    Flags = TextInputFlags.EscapeReverts
}
```

带字符过滤：

```csharp
new TextField(State.Code,
    v => SetState(s => s.Code = v),
    key: "code")
{
    MaxLength = 12,
    Flags = TextInputFlags.Uppercase | TextInputFlags.NoBlank,
    CharFilter = ch => ch != '_'
}

new TextField(State.Hex,
    v => SetState(s => s.Hex = v),
    key: "hex")
{
    MaxLength = 8,
    Flags = TextInputFlags.Hexadecimal | TextInputFlags.Uppercase
}
```

TextField 设计要点：

```text
1. 需要稳定 key。
2. focused 时使用内部 EditingValue。
3. unfocused 时从 Widget.Value 同步 EditingValue。
4. 输入变化可以即时更新 buffer，并通过 callback queue 通知外部 state。
5. Placeholder 仅在 Value 为空且未 focused 时显示。
6. AutoFocus 只在对应 TextFieldState 生命周期内应用一次。
7. OnSubmit 在 focused 状态下按 Return / KeypadEnter 时触发。
8. OnBlur 在控件从 focused 变为 unfocused 时触发，适合数字校正、名称规范化等通用场景。
9. TextInputFlags.ReadOnly 禁止修改，但仍保留统一绘制和查询语义。
10. Decimal / Hexadecimal / Scientific / Uppercase / NoBlank / CharFilter 由 TextInputFilter.Apply 统一处理。
11. EscapeClearsAll 清空当前编辑值；EscapeReverts 回到获得焦点时的 FocusStartValue。
12. TextArea 默认允许 Return 输入换行；设置 EnterReturnsTrue 后 Return / KeypadEnter 触发 OnSubmit。
13. OnEdit 表示 MaximumUI edit callback，和 OnChange 同步进入 callback queue；调用方仍应以受控 Value 作为最终状态。
```

---

## 17.6 ComboBox<T>

用途：下拉选择。

```csharp
public sealed class ComboBox<T> : Widget
{
    public string Label;
    public T Value;
    public IList<T> Options;
    public Func<T, string> GetText;
    public Action<T> OnChange;
    public int MaxVisibleItems;

    public ComboBox(
        string label,
        T value,
        IEnumerable<T> options,
        Func<T, string> getText = null,
        Action<T> onChange = null,
        object key = null)
        : base(key)
    {
        Label = label ?? string.Empty;
        Value = value;
        Options = new List<T>();

        if (options != null)
        {
            foreach (T item in options)
                Options.Add(item);
        }

        GetText = getText ?? DefaultGetText;
        OnChange = onChange;
        MaxVisibleItems = 8;
    }

    private static string DefaultGetText(T value)
    {
        return value != null ? value.ToString() : string.Empty;
    }
}
```

内部状态：

```csharp
public sealed class ComboBoxState
{
    public bool Open;
    public Rect ButtonRect;
    public bool HasButtonRect;
    public string WindowStateId;
    public string ControlName;
}
```

使用：

```csharp
new ComboBox<string>(
    "Mode",
    State.Mode,
    State.Modes,
    x => x,
    v => SetState(s => s.Mode = v),
    key: "mode")
```

渲染规则：

```text
1. 正常 layout 中绘制当前值按钮。
2. 点击按钮切换 Open。
3. Open 时向 OverlayLayer 注册 dropdown。
4. Dropdown 在所有普通窗口之后绘制。
5. 点击 item 触发 OnChange 并关闭。
6. 控件失焦、点击 dropdown 外部或 Escape 关闭。
7. Dropdown 高度由 MaxVisibleItems 限制，超过时使用 scroll。
8. ComboBox 需要稳定 key。
9. Dropdown 空间不足时向上弹出。
10. Dropdown 样式由 Theme token 决定；普通项、hover、active、focused、selected 和 disabled 状态必须有明确视觉区分。
```

---

## 17.7 Separator

用途：分隔线。

```csharp
public sealed class Separator : Widget
{
    public float Thickness;
    public GUIStyle Style;

    public Separator(float thickness = 1f, object key = null)
        : base(key)
    {
        Thickness = thickness;
        Style = null;
    }
}
```

使用：

```csharp
new Separator()
new Separator(2f)
```

---

## 17.8 Space / Spacer

用途：固定空白 / 弹性空白。

```csharp
public sealed class Space : Widget
{
    public float Size;

    public Space(float size, object key = null)
        : base(key)
    {
        Size = size;
    }
}

public sealed class Spacer : Widget
{
    public Spacer(object key = null)
        : base(key)
    {
    }
}
```

使用：

```csharp
new Space(8f)

new HStack
{
    new Label("Ready"),
    new Spacer(),
    new Button("Close", Close)
}
```

---

## 17.9 VStack / HStack

用途：垂直 / 水平布局。

```csharp
public sealed class VStack : Widget
{
    public float Gap;
    public GUIStyle Style;

    public VStack(float gap = 0f, object key = null)
        : base(key)
    {
        Gap = gap;
        Style = null;
    }
}

public sealed class HStack : Widget
{
    public float Gap;
    public GUIStyle Style;

    public HStack(float gap = 0f, object key = null)
        : base(key)
    {
        Gap = gap;
        Style = null;
    }
}
```

使用：

```csharp
new VStack(gap: 8f)
{
    new Label("A"),
    new Label("B")
}

new HStack(gap: 6f)
{
    new Label("Opacity").Width(80f),
    new Slider(...)
}
```

---

## 17.10 Panel

用途：轻量内容容器。

```csharp
public sealed class Panel : Widget
{
    public Panel(object key = null)
        : base(key)
    {
    }
}
```

使用：

```csharp
new Panel
{
    new Toggle("Verbose log", State.VerboseLog,
        v => SetState(s => s.VerboseLog = v))
}
```

Panel 设计要点：

```text
1. Panel 默认只提供轻量 padding，不默认使用圆角和粗边框。
2. 需要强调边框、提示或错误状态时使用 HelpBox、Dialog 或显式样式扩展。
3. 页面结构不应依赖嵌套圆角框堆叠。
```

---

## 17.11 Window

用途：可拖动窗口。

```csharp
public sealed class Window : Widget
{
    public string Title;
    public float Width;
    public float Height;
    public float MinWidth;
    public float MinHeight;
    public float FoldedHeight;
    public bool Foldable;
    public bool Closable;
    public bool Visible;
    public Action<bool> OnVisibleChange;

    public Window(
        string title,
        float width = 420f,
        float height = 300f,
        object key = null,
        float minWidth = 320f,
        float minHeight = 240f,
        bool foldable = true,
        bool closable = false,
        Action<bool> onVisibleChange = null)
        : base(key)
    {
        Title = title ?? string.Empty;
        Width = width;
        Height = height;
        MinWidth = minWidth;
        MinHeight = minHeight;
        FoldedHeight = 20f;
        Foldable = foldable;
        Closable = closable;
        Visible = true;
        OnVisibleChange = onVisibleChange;
    }
}
```

内部状态：

```csharp
public sealed class WindowState
{
    public Rect Rect;
    public bool Initialized;
    public bool Folded;
}
```

渲染规则：

```text
1. 首次渲染使用默认位置和 Width / Height 初始化 Rect。
2. 后续渲染使用 WindowState.Rect。
3. Window painter 自绘标题栏、背景、边框和内容区，不依赖 GUI.Window。
4. 标题栏可拖动，拖动结果写回 WindowState.Rect。
5. Foldable 为 true 时绘制折叠按钮。
6. Closable 为 true 时绘制关闭按钮，并通过 OnVisibleChange 通知外部状态。
7. Window 应提供稳定 key。
8. Window 内容区使用 Theme.WindowContentPadding，插件不需要手动留边。
9. 标题文字的垂直修正由 Window painter 和 Theme token 共同决定。
```

---

## 17.12 ScrollView

用途：滚动区域。

```csharp
public sealed class ScrollView : Widget
{
    public bool AlwaysShowHorizontal;
    public bool AlwaysShowVertical;
    public float Height;

    public ScrollView(object key = null)
        : base(key)
    {
        AlwaysShowHorizontal = false;
        AlwaysShowVertical = false;
        Height = 0f;
    }
}
```

内部状态：

```csharp
public sealed class ScrollViewState
{
    public Vector2 Position;
}
```

使用：

```csharp
new ScrollView(key: "material-scroll")
{
    MUI.ForEach(State.Materials, material =>
        new HStack(key: material.Name)
        {
            new Label(material.Name).ExpandWidth(),
            new Button("Select", () => Select(material)).Width(80f)
        })
}
```

---

## 17.13 Foldout

用途：折叠内容区域。

```csharp
public sealed class Foldout : Widget
{
    public string Title;
    public bool Open;
    public Action<bool> OnChange;

    public Foldout(
        string title,
        bool open,
        Action<bool> onChange = null,
        object key = null)
        : base(key)
    {
        Title = title ?? string.Empty;
        Open = open;
        OnChange = onChange;
    }
}
```

使用：

```csharp
new Foldout("Advanced", State.AdvancedOpen,
    v => SetState(s => s.AdvancedOpen = v))
{
    new Toggle("Verbose log", State.VerboseLog,
        v => SetState(s => s.VerboseLog = v))
}
```

渲染：

```text
1. 标题使用 MaximumUI painter 绘制成可点击 header。
2. 标题文本使用 "[-] Title" / "[+] Title" 表示状态。
3. 点击标题切换 Open。
4. Open 时以缩进内容区渲染 children。
```

---

## 17.14 Tabs / Tab

用途：多页签内容。

```csharp
public sealed class Tabs : Widget
{
    public int SelectedIndex;
    public Action<int> OnChange;
    public Action<int> OnClose;
    public Action<int, int> OnReorder;
    public bool Reorderable;
    public bool CloseWithMiddleMouse;
    public bool NoTooltip;

    public Tabs(
        int selectedIndex,
        Action<int> onChange = null,
        object key = null)
        : base(key)
    {
        SelectedIndex = selectedIndex;
        OnChange = onChange;
        OnClose = null;
        OnReorder = null;
        Reorderable = false;
        CloseWithMiddleMouse = true;
        NoTooltip = false;
    }
}

public sealed class Tab : Widget
{
    public string Title;
    public bool Closable;
    public bool Unsaved;
    public bool Visible;
    public bool NoTooltip;
    public Action OnClose;

    public Tab(string title, object key = null)
        : base(key)
    {
        Title = title ?? string.Empty;
        Closable = false;
        Unsaved = false;
        Visible = true;
        NoTooltip = false;
        OnClose = null;
    }
}

public enum TabButtonPlacement
{
    Leading,
    Trailing
}

public class TabItemButton : Widget
{
    public string Text;
    public Action OnClick;
    public TabButtonPlacement Placement;
    public float ButtonWidth;
}

public sealed class LeadingTabButton : TabItemButton
{
    public LeadingTabButton(string text, Action onClick = null, object key = null);
}

public sealed class TrailingTabButton : TabItemButton
{
    public TrailingTabButton(string text, Action onClick = null, object key = null);
}
```

`SelectedIndex`、`OnClose`、`OnReorder` 使用可见 Tab 的序号，不计算 `LeadingTabButton` / `TrailingTabButton`，也不计算 `Visible=false` 的 Tab。

使用：

```csharp
new Tabs(State.Tab, v => SetState(s => s.Tab = v), key: "main-tabs")
{
    Reorderable = true,
    OnClose = index => CloseTab(index),
    OnReorder = (from, to) => MoveTab(from, to)
}
.Content(new LeadingTabButton("+", AddTab))
.Content(new Tab("General")
{
    Unsaved = State.GeneralDirty
}.Content(new Label("General settings")))
.Content(new Tab("Debug")
{
    Closable = true,
    Visible = State.ShowDebug,
    OnClose = () => SetState(s => s.ShowDebug = false)
}.Content(new Button("Dump", Dump)))
.Content(new TrailingTabButton("Log", DumpLog));
```

基础使用也可以只放 Tab：

```csharp
new Tabs(State.Tab, v => SetState(s => s.Tab = v))
{
    new Tab("General")
    {
        new Label("General settings")
    },

    new Tab("Debug")
    {
        new Button("Dump", Dump)
    }
}
```

渲染：

```text
1. 顶部使用 MaximumUI painter 渲染所有 Tab 按钮。
2. 当前 SelectedIndex 对应 Tab 使用 onNormal / selected style。
3. 点击 Tab 调用 OnChange。
4. 内容区只渲染 selected Tab 的 children。
5. 活动 tab 必须有明确视觉状态。
6. Closable tab 显示关闭按钮；点击关闭按钮调用 Tab.OnClose，未提供时调用 Tabs.OnClose(index)。
7. Unsaved tab 显示未保存标记，不改变关闭逻辑；调用方负责弹出确认或保存。
8. LeadingTabButton / TrailingTabButton 只参与 header，不进入内容区，也不计入 SelectedIndex。
9. Reorderable tab 的拖拽只上报 OnReorder(from, to)；调用方负责重排自己的 tab state。
10. Tab 是正式容器，必须进入 LayoutEngine 测量和 Arrange，不得把内容当作未布局子树渲染。
```

---

## 17.15 Dialog

用途：模态弹窗。

```csharp
public sealed class Dialog : Widget
{
    public string Title;
    public float Width;
    public float Height;
    public bool CloseOnEscape;
    public bool CloseOnOutsideClick;
    public Action OnClose;

    public Dialog(string title, object key = null)
        : base(key)
    {
        Title = title ?? string.Empty;
        Width = 360f;
        Height = 180f;
        CloseOnEscape = true;
        CloseOnOutsideClick = false;
        OnClose = null;
    }
}
```

声明式使用：

```csharp
MUI.If(State.ShowConfirm, () =>
    new Dialog("Confirm", key: "confirm")
    {
        new VStack(gap: 8f)
        {
            new Label("Apply changes?"),

            new Separator(),

            new HStack(gap: 6f)
            {
                new Spacer(),

                new Button("Cancel", () =>
                    SetState(s => s.ShowConfirm = false)),

                new Button("Apply", () =>
                {
                    Apply();
                    SetState(s => s.ShowConfirm = false);
                })
            }
        }
    })
```

DialogService：

```csharp
public sealed class DialogService
{
    public void Alert(string title, string message, Action onClose = null);

    public void Confirm(
        string title,
        string message,
        Action onConfirm,
        Action onCancel = null);

    public void Show(Dialog dialog);

    public void CloseTop();

    public void CloseAll();
}
```

DialogService 行为：

```text
1. Alert / Confirm 根据 message 行数、最长行和按钮数量计算默认 Width / Height。
2. Alert / Confirm 的尺寸仍会受屏幕边界限制。
3. Show(Dialog) 保留调用方提供的 Dialog.Width / Dialog.Height。
```

Dialog 行为：

```text
1. Dialog 在 dialog layer 中绘制。
2. Dialog 打开时阻止 MaximumUI 下层控件响应。
3. Escape 关闭顶层 Dialog。
4. 点击外部是否关闭由 CloseOnOutsideClick 决定。
5. Dialog 首次显示时居中，之后通过稳定 key 保留位置。
6. Dialog 标题区域可拖动，拖动后不得在下一帧自动回正。
7. Dialog 尺寸由 Width / Height 决定。
8. Inline Dialog 存在时，包含该 Dialog 的父级 layout 仍可遍历渲染，但其下层交互控件必须被禁用，Dialog 自身保持可操作。
```

---

## 17.16 HelpBox

用途：提示、警告、错误。

```csharp
public enum HelpBoxType
{
    Info,
    Warning,
    Error
}

public sealed class HelpBox : Widget
{
    public string Message;
    public HelpBoxType Type;

    public HelpBox(
        string message,
        HelpBoxType type = HelpBoxType.Info,
        object key = null)
        : base(key)
    {
        Message = message ?? string.Empty;
        Type = type;
    }
}
```

使用：

```csharp
new HelpBox("No character selected.", HelpBoxType.Warning)
new HelpBox("Operation failed.", HelpBoxType.Error)
```

---

# 18. OverlayLayer

OverlayLayer 负责绘制不应受普通布局树裁剪和层级限制的浮层。

包含：

```text
ComboBox dropdown
Context menu
Dialog background
Dialog window
```

接口：

```csharp
public sealed class OverlayLayer
{
    private readonly List<OverlayRequest> _requests =
        new List<OverlayRequest>();

    public void Register(OverlayRequest request)
    {
        if (request != null)
            _requests.Add(request);
    }

    public void Render(RenderContext context)
    {
        for (int i = 0; i < _requests.Count; i++)
            _requests[i].Render(context);

        _requests.Clear();
    }
}

public sealed class OverlayRequest
{
    public int SortOrder;
    public Rect Anchor;
    public Action<RenderContext> Render;
}
```

绘制顺序：

```text
1. 普通 Window / Panel / controls。
2. Dropdown。
3. Context menu。
4. Dialog backdrop。
5. Dialog。
```

---

# 19. 错误处理

MaximumUI 不应让单个控件异常导致整个插件 UI 不可用。

Runtime 应在 View.Build、Layout、InputDispatch 和 Paint 边界捕获异常：

```csharp
try
{
    Widget root = mount.View.Build(context);
    renderer.Render(root, context);
}
catch (Exception ex)
{
    RenderErrorWindow(ex, context);
}
```

错误显示：

```text
1. 显示异常 message。
2. 可显示 stack trace。
3. 避免异常在每次 OnGUI 中无限刷日志。
4. 同一异常可节流记录。
```

错误窗口可使用内置控件：

```csharp
new Window("MaximumUI Error", key: "maximumui-error")
{
    new VStack(gap: 6f)
    {
        new HelpBox(ex.Message, HelpBoxType.Error),
        new TextArea(ex.ToString())
    }
}
```

---

# 20. 性能约束

Koikatsu / Unity 5.6 / old Mono GC 环境下，OnGUI 分配需要控制。自有布局和 DrawList 可以降低 GUILayout 黑箱行为带来的不确定性，但仍需要控制 Widget 数量、文本测量、动态列表和 DrawCommand 分配。

性能原则：

```text
1. Build 中不要扫描场景。
2. Build 中不要大量 LINQ。
3. Build 中不要反射遍历。
4. 大列表需要搜索、分页、过滤、缓存。
5. ScrollView 不等于虚拟列表，默认仍然会布局和渲染所有 children。
6. 高频控件尽量避免重复文本测量。
7. Enum.GetValues、ToList、string.Format 等分配应缓存。
8. ComboBox options 应由外部状态缓存，不要每次 Build 重建大数组。
9. DrawCommand 列表、布局节点列表和临时 hit-test 列表应复用。
10. LayoutEngine 应区分 dirty layout 和仅状态 repaint，避免无意义全量测量。
11. 大量动态列表应规划 VirtualList / Table virtualization，而不是依赖普通 ScrollView。
```

建议提供：

```text
1. Runtime debug profiler。
2. 每个 mount 的 Build 时间统计。
3. 每个 mount 的 Layout 时间统计。
4. 每个 mount 的 Paint 时间统计。
5. 每帧 Widget 数统计。
6. 每帧 LayoutNode 数统计。
7. 每帧 DrawCommand 数统计。
8. 可选 GC allocation 观察辅助。
```

---

# 21. KK 插件可行性边界

MaximumUI 适合：

```text
插件设置窗口
Studio / Maker 工具面板
角色、材质、Renderer、Object 列表
运行时 debug overlay
参数调节工具
确认框、提示框、下拉选择
小型编辑器类插件 UI
```

MaximumUI 不适合：

```text
替换游戏主 UI
复杂动画界面
高性能超大表格
复杂 uGUI prefab 界面
强交互拖拽编辑器
需要完整输入拦截的玩法 UI
需要高质量排版的图文界面
```

关键限制：

```text
1. IMGUI 视觉能力有限。
2. Unity OnGUI 调用频繁，事件类型和绘制阶段需要严格区分。
3. OnGUI 会高频调用。
4. Event.current.Use 只影响 IMGUI event。
5. TextField / ComboBox / Dialog 需要仔细处理状态和焦点。
6. 大列表性能需要额外控制。
```

---

# 22. 示例：基础设置窗口

```csharp
public sealed class SettingsState
{
    public bool Enabled = true;
    public float Opacity = 0.8f;
    public string Mode = "Normal";

    public string[] Modes = new string[]
    {
        "Normal",
        "Advanced",
        "Debug"
    };
}

public sealed class SettingsView : StatefulView<SettingsState>
{
    public SettingsView()
        : base(new SettingsState())
    {
    }

    public override Widget Build(BuildContext context)
    {
        return new Window("MaximumUI Settings", width: 420f, height: 260f, key: "settings")
        {
            new VStack(gap: 8f)
            {
                new Label("MaximumUI Settings"),

                new Toggle("Enable plugin", State.Enabled,
                    v => SetState(s => s.Enabled = v)),

                new Panel
                {
                    new VStack(gap: 6f)
                    {
                        new HStack(gap: 6f)
                        {
                            new Label("Opacity").Width(80f),

                            new Slider(State.Opacity, 0f, 1f,
                                v => SetState(s => s.Opacity = v))
                                .ExpandWidth(),

                            new Label(State.Opacity.ToString("0.00")).Width(48f)
                        },

                        new ComboBox<string>(
                            "Mode",
                            State.Mode,
                            State.Modes,
                            x => x,
                            v => SetState(s => s.Mode = v),
                            key: "mode")
                    }
                }.Disabled(!State.Enabled),

                new Separator(),

                new HStack(gap: 6f)
                {
                    new Spacer(),

                    new Button("Apply", Apply).Width(90f).Disabled(!State.Enabled),

                    new Button("Reset", Reset).Width(90f)
                }
            }
        };
    }

    private void Apply()
    {
    }

    private void Reset()
    {
        SetState(delegate(SettingsState s)
        {
            s.Enabled = true;
            s.Opacity = 0.8f;
            s.Mode = "Normal";
        });
    }
}
```

---

# 23. 示例：材质列表工具

```csharp
public sealed class MaterialToolState
{
    public string Search = string.Empty;
    public List<string> Materials = new List<string>();
    public string Selected;
}

public sealed class MaterialToolView : StatefulView<MaterialToolState>
{
    public MaterialToolView()
        : base(new MaterialToolState())
    {
    }

    public override Widget Build(BuildContext context)
    {
        return new Window("Material Tool", width: 460f, height: 380f, key: "material-tool")
        {
            new VStack(gap: 6f)
            {
                new HStack(gap: 6f)
                {
                    new TextField(State.Search,
                        v => SetState(s => s.Search = v),
                        key: "search")
                        .ExpandWidth(),

                    new Button("Refresh", Refresh).Width(80f)
                },

                new Separator(),

                new ScrollView(key: "material-scroll")
                {
                    MUI.ForEach(GetFilteredMaterials(), material =>
                        new HStack(key: material)
                        {
                            new Label(material).ExpandWidth(),

                            new Button("Select", delegate
                            {
                                SelectMaterial(material);
                            }).Width(80f)
                        })
                },

                new Separator(),

                new Label("Selected: " + (State.Selected ?? "None"))
            }
        };
    }

    private IEnumerable<string> GetFilteredMaterials()
    {
        if (State.Search == null || State.Search.Length == 0)
            return State.Materials;

        List<string> result = new List<string>();

        for (int i = 0; i < State.Materials.Count; i++)
        {
            string name = State.Materials[i];

            if (name != null &&
                name.IndexOf(State.Search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.Add(name);
            }
        }

        return result;
    }

    private void Refresh()
    {
        SetState(delegate(MaterialToolState s)
        {
            s.Materials.Clear();

            s.Materials.Add("cf_m_body");
            s.Materials.Add("cf_m_face");
            s.Materials.Add("cf_m_eyeline");
            s.Materials.Add("cf_m_tang");
        });
    }

    private void SelectMaterial(string material)
    {
        SetState(delegate(MaterialToolState s)
        {
            s.Selected = material;
        });
    }
}
```

---

# 24. 示例：Dialog 确认框

```csharp
public sealed class ConfirmState
{
    public bool ShowDialog;
}

public sealed class ConfirmView : StatefulView<ConfirmState>
{
    public ConfirmView()
        : base(new ConfirmState())
    {
    }

    public override Widget Build(BuildContext context)
    {
        return new Window("Confirm Example", width: 360f, height: 180f, key: "confirm-main")
        {
            new VStack(gap: 8f)
            {
                new Button("Dangerous Operation", delegate
                {
                    SetState(s => s.ShowDialog = true);
                }),

                MUI.If(State.ShowDialog, () =>
                    new Dialog("Confirm", key: "confirm-dialog")
                    {
                        new VStack(gap: 8f)
                        {
                            new Label("Run this operation?"),

                            new Separator(),

                            new HStack(gap: 6f)
                            {
                                new Spacer(),

                                new Button("Cancel", delegate
                                {
                                    SetState(s => s.ShowDialog = false);
                                }),

                                new Button("Run", delegate
                                {
                                    RunOperation();

                                    SetState(s => s.ShowDialog = false);
                                })
                            }
                        }
                    })
            }
        };
    }

    private void RunOperation()
    {
    }
}
```

---

# 25. 示例：Tabs

```csharp
public sealed class TabState
{
    public int Tab;
    public bool Enabled = true;
    public bool Debug;
}

public sealed class TabbedView : StatefulView<TabState>
{
    public TabbedView()
        : base(new TabState())
    {
    }

    public override Widget Build(BuildContext context)
    {
        return new Window("Tabbed Tool", width: 460f, height: 320f, key: "tabs")
        {
            new Tabs(State.Tab, v => SetState(s => s.Tab = v), key: "main-tabs")
            {
                new Tab("General")
                {
                    new VStack(gap: 8f)
                    {
                        new Toggle("Enabled", State.Enabled,
                            v => SetState(s => s.Enabled = v)),

                        new Label("General plugin settings.")
                    }
                },

                new Tab("Debug")
                {
                    new VStack(gap: 8f)
                    {
                        new Toggle("Debug", State.Debug,
                            v => SetState(s => s.Debug = v)),

                        new Button("Dump State", DumpState)
                    }
                }
            }
        };
    }

    private void DumpState()
    {
    }
}
```

---

# 26. 示例：DialogService

```csharp
public sealed class DialogServiceView : View
{
    public override Widget Build(BuildContext context)
    {
        return new Window("Dialog Service", width: 360f, height: 180f, key: "dialog-service")
        {
            new VStack(gap: 8f)
            {
                new Button("Show Alert", delegate
                {
                    context.Dialogs.Alert(
                        "Saved",
                        "Settings have been saved.");
                }),

                new Button("Show Confirm", delegate
                {
                    context.Dialogs.Confirm(
                        "Reset",
                        "Reset all settings?",
                        ResetSettings);
                })
            }
        };
    }

    private void ResetSettings()
    {
    }
}
```

---

# 27. 测试策略

MaximumUI 的测试只包含两类：无头自动化测试和人工 Koikatsu 测试。不建立 UnityHarness 或 BepInExHarness。

## 27.1 无头自动化测试

无头测试用于验证 MaximumUI.Core 中不依赖 Unity / Koikatsu 的逻辑。它应覆盖：

```text
Widget tree 构建
key path 生成
StateStore
LayoutEngine measure / arrange
InputDispatcher hover / active / focus 状态机
Callback queue
Overlay ordering
ScrollView clip 与滚动状态
DrawList command 生成
Theme token resolution
受控组件 Value / OnChange 一致性
```

无头测试不验证真实 Unity 字体绘制、IME 输入、GUIUtility.hotControl、屏幕截图或 Koikatsu 输入冲突。这些属于人工 KK 测试范围。

布局测试应使用固定输入和固定 viewport，断言布局树：

```text
Window      x=0   y=0   w=300 h=200
VStack      x=8   y=28  w=284 h=164
Label A     x=8   y=28  w=284 h=20
Button B    x=8   y=56  w=284 h=24
Spacer      x=8   y=88  w=284 h=76
Button C    x=8   y=172 w=284 h=24
```

绘制测试优先比较 DrawList snapshot，而不是截图：

```text
FillRect WindowBg  rect=(0,0,300,200)
Text     "Test"    rect=(8,4,284,20)
FillRect ButtonBg  rect=(8,56,284,24)
Text     "Apply"   rect=(8,56,284,24)
```

DrawList snapshot 应覆盖：

```text
绘制顺序
clip push / pop 匹配
normal / hover / active / selected / disabled 状态
dropdown 是否位于普通窗口之后
tooltip 是否位于普通控件之后
dialog backdrop 是否位于 dialog 之前
modal 状态是否阻止下层控件响应
```

## 27.2 人工 Koikatsu 测试

人工 KK 测试用于验证真实游戏环境下的集成问题。它应覆盖：

```text
BepInEx 5 插件加载
MaximumUIRuntime.EnsureHost
Mount / Unmount
ToggleKey 显示隐藏
场景切换后窗口状态
Studio / Maker / Free H 模式下的基本显示
与其他插件窗口叠加
鼠标 soft capture
TextField 输入、复制粘贴、中日文 IME 基础行为
ComboBox / ContextMenu / Tooltip / Dialog overlay 层级
ScrollView 滚轮行为
全屏、窗口化、不同分辨率和 UI 缩放
长时间打开窗口后的 GC 和卡顿情况
```

人工 KK 测试只作为集成 smoke test，不应承担核心布局、输入状态机或 DrawList 的主要回归测试。核心行为必须优先由无头测试覆盖。

---

# 28. 实现检查清单

MaximumUI 需要实现的核心能力：

```text
Runtime:
    MaximumUIRuntime
    MaximumUIHost
    Mount / Unmount
    MountOptions
    Callback queue
    Soft capture flags
    Sort order

Core:
    View
    StatefulView<TState>
    BuildContext
    Widget
    Fragment
    EmptyWidget
    MUI.If
    MUI.ForEach
    UiStateStore

Backend / Rendering:
    LayoutEngine
    InputDispatcher
    DrawList
    UnityOnGuiBackend
    UnityPainter
    RenderContext
    Widget dispatch
    LayoutHints conversion
    Error boundary

Layout:
    Window
    Panel
    ChildPanel
    VStack
    HStack
    Grid
    Table
    ScrollView
    Space
    Spacer
    Separator

Controls:
    Label
    Button
    Toggle
    RadioButton
    Slider
    TextField
    ComboBox<T>
    Foldout
    Tabs
    Tab
    Dialog
    HelpBox
    ProgressBar
    Selectable
    TreeNode
    SearchBox
    ColorEdit
    VectorField

Overlay:
    OverlayLayer
    Dropdown rendering
    Tooltip rendering
    ContextMenu rendering
    Dialog rendering
    Outside click close
    Escape handling

Style:
    Theme
    DefaultTheme
    Theme token resolution
    GUIStyle text cache
    Per-widget text style / state color override
    Width / Height / ExpandWidth / ExpandHeight / Content / Disabled extensions

Testing:
    Headless layout tests
    Headless input state tests
    Headless DrawList snapshot tests
    Headless callback queue tests
    Manual Koikatsu smoke test checklist

Koikatsu-oriented utilities:
    Scene visibility helper
    Studio-only mount helper
    Maker-only mount helper
    Optional input blocker extension package
```

---

# 29. 总结

MaximumUI 应实现为一个专门服务于 Koikatsu 插件开发的声明式 IMGUI 库。

它的核心体验是：

```text
1. 用 C# 对象树表达 UI。
2. 用 collection initializer 表达 children。
3. 用 named / optional arguments 表达 props。
4. 用 StatefulView 管理 UI 状态。
5. 用 SetState 触发重绘。
6. 用 key 保持内部控件状态。
7. 用自有 LayoutEngine 计算布局。
8. 用 DrawList 表达平台无关绘制命令。
9. 用 Unity OnGUI 低级绘制能力执行默认后端。
10. 用 Content / Width / Height / ExpandWidth / Disabled 等扩展方法改善组合体验。
11. 用 overlay layer 支持 ComboBox / Dialog / Tooltip / ContextMenu。
12. 用 soft capture 保守处理 IMGUI 输入。
13. 用无头测试覆盖核心行为，用人工 KK 测试覆盖真实集成。
14. 用 net35-compatible API 保证 KK / BepInEx 5 / Unity 5.6 可用。
```

推荐主语法：

```csharp
return new Window("My Plugin", width: 480f, height: 360f, key: "my-plugin")
{
    new VStack(gap: 8f)
    {
        new Label("My Plugin"),

        new Toggle("Enabled", State.Enabled,
            v => SetState(s => s.Enabled = v)),

        new ComboBox<string>(
            "Mode",
            State.Mode,
            State.Modes,
            x => x,
            v => SetState(s => s.Mode = v),
            key: "mode"),

        new Separator(),

        new HStack(gap: 6f)
        {
            new Spacer(),
            new Button("Apply", Apply).Width(90f),
            new Button("Close", Close).Width(90f)
        }
    }
};
```

这就是 MaximumUI 的核心形态：**面向 KK 插件的、net35 友好的、拥有自有布局和自绘控件系统的 Flutter-like C# 工具 UI DSL。**
