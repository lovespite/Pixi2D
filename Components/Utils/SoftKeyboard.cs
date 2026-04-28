using Pixi2D.Controls;
using Pixi2D.Core;
using Pixi2D.Events;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Components.Utils;

/// <summary>
/// 屏幕软键盘 (On-Screen Keyboard)。
/// <para>
/// 通过点击按键，将字符 (KeyPress) 与按键码 (KeyDown) 派发给目标对象。
/// 默认派发到 <see cref="Stage.FocusedObject"/>。
/// </para>
/// <para>
/// 用法:
/// <code>
/// var kb = new SoftKeyboard(textFactory);
/// kb.X = 50; kb.Y = 400;
/// stage.AddChild(kb);
/// </code>
/// </para>
/// </summary>
public class SoftKeyboard : Panel
{
    /// <summary>
    /// 软键盘当前显示的布局模式。
    /// </summary>
    public enum LayoutMode
    {
        /// <summary>字母 + 数字。</summary>
        Letters,
        /// <summary>符号布局。</summary>
        Symbols
    }

    /// <summary>
    /// 单个按键的语义动作。
    /// </summary>
    private enum KeyAction
    {
        Char,
        Backspace,
        Enter,
        Tab,
        Space,
        Shift,
        CapsLock,
        ToggleSymbols,
        Hide,
    }

    private sealed class KeyDef
    {
        public string LowerLabel = string.Empty;
        public string UpperLabel = string.Empty;
        public KeyAction Action = KeyAction.Char;
        public float WidthUnits = 1f;
        public Button? Button;
    }

    private const int VK_BACK = 8;
    private const int VK_TAB = 9;
    private const int VK_RETURN = 13;
    private const int VK_SHIFT = 16;
    private const int VK_CAPITAL = 20;
    private const int VK_SPACE = 32;
    private const int VK_ESCAPE = 27;

    private readonly Text.Factory _textFactory;
    private readonly Container _keysContainer;
    private readonly List<List<KeyDef>> _rows = [];

    private LayoutMode _mode = LayoutMode.Letters;
    private bool _shift;
    private bool _capsLock;

    private float _keyGap = 4f;
    private float _rowGap = 4f;
    private float _keyHeight = 40f;

    private DisplayObject? _lastFocused;

    // 自动 dismiss 相关
    private DisplayObject? _autoDismissTarget;
    private Action? _previousBlurHandler;
    private Action? _autoDismissBlurHook;

    /// <summary>
    /// 显式指定接收按键事件的目标对象。<br />
    /// 若为 null，则使用最近一次拥有焦点 (且不属于本软键盘) 的对象。
    /// </summary>
    public DisplayObject? Target { get; set; }

    /// <summary>
    /// 当用户点击一个可输入字符的按键时触发 (相当于 KeyPress)。
    /// </summary>
    public event Action<char>? OnCharInput;

    /// <summary>
    /// 当用户点击一个功能键时触发 (例如 Backspace, Enter, Tab)，参数为 Virtual-Key 码。
    /// </summary>
    public event Action<int>? OnVirtualKey;

    /// <summary>
    /// 当用户点击 Hide 按键时触发。
    /// </summary>
    public event Action? OnHideRequested;

    /// <summary>
    /// 当布局模式变化时触发 (Letters/Symbols)。
    /// </summary>
    public event Action<LayoutMode>? OnLayoutModeChanged;

    /// <summary>
    /// 当用户点击 Enter 键时触发。
    /// 若已订阅此事件, 则不会再向目标对象派发默认的 Enter (KeyPress '\r' / KeyDown VK_RETURN) 事件,
    /// 由订阅方决定 Enter 的行为 (例如提交搜索、关闭面板等)。
    /// </summary>
    public event Action<SoftKeyboard>? OnEnterPressed
    {
        add { _onEnterPressed += value; RefreshEnterStyle(); }
        remove { _onEnterPressed -= value; RefreshEnterStyle(); }
    }
    private Action<SoftKeyboard>? _onEnterPressed;

    /// <summary>
    /// 清空所有 Enter 回调订阅 (用于复用共享实例时重置)。
    /// </summary>
    public void ClearEnterHandlers()
    {
        _onEnterPressed = null;
        RefreshEnterStyle();
    }

    private string _enterText = "Enter";
    /// <summary>
    /// Enter 按键上显示的文字 (例如 "Enter", "Search", "Send", "Go", "完成")。
    /// </summary>
    public string EnterText
    {
        get => _enterText;
        set
        {
            value ??= "Enter";
            if (_enterText == value) return;
            _enterText = value;
            UpdateEnterKeyLabel();
        }
    }

    /// <summary>
    /// 当前布局模式 (字母 / 符号)。
    /// </summary>
    public LayoutMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            RefreshKeyLabels();
            OnLayoutModeChanged?.Invoke(_mode);
        }
    }

    /// <summary>
    /// 是否处于临时大写状态 (Shift)。点击字符键后会自动复位。
    /// </summary>
    public bool ShiftActive
    {
        get => _shift;
        set { if (_shift == value) return; _shift = value; RefreshKeyLabels(); }
    }

    /// <summary>
    /// 是否处于大写锁定状态 (Caps Lock)。
    /// </summary>
    public bool CapsLockActive
    {
        get => _capsLock;
        set { if (_capsLock == value) return; _capsLock = value; RefreshKeyLabels(); }
    }

    /// <summary>
    /// 创建软键盘。
    /// </summary>
    /// <summary>
    /// 无参构造：使用 <see cref="UIContext.Current"/> 默认文本工厂。
    /// </summary>
    public SoftKeyboard() : this(UIContext.Current.DefaultTextFactory) { }

    public SoftKeyboard(Text.Factory textFactory, float width = 640f, float height = 240f)
        : base(width, height)
    {
        _textFactory = textFactory;

        BackgroundColor = new RawColor4(0.12f, 0.12f, 0.14f, 0.96f);
        BorderColor = new RawColor4(0.30f, 0.30f, 0.35f, 1f);
        BorderWidth = 1f;
        BorderRadius = 6f;
        SetPadding(8f);

        // 软键盘自身不接受焦点，避免点击时夺走目标输入框的焦点。
        Interactive = true;
        AcceptFocus = false;

        _keysContainer = new Container();
        AddContent(_keysContainer);

        BuildLayoutDefinition();
        CreateButtons();
        LayoutKeys();
    }

    /// <summary>
    /// 显示软键盘。
    /// </summary>
    public void Show() => Visible = true;

    /// <summary>
    /// 隐藏软键盘 (并解除自动 dismiss 绑定)。
    /// </summary>
    public void Hide()
    {
        DetachAutoDismiss();
        Visible = false;
        OnHideRequested?.Invoke();
    }

    /// <summary>
    /// 切换显示状态。
    /// </summary>
    public void Toggle()
    {
        if (Visible) Hide(); else Show();
    }

    public override float Width
    {
        get => base.Width;
        set
        {
            if (base.Width == value) return;
            base.Width = value;
            LayoutKeys();
        }
    }

    public override float Height
    {
        get => base.Height;
        set
        {
            if (base.Height == value) return;
            base.Height = value;
            LayoutKeys();
        }
    }

    public override void Update(float deltaTime)
    {
        // 跟踪最近的焦点对象 (排除自身及其后代)，用于在点击按键时派发事件。
        var stage = GetStage();
        if (stage?.FocusedObject is { } fo
            && !ReferenceEquals(fo, this)
            && !ReferenceEquals(fo, stage)
            && !IsDescendant(fo))
        {
            _lastFocused = fo;
        }

        // 关键: 把 FocusTarget 指向外部输入目标。
        // 这样点击键盘上的任何按键时，Stage 的焦点遍历 (FindFirstFocusableTarget)
        // 在 SoftKeyboard 这一层会被截断, 直接返回真正的输入对象,
        // 而不会向上冒泡到 Stage 导致目标 TextBox 失焦。
        //
        // 注意: Panel 用 `new` 隐藏了 FocusTarget 并重定向到内部 _background,
        // 而 FindFirstFocusableTarget 通过 DisplayObject 静态类型访问 FocusTarget,
        // 是非虚的, 不会走 Panel 的 `new` 实现。
        // 因此必须显式写到基类 (DisplayObject) 的 FocusTarget 字段, 否则不会生效。
        var focusForward = Target ?? _lastFocused;
        if (focusForward is not null && !ReferenceEquals(focusForward, this) && !IsDescendant(focusForward))
        {
            ((DisplayObject)this).FocusTarget = focusForward;
        }

        base.Update(deltaTime);
    }

    private bool IsDescendant(DisplayObject obj)
    {
        var p = obj.Parent;
        while (p is not null)
        {
            if (ReferenceEquals(p, this)) return true;
            p = p.Parent;
        }
        return false;
    }

    #region Layout Definition

    private void BuildLayoutDefinition()
    {
        _rows.Clear();

        // Row 0: 数字 / 符号行
        _rows.Add(MakeCharRow(
            ("1", "!"), ("2", "@"), ("3", "#"), ("4", "$"), ("5", "%"),
            ("6", "^"), ("7", "&"), ("8", "*"), ("9", "("), ("0", ")"),
            FunctionKey("⌫", KeyAction.Backspace, 1.5f)));

        // Row 1: QWERTY
        _rows.Add(MakeCharRow(
            ("q", "Q"), ("w", "W"), ("e", "E"), ("r", "R"), ("t", "T"),
            ("y", "Y"), ("u", "U"), ("i", "I"), ("o", "O"), ("p", "P"),
            FunctionKey("Tab", KeyAction.Tab, 1.5f)));

        // Row 2: ASDFG
        _rows.Add(MakeCharRow(
            ("a", "A"), ("s", "S"), ("d", "D"), ("f", "F"), ("g", "G"),
            ("h", "H"), ("j", "J"), ("k", "K"), ("l", "L"),
            FunctionKey(_enterText, KeyAction.Enter, 2.5f)));

        // Row 3: ZXCVB
        var row3 = new List<KeyDef>
        {
            FunctionKey("Shift", KeyAction.Shift, 1.5f),
            CharKey("z", "Z"), CharKey("x", "X"), CharKey("c", "C"), CharKey("v", "V"),
            CharKey("b", "B"), CharKey("n", "N"), CharKey("m", "M"),
            CharKey(",", "<"), CharKey(".", ">"), CharKey("?", "/"),
            FunctionKey("⇪", KeyAction.CapsLock, 1.5f),
        };
        _rows.Add(row3);

        // Row 4: 控制行
        var row4 = new List<KeyDef>
        {
            FunctionKey("&123", KeyAction.ToggleSymbols, 1.5f),
            CharKey("'", "\""), CharKey(";", ":"),
            FunctionKey("Space", KeyAction.Space, 6f),
            CharKey("-", "_"), CharKey("=", "+"),
            FunctionKey("Hide", KeyAction.Hide, 1.5f),
        };
        _rows.Add(row4);
    }

    private static List<KeyDef> MakeCharRow(params object[] items)
    {
        var row = new List<KeyDef>(items.Length);
        foreach (var it in items)
        {
            row.Add(it switch
            {
                ValueTuple<string, string> t => CharKey(t.Item1, t.Item2),
                KeyDef k => k,
                _ => throw new ArgumentException("Unsupported row item: " + it)
            });
        }
        return row;
    }

    private static KeyDef CharKey(string lower, string upper) => new()
    {
        LowerLabel = lower,
        UpperLabel = upper,
        Action = KeyAction.Char,
        WidthUnits = 1f
    };

    private static KeyDef FunctionKey(string label, KeyAction action, float widthUnits) => new()
    {
        LowerLabel = label,
        UpperLabel = label,
        Action = action,
        WidthUnits = widthUnits
    };

    #endregion

    #region Build & Layout

    private void CreateButtons()
    {
        foreach (var row in _rows)
        {
            foreach (var k in row)
            {
                var label = _textFactory.Create(GetDisplayLabel(k));
                var btn = new Button(label, 40f, _keyHeight)
                {
                    BorderWidth = 1f,
                    BorderRadius = 4f,
                    NormalStyle = IsFunction(k.Action)
                        ? new BrushStyle(new RawColor4(0.22f, 0.22f, 0.26f, 1f))
                        : new BrushStyle(new RawColor4(0.32f, 0.32f, 0.36f, 1f)),
                    HoverStyle = new BrushStyle(new RawColor4(0.42f, 0.42f, 0.48f, 1f)),
                    PressedStyle = new BrushStyle(new RawColor4(0.18f, 0.18f, 0.22f, 1f)),
                    BorderStyle = new BrushStyle(new RawColor4(0.45f, 0.45f, 0.50f, 1f)),
                    AcceptFocus = false,
                };

                if (k.Action == KeyAction.Hide)
                {
                    btn.BorderStyle = new BrushStyle(new RawColor4(0.80f, 0.30f, 0.30f, 1f));
                    btn.TextColor = new RawColor4(0.95f, 0.80f, 0.80f, 1f);
                }

                var captured = k;
                btn.OnButtonClick += _ => HandleKeyPressed(captured);
                k.Button = btn;
                _keysContainer.AddChild(btn);
            }
        }
        RefreshEnterStyle();
    }

    private static bool IsFunction(KeyAction a) => a != KeyAction.Char;

    private void LayoutKeys()
    {
        if (_rows.Count == 0) return;

        float innerWidth = Width - PaddingLeft - PaddingRight;
        float innerHeight = Height - PaddingTop - PaddingBottom;

        // 计算每行总宽度单位 (含间隙)，按行内最大宽度做归一化。
        // 我们以"统一基础宽度"为标准：基础宽度 = (innerWidth - gaps) / maxUnits。
        float maxUnits = 0f;
        int maxKeyCount = 0;
        foreach (var row in _rows)
        {
            float u = 0f;
            foreach (var k in row) u += k.WidthUnits;
            if (u > maxUnits) maxUnits = u;
            if (row.Count > maxKeyCount) maxKeyCount = row.Count;
        }
        if (maxUnits <= 0f) return;

        // 用最长一行的间隙数量来决定基础键宽
        float gapsForRow = (maxKeyCount - 1) * _keyGap;
        float unitWidth = (innerWidth - gapsForRow) / maxUnits;
        if (unitWidth < 16f) unitWidth = 16f;

        // 计算行高
        float rowsTotal = _rows.Count * _keyHeight + (_rows.Count - 1) * _rowGap;
        float startY = Math.Max(0, (innerHeight - rowsTotal) / 2f);

        for (int rIdx = 0; rIdx < _rows.Count; rIdx++)
        {
            var row = _rows[rIdx];

            float rowGapTotal = (row.Count - 1) * _keyGap;
            float rowUnits = 0f;
            foreach (var k in row) rowUnits += k.WidthUnits;
            float rowWidth = rowUnits * unitWidth + rowGapTotal;
            float startX = Math.Max(0, (innerWidth - rowWidth) / 2f);

            float x = startX;
            float y = startY + rIdx * (_keyHeight + _rowGap);

            foreach (var k in row)
            {
                if (k.Button is null) continue;
                float w = k.WidthUnits * unitWidth;
                k.Button.Width = w;
                k.Button.Height = _keyHeight;
                k.Button.X = x;
                k.Button.Y = y;
                x += w + _keyGap;
            }
        }
    }

    private void RefreshKeyLabels()
    {
        foreach (var row in _rows)
        {
            foreach (var k in row)
            {
                if (k.Button is null) continue;
                k.Button.Text = GetDisplayLabel(k);
            }
        }
    }

    /// <summary>
    /// 根据是否订阅了 OnEnterPressed, 更新 Enter 键的视觉样式 (高亮/默认)。
    /// </summary>
    private void RefreshEnterStyle()
    {
        bool highlight = _onEnterPressed is not null;
        foreach (var row in _rows)
        {
            foreach (var k in row)
            {
                if (k.Action != KeyAction.Enter || k.Button is null) continue;
                if (highlight)
                {
                    k.Button.NormalStyle = new BrushStyle(new RawColor4(0.20f, 0.50f, 0.95f, 1f));
                    k.Button.HoverStyle = new BrushStyle(new RawColor4(0.35f, 0.62f, 1.00f, 1f));
                    k.Button.PressedStyle = new BrushStyle(new RawColor4(0.12f, 0.38f, 0.78f, 1f));
                    k.Button.BorderStyle = new BrushStyle(new RawColor4(0.55f, 0.78f, 1.00f, 1f));
                    k.Button.TextColor = new RawColor4(1f, 1f, 1f, 1f);
                }
                else
                {
                    k.Button.NormalStyle = new BrushStyle(new RawColor4(0.22f, 0.22f, 0.26f, 1f));
                    k.Button.HoverStyle = new BrushStyle(new RawColor4(0.42f, 0.42f, 0.48f, 1f));
                    k.Button.PressedStyle = new BrushStyle(new RawColor4(0.18f, 0.18f, 0.22f, 1f));
                    k.Button.BorderStyle = new BrushStyle(new RawColor4(0.45f, 0.45f, 0.50f, 1f));
                    k.Button.TextColor = new RawColor4(1f, 1f, 1f, 1f);
                }
            }
        }
    }

    private void UpdateEnterKeyLabel()
    {
        foreach (var row in _rows)
        {
            foreach (var k in row)
            {
                if (k.Action != KeyAction.Enter) continue;
                k.LowerLabel = _enterText;
                k.UpperLabel = _enterText;
                if (k.Button is not null) k.Button.Text = _enterText;
            }
        }
    }

    private string GetDisplayLabel(KeyDef k)
    {
        if (k.Action != KeyAction.Char) return k.LowerLabel;

        if (_mode == LayoutMode.Symbols)
        {
            // 在符号模式下，所有字符键都用 upper (即 shift 标记的字符)
            return k.UpperLabel;
        }

        bool upper = _shift ^ _capsLock;
        // CapsLock 仅作用于字母键
        if (_capsLock && !_shift && IsLetterKey(k))
            upper = true;
        else if (_capsLock && _shift && IsLetterKey(k))
            upper = false;

        return upper ? k.UpperLabel : k.LowerLabel;
    }

    private static bool IsLetterKey(KeyDef k)
        => k.LowerLabel.Length == 1 && char.IsLetter(k.LowerLabel[0]);

    #endregion

    #region Input Handling

    private void HandleKeyPressed(KeyDef k)
    {
        switch (k.Action)
        {
            case KeyAction.Shift:
                ShiftActive = !ShiftActive;
                return;

            case KeyAction.CapsLock:
                CapsLockActive = !CapsLockActive;
                return;

            case KeyAction.ToggleSymbols:
                Mode = _mode == LayoutMode.Letters ? LayoutMode.Symbols : LayoutMode.Letters;
                return;

            case KeyAction.Hide:
                Hide();
                return;

            case KeyAction.Backspace:
                DispatchKeyDown(VK_BACK);
                OnVirtualKey?.Invoke(VK_BACK);
                return;

            case KeyAction.Enter:
                if (_onEnterPressed is not null)
                {
                    // 用户自定义 Enter 行为, 跳过默认派发。
                    _onEnterPressed.Invoke(this);
                }
                else
                {
                    DispatchKeyPress('\r');
                    DispatchKeyDown(VK_RETURN);
                }
                OnVirtualKey?.Invoke(VK_RETURN);
                return;

            case KeyAction.Tab:
                DispatchKeyPress('\t');
                DispatchKeyDown(VK_TAB);
                OnVirtualKey?.Invoke(VK_TAB);
                return;

            case KeyAction.Space:
                DispatchKeyPress(' ');
                DispatchKeyDown(VK_SPACE);
                OnVirtualKey?.Invoke(VK_SPACE);
                return;

            case KeyAction.Char:
                {
                    var label = GetDisplayLabel(k);
                    if (label.Length > 0)
                    {
                        char c = label[0];
                        DispatchKeyPress(c);
                        OnCharInput?.Invoke(c);
                    }
                    // Shift 是一次性的，按完字符后复位 (CapsLock 不变)
                    if (_shift) ShiftActive = false;
                    return;
                }
        }
    }

    private DisplayObject? ResolveTarget()
    {
        if (Target is not null) return Target;
        if (_lastFocused is not null) return _lastFocused;

        var stage = GetStage();
        if (stage?.FocusedObject is { } fo
            && !ReferenceEquals(fo, this)
            && !ReferenceEquals(fo, stage)
            && !IsDescendant(fo))
        {
            return fo;
        }
        return null;
    }

    private void DispatchKeyPress(char c)
    {
        var target = ResolveTarget();
        if (target?.OnKeyPress is null) return;

        var evt = new DisplayObjectEvent
        {
            Target = target,
            CurrentTarget = target,
            Data = new DisplayObjectEventData
            {
                KeyChar = c
            }
        };
        target.OnKeyPress.Invoke(evt);
    }

    private void DispatchKeyDown(int keyCode, bool ctrl = false, bool alt = false, bool shift = false)
    {
        var target = ResolveTarget();
        if (target?.OnKeyDown is null) return;

        var evt = new DisplayObjectEvent
        {
            Target = target,
            CurrentTarget = target,
            Data = new DisplayObjectEventData
            {
                KeyCode = keyCode,
                Ctrl = ctrl,
                Alt = alt,
                Shift = shift,
            }
        };
        target.OnKeyDown.Invoke(evt);
    }

    #endregion

    #region ShowFor / Auto-Positioning / Auto-Dismiss

    /// <summary>
    /// 共享实例 (用于 <see cref="ShowFor(Stage, DisplayObject, Text.Factory?)"/>)。
    /// </summary>
    private static SoftKeyboard? s_shared;

    /// <summary>
    /// 默认 Text.Factory，用于 <see cref="ShowFor(Stage, DisplayObject, Text.Factory?)"/>。
    /// 若未通过参数传入，则使用此处配置的工厂。
    /// </summary>
    public static Text.Factory? DefaultTextFactory { get; set; }

    /// <summary>
    /// 弹出软键盘到指定对象附近。
    /// <para>
    /// 自动根据 <paramref name="o"/> 的世界坐标 / 大小，以及 <paramref name="stage"/> 的尺寸，
    /// 选择合适位置（优先放在对象下方，空间不足时放在上方），并将 X 钳制在舞台范围内。
    /// </para>
    /// <para>
    /// 当 <paramref name="o"/> 失去焦点 (OnBlur) 时自动隐藏；用户也可点击键盘上的 "Hide" 按键手动关闭。
    /// </para>
    /// </summary>
    /// <param name="stage">目标 Stage。</param>
    /// <param name="o">触发软键盘的对象 (通常是 TextBox)。</param>
    /// <param name="textFactory">用于创建按键文本，若为 null 则使用 <see cref="DefaultTextFactory"/>。</param>
    /// <param name="enterText">
    /// 自定义 Enter 按键的显示文字 (例如 "Search", "Send", "Go", "完成")。
    /// 若为 null 则使用上次设置 (默认为 "Enter")。
    /// </param>
    /// <param name="onEnter">
    /// 自定义 Enter 行为的回调。若不为 null, 点击 Enter 时不再向目标派发默认的回车事件,
    /// 而是直接调用此回调 (参数为当前 SoftKeyboard 实例, 可通过 <c>kb.Target</c> 拿到输入对象)。
    /// </param>
    /// <returns>当前显示的 SoftKeyboard 实例。</returns>
    public static SoftKeyboard ShowFor(
        Stage stage,
        DisplayObject o,
        Text.Factory? textFactory = null,
        string? enterText = null,
        Action<SoftKeyboard>? onEnter = null)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(o);

        var factory = textFactory
            ?? DefaultTextFactory
            ?? throw new InvalidOperationException(
                "SoftKeyboard.ShowFor 需要一个 Text.Factory，请传入参数或设置 SoftKeyboard.DefaultTextFactory。");

        var kb = s_shared ??= new SoftKeyboard(factory);

        // 添加到 Stage 顶层 (置于其他子节点之上)
        if (!ReferenceEquals(kb.Parent, stage))
        {
            kb.Parent?.RemoveChild(kb);
            stage.AddChild(kb);
        }
        else
        {
            // 已在 stage 中，移到末尾以确保在最上层
            stage.RemoveChild(kb);
            stage.AddChild(kb);
        }

        // 重置上次 ShowFor 设置的 Enter 行为, 然后按本次参数应用。
        kb.ClearEnterHandlers();
        kb.EnterText = enterText ?? "Enter";
        if (onEnter is not null) kb.OnEnterPressed += onEnter;

        kb.Target = o;
        // 必须写到基类字段, 详见 Update() 中的注释。
        ((DisplayObject)kb).FocusTarget = o;
        kb.Show();
        kb.PositionNear(stage, o);
        kb.AttachAutoDismiss(o);

        return kb;
    }

    /// <summary>
    /// 隐藏共享的软键盘实例 (如果存在)。
    /// </summary>
    public static void HideShared()
    {
        s_shared?.Hide();
    }

    /// <summary>
    /// 将软键盘定位到目标对象附近，并保证在 stage 的可视范围内。
    /// </summary>
    public void PositionNear(Stage stage, DisplayObject target, float margin = 6f)
    {
        var origin = target.ToWorldPoint(0, 0);
        float tw = target.Width;
        float th = target.Height;
        float kw = Width;
        float kh = Height;

        float stageW = stage.Width > 0 ? stage.Width : (kw + origin.X + tw);
        float stageH = stage.Height > 0 ? stage.Height : (kh + origin.Y + th);

        // 垂直: 优先放在下方; 若下方空间不足且上方更宽裕, 则放上方
        float spaceBelow = stageH - (origin.Y + th) - margin;
        float spaceAbove = origin.Y - margin;

        float y;
        if (spaceBelow >= kh)
        {
            y = origin.Y + th + margin;
        }
        else if (spaceAbove >= kh)
        {
            y = origin.Y - kh - margin;
        }
        else
        {
            // 两边都不够: 选择空间更大的一边并贴边
            y = spaceBelow >= spaceAbove
                ? Math.Max(margin, stageH - kh - margin)
                : margin;
        }

        // 水平: 试着与目标左边对齐, 然后钳制到 stage 内
        float x = origin.X;
        if (x + kw > stageW - margin) x = stageW - kw - margin;
        if (x < margin) x = margin;

        // 父级偏移 (软键盘的父是 stage, world == local; 但保险起见考虑)
        if (Parent is { } p && !(p is Stage))
        {
            var parentOrigin = p.ToWorldPoint(0, 0);
            x -= parentOrigin.X;
            y -= parentOrigin.Y;
        }

        X = x;
        Y = y;
    }

    /// <summary>
    /// 绑定 <paramref name="target"/> 的 OnBlur 事件，使其失焦时自动隐藏键盘。
    /// </summary>
    private void AttachAutoDismiss(DisplayObject target)
    {
        // 解除上一次绑定 (如果有)
        DetachAutoDismiss();

        _autoDismissTarget = target;
        _previousBlurHandler = target.OnBlur;

        _autoDismissBlurHook = () =>
        {
            // 调用之前的 handler (保留链式调用)
            _previousBlurHandler?.Invoke();

            // 焦点可能转移到了我们自己的子按钮 — 此时不应 dismiss
            var stage = GetStage();
            if (stage?.FocusedObject is { } fo
                && (ReferenceEquals(fo, this) || IsDescendant(fo)))
            {
                return;
            }
            Hide();
        };

        target.OnBlur = _autoDismissBlurHook;
    }

    private void DetachAutoDismiss()
    {
        if (_autoDismissTarget is null || _autoDismissBlurHook is null) return;

        // 仅在当前 OnBlur 仍指向我们安装的 hook 时还原 (避免覆盖用户后续设置)
        if (ReferenceEquals(_autoDismissTarget.OnBlur, _autoDismissBlurHook))
        {
            _autoDismissTarget.OnBlur = _previousBlurHandler;
        }

        _autoDismissTarget = null;
        _autoDismissBlurHook = null;
        _previousBlurHandler = null;
    }

    #endregion
}

