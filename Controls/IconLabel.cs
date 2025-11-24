using Pixi2D.Core;
using SharpDX.Direct2D1;
using System;

namespace Pixi2D.Controls;

/// <summary>
/// 一个轻量级的图标文本控件。
/// 显示一个图标和一行文本，默认布局是图标显示在文本前。
/// 此控件不使用 FlowLayout，通过手动计算坐标实现布局。
/// </summary>
public class IconLabel : Container
{
    private readonly Sprite _icon;
    private readonly Text _label;
    private float _spacing = 5f;
    private bool _layoutDirty = true;

    /// <summary>
    /// 获取内部的 Sprite 对象，可用于进一步调整图标样式（如颜色、透明度）。
    /// </summary>
    public Sprite IconSprite => _icon;

    /// <summary>
    /// 获取内部的 Text 对象，可用于进一步调整文本样式（如字体、颜色）。
    /// </summary>
    public Text Label => _label;

    /// <summary>
    /// 创建一个新的 IconLabel。
    /// </summary>
    /// <param name="textFactory">用于创建文本的工厂。</param>
    /// <param name="text">显示的文本内容。</param>
    /// <param name="icon">显示的图标位图。</param>
    /// <param name="spacing">图标和文本之间的间距（像素）。</param>
    public IconLabel(Text.Factory textFactory, string text, Bitmap1 icon, float spacing = 5f)
    {
        _spacing = spacing;

        // 1. 创建并添加图标
        _icon = new Sprite(icon);
        AddChild(_icon);

        // 2. 创建并添加文本
        _label = textFactory.Create(text);
        AddChild(_label);

        // 3. 初始布局
        UpdateLayout();
    }

    /// <summary>
    /// 设置或获取显示的文本内容。
    /// </summary>
    public string Text
    {
        get => _label.Content;
        set
        {
            if (_label.Content != value)
            {
                _label.Content = value;
                MarkLayoutDirty();
            }
        }
    }

    /// <summary>
    /// 设置或获取显示的图标。
    /// </summary>
    public Bitmap1 Icon
    {
        get => _icon.Bitmap!;
        set
        {
            if (_icon.Bitmap != value)
            {
                _icon.Bitmap = value;
                if (value != null)
                {
                    _icon.Width = value.Size.Width;
                    _icon.Height = value.Size.Height;
                }
                MarkLayoutDirty();
            }
        }
    }

    /// <summary>
    /// 设置或获取图标和文本之间的水平间距。
    /// </summary>
    public float Spacing
    {
        get => _spacing;
        set
        {
            if (_spacing != value)
            {
                _spacing = value;
                MarkLayoutDirty();
            }
        }
    }

    /// <summary>
    /// 标记布局需要更新。
    /// </summary>
    public void MarkLayoutDirty()
    {
        _layoutDirty = true;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        // 懒加载布局更新：仅在需要时重新计算
        // 这也确保了如果 Text 在创建时没有 RenderTarget 导致尺寸为 0，
        // 在后续帧中有机会修正布局。
        if (_layoutDirty)
        {
            UpdateLayout();
        }
    }

    /// <summary>
    /// 执行布局计算。
    /// </summary>
    public void UpdateLayout()
    {
        // 1. 获取图标尺寸 (考虑缩放)
        float iconW = _icon.Width * _icon.ScaleX;
        float iconH = _icon.Height * _icon.ScaleY;

        // 2. 获取文本尺寸
        // 尝试从 DirectWrite 获取精确尺寸。
        // 如果尚未渲染过，GetTextRect 可能返回 0 宽，这里使用 FontSize 作为高度的保底估算。
        float textW = 0f;
        float textH = _label.FontSize;

        var rect = _label.GetTextRect();
        if (rect.Width > 0)
        {
            textW = rect.Width;
            textH = rect.Height;
        }

        // 3. 垂直居中对齐计算
        // 容器的总高度由最高的元素决定
        float totalH = Math.Max(iconH, textH);

        // 图标位置：左侧 (0)，垂直居中
        _icon.X = 0;
        _icon.Y = (totalH - iconH) / 2f;

        // 文本位置：图标右侧 + 间距，垂直居中
        _label.X = iconW + _spacing;
        _label.Y = (totalH - textH) / 2f;

        // 4. 更新容器自身的宽高以包裹内容
        this.Width = _label.X + textW;
        this.Height = totalH;

        _layoutDirty = false;
    }
}