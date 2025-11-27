using Pixi2D.Core;
using Pixi2D.Events;
using Pixi2D.Extensions;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Diagnostics;
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
    public const int VK_UP = 38;
    public const int VK_DOWN = 40;
    public const int VK_TAB = 9;
    public const int VK_ENTER = 13;
    // 用于剪贴板和全选
    public const int VK_C = 0x43;
    public const int VK_V = 0x56;
    public const int VK_X = 0x58;
    public const int VK_A = 0x41;

    // --- 子控件  ---
    private readonly Graphics _background;
    private readonly Container _textClipContainer; // 用于裁剪
    private readonly Container _textContainer;     // 用于滚动
    private readonly Graphics _selectionHighlight; //  用于绘制选中高亮 
    private readonly Text _textDisplay;
    private readonly Text _placeholder;
    private readonly Graphics _caret; // 光标 
    private readonly Text.Factory _textFactory; // 用于创建和测量文本 

    // --- 内部状态  ---
    private readonly StringBuilder _textBuilder = new();
    private bool _isFocused = false;
    private int _caretIndex = 0; // 光标在字符串中的索引 
    private int _selectionStart = 0; //  选区的“锚点” 
    private bool _isSelecting = false; //  鼠标是否按下并移动 
    private float _blinkTimer = 0f;
    private const float BlinkRate = 0.5f; // 光标闪烁速率  
    private bool _multiline = false;
    private bool _caretPositionDirty = true;
    private bool _displayStateDirty = true;

    // --- 样式属性  ---
    private float _boxWidth = 200f;
    private float _boxHeight = 30f;
    private BrushStyle _focusedBorderStyle = new(new RawColor4(0.0f, 0.6f, 1.0f, 1.0f));
    private BrushStyle _borderStyle = new(Color.DarkGray);
    private readonly float _paddingX = 5f;
    private readonly float _paddingY = 2f;
    private float _borderWidth = 1f;
    private float _borderRadius = 4f;

    private bool _bgDirty = true;
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
            _caretIndex = 0;
            _selectionStart = 0; //  重置选区
            _caretPositionDirty = true; // 标记为脏 
            _displayStateDirty = true;
            TryUpdateTextDisplay();
            TryUpdateCaretPosition();
        }
    }

    /// <summary>
    /// Gets or sets the placeholder text displayed when the input field is empty.
    /// </summary>
    public string PlaceholderText
    {
        get => _placeholder.Content;
        set
        {
            _placeholder.Content = value;
        }
    }

    #region Layout Properties

    public override float Height
    {
        get => _boxHeight;
        set
        {
            _boxHeight = value;
            if (_multiline)
            {
                _textClipContainer.Y = _paddingY;
                _textClipContainer.ClipHeight = _boxHeight - (_paddingY * 2);
            }
            else
            {
                float textHeight = _textFactory.FontSize + 2f;
                _textClipContainer.Y = (_boxHeight - textHeight) / 2;
                _textClipContainer.ClipHeight = textHeight + _paddingY;
            }
            _bgDirty = true;
        }
    }

    public override float Width
    {
        get => _boxWidth;
        set
        {
            _boxWidth = value;
            _textClipContainer.ClipWidth = _boxWidth - (_paddingX * 2);
            _bgDirty = true;
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

    #endregion

    #region Selection Logic
    //  检查是否有选区 
    private bool HasSelection => _selectionStart != _caretIndex;

    /// <summary>
    ///  获取标准化的选区范围 (start, end)。 
    /// </summary>
    private (int, int) GetSelectionRange()
    {
        int start = Math.Min(_selectionStart, _caretIndex);
        int end = Math.Max(_selectionStart, _caretIndex);
        return (start, end);
    }

    /// <summary>
    /// 获取选中的文本。
    /// </summary>
    private string GetSelectedText()
    {
        if (!HasSelection) return string.Empty;
        (int start, int end) = GetSelectionRange();
        return _textBuilder.ToString(start, end - start);
    }

    /// <summary>
    /// 删除当前选中的文本 。
    /// </summary>
    /// <returns>如果删除了选区，则为 true。 </returns>
    private bool DeleteSelection()
    {
        if (!HasSelection) return false;

        (int start, int end) = GetSelectionRange();
        _textBuilder.Remove(start, end - start);
        _caretIndex = start;
        _selectionStart = start; // 清除选区

        return true;
    }


    public void SetSelection(int start, int end)
    {
        start = Math.Clamp(start, 0, _textBuilder.Length);
        end = Math.Clamp(end, 0, _textBuilder.Length);
        _selectionStart = start;
        _caretIndex = end;
        TryUpdateCaretPosition();
    }

    public void SelectAll()
    {
        _selectionStart = 0;
        _caretIndex = _textBuilder.Length;
        TryUpdateCaretPosition();
    }

    #endregion

    #region Style Properties

    public BrushStyle BackgroundStyle
    {
        get => _background.FillStyle;
        set
        {
            _background.FillStyle = value;
            _bgDirty = true;
        }
    }

    public BrushStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            _bgDirty = true;
        }
    }

    public BrushStyle FocusedBorderStyle
    {
        get => _focusedBorderStyle;
        set
        {
            _focusedBorderStyle = value;
            _bgDirty = true;
        }
    }

    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = value;
            _bgDirty = true;
        }
    }

    public float BorderRadius
    {
        get => _borderRadius;
        set
        {
            _borderRadius = value;
            _bgDirty = true;
        }
    }

    #endregion

    #region Behavior Properties

    /// <summary>
    /// 指示输入框是否接受 Tab 键作为输入字符。
    /// </summary>
    public bool AcceptTab { get; set; } = false;

    /// <summary>
    /// 指示在多行模式下是否需要按住 Shift 键才能插入新行。
    /// </summary>
    public bool RequireShiftForNewLine { get; set; } = false;

    #endregion

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
            FillStyle = new(Color.Transparent),
            StrokeStyle = _borderStyle,
        };
        UpdateBackground();
        AddChild(_background);

        // 2. 创建裁剪容器
        _textClipContainer = new Container
        {
            X = _paddingX,
            Y = (_boxHeight - textHeight) / 2,
            ClipContent = true,
            ClipWidth = _boxWidth - (_paddingX * 2),
            ClipHeight = textHeight + _paddingY // 高度足以容纳光标
        };
        AddChild(_textClipContainer);

        // 3. 创建滚动容器 
        _textContainer = new Container();
        _textClipContainer.AddChild(_textContainer);

        // 4. 创建选区高亮
        _selectionHighlight = new Graphics
        {
            FillColor = new RawColor4(0.0f, 0.4f, 0.8f, 0.4f), // 半透明蓝
            Visible = false
        };
        _textContainer.AddChild(_selectionHighlight);

        // 5. 创建文本显示
        _textDisplay = _textFactory.Create("");
        _textDisplay.WordWrap = false; // 默认: 不换行
        _textDisplay.MaxWidth = float.MaxValue; // 默认: 无限宽度
        _textContainer.AddChild(_textDisplay);

        // 5.5 创建占位符文本 (可选)
        _placeholder = _textFactory.Create("Enter text...");
        _placeholder.FillColor = new RawColor4(0.6f, 0.6f, 0.6f, 1.0f); // 灰色 
        _textContainer.AddChild(_placeholder);

        // 6. 创建光标
        _caret = new Graphics
        {
            Visible = false,
            FillColor = _textFactory.FillColor.ToRawColor4() // 使用文本颜色 
        };
        // Y = -1f, 高度 + 2 确保光标略高于和低于文本 
        _caret.DrawRectangle(0, -1f, 2f, _textDisplay.FontSize + 2);
        _caret.Y = 0; // Y 坐标由 _textContainer 控制
        _textContainer.AddChild(_caret);

        // 7. 设置交互
        Interactive = true; // 使 TextBox 容器本身可交互 
        AcceptFocus = true; // 允许接受焦点
        _background.OnMouseDown += HandleMouseDown; // 背景也响应点击

        // 注册事件
        // 当点击时，请求 Stage 设置我们为焦点 
        this.OnMouseDown += HandleMouseDown;
        this.OnMouseMove += HandleMouseMove; //
        this.OnMouseUp += HandleMouseUp;     //
        this.OnMouseOut += HandleMouseUp;    // 防止拖出控件外时仍保持选区
        _background.OnMouseMove += HandleMouseMove; //
        _background.OnMouseUp += HandleMouseUp;     //
        _background.OnMouseOut += HandleMouseUp;    //

        // 挂接键盘事件
        this.OnKeyDown += HandleKeyDown;
        this.OnKeyUp += HandleKeyUp;
        this.OnKeyPress += HandleKeyPress;
        this.OnMouseWheel += HandleMouseWheel;
    }

    public override void Dispose()
    {
        // Unsubscribe from all events
        this.OnMouseDown -= HandleMouseDown;
        this.OnKeyDown -= HandleKeyDown;
        this.OnKeyUp -= HandleKeyUp;
        this.OnKeyPress -= HandleKeyPress;
        this.OnMouseWheel -= HandleMouseWheel;
        this.OnMouseMove -= HandleMouseMove;
        this.OnMouseUp -= HandleMouseUp;
        this.OnMouseOut -= HandleMouseUp;

        _background.OnMouseDown -= HandleMouseDown;
        _background.OnMouseMove -= HandleMouseMove;
        _background.OnMouseUp -= HandleMouseUp;
        _background.OnMouseOut -= HandleMouseUp;

        base.Dispose(); // This will dispose children like _background, _textClipContainer, etc.
    }

    #region Core Event Handlers
    // --- 核心事件处理器 ---

    private void HandleMouseWheel(DisplayObjectEvent evt)
    {
        // 仅在多行、有焦点且事件数据存在时滚动 
        if (!_isFocused || !_multiline || evt.Data is null) return;

        float deltaY = evt.Data.MouseWheelDeltaY;
        if (deltaY == 0) return;

        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout is null) return;

        float textHeight = layout.Metrics.Height;
        float clipHeight = _textClipContainer.ClipHeight ?? 0f;

        float maxScrollY = Math.Max(0, textHeight - clipHeight);
        if (maxScrollY <= 0) return;

        float lineHeight = _textFactory.FontSize;
        if (lineHeight <= 0) lineHeight = 16f; // 默认行高
        float scrollAmount = (lineHeight * 3) * Math.Sign(deltaY);

        float newY = _textContainer.Y + scrollAmount;
        _textContainer.Y = Math.Clamp(newY, -maxScrollY, 0f);

        evt.StopPropagation();

        _blinkTimer = 0f;
        _caret.Visible = _isFocused;
    }

    private void HandleMouseDown(DisplayObjectEvent evt)
    {
        this.Focus();
        evt.StopPropagation(); // 停止冒泡，防止点击穿透

        _isSelecting = true; // 开始选区跟踪

        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout is null) return;

        // 1. 计算点击位置
        float clickX = evt.LocalPosition.X - (_textClipContainer.X + _textContainer.X);
        float clickY = evt.LocalPosition.Y - (_textClipContainer.Y + _textContainer.Y);

        try
        {
            // 2. 使用 HitTestPoint 找到最近的文本位置
            //
            var hitTestMetrics = layout.HitTestPoint(clickX, clickY, out var isTrailingHit, out var isInside);

            // 3. 设置光标索引
            int newCaretIndex = hitTestMetrics.TextPosition + (isTrailingHit ? 1 : 0);
            _caretIndex = Math.Clamp(newCaretIndex, 0, _textBuilder.Length);

            // 处理 Shift+Click
            if (evt.Data?.Shift == true)
            {
                // Shift+Click: 保持 _selectionStart, 更新 _caretIndex
            }
            else
            {
                // Simple Click: 重置选区开始
                _selectionStart = _caretIndex;
            }

            // 4. 更新光标
            TryUpdateCaretPosition(); // 这现在也会更新选区高亮
        }
        catch (Exception ex)
        {
            Console.WriteLine("MouseDown HitTestPoint failed: " + ex.Message);
        }
    }

    /// <summary>
    /// 处理鼠标移动以实现拖动选择。
    /// </summary>
    private void HandleMouseMove(DisplayObjectEvent evt)
    {
        if (!_isSelecting || !_isFocused) return; // 仅在拖动时更新

        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout is null) return;

        float clickX = evt.LocalPosition.X - (_textClipContainer.X + _textContainer.X);
        float clickY = evt.LocalPosition.Y - (_textClipContainer.Y + _textContainer.Y);

        try
        {
            var hitTestMetrics = layout.HitTestPoint(clickX, clickY, out var isTrailingHit, out var isInside);

            int newCaretIndex;
            // 如果拖动到布局外部，则钳制到开头或结尾
            if (!isInside)
            {
                //
                if ((clickX < 0 && !_multiline) || (clickY < 0 && clickX < 0))
                    newCaretIndex = 0;
                else if ((clickX > layout.Metrics.Width && !_multiline) || (clickY > layout.Metrics.Height))
                    newCaretIndex = _textBuilder.Length;
                else
                    newCaretIndex = hitTestMetrics.TextPosition + (isTrailingHit ? 1 : 0);
            }
            else
            {
                newCaretIndex = hitTestMetrics.TextPosition + (isTrailingHit ? 1 : 0);
            }

            _caretIndex = Math.Clamp(newCaretIndex, 0, _textBuilder.Length);

            // TODO: 实现拖动时的自动滚动

            TryUpdateCaretPosition(); // 更新光标和选区高亮
        }
        catch (Exception ex)
        {
            Console.WriteLine("MouseMove HitTestPoint failed: " + ex.Message);
        }
    }

    /// <summary>
    /// 处理鼠标抬起以停止拖动选择。
    /// </summary>
    private void HandleMouseUp(DisplayObjectEvent evt)
    {
        _isSelecting = false; // 停止选区跟踪
    }

    private void HandleKeyPress(DisplayObjectEvent evt)
    {
        if (evt.Data is null || evt.Data.KeyChar == '\0') return;

        char c = evt.Data.KeyChar;

        // 过滤掉控制字符
        if (char.IsControl(c)) return;

        InsertCharToCaret(c);
    }

    private void InsertCharToCaret(char c)
    {
        if (c == '\r') c = '\n'; // 转换为换行符
        DeleteSelection(); // 先删除选区
        _textBuilder.Insert(_caretIndex, c);
        _caretIndex++;
        _selectionStart = _caretIndex; // 清除选区
        UpdateTextAndCaret();
    }

    private void HandleKeyDown(DisplayObjectEvent evt)
    {
        if (evt.Data is null) return;

        bool caretMoved = false;
        bool shiftPressed = evt.Data.Shift; // 检查 Shift 键
        bool ctrlPressed = evt.Data.Ctrl;   // 检查 Ctrl 键

        if (ctrlPressed)
        {
            switch (evt.Data.KeyCode)
            {
                case VK_A: // Ctrl+A
                    _selectionStart = 0;
                    _caretIndex = _textBuilder.Length;
                    UpdateTextAndCaret();
                    return;

                case VK_C: // Ctrl+C 
                    GetStage()?.SetClipboardText(GetSelectedText());
                    return;

                case VK_X: // Ctrl+X
                    GetStage()?.SetClipboardText(GetSelectedText());
                    if (DeleteSelection())
                    {
                        UpdateTextAndCaret();
                    }
                    return;
                case VK_V: // Ctrl+V 
                    var pasteText = GetStage()?.GetClipboardText();
                    if (!string.IsNullOrEmpty(pasteText))
                    {
                        DeleteSelection(); // 替换选区
                        _textBuilder.Insert(_caretIndex, pasteText);
                        _caretIndex += pasteText.Length;
                        _selectionStart = _caretIndex;
                        UpdateTextAndCaret();
                    }
                    return;
            }
        }
        else
        {
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
                        caretMoved = true;
                    }
                    break;

                case VK_BACKSPACE: // Backspace
                    if (DeleteSelection()) // 优先删除选区
                    {
                        UpdateTextAndCaret();
                    }
                    else if (_caretIndex > 0)
                    {
                        _textBuilder.Remove(_caretIndex - 1, 1);
                        _caretIndex--;
                        _selectionStart = _caretIndex; // 清除选区
                        UpdateTextAndCaret();
                    }
                    break;

                case VK_DELETE: // Delete
                    if (DeleteSelection()) // 优先删除选区
                    {
                        UpdateTextAndCaret();
                    }
                    else if (_caretIndex < _textBuilder.Length)
                    {
                        _textBuilder.Remove(_caretIndex, 1);
                        // 光标位置不变
                        _selectionStart = _caretIndex; // 清除选区
                        UpdateTextAndCaret(); // 文本和光标 X 坐标需要更新
                    }
                    break;

                case VK_TAB: // Tab
                    if (AcceptTab)
                    {
                        InsertCharToCaret('\t');
                    }
                    break;

                case VK_ENTER: // Enter
                    if (_multiline)
                    {
                        if (!RequireShiftForNewLine || (RequireShiftForNewLine && shiftPressed))
                            InsertCharToCaret('\n');
                    }
                    break;
            }
        }


        if (caretMoved)
        {
            // 如果没有按 Shift，则折叠选区
            if (!shiftPressed)
            {
                _selectionStart = _caretIndex;
            }

            TryUpdateCaretPosition();
        }
    }

    private void HandleKeyUp(DisplayObjectEvent evt)
    {
    }

    #endregion

    #region Rendering Methods 

    /// <summary>
    /// 处理多行模式下的上/下光标移动。
    /// </summary>
    /// <param name="direction">-1 表示上, 1 表示下。</param>
    private void MoveCaretVertical(int direction)
    {
        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout is null) return;

        try
        {
            // 1. 获取当前光标的 (X, Y) 坐标 (Get current caret (X, Y))
            var metrics = layout.HitTestTextPosition(_caretIndex, false, out float currentX, out float currentY);
            float lineHeight = metrics.Height;
            if (lineHeight <= 0) lineHeight = _textFactory.FontSize;

            // 2. 计算目标 Y 坐标
            float targetY = currentY + (direction * lineHeight);
            // (修正: 目标 Y 应在目标行的中心)
            if (direction < 0) targetY = currentY - (lineHeight * 0.5f);
            else targetY = currentY + (lineHeight * 1.5f);

            // 3. 使用 HitTestPoint 找到目标 (X, Y) 处最近的文本索引
            var hitTestMetrics = layout.HitTestPoint(currentX, targetY, out var isTrailingHit, out var isInside);

            // 4. 设置新的光标索引
            int newCaretIndex = hitTestMetrics.TextPosition + (isTrailingHit ? 1 : 0);

            _caretIndex = Math.Clamp(newCaretIndex, 0, _textBuilder.Length);
            // TryUpdateCaretPosition();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("MoveCaretVertical failed: " + ex.Message);
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

        if (_multiline)
        {
            var stage = GetStage();
            if (stage is not null)
            {
                _textClipContainer.Y = _paddingY;
                _textClipContainer.ClipHeight = _boxHeight - (_paddingY * 2);
                _displayStateDirty = false;
            }
            else
            {
                _displayStateDirty = true;
            }
        }
        else
        {
            float textHeight = _textFactory.FontSize + 2f;
            _textClipContainer.ClipHeight = textHeight + _paddingY;
            _textClipContainer.Y = (_boxHeight - textHeight) / 2;
        }
    }

    /// <summary>
    /// 计算并更新光标的 X 坐标 (以及现在的选区)。
    /// </summary>
    private void TryUpdateCaretPosition()
    {
        float caretX = 0f, caretY = 0f, caretHeight = _textFactory.FontSize;

        var stage = GetStage();
        var rt = stage?.GetCachedRenderTarget();
        var textLayout = _textDisplay.GetTextLayout(rt); // 获取 TextLayout

        if (textLayout is null)
        {
            _caretPositionDirty = true; // 失败，保持脏标记 (Failed, keep dirty flag)
            return;
        }

        try
        {
            // 使用 HitTestTextPosition 获取光标在文本索引处的准确 (X, Y) 坐标
            var metrics = textLayout.HitTestTextPosition(_caretIndex, false, out float htmX, out float htmY);
            caretX = metrics.Left; // 使用 metrics.Left (Corrected: use metrics.Left)
            caretY = metrics.Top;  // 使用 metrics.Top (Corrected: use metrics.Top)

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
        _caret.DrawRectangle(0, -1f, 2f, caretHeight + 2f);
        _caret.X = caretX;
        _caret.Y = caretY;

        // --- 更新选区高亮图形 ---
        _selectionHighlight.Clear();
        if (HasSelection)
        {
            _selectionHighlight.Visible = true;
            (int start, int end) = GetSelectionRange();

            try
            {
                // 获取选中文本范围的边界框
                //
                var hitTestMetrics = textLayout.HitTestTextRange(start, end - start, 0, 0);

                foreach (var rect in hitTestMetrics)
                {
                    // 为选区的每个部分绘制一个矩形
                    _selectionHighlight.DrawRectangle(rect.Left, rect.Top, rect.Width, rect.Height);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Selection HitTestRange failed: " + ex.Message);
                _selectionHighlight.Visible = false;
            }
        }
        else
        {
            _selectionHighlight.Visible = false;
        }


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
                _textContainer.Y = -Math.Max(0, caretY - caretMargin);
            }
            else if (caretY + caretHeight > viewEndY - caretMargin)
            {
                _textContainer.Y = -(caretY + caretHeight - clipHeight + caretMargin);
            }
        }
        // --- 水平滚动逻辑 (单行) ---
        else
        {
            if (caretX < viewStartX + caretMargin)
            {
                _textContainer.X = -Math.Max(0, caretX - caretMargin);
            }
            else if (caretX > viewEndX - caretMargin)
            {
                _textContainer.X = -(caretX - clipWidth + caretMargin);
            }
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
        _bgDirty = false;
        _background.Clear();
        _background.StrokeWidth = _borderWidth;
        _background.StrokeStyle = _isFocused ? _focusedBorderStyle : _borderStyle;
        _background.DrawRoundedRectangle(0, 0, _boxWidth, _boxHeight, _borderRadius, _borderRadius);
    }

    /// <summary>
    /// 每帧更新。
    /// </summary>
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime); // 更新子控件
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

        if (hasFocus != _isFocused || _bgDirty)
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

        _placeholder.Visible = _textBuilder.Length == 0;
    }

    #endregion

    #region Scrolling Methods

    public void ScrollToBottom()
    {
        if (!_multiline) return;
        var layout = _textDisplay.GetTextLayout(GetStage()?.GetCachedRenderTarget());
        if (layout is null) return;
        float textHeight = layout.Metrics.Height;
        float clipHeight = _textClipContainer.ClipHeight ?? 0f;
        float maxScrollY = Math.Max(0, textHeight - clipHeight);
        if (maxScrollY <= 0) return;
        _textContainer.Y = -maxScrollY;
    }

    public void ScollToTop()
    {
        if (!_multiline) return;
        _textContainer.Y = 0f;
    }

    /// <summary>
    /// 滚动视图以确保光标可见。
    /// </summary>
    public void ScrollToCaret()
    {
        // 强制重新计算，即使没有标记为 dirty
        _caretPositionDirty = false; // 避免跳过计算
        TryUpdateCaretPosition();
    }

    #endregion
}