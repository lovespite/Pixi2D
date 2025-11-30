using Pixi2D.Components;
using Pixi2D.Core;
using Pixi2D.Events;
using Pixi2D.Extensions;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 开关控件 (Toggle Switch)。
/// </summary>
public class Switch : Container
{
    private readonly Graphics _track;
    private readonly Graphics _thumb;
    private readonly FancyText _label;

    private bool _isOn = false;
    private float _switchWidth;
    private float _switchHeight;
    private float _thumbPadding = 3f;

    // --- 样式属性 ---
    private RawColor4 _trackColorOn = new(0.298f, 0.851f, 0.392f, 1.0f); // 绿色 (RGB: 76, 217, 100)
    private RawColor4 _trackColorOff = new(0.9f, 0.9f, 0.9f, 1.0f);      // 浅灰
    private RawColor4 _thumbColor = new(1.0f, 1.0f, 1.0f, 1.0f);         // 白色
    private RawColor4 _textColorOn = new(1.0f, 1.0f, 1.0f, 1.0f);        // 白色文字 (On)
    private RawColor4 _textColorOff = new(0.4f, 0.4f, 0.4f, 1.0f);       // 深灰文字 (Off)

    private string _positiveText = "ON";
    private string _negativeText = "OFF";

    /// <summary>
    /// 当开关状态改变时触发。
    /// 参数: (Switch sender, bool isOn)
    /// </summary>
    public event Action<Switch, bool>? OnChanged;

    /// <summary>
    /// 创建一个新的开关控件。
    /// </summary>
    /// <param name="textFactory">用于创建内部文本的工厂。</param>
    /// <param name="width">控件宽度。</param>
    /// <param name="height">控件高度。</param>
    public Switch(Text.Factory textFactory, float width = 48f, float height = 24f)
    {
        _switchWidth = width;
        _switchHeight = height;

        // 1. 创建轨道 (背景)
        _track = new Graphics();
        AddChild(_track);

        // 2. 创建文本 (显示在轨道内部)
        _label = FancyText.Factory.From(textFactory).Create(_negativeText, height * 0.4f, Color.Gray);
        _label.SetAnchor(0.5f, 0.5f); // 居中锚点，方便定位
        AddChild(_label);

        // 3. 创建滑块 (圆钮)
        _thumb = new Graphics();
        // 给滑块加一点阴影效果 (通过 Graphics 模拟，或简化为纯色)
        // 这里简单使用纯色
        AddChild(_thumb);

        // 4. 交互设置
        Interactive = true;
        _track.Interactive = true; // 轨道可点击
        _thumb.Interactive = true; // 滑块可点击

        // 绑定点击事件 (点击自身切换)
        OnClick += HandleClick;

        // 初始化视觉状态
        UpdateLayout();
        UpdateState(animate: false);
    }

    /// <summary>
    /// 获取或设置开关状态。
    /// </summary>
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn != value)
            {
                _isOn = value;
                UpdateState(animate: true);
                OnChanged?.Invoke(this, _isOn);
            }
        }
    }

    /// <summary>
    /// 开启状态显示的文本 (例如 "ON")。
    /// </summary>
    public string PositiveText
    {
        get => _positiveText;
        set
        {
            _positiveText = value;
            if (_isOn) UpdateLabelText();
        }
    }

    /// <summary>
    /// 关闭状态显示的文本 (例如 "OFF")。
    /// </summary>
    public string NegativeText
    {
        get => _negativeText;
        set
        {
            _negativeText = value;
            if (!_isOn) UpdateLabelText();
        }
    }

    /// <summary>
    /// 开启时的轨道颜色。
    /// </summary>
    public Color TrackColorOn
    {
        get => _trackColorOn.ToColor();
        set { _trackColorOn = value.ToRawColor4(); if (_isOn) _track.FillColor = _trackColorOn; }
    }

    /// <summary>
    /// 关闭时的轨道颜色。
    /// </summary>
    public Color TrackColorOff
    {
        get => _trackColorOff.ToColor();
        set { _trackColorOff = value.ToRawColor4(); if (!_isOn) _track.FillColor = _trackColorOff; }
    }

    /// <summary>
    /// 滑块颜色。
    /// </summary>
    public Color ThumbColor
    {
        get => _thumbColor.ToColor();
        set { _thumbColor = value.ToRawColor4(); _thumb.FillColor = _thumbColor; }
    }

    public Color TextColorOn
    {
        get => _textColorOn.ToColor();
        set { _textColorOn = value.ToRawColor4(); if (_isOn) _label.TextStyle = new(_textColorOn); }
    }

    public Color TextColorOff
    {
        get => _textColorOff.ToColor();
        set { _textColorOff = value.ToRawColor4(); if (!_isOn) _label.TextStyle = new(_textColorOff); }
    }

    private void HandleClick(DisplayObjectEvent e)
    {
        // 切换状态
        IsOn = !IsOn;
    }

    /// <summary>
    /// 重绘静态图形 (轨道形状、滑块形状)。
    /// </summary>
    private void UpdateLayout()
    {
        float radius = _switchHeight / 2f;

        // 绘制轨道
        _track.Clear();
        // 如果需要边框，可以设置 Stroke
        // _track.StrokeColor = new RawColor4(0.8f, 0.8f, 0.8f, 1f);
        // _track.StrokeWidth = 1f;
        _track.FillColor = _isOn ? _trackColorOn : _trackColorOff;
        _track.DrawRoundedRectangle(0, 0, _switchWidth, _switchHeight, radius, radius);

        // 绘制滑块
        _thumb.Clear();
        _thumb.FillColor = _thumbColor;

        float thumbSize = _switchHeight - (_thumbPadding * 2);
        float thumbRadius = thumbSize / 2f;

        // 在滑块 Graphics 内部绘制圆形
        // 偏移 radius 以确保 (0,0) 是滑块的左上角位置
        _thumb.DrawEllipse(thumbRadius, thumbRadius, thumbRadius, thumbRadius);

        // 初始 Y 位置
        _thumb.Y = _thumbPadding;
    }

    /// <summary>
    /// 更新动态状态 (颜色、位置、文字)。
    /// </summary>
    private void UpdateState(bool animate)
    {
        // 1. 更新轨道颜色
        _track.FillColor = _isOn ? _trackColorOn : _trackColorOff;

        // 2. 更新文字内容和颜色
        UpdateLabelText();

        // 3. 计算滑块目标 X 坐标
        float thumbSize = _switchHeight - (_thumbPadding * 2);
        float targetX = _isOn
            ? _switchWidth - thumbSize - _thumbPadding
            : _thumbPadding;

        // 4. 计算文字目标 X 坐标
        // 如果是 ON (滑块在右)，文字在左侧区域居中
        // 如果是 OFF (滑块在左)，文字在右侧区域居中
        float textAreaWidth = _switchWidth - thumbSize - (_thumbPadding * 3);
        float textTargetX = (_isOn
            ? _thumbPadding + (textAreaWidth / 2f)
            : _switchWidth - _thumbPadding - (textAreaWidth / 2f)) - (_label.Width / 2f);

        // 应用位置 (动画或直接设置)
        if (animate)
        {
            // _thumb.MoveXTo(targetX, 0.2f, EasingFunction.CubicEaseOut);
            _thumb.Animate(0.2f, (_, progress) =>
            {
                float newX = _thumb.X + (targetX - _thumb.X) * progress;
                _thumb.X = newX;
            }, EasingFunction.CubicEaseOut);

            // 文字可以简单移动，或者淡入淡出，这里我们让它跟随移动
            // 为了视觉效果，直接设置位置，因为文字内容也变了
            _label.X = textTargetX;
        }
        else
        {
            _thumb.X = targetX;
            _label.X = textTargetX;
        }

        // 垂直居中
        _label.Y = _switchHeight / 2f - _label.Height / 2f;
    }

    private void UpdateLabelText()
    {
        if (_isOn)
        {
            _label.Content = _positiveText;
            _label.TextStyle = new(_textColorOn);
        }
        else
        {
            _label.Content = _negativeText;
            _label.TextStyle = new(_textColorOff);
        }
    }

    public override float Width
    {
        get => _switchWidth;
        set
        {
            _switchWidth = value;
            UpdateLayout();
            UpdateState(false);
        }
    }

    public override float Height
    {
        get => _switchHeight;
        set
        {
            _switchHeight = value;
            UpdateLayout();
            UpdateState(false);
        }
    }
}