using Pixi2D.Core;
using SharpDX.Mathematics.Interop;
using System;

namespace Pixi2D.Controls;

/// <summary>
/// 进度条控件 (ProgressBar)。
/// 用于可视化地显示操作的完成进度。
/// </summary>
public class ProgressBar : Container
{
    private readonly Graphics _track; // 背景轨道
    private readonly Graphics _fill;  // 前景填充

    private float _minimum = 0f;
    private float _maximum = 100f;
    private float _value = 0f;

    private float _barWidth = 200f;
    private float _barHeight = 20f;
    private float _borderRadius = 0f;
    private float _padding = 2f; // 默认内边距

    private RawColor4 _trackColor = new(0.2f, 0.2f, 0.2f, 1.0f); // 深灰色背景
    private RawColor4 _fillColor = new(0.0f, 0.6f, 1.0f, 1.0f);  // 蓝色填充
    private RawColor4 _borderColor = new(0.5f, 0.5f, 0.5f, 1.0f); // 灰色边框
    private float _borderWidth = 1f;

    /// <summary>
    /// 创建一个新的进度条。
    /// </summary>
    /// <param name="width">宽度。</param>
    /// <param name="height">高度。</param>
    public ProgressBar(float width = 200f, float height = 20f)
    {
        _barWidth = width;
        _barHeight = height;

        // 1. 创建背景轨道
        _track = new Graphics();
        AddChild(_track);

        // 2. 创建前景填充
        _fill = new Graphics();
        AddChild(_fill);

        // 初始布局
        UpdateLayout();
    }

    #region Public Properties

    /// <summary>
    /// 进度条的宽度。
    /// </summary>
    public override float Width
    {
        get => _barWidth;
        set
        {
            if (_barWidth != value)
            {
                _barWidth = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 进度条的高度。
    /// </summary>
    public override float Height
    {
        get => _barHeight;
        set
        {
            if (_barHeight != value)
            {
                _barHeight = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 最小数值 (默认 0)。
    /// </summary>
    public float Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum != value)
            {
                _minimum = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 最大数值 (默认 100)。
    /// </summary>
    public float Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum != value)
            {
                _maximum = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 当前数值。
    /// </summary>
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, _minimum, _maximum);
            if (_value != clamped)
            {
                _value = clamped;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 进度的百分比 (0.0 - 1.0)。
    /// </summary>
    public float Percentage
    {
        get
        {
            float range = _maximum - _minimum;
            if (range <= 0) return 0f;
            return Math.Clamp((_value - _minimum) / range, 0f, 1f);
        }
        set
        {
            Value = _minimum + (value * (_maximum - _minimum));
        }
    }

    /// <summary>
    /// 圆角半径。
    /// </summary>
    public float BorderRadius
    {
        get => _borderRadius;
        set
        {
            if (_borderRadius != value)
            {
                _borderRadius = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 填充内容与边框之间的间距 (内边距)。
    /// </summary>
    public float Padding
    {
        get => _padding;
        set
        {
            if (_padding != value)
            {
                _padding = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 背景轨道颜色。
    /// </summary>
    public RawColor4 TrackColor
    {
        get => _trackColor;
        set
        {
            _trackColor = value;
            UpdateLayout();
        }
    }

    /// <summary>
    /// 进度填充颜色。
    /// </summary>
    public RawColor4 FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            UpdateLayout();
        }
    }

    /// <summary>
    /// 边框颜色。
    /// </summary>
    public RawColor4 BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            UpdateLayout();
        }
    }

    /// <summary>
    /// 边框宽度。
    /// </summary>
    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            if (_borderWidth != value)
            {
                _borderWidth = value;
                UpdateLayout();
            }
        }
    }

    #endregion

    /// <summary>
    /// 更新绘制逻辑。
    /// </summary>
    private void UpdateLayout()
    {
        // 1. 绘制背景轨道 (Track)
        _track.Clear();
        _track.FillColor = _trackColor;
        _track.StrokeColor = _borderColor;
        _track.StrokeWidth = _borderWidth;

        if (_borderRadius > 0)
        {
            _track.DrawRoundedRectangle(0, 0, _barWidth, _barHeight, _borderRadius, _borderRadius);
        }
        else
        {
            _track.DrawRectangle(0, 0, _barWidth, _barHeight);
        }

        // 2. 绘制前景填充 (Fill)
        _fill.Clear();

        float pct = Percentage;
        if (pct <= 0) return; // 没有进度不绘制

        _fill.FillColor = _fillColor;

        // 计算填充区域的尺寸
        // 考虑到边框一般是居中绘制，这里我们假设 Padding 包含了避免压住边框所需的空间。
        // 为了更好的显示效果，我们自动将 BorderWidth/2 加到 Padding 上作为偏移。
        float offset = (_borderWidth / 2f) + _padding;

        float maxFillWidth = _barWidth - (offset * 2);
        float fillHeight = _barHeight - (offset * 2);

        if (maxFillWidth <= 0 || fillHeight <= 0) return;

        float currentFillWidth = maxFillWidth * pct;

        // 调整内部圆角: 内部圆角 = 外部圆角 - 偏移量
        float innerRadius = Math.Max(0, _borderRadius - offset);

        float startX = offset;
        float startY = offset;

        if (innerRadius > 0)
        {
            _fill.DrawRoundedRectangle(startX, startY, currentFillWidth, fillHeight, innerRadius, innerRadius);
        }
        else
        {
            _fill.DrawRectangle(startX, startY, currentFillWidth, fillHeight);
        }
    }
}