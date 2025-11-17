using Pixi2D.Core;
using Pixi2D.Events;
using Pixi2D.Extensions;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Text;

namespace Pixi2D.Controls;

/// <summary>
/// 一个文本输入框控件，用于接收用户键盘输入。
/// </summary>
public class TextBox : Container
{
    // 常见的 Virtual-Key 码
    public const int VK_BACKSPACE = 8;
    public const int VK_DELETE = 46;
    public const int VK_LEFT = 37;
    public const int VK_RIGHT = 39;
    public const int VK_HOME = 36;
    public const int VK_END = 35;
    public const int VK_UP = 38;   // 新增：上箭头
    public const int VK_DOWN = 40; // 新增：下箭头

    // --- 子控件 ---
    private readonly Graphics _background;
    private readonly Container _textClipContainer; // 用于裁剪
    private readonly Container _textContainer;     // 用于滚动
    private readonly Text _textDisplay;
    private readonly Graphics _caret; // 光标
    private readonly Text.Factory _textFactory; // 用于创建和测量文本

    // --- 内部状态 ---
    private readonly StringBuilder _textBuilder = new();
    private bool _isFocused = false;
    private int _caretIndex = 0; // 光标在字符串中的索引
    private float _blinkTimer = 0f;
    private const float BlinkRate = 0.5f; // 光标闪烁速率 (秒)
    private bool _multiline = false;
    private bool _caretPositionDirty = true;
    private bool _displayStateDirty = true;

    // --- 样式属性 ---
    private float _boxWidth = 200f;
    private float _boxHeight = 30f;
    private RawColor4 _backgroundColor = new(0.1f, 0.1f, 0.1f, 1.0f);
    private RawColor4 _borderColor = new(0.5f, 0.5f, 0.5f, 1.0f);
    private RawColor4 _focusedBorderColor = new(0.0f, 0.6f, 1.0f, 1.0f);
    private readonly float _borderWidth = 1f;
    private readonly float _paddingX = 5f;
    private readonly float _paddingY = 2f;

    public override float Height
    {
        get => _boxHeight;
        set => _boxHeight = value;
    }

    public override float Width
    {
        get => _boxWidth;
        set => _boxWidth = value;
    }

    /// <summary>
    /// 获取或设置输入框中的文本内容。
    /// </summary>
    public string Text
    {
        get => _textBuilder.ToString();
        set
        {
            _textBuilder.Clear();
            _textBuilder.Append(value);
            // 确保光标位置有效
            _caretIndex = Math.Clamp(value.Length, 0, _textBuilder.Length);
            TryUpdateTextDisplay();
            _caretPositionDirty = true; // 标记为脏
            TryUpdateCaretPosition();
        }
    }

    /// <summary>
    /// 获取或设置是否启用多行模式。
    /// 默认 false (单行，水平滚动)。
    /// true (多行，垂直换行，内容裁剪)。
    /// </summary>
    public bool Multiline
    {
        get => _multiline;
        set
        {
            if (_multiline != value)
            {
                _multiline = value;
                _textDisplay.WordWrap = value; // 启用或禁用文本换行
                if (value)
                {
                    // 多行: 按宽度换行，重置滚动
                    _textDisplay.MaxWidth = _boxWidth - (_paddingX * 2);
                    _textContainer.X = 0;
                }
                else
                {
                    // 单行: 不换行，无限宽度
                    _textDisplay.MaxWidth = float.MaxValue;
                }
                _caretPositionDirty = true;
                _displayStateDirty = true;
                UpdateTextAndCaret();
            }
        }
    }

    /// <summary>
    /// 创建一个新的文本输入框。
    /// </summary>
    /// <param name="textFactory">用于创建内部 Text 对象的工厂。</param>
    /// <param name="width">输入框宽度。</param>
    /// <param name="height">输入框高度。</param>
    public TextBox(Text.Factory textFactory, float width = 200f, float height = 30f)
    {
        _textFactory = textFactory;
        _boxWidth = width;
        _boxHeight = height;

        float textHeight = _textFactory.FontSize + 2f; // 估算单行文本高度
        // 1. 创建背景
        _background = new Graphics
        {
            Interactive = true,
            FocusTarget = this,
        };
        UpdateBackground();
        AddChild(_background);

        // 2. 创建裁剪容器
        _textClipContainer = new Container
        {
            X = _paddingX,
            // 垂直居中 (默认单行)
            Y = (_boxHeight - textHeight) / 2,
            ClipContent = true,
            ClipWidth = _boxWidth - (_paddingX * 2),
            ClipHeight = textHeight + _paddingY // 高度足以容纳光标
        };
        AddChild(_textClipContainer);

        // 3. 创建滚动容器 (在裁剪容器内)
        _textContainer = new Container();
        _textClipContainer.AddChild(_textContainer);

        // 4. 创建文本显示 (在滚动容器内)
        _textDisplay = _textFactory.Create("");
        _textDisplay.WordWrap = false; // 默认: 不换行
        _textDisplay.MaxWidth = float.MaxValue; // 默认: 无限宽度
        _textContainer.AddChild(_textDisplay);

        // 5. 创建光标 (Caret) (在滚动容器内)
        _caret = new Graphics
        {
            Visible = false,
            FillColor = _textFactory.FillColor.ToRawColor4() // 使用文本颜色
        };
        // Y = -1f, 高度 + 2 确保光标略高于和低于文本
        _caret.DrawRectangle(0, -1f, 2f, _textDisplay.FontSize + 2);
        _caret.Y = 0; // Y 坐标由 _textContainer 控制
        _textContainer.AddChild(_caret);

        // 6. 设置交互
        Interactive = true; // 使 TextBox 容器本身可交互
        AcceptFocus = true; // 允许接受焦点
        _background.OnMouseDown += HandleMouseDown; // 背景也响应点击

        // 注册事件
        // 当点击时，请求 Stage 设置我们为焦点
        this.OnMouseDown += HandleMouseDown;

        // 挂接键盘事件
        this.OnKeyDown += HandleKeyDown;
        this.OnKeyUp += HandleKeyUp;
        this.OnKeyPress += HandleKeyPress;
        this.OnMouseWheel += HandleMouseWheel; // 新增：挂接滚轮事件
    }

    public override void Dispose()
    {
        // Unsubscribe from all events
        this.OnMouseDown -= HandleMouseDown;
        this.OnKeyDown -= HandleKeyDown;
        this.OnKeyUp -= HandleKeyUp;
        this.OnKeyPress -= HandleKeyPress;
        this.OnMouseWheel -= HandleMouseWheel;
        _background.OnMouseDown -= HandleMouseDown;

        base.Dispose(); // This will dispose children like _background, _textClipContainer, etc.
    }

    private void HandleMouseWheel(DisplayObjectEvent evt)
    {
        // 仅在多行、有焦点且事件数据存在时滚动
        if (!_isFocused || !_multiline || evt.Data == null) return;

        float deltaY = evt.Data.MouseWheelDeltaY; // 假设：正值 = 向上滚, 负值 = 向下滚
        if (deltaY == 0) return;

        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout == null) return;

        float textHeight = layout.Metrics.Height;
        float clipHeight = _textClipContainer.ClipHeight ?? 0f;

        // Max scroll (content is moved up, so Y is negative)
        // 0 is the top (no scroll)
        float maxScrollY = Math.Max(0, textHeight - clipHeight);

        // 如果内容未溢出，则无需滚动
        if (maxScrollY <= 0) return;

        // --- 计算滚动量 ---
        // 每次滚动 3 行
        float lineHeight = _textFactory.FontSize;
        if (lineHeight <= 0) lineHeight = 16f; // 默认行高
        float scrollAmount = (lineHeight * 3) * Math.Sign(deltaY);

        // --- 应用新位置 ---
        // 向上滚 (deltaY > 0)，内容向下移 (Y 增加，朝 0 靠近)
        // 向下滚 (deltaY < 0)，内容向上移 (Y 减少，朝 -maxScrollY 靠近)
        float newY = _textContainer.Y + scrollAmount;

        // --- 限制滚动范围 ---
        // _textContainer.Y 应该在 [-maxScrollY, 0] 之间
        _textContainer.Y = Math.Clamp(newY, -maxScrollY, 0f);

        // 阻止事件冒泡 (例如，防止舞台缩放)
        evt.StopPropagation();

        // 重置光标闪烁
        _blinkTimer = 0f;
        _caret.Visible = _isFocused;
    }

    private void HandleMouseDown(DisplayObjectEvent evt)
    {
        this.Focus();
        evt.StopPropagation(); // 停止冒泡，防止点击穿透

        // --- 新增：点击设置光标位置 ---
        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout == null) return;

        // 1. 计算点击位置相对于 _textDisplay 的坐标
        // evt.LocalPosition 是相对于 _background (即 TextBox 内部的 0,0)
        float clickX = evt.LocalPosition.X - (_textClipContainer.X + _textContainer.X);
        float clickY = evt.LocalPosition.Y - (_textClipContainer.Y + _textContainer.Y);

        try
        {
            // 2. 使用 HitTestPoint 找到最近的文本位置
            var hitTestMetrics = layout.HitTestPoint(clickX, clickY, out var isTrailingHit, out var isInside);

            // 3. 设置光标索引
            int newCaretIndex = hitTestMetrics.TextPosition + (isTrailingHit ? 1 : 0);
            _caretIndex = Math.Clamp(newCaretIndex, 0, _textBuilder.Length);

            // 4. 更新光标
            TryUpdateCaretPosition();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MouseDown HitTestPoint failed: " + ex.Message);
        }
    }

    #region Core Event Handlers
    // --- 核心事件处理器 ---

    private void HandleKeyPress(DisplayObjectEvent evt)
    {
        if (evt.Data is null || evt.Data.KeyChar == '\0') return;

        char c = evt.Data.KeyChar;

        // 过滤掉控制字符 (例如 backspace, enter, tab)
        // (多行模式下也许要允许 Enter)
        if (char.IsControl(c) && (!_multiline || c != '\r')) return;

        if (_multiline && c == '\r') c = '\n'; // 转换为换行符

        _textBuilder.Insert(_caretIndex, c);
        _caretIndex++;
        UpdateTextAndCaret();
    }

    private void HandleKeyDown(DisplayObjectEvent evt)
    {
        if (evt.Data is null) return;

        switch (evt.Data.KeyCode)
        {
            case VK_BACKSPACE: // Backspace
                if (_caretIndex > 0)
                {
                    _textBuilder.Remove(_caretIndex - 1, 1);
                    _caretIndex--;
                    UpdateTextAndCaret();
                }
                break;

            case VK_DELETE: // Delete
                if (_caretIndex < _textBuilder.Length)
                {
                    _textBuilder.Remove(_caretIndex, 1);
                    UpdateTextAndCaret(); // 光标位置不变，但文本和光标 X 坐标需要更新
                }
                break;
        }
    }

    private void HandleKeyUp(DisplayObjectEvent evt)
    {
        if (evt.Data is null) return;

        bool caretMoved = false;

        switch (evt.Data.KeyCode)
        {
            case VK_LEFT: // Left Arrow
                _caretIndex = Math.Max(0, _caretIndex - 1);
                caretMoved = true;
                break;

            case VK_RIGHT: // Right Arrow
                _caretIndex = Math.Min(_textBuilder.Length, _caretIndex + 1);
                caretMoved = true;
                break;

            case VK_HOME: // Home
                _caretIndex = 0;
                caretMoved = true;
                break;

            case VK_END: // End
                _caretIndex = _textBuilder.Length;
                caretMoved = true;
                break;

            case VK_UP: // Up Arrow
                if (_multiline)
                {
                    MoveCaretVertical(-1); // -1 for Up
                    caretMoved = true; // MoveCaretVertical 内部会调用 UpdateCaretPosition
                }
                break;

            case VK_DOWN: // Down Arrow
                if (_multiline)
                {
                    MoveCaretVertical(1); // 1 for Down
                    caretMoved = true; // MoveCaretVertical 内部会调用 UpdateCaretPosition
                }
                break;
        }

        if (caretMoved)
        {
            // 如果 MoveCaretVertical 没有被调用，则手动更新
            if (evt.Data.KeyCode != VK_UP && evt.Data.KeyCode != VK_DOWN)
            {
                TryUpdateCaretPosition();
            }
        }
    }

    #endregion

    #region Rendering Methods
    // --- 更新方法 ---

    /// <summary>
    /// (新增) 处理多行模式下的上/下光标移动。
    /// </summary>
    /// <param name="direction">-1 表示上, 1 表示下。</param>
    private void MoveCaretVertical(int direction)
    {
        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout == null) return;

        try
        {
            // 1. 获取当前光标的 (X, Y) 坐标
            var metrics = layout.HitTestTextPosition(_caretIndex, false, out float currentX, out float currentY);
            float lineHeight = metrics.Height; // 使用当前行高作为参考

            if (lineHeight <= 0) lineHeight = _textFactory.FontSize; // 回退

            // 2. 计算目标 Y 坐标
            float targetY = currentY;
            if (direction < 0) // Up
            {
                targetY = currentY - (lineHeight * 0.5f); // 目标 Y 位于上一行的中间
            }
            else // Down
            {
                targetY = currentY + (lineHeight * 1.5f); // 目标 Y 位于下一行的中间
            }

            // 3. 使用 HitTestPoint 找到目标 (X, Y) 处最近的文本索引
            // 我们使用 currentX 和 targetY
            var hitTestMetrics = layout.HitTestPoint(currentX, targetY, out var isTrailingHit, out var isInside);

            // 4. 设置新的光标索引
            int newCaretIndex = hitTestMetrics.TextPosition + (isTrailingHit ? 1 : 0);

            _caretIndex = Math.Clamp(newCaretIndex, 0, _textBuilder.Length);
            TryUpdateCaretPosition();
        }
        catch (Exception ex)
        {
            Console.WriteLine("MoveCaretVertical failed: " + ex.Message);
        }
    }

    private void UpdateTextAndCaret()
    {
        TryUpdateTextDisplay();
        TryUpdateCaretPosition();
    }

    private void TryUpdateTextDisplay()
    {
        _textDisplay.Content = _textBuilder.ToString();

        // 如果是多行，我们还需要更新裁剪高度
        if (_multiline)
        {
            var stage = GetStage();
            if (stage is not null)
            {
                // 多行模式，Y 始终在顶部
                _textClipContainer.Y = _paddingY;
                _textClipContainer.ClipHeight = _boxHeight - (_paddingY * 2); // 裁剪区域始终是框的内部高度
                _displayStateDirty = false;
            }
            else
            {
                _displayStateDirty = true; // 标记为脏，等待下一次更新
            }
        }
        else
        {
            // 单行，重置裁剪高度并垂直居中
            float textHeight = _textFactory.FontSize + 2f;
            _textClipContainer.ClipHeight = textHeight + _paddingY;
            _textClipContainer.Y = (_boxHeight - textHeight) / 2;
        }
    }

    /// <summary>
    /// 计算并更新光标的 X 坐标。
    /// </summary>
    private void TryUpdateCaretPosition()
    {
        float caretX = 0f, caretY = 0f, caretHeight = _textFactory.FontSize;

        var stage = GetStage();
        var rt = stage?.GetCachedRenderTarget();
        var textLayout = _textDisplay.GetTextLayout(rt); // 获取 TextLayout

        if (textLayout is null)
        {
            _caretPositionDirty = true; // 失败，保持脏标记
            return;
        }

        try
        {
            // 使用 HitTestTextPosition 获取光标在文本索引处的准确 (X, Y) 坐标 
            var metrics = textLayout.HitTestTextPosition(_caretIndex, false, out float htmX, out float htmY);
            caretX = metrics.Left;
            caretY = metrics.Top;

            if (metrics.Height > 0)
            {
                caretHeight = metrics.Height;
            }
        }
        catch (Exception ex)
        {
            // 异常回退
            Console.WriteLine("Caret HitTest failed: " + ex.Message);
            caretX = _caretIndex * _textDisplay.FontSize * 0.6f; // 粗略估算
        }

        // --- 更新光标图形 ---
        _caret.Clear();
        // Y = -1f, 高度 + 2 确保光标略高于和低于文本
        _caret.DrawRectangle(0, -1f, 2f, caretHeight + 2f);
        _caret.X = caretX;
        _caret.Y = caretY;

        // --- 滚动逻辑 ---
        float clipWidth = _textClipContainer.ClipWidth ?? 0f;
        float clipHeight = _textClipContainer.ClipHeight ?? 0f;
        float textContainerX = _textContainer.X;
        float textContainerY = _textContainer.Y;

        float viewStartX = -textContainerX;
        float viewEndX = viewStartX + clipWidth;
        float viewStartY = -textContainerY;
        float viewEndY = viewStartY + clipHeight;

        float caretMargin = 4f; // 留出一点边距

        // --- 垂直滚动逻辑 (多行) ---
        if (_multiline)
        {
            if (caretY < viewStartY + caretMargin)
            {
                // 光标在可视区域上方，向上滚动
                _textContainer.Y = -Math.Max(0, caretY - caretMargin);
            }
            else if (caretY + caretHeight > viewEndY - caretMargin)
            {
                // 光标在可视区域下方，向下滚动
                _textContainer.Y = -(caretY + caretHeight - clipHeight + caretMargin);
            }
            // 否则，光标在视图内，不滚动
        }
        // --- 水平滚动逻辑 (单行) ---
        else
        {
            if (caretX < viewStartX + caretMargin)
            {
                // 光标在可视区域左侧
                _textContainer.X = -Math.Max(0, caretX - caretMargin);
            }
            else if (caretX > viewEndX - caretMargin)
            {
                // 光标在可视区域右侧
                _textContainer.X = -(caretX - clipWidth + caretMargin);
            }
            // 否则，光标在视图内，不滚动
        }
        // --- 结束滚动逻辑 ---

        // 重置光标闪烁
        _blinkTimer = 0f;
        _caret.Visible = _isFocused;

        _caretPositionDirty = false;
        return;
    }

    /// <summary>
    /// 更新背景的视觉状态 (边框颜色)。
    /// </summary>
    private void UpdateBackground()
    {
        _background.Clear();
        _background.FillColor = _backgroundColor;
        _background.StrokeWidth = _borderWidth;
        _background.StrokeColor = _isFocused ? _focusedBorderColor : _borderColor;
        _background.DrawRoundedRectangle(0, 0, _boxWidth, _boxHeight, 3f, 3f);
    }

    /// <summary>
    /// 每帧更新 (用于检查焦点和光标闪烁)。
    /// </summary>
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime); // 更新子控件 (Text, Graphics)
        if (_caretPositionDirty)
        {
            TryUpdateCaretPosition();
        }
        if (_displayStateDirty)
        {
            TryUpdateTextDisplay();
        }

        // 检查焦点状态是否改变 
        bool hasFocus = IsFocused();

        if (hasFocus != _isFocused)
        {
            _isFocused = hasFocus;
            UpdateBackground(); // 更新边框颜色
        }

        // 处理光标闪烁
        if (_isFocused)
        {
            _blinkTimer += deltaTime;
            if (_blinkTimer > BlinkRate * 2)
            {
                _blinkTimer = 0f;
            }
            _caret.Visible = _blinkTimer < BlinkRate;
        }
        else
        {
            _caret.Visible = false;
        }
    }

    #endregion
}