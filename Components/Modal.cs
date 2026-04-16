using Pixi2D.Controls;
using Pixi2D.Core;
using Pixi2D.Extensions;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Components;

/// <summary>
/// 全新的静态 Modal 弹窗组件
/// 采用实例即用即毁的模式，支持防点击穿透，支持字体大小调节
/// </summary>
public class Modal : Container
{
    protected int Padding { get; private set; } = 20;
    protected Size MaxSize { get; private set; } = new Size(400, 600);
    protected bool MaskClosable { get; private set; }
    protected RawColor4 BackColor { get; private set; } = new RawColor4(1, 1, 1, 1);
    protected RawColor4 MaskColor { get; private set; } = new RawColor4(0, 0, 0, 0.25f);
    protected RawColor4 ForeColor { get; private set; } = new RawColor4(0, 0, 0, 0.85f);
    protected float FontSize { get; private set; } = 16f;
    protected FontWeight FontWeight { get; private set; } = FontWeight.Regular;
    protected Text.Factory? TextFactory { get; private set; }
    protected string Content { get; set; }
    protected PopupPosition PopPosition { get; private set; } = PopupPosition.Center;
    protected bool AutoOffset { get; private set; } = true;

    protected readonly List<ModalAction> m_actions;

    public enum PopupPosition
    {
        Center,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// <summary>
    /// 私有构造函数，只允许通过静态方法创建
    /// </summary>
    private Modal()
    {
        m_actions = [];
        Content = string.Empty;
    }

    public void AddAction(ModalAction action)
    {
        m_actions.Add(action);
    }

    public void AddAction(string title, Action? action = null, Text.Factory? factory = null)
    {
        m_actions.Add(new ModalAction { Title = title, Callback = action, Factory = factory });
    }

    public void Popup(Stage stage)
    {
        TextFactory ??= new Text.Factory
        {
            FontSize = FontSize,
            FontWeight = FontWeight,
            FillColor = Color.FromArgb(
                red: (int)(ForeColor.R * 255),
                green: (int)(ForeColor.G * 255),
                blue: (int)(ForeColor.B * 255),
                alpha: (int)(ForeColor.A * 255)),
        };

        var width = stage.Width;
        var height = stage.Height;

        var mask = new Panel(width + 20, height + 20)
        {
            X = -10,
            Y = -10,
            Interactive = true,
            BackgroundColor = MaskColor,
        };
        mask.OnClick += (e) => { if (MaskClosable) DestroyModal(); };

        var contentText = TextFactory.Create(Content);
        contentText.FillColor = ForeColor;
        contentText.FontSize = FontSize;
        contentText.FontWeight = FontWeight;
        contentText.X = Padding;
        contentText.Y = Padding;
        contentText.WordWrap = true;
        contentText.MaxWidth = MaxSize.Width - Padding * 2;
        var contentTextRect = contentText.GetTextRect(forceUpdate: true, stage.GetCachedRenderTarget());
        var outerWidth = Math.Clamp(contentTextRect.Width + Padding * 2, 320, MaxSize.Width);

        var actionsBar = new AutoFlowLayout
        {
            Direction = FlowLayout.LayoutDirection.Horizontal,
            JustifyMain = FlowLayout.JustifyContent.End,
            AlignCross = FlowLayout.AlignItems.Center,
            Gap = 20f,
            Width = outerWidth - 20f,
        };

        foreach (var a in m_actions)
        {
            var btnText = (a.Factory ?? TextFactory).Create(a.Title);
            var btnRect = btnText.GetTextRect(forceUpdate: true, stage.GetCachedRenderTarget());
            var button = new Button(btnText, btnRect.Width + 20, btnRect.Height + 10);
            actionsBar.AddChild(button);
            button.OnButtonClick += (e) =>
            {
                a.Callback?.Invoke();
                DestroyModal();
            };
            if (button.Height > actionsBar.Height) actionsBar.Height = button.Height;
        }

        var outerHeight = Math.Clamp(contentTextRect.Height + actionsBar.Height + Padding * 2, 120, MaxSize.Height);

        //var boxX = (stage.Width - outerWidth) / 2;
        //var boxY = (stage.Height - outerHeight) / 2;

        var position = GetPopupPostion(new SizeF(outerWidth, outerHeight), new SizeF(stage.Width, stage.Height));

        var vbox = new AutoFlowLayout
        {
            Direction = FlowLayout.LayoutDirection.Vertical,
            JustifyMain = FlowLayout.JustifyContent.SpaceBetween,
            Gap = 20f,
            X = position.X + 10,
            Y = position.Y + 10,
            Height = outerHeight,
        };

        vbox.AddChildren(contentText, actionsBar);

        var bg = new Graphics
        {
            X = position.X,
            Y = position.Y,
            FillColor = BackColor,
        };
        bg.DrawRoundedRectangle(0, 0, outerWidth, outerHeight + Padding, 10, 10);

        mask.AddChildren(bg, vbox);
        stage.AddChild(mask);
        Interlocked.Increment(ref s_modalCounter);

        void DestroyModal()
        {
            Interlocked.Decrement(ref s_modalCounter);
            stage.RemoveChild(mask);
            mask.Dispose();
        }
    }

    private static long s_modalCounter = 0;

    protected RawVector2 GetPopupPostion(SizeF contentSize, SizeF stageSize)
    {
        var v2 = PopPosition switch
        {
            PopupPosition.Center => new RawVector2((stageSize.Width - contentSize.Width) / 2, (stageSize.Height - contentSize.Height) / 2),
            PopupPosition.TopLeft => new RawVector2(20, 20),
            PopupPosition.TopRight => new RawVector2(stageSize.Width - contentSize.Width - 20, 20),
            PopupPosition.BottomLeft => new RawVector2(20, stageSize.Height - contentSize.Height - 20),
            PopupPosition.BottomRight => new RawVector2(stageSize.Width - contentSize.Width - 20, stageSize.Height - contentSize.Height - 20),
            _ => new RawVector2((stageSize.Width - contentSize.Width) / 2, (stageSize.Height - contentSize.Height) / 2),
        };

        if (AutoOffset)
        {
            var offset = s_modalCounter * 20;
            v2.X += offset;
            v2.Y += offset;

            // 预留右侧和底部的安全边界 (20)
            float maxX = stageSize.Width - contentSize.Width - 20;
            float maxY = stageSize.Height - contentSize.Height - 20;

            // 计算可活动的有效区间跨度 (起始20 到 maxX 的距离)
            // 使用 Math.Max 确保跨度大于0，避免窗体比Stage大时发生除零错误
            float spanX = Math.Max(1, maxX - 20);
            float spanY = Math.Max(1, maxY - 20);

            // 溢出时利用取模(%)使其折返到初始边距(20)重新开始阶梯排列，实现绕场周旋
            if (v2.X > maxX) v2.X = 20 + ((v2.X - 20) % spanX);
            if (v2.Y > maxY) v2.Y = 20 + ((v2.Y - 20) % spanY);
        }

        return v2;
    }

    public class ModalAction
    {
        public Text.Factory? Factory { get; init; }
        public required string Title { get; init; }
        public Action? Callback { get; init; }
    }

    public static void Alert(Stage stage, string content, string okText = "确定", Text.Factory? factory = null)
    {
        new Builder(factory)
            .SetContent(content)
            .AddAction(okText)
            .Build()
            .Popup(stage);
    }

    public class Builder
    {
        private readonly Modal m_modal;
        public Builder(Text.Factory? factory = null)
        {
            m_modal = new Modal
            {
                TextFactory = factory,
            };
            if (factory is not null)
            {
                m_modal.FontSize = factory.FontSize;
                m_modal.FontWeight = factory.FontWeight;
                m_modal.ForeColor = factory.FillColor.ToRawColor4();
            }
        }
        public Builder WithNoOffset()
        {
            m_modal.AutoOffset = false;
            return this;
        }
        public Builder SetPosition(PopupPosition position)
        {
            m_modal.PopPosition = position;
            return this;
        }
        public Builder SetContent(string content)
        {
            m_modal.Content = content;
            return this;
        }
        public Builder SetTextFactory(Text.Factory factory)
        {
            m_modal.TextFactory = factory;
            return this;
        }
        public Builder SetPadding(int padding)
        {
            m_modal.Padding = padding;
            return this;
        }
        public Builder SetMaxSize(int width, int height)
        {
            m_modal.MaxSize = new Size(width, height);
            return this;
        }
        public Builder SetMaskClosable(bool maskClosable)
        {
            m_modal.MaskClosable = maskClosable;
            return this;
        }
        public Builder SetBackColor(RawColor4 backColor)
        {
            m_modal.BackColor = backColor;
            return this;
        }
        public Builder SetMaskColor(RawColor4 maskColor)
        {
            m_modal.MaskColor = maskColor;
            return this;
        }
        public Builder SetForeColor(RawColor4 foreColor)
        {
            m_modal.ForeColor = foreColor;
            return this;
        }
        public Builder SetBackColor(System.Drawing.Color backColor)
        {
            m_modal.BackColor = backColor.ToRawColor4();
            return this;
        }
        public Builder SetMaskColor(System.Drawing.Color maskColor)
        {
            m_modal.MaskColor = maskColor.ToRawColor4();
            return this;
        }
        public Builder SetForeColor(System.Drawing.Color foreColor)
        {
            m_modal.ForeColor = foreColor.ToRawColor4();
            return this;
        }
        public Builder AddAction(string title, Action? action = null, Text.Factory? factory = null)
        {
            m_modal.AddAction(title, action, factory);
            return this;
        }
        public Modal Build()
        {
            return m_modal;
        }
    }
}
