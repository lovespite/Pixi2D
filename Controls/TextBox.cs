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

    // --- 样式属性 ---
    private float _boxWidth = 200f;
    private float _boxHeight = 30f;
    private RawColor4 _backgroundColor = new(0.1f, 0.1f, 0.1f, 1.0f);
    private RawColor4 _borderColor = new(0.5f, 0.5f, 0.5f, 1.0f);
    private RawColor4 _focusedBorderColor = new(0.0f, 0.6f, 1.0f, 1.0f);
    private float _borderWidth = 1f;
    private float _paddingX = 5f;
    private float _paddingY = 2f;

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
            UpdateTextDisplay();
            UpdateCaretPosition();
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
                UpdateTextDisplay();
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
            // 垂直居中
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
    }

    private void HandleMouseDown(DisplayObjectEvent evt)
    {
        this.Focus();
        evt.StopPropagation(); // 停止冒泡，防止点击穿透
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

        switch (evt.Data.KeyCode)
        {
            case VK_LEFT: // Left Arrow
                _caretIndex = Math.Max(0, _caretIndex - 1);
                UpdateCaretPosition();
                break;

            case VK_RIGHT: // Right Arrow
                _caretIndex = Math.Min(_textBuilder.Length, _caretIndex + 1);
                UpdateCaretPosition();
                break;

            case VK_HOME: // Home
                _caretIndex = 0;
                UpdateCaretPosition();
                break;

            case VK_END: // End
                _caretIndex = _textBuilder.Length;
                UpdateCaretPosition();
                break;
        }
    }

    #endregion

    #region Rendering Methods
    // --- 更新方法 ---

    private void UpdateTextAndCaret()
    {
        UpdateTextDisplay();
        UpdateCaretPosition();
    }

    private void UpdateTextDisplay()
    {
        _textDisplay.Content = _textBuilder.ToString();

        // 如果是多行，我们还需要更新裁剪高度
        if (_multiline)
        {
            var stage = GetStage();
            if (stage != null)
            {
                // 强制更新文本布局以获取高度
                var rect = _textDisplay.GetTextRect(true, stage.GetCachedRenderTarget());
                float textHeight = rect.Height;

                // 更新裁剪容器高度，但不超过文本框的总高度
                float newClipHeight = Math.Max(
                    _textFactory.FontSize + 2, // 最小高度
                    Math.Min(textHeight + _paddingY, _boxHeight - (_paddingY * 2)) // 最大高度
                );
                _textClipContainer.Y = _paddingY;
                _textClipContainer.ClipHeight = newClipHeight;
            }
        }
        else
        {
            // 单行，重置裁剪高度
            float textHeight = _textFactory.FontSize + 2f;
            _textClipContainer.ClipHeight = textHeight + _paddingY;
            _textClipContainer.Y = (_boxHeight - textHeight) / 2;
        }
    }

    /// <summary>
    /// 计算并更新光标的 X 坐标。
    /// </summary>
    private void UpdateCaretPosition()
    {
        float caretX = 0f, caretY = 0f, caretHeight = _textFactory.FontSize;

        var stage = GetStage();
        var rt = stage?.GetCachedRenderTarget();
        var textLayout = _textDisplay.GetTextLayout(rt); // 获取 TextLayout

        if (textLayout != null)
        {
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