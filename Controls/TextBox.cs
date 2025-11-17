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
    private readonly Text _textDisplay;
    private readonly Graphics _caret; // 光标
    private readonly Text.Factory _textFactory; // 用于创建和测量文本

    // --- 内部状态 ---
    private readonly StringBuilder _textBuilder = new();
    private bool _isFocused = false;
    private int _caretIndex = 0; // 光标在字符串中的索引
    private float _blinkTimer = 0f;
    private const float BlinkRate = 0.5f; // 光标闪烁速率 (秒)

    // --- 样式属性 ---
    private float _boxWidth = 200f;
    private float _boxHeight = 30f;
    private RawColor4 _backgroundColor = new(0.1f, 0.1f, 0.1f, 1.0f);
    private RawColor4 _borderColor = new(0.5f, 0.5f, 0.5f, 1.0f);
    private RawColor4 _focusedBorderColor = new(0.0f, 0.6f, 1.0f, 1.0f);
    private float _borderWidth = 1f;

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

        // 1. 创建背景
        _background = new Graphics
        {
            Interactive = true,
            FocusTarget = this,
        };
        UpdateBackground();
        base.AddChild(_background);

        // 2. 创建文本显示
        _textDisplay = _textFactory.Create("");
        _textDisplay.X = 5f; // 5px 内边距
        _textDisplay.Y = (_boxHeight - _textDisplay.FontSize) / 2; // 垂直居中
        _textDisplay.MaxWidth = _boxWidth - 10f; // 限制宽度
        base.AddChild(_textDisplay);

        // 3. 创建光标 (Caret)
        _caret = new Graphics
        {
            Visible = false,
            FillColor = _textFactory.FillColor.ToRawColor4() // 使用文本颜色
        };
        _caret.DrawRectangle(0, -1f, 2f, _textDisplay.FontSize + 2); // 光标高度
        _caret.Y = (_boxHeight - _textDisplay.FontSize) / 2;
        base.AddChild(_caret);

        // 4. 设置交互
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

    // --- 核心事件处理器 ---

    private void HandleKeyPress(DisplayObjectEvent evt)
    {
        if (evt.Data is null || evt.Data.KeyChar == '\0') return;

        char c = evt.Data.KeyChar;

        // 过滤掉控制字符 (例如 backspace, enter, tab)
        if (char.IsControl(c)) return;

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

    // --- 更新方法 ---

    private void UpdateTextAndCaret()
    {
        UpdateTextDisplay();
        UpdateCaretPosition();
    }

    private void UpdateTextDisplay()
    {
        _textDisplay.Content = _textBuilder.ToString();
    }

    /// <summary>
    /// 计算并更新光标的 X 坐标。
    /// </summary>
    private void UpdateCaretPosition()
    {
        float caretX = 0f;
        if (_caretIndex > 0)
        {
            try
            {
                // 使用 Text.Factory 和 Text.GetTextFormat 来测量子字符串 
                var format = _textDisplay.GetTextFormat();

                // 创建一个临时的 TextLayout 来测量子字符串
                var tempLayout = new TextLayout(
                    _textFactory.DwfInstance,
                    _textBuilder.ToString(0, _caretIndex), // 测量从开头到光标位置的文本
                    format,
                    float.MaxValue,
                    float.MaxValue);

                // 获取测量宽度
                caretX = tempLayout.Metrics.WidthIncludingTrailingWhitespace;
                tempLayout.Dispose();
            }
            catch (Exception ex)
            {
                // 异常回退 (例如在 DirectWrite 尚未初始化时)
                Console.WriteLine("Caret update failed: " + ex.Message);
                caretX = _caretIndex * _textDisplay.FontSize * 0.6f; // 粗略估算
            }
        }

        // 设置光标位置 (加上文本的内边距)
        _caret.X = _textDisplay.X + caretX;

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
}