using Pixi2D.Controls;
using Pixi2D.Core;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Components;

/// <summary>
/// 一个可复用的模态对话框控件 (MessageBox)。  <br />
/// 静态方法 (Show, Confirm, Input) 依赖于 
/// [MessageBox.TextFactory] 和 [MessageBox.DefaultStage] 
/// 必须在调用前被设置。 
/// </summary>
public partial class MessageBox : Panel
{
    /// <summary>
    /// 半透明的模态遮罩层。 
    /// </summary>
    private readonly Graphics _overlay;

    /// <summary>
    /// 容纳自定义内容 (消息, 输入框等) 的内部面板。 
    /// </summary>
    private readonly Panel _contentHolder;

    /// <summary>
    /// 容纳 "OK", "Cancel" 等按钮的流式布局。 
    /// </summary>
    private readonly FlowLayout _actionsLayout;

    private readonly Stage _stage;

    /// <summary>
    /// 当对话框显示时，默认获得焦点的目标对象。默认为null，表示 MessageBox 本身。
    /// </summary>
    public DisplayObject? DefaultFocusTarget { get; set; }

    public static float DefaultPadding { get; set; } = 10;
    public static float DefaultButtonHeight { get; set; } = 30;
    public static float DefaultBorderRadius { get; set; } = 8;
    public static float DefaultBorderWidth { get; set; } = 0;

    /// <summary>
    /// 创建一个新的 MessageBox 实例。 
    /// </summary>
    /// <param name="width">对话框宽度。 </param>
    /// <param name="height">对话框高度。 </param>
    public MessageBox(Stage stage, SizeF size, Container content, Button[] actions) : base(size.Width, size.Height)
    {
        float width = size.Width, height = size.Height;
        _stage = stage;
        // 1. 初始化遮罩层 
        _overlay = new Graphics
        {
            Interactive = true, // 阻挡点击穿透 
            Visible = false,
            FillColor = new RawColor4(0, 0, 0, 0.5f) // 半透明黑色 
        };

        // 2. 配置 MessageBox (自身 - Panel) 
        SetPadding(DefaultPadding);
        CornerRadius = DefaultBorderRadius;
        BackgroundColor = new RawColor4(0.2f, 0.2f, 0.25f, 1f);
        BorderColor = new RawColor4(0.4f, 0.4f, 0.4f, 1f);
        BorderWidth = DefaultBorderWidth;
        Visible = false; // 默认隐藏  

        // 3. 创建内部布局 (使用 FlowLayout) 
        var mainLayout = new FlowLayout
        {
            Direction = FlowLayout.LayoutDirection.Vertical,
            Width = width - DefaultPadding * 2, // 减去 Panel 的内边距 
            Gap = 8,
        };
        AddContent(mainLayout); // 添加到 Panel 的 ContentContainer

        // 4. 创建内容占位符 
        _contentHolder = new Panel(
            width: mainLayout.Width,
            height: height - DefaultButtonHeight - DefaultPadding * 2 - mainLayout.Gap * 2 // 预留按钮和Padding空间 
        )
        {
            BackgroundColor = new RawColor4(0, 0, 0, 0),
            BorderWidth = 0,
            ClipContent = true, // 裁剪超出范围的内容 
            Interactive = true,
            FocusTarget = this,
            PaddingLeft = 2,
        };
        _contentHolder.AddContent(content);
        mainLayout.AddChild(_contentHolder);

        // 5. 创建按钮布局 
        _actionsLayout = new FlowLayout
        {
            Direction = FlowLayout.LayoutDirection.Horizontal,
            Gap = 10,
            Width = width - DefaultPadding * 2,
        };
        _actionsLayout.AddChildren(actions);
        mainLayout.AddChild(_actionsLayout);

        // 6. 添加到舞台 (遮罩层在下，对话框在上) 
        stage.AddChildren(_overlay, this);

        stage.OnResize += HandleStageResize;
        ResizeAndCenter();
    }

    /// <summary>
    /// 调整遮罩层和对话框的大小并居中。 
    /// </summary>
    private void ResizeAndCenter()
    {
        var stage = _stage;
        // 调整遮罩层以覆盖整个舞台 
        _overlay.Clear();
        _overlay.DrawRectangle(0, 0, stage.Width, stage.Height);
        _overlay.Visible = true;

        // 居中对话框
        // (Center the dialog)
        this.X = (stage.Width - this.Width) / 2;
        this.Y = (stage.Height - this.Height) / 2;
        this.Visible = true;
    }

    /// <summary>
    /// 隐藏对话框和遮罩层。 
    /// </summary>
    public void Hide()
    {
        this.Visible = false;
        _overlay.Visible = false;
    }

    /// <summary>
    /// 显示一个带有自定义内容和自定义按钮的对话框。 
    /// </summary>
    /// <param name="contentContainer">要显示的内容容器。 </param>
    /// <param name="actions">要显示的按钮数组。 </param> 
    public void Show()
    {
        // 调整大小和位置 
        ResizeAndCenter();

        Visible = true;
        _overlay.Visible = true;

        (DefaultFocusTarget ?? this).Focus();
    }

    private void HandleStageResize(Stage _, float width, float height)
    {
        ResizeAndCenter();
    }

    public override void Dispose()
    {
        _stage.OnResize -= HandleStageResize;
        // _contentHolder.Dispose(); // 随容器自动销毁，不需要在这里处理
        _overlay.Dispose();
        base.Dispose();
    }
}

partial class MessageBox
{

    #region Static Methods

    /// <summary>
    /// 默认的 "确认" 按钮文本。
    /// </summary>
    public static string DefaultOkText { get; set; } = "确定(OK)";

    /// <summary>
    /// 默认的 "取消" 按钮文本。
    /// </summary>
    public static string DefaultCancelText { get; set; } = "取消(Cancel)";

    // --- 静态依赖 ---
    // (Static Dependencies)

    /// <summary>
    /// 静态方法用于创建文本和按钮的 Text.Factory。  <br />
    /// 必须在调用静态方法前设置此项。 
    /// </summary>
    public static Text.Factory? TextFactory { get; set; }

    /// <summary>
    /// 静态方法用于显示对话框的默认舞台。  <br />
    /// 必须在调用静态方法前设置此项。 
    /// </summary>
    public static Stage? DefaultStage { get; set; }

    // --- 静态辅助方法 ---
    // (Static Helper Methods)

    /// <summary>
    /// 检查静态依赖项是否已设置。 
    /// </summary>
    private static void CheckStaticDependencies()
    {
        if (TextFactory == null)
            throw new InvalidOperationException("MessageBox.TextFactory must be set before using static methods.");
        if (DefaultStage == null)
            throw new InvalidOperationException("MessageBox.DefaultStage must be set before using static methods.");
    }

    /// <summary>
    /// 快速显示一个带 "确认" 按钮的提示消息。 
    /// </summary>
    /// <param name="message">要显示的消息字符串。 </param>
    public static async Task Show(string message)
    {
        CheckStaticDependencies();
        var stage = DefaultStage!;
        var textFactory = TextFactory!;
        // 创建内容
        // (Create content)
        var content = new Container();
        var text = textFactory.Create(message, 14, Color.White);
        text.MaxWidth = 330; // 350 - 20 padding
        content.AddChild(text);

        var tcs = new TaskCompletionSource();
        var button = new Button(textFactory.Create(DefaultOkText), 80, 30);
        using var mb = new MessageBox(stage, new SizeF(350, 150), content, [button]);

        button.OnClick += (btn) =>
        {
            mb.Hide();
            tcs.SetResult();
        };

        mb.Show();

        await tcs.Task;
    }

    /// <summary>
    /// 异步显示一个带 "确认" 和 "取消" 按钮的询问消息。 
    /// </summary>
    /// <param name="message">要显示的询问消息。 </param>
    /// <param name="okText">显示在“确认”按钮上的文本</param>
    /// <param name="cancelText">显示在“取消”按钮上的文本</param> 
    /// <returns>如果点击了 "确认" 则为 true，否则为 false。 </returns>
    public static async Task<bool> Confirm(string message, string? okText = null, string? cancelText = null)
    {
        CheckStaticDependencies();
        var stage = DefaultStage!;
        var textFactory = TextFactory!;

        // 创建内容
        // (Create content)
        var content = new Container();
        var text = textFactory.Create(message, 14, Color.White);
        text.MaxWidth = 330; // 350 - 20 padding
        content.AddChild(text);

        // 创建按钮
        // (Create buttons)
        var okButton = new Button(textFactory.Create(okText ?? DefaultOkText), 80, 30) { BorderWidth = 2 };
        var cancelButton = new Button(textFactory.Create(cancelText ?? DefaultCancelText), 80, 30) { BorderWidth = 2 };

        var tcs = new TaskCompletionSource<object?>();
        using var mb = new MessageBox(stage, new SizeF(350, 150), content, [okButton, cancelButton])
        {
            DefaultFocusTarget = okButton,
        };

        okButton.OnButtonClick += (btn) =>
        {
            mb.Hide();
            tcs.TrySetResult(true);
        };
        cancelButton.OnButtonClick += (btn) =>
        {
            mb.Hide();
            tcs.TrySetResult(false);
        };


        mb.Show();

        var result = await tcs.Task;
        return (bool)(result ?? false);
    }

    /// <summary>
    /// 异步显示一个带提示和 **单行** 文本输入框的对话框。
    /// </summary>
    /// <param name="prompt">要显示的提示消息。</param>
    /// <param name="defaultText">输入框中的默认文本。</param>
    /// <param name="okText">显示在“确认”按钮上的文本</param>
    /// <param name="cancelText">显示在“取消”按钮上的文本</param>
    /// <returns>如果点击 "确认" 则为输入的字符串，如果点击 "取消" 则为 null。</returns>
    public static async Task<string?> Input(string prompt, string? defaultText = null, string? okText = null, string? cancelText = null)
    {
        // 调用内部实现，指定为单行
        return await InputInternal(prompt, defaultText, false, 30f, okText, cancelText);
    }

    /// <summary>
    /// 异步显示一个带提示和 **多行** 文本输入框的对话框。
    /// </summary>
    /// <param name="prompt">要显示的提示消息。</param>
    /// <param name="inputHeight">多行输入框的高度。</param>
    /// <param name="defaultText">输入框中的默认文本。</param>
    /// <param name="okText">显示在“确认”按钮上的文本</param>
    /// <param name="cancelText">显示在“取消”按钮上的文本</param>
    /// <returns>如果点击 "确认" 则为输入的字符串，如果点击 "取消" 则为 null。</returns>
    public static async Task<string?> Input(string prompt, float inputHeight, string? defaultText = null, string? okText = null, string? cancelText = null)
    {
        // 调用内部实现，指定为多行
        return await InputInternal(prompt, defaultText, true, inputHeight, okText, cancelText);
    }

    /// <summary>
    /// 内部 Input 实现，用于处理单行和多行。
    /// </summary>
    private static async Task<string?> InputInternal(string prompt, string? defaultText, bool multiline, float inputHeight, string? okText, string? cancelText)
    {
        CheckStaticDependencies();
        var stage = DefaultStage!;
        var textFactory = TextFactory!;

        // 创建内容 (流式布局)
        var contentLayout = new FlowLayout
        {
            Direction = FlowLayout.LayoutDirection.Vertical,
            Gap = 10,
            Width = 380, // 400 - 20 padding
        };

        // 添加提示
        var text = textFactory.Create(prompt, 14, Color.White);
        contentLayout.AddChild(text);
        text.MaxWidth = 380;
        var promptTextRect = text.GetTextRect(forceUpdate: true, stage.GetCachedRenderTarget());
        text.Height = promptTextRect.Height; // 自动高度

        // 添加输入框
        var textBox = new TextBox(textFactory, 375, inputHeight)
        {
            Multiline = multiline,
            RequireShiftForNewLine = multiline,
            Text = defaultText ?? string.Empty,
        };
        contentLayout.AddChild(textBox);

        // 创建按钮
        var okButton = new Button(textFactory.Create(okText ?? DefaultOkText), 80, 30);
        var cancelButton = new Button(textFactory.Create(cancelText ?? DefaultCancelText), 80, 30);

        var tcs = new TaskCompletionSource<object?>();

        // 计算 MessageBox 高度
        // 基础高度 (Padding + 按钮 + 间隙) = 20 (上下 padding) + 30 (按钮) + 10 (间隙) = 60
        // 内容高度 = promptHeight + 10 (间隙) + inputHeight
        float messageBoxHeight = 60 + promptTextRect.Height + inputHeight + 16;
        messageBoxHeight = Math.Max(150, messageBoxHeight); // 最小高度

        using var mb = new MessageBox(stage, new SizeF(400, messageBoxHeight + 4), contentLayout, [okButton, cancelButton])
        {
            DefaultFocusTarget = textBox,
        };

        // 仅在单行模式下，回车键才确认

        textBox.OnKeyDown += (evt) =>
        {
            bool isEnter = evt.Data?.KeyCode == 13;
            bool shift = evt.Data?.Shift == true;
            // 如果按下回车键则确认输入
            if ((multiline && !shift && isEnter) || (!multiline && !shift && isEnter)) // Enter key
            {
                mb.Hide();
                tcs.TrySetResult(textBox.Text);
            }
        };

        contentLayout.FocusTarget = textBox;

        okButton.OnButtonClick += (btn) =>
        {
            mb.Hide();
            tcs.TrySetResult(textBox.Text);
        };

        cancelButton.OnButtonClick += (btn) =>
        {
            mb.Hide();
            tcs.TrySetResult(null); // Cancel 返回 null
        };

        // 自动聚焦到输入框
        // mb.OnFocus += textBox.Focus;
        mb.Show();

        textBox.SelectAll();
        var result = await tcs.Task;
        return (string?)result;
    }

    #endregion
}
