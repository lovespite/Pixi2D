using Pixi2D.Components;
using Pixi2D.Core;
using Pixi2D.Events;
using Pixi2D.Extensions;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 单个选项模型：名称 + 可选图标。
/// </summary>
public sealed class SwitchButtonOption
{
    public string Name { get; set; } = string.Empty;
    public Sprite? Sprite { get; set; }

    public SwitchButtonOption() { }

    public SwitchButtonOption(string name, Sprite? sprite = null)
    {
        Name = name ?? string.Empty;
        Sprite = sprite;
    }
}

/// <summary>
/// 单选分段按钮（Radio-like segmented switch）。
/// </summary>
public sealed class SwitchButton : Container
{
    private sealed class SegmentVisual(
        int index,
        Container root,
        Graphics hitArea,
        Text label,
        Sprite? icon,
        Action<DisplayObjectEvent> clickHandler)
    {
        public int Index { get; } = index;
        public Container Root { get; } = root;
        public Graphics HitArea { get; } = hitArea;
        public Text Label { get; } = label;
        public Sprite? Icon { get; } = icon;
        public Action<DisplayObjectEvent> ClickHandler { get; } = clickHandler;
    }

    private readonly Text.Factory _textFactory;
    private readonly Graphics _track = new();
    private readonly Graphics _indicator = new();
    private readonly Container _segmentsContainer = new();
    private readonly List<SegmentVisual> _segments = [];

    private SwitchButtonOption[] _options = [];
    private int _selectedIndex = -1;

    private float _controlWidth;
    private float _controlHeight;
    private float _cornerRadius = 8f;
    private float _indicatorPadding = 2f;
    private float _contentGap = 6f;

    private RawColor4 _trackColor = new(0.18f, 0.18f, 0.18f, 1f);
    private RawColor4 _borderColor = new(0.36f, 0.36f, 0.36f, 1f);
    private RawColor4 _indicatorColor = new(0.22f, 0.52f, 0.98f, 1f);
    private RawColor4 _textColor = new(0.80f, 0.80f, 0.80f, 1f);
    private RawColor4 _selectedTextColor = new(1f, 1f, 1f, 1f);
    private float _borderWidth = 1f;

    private Animator? _indicatorAnimator;

    public float AnimationDuration { get; set; } = 0.2f;
    public EasingFunction AnimationEasing { get; set; } = EasingFunction.CubicEaseOut;

    /// <summary>用户点击选项时触发。参数为被点击的选项名。</summary>
    public event Action<string>? OnButtonClick;
    /// <summary>当选中索引变化时触发（用户点击 + 程序设置均会触发）。</summary>
    public event Action<int>? SelectedIndexChanged;

    public SwitchButton() : this(UIContext.Current.DefaultTextFactory) { }

    public SwitchButton(Text.Factory textFactory, float width = 240f, float height = 40f)
    {
        _textFactory = textFactory;
        _controlWidth = Math.Max(1f, width);
        _controlHeight = Math.Max(1f, height);

        Interactive = true;
        _track.Interactive = false;
        _indicator.Interactive = false;
        _segmentsContainer.Interactive = true;

        AddChildren(_track, _indicator, _segmentsContainer);
        RedrawTrack();
        UpdateIndicatorLayout(animate: false);
    }

    public SwitchButtonOption[] Options
    {
        get
        {
            var copy = new SwitchButtonOption[_options.Length];
            for (int i = 0; i < _options.Length; i++)
            {
                var opt = _options[i];
                copy[i] = new SwitchButtonOption(opt.Name, opt.Sprite);
            }
            return copy;
        }
        set => SetOptions(value);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndexCore(value, animate: true, raiseChangedEvent: true);
    }

    public string SelectedOption
    {
        get
        {
            if (_selectedIndex < 0 || _selectedIndex >= _options.Length) return string.Empty;
            return _options[_selectedIndex].Name;
        }
        set
        {
            if (_options.Length == 0) return;
            string target = value ?? string.Empty;
            for (int i = 0; i < _options.Length; i++)
            {
                if (string.Equals(_options[i].Name, target, StringComparison.Ordinal))
                {
                    SetSelectedIndexCore(i, animate: true, raiseChangedEvent: true);
                    return;
                }
            }
        }
    }

    public Color TrackColor
    {
        get => _trackColor.ToColor();
        set
        {
            _trackColor = value.ToRawColor4();
            RedrawTrack();
        }
    }

    public Color BorderColor
    {
        get => _borderColor.ToColor();
        set
        {
            _borderColor = value.ToRawColor4();
            RedrawTrack();
        }
    }

    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = Math.Max(0f, value);
            RedrawTrack();
        }
    }

    public Color IndicatorColor
    {
        get => _indicatorColor.ToColor();
        set
        {
            _indicatorColor = value.ToRawColor4();
            UpdateIndicatorLayout(animate: false);
        }
    }

    public Color TextColor
    {
        get => _textColor.ToColor();
        set
        {
            _textColor = value.ToRawColor4();
            UpdateTextColors();
        }
    }

    public Color SelectedTextColor
    {
        get => _selectedTextColor.ToColor();
        set
        {
            _selectedTextColor = value.ToRawColor4();
            UpdateTextColors();
        }
    }

    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0f, value);
            RedrawTrack();
            UpdateIndicatorLayout(animate: false);
        }
    }

    public float IndicatorPadding
    {
        get => _indicatorPadding;
        set
        {
            _indicatorPadding = Math.Max(0f, value);
            UpdateIndicatorLayout(animate: false);
        }
    }

    public float ContentGap
    {
        get => _contentGap;
        set
        {
            _contentGap = Math.Max(0f, value);
            RebuildSegments();
        }
    }

    public override float Width
    {
        get => _controlWidth;
        set
        {
            var next = Math.Max(1f, value);
            if (Math.Abs(_controlWidth - next) < 0.01f) return;
            _controlWidth = next;
            RedrawTrack();
            RebuildSegments();
        }
    }

    public override float Height
    {
        get => _controlHeight;
        set
        {
            var next = Math.Max(1f, value);
            if (Math.Abs(_controlHeight - next) < 0.01f) return;
            _controlHeight = next;
            RedrawTrack();
            RebuildSegments();
        }
    }

    public override void Dispose()
    {
        _indicatorAnimator?.Stop();
        _indicatorAnimator = null;
        DetachSegmentHandlers();
        base.Dispose();
    }

    private void SetOptions(SwitchButtonOption[]? options)
    {
        var normalized = NormalizeOptions(options);
        int previousSelected = _selectedIndex;

        _options = normalized;
        if (_options.Length == 0)
        {
            _selectedIndex = -1;
        }
        else if (_selectedIndex < 0 || _selectedIndex >= _options.Length)
        {
            _selectedIndex = 0;
        }

        RebuildSegments();

        // Options 变化导致索引被动调整时，不触发 SelectedIndexChanged（仅响应显式 SelectedIndex 变更和用户点击）。
        if (previousSelected != _selectedIndex)
        {
            UpdateIndicatorLayout(animate: false);
        }
    }

    private static SwitchButtonOption[] NormalizeOptions(SwitchButtonOption[]? options)
    {
        if (options is null || options.Length == 0) return [];

        var normalized = new SwitchButtonOption[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            var raw = options[i];
            if (raw is null)
            {
                normalized[i] = new SwitchButtonOption(string.Empty);
                continue;
            }

            normalized[i] = new SwitchButtonOption(raw.Name ?? string.Empty, raw.Sprite);
        }
        return normalized;
    }

    private void SetSelectedIndexCore(int requested, bool animate, bool raiseChangedEvent)
    {
        int normalized = NormalizeSelectedIndex(requested);
        if (normalized == _selectedIndex)
        {
            UpdateIndicatorLayout(animate: false);
            return;
        }

        _selectedIndex = normalized;
        UpdateIndicatorLayout(animate);

        if (raiseChangedEvent)
            SelectedIndexChanged?.Invoke(_selectedIndex);
    }

    private int NormalizeSelectedIndex(int requested)
    {
        if (_options.Length == 0) return -1;
        if (requested < 0) return 0;
        if (requested >= _options.Length) return _options.Length - 1;
        return requested;
    }

    private void RebuildSegments()
    {
        DetachSegmentHandlers();
        _segmentsContainer.ClearChildren(dispose: true);
        _segments.Clear();

        if (_options.Length == 0)
        {
            UpdateIndicatorLayout(animate: false);
            return;
        }

        float segmentWidth = _controlWidth / _options.Length;
        for (int i = 0; i < _options.Length; i++)
        {
            var root = new Container
            {
                X = i * segmentWidth,
                Y = 0,
                Interactive = true
            };

            var hit = new Graphics
            {
                Interactive = true,
                FillColor = new RawColor4(0f, 0f, 0f, 0.01f),
            };
            hit.DrawRectangle(0, 0, segmentWidth, _controlHeight);
            root.AddChild(hit);

            var option = _options[i];
            var label = _textFactory.Create(option.Name ?? string.Empty);
            label.WordWrap = false;
            label.MaxWidth = Math.Max(1f, segmentWidth - 8f);

            Sprite? icon = CreateRenderableSprite(option.Sprite);

            LayoutSegmentContent(segmentWidth, _controlHeight, label, icon);
            if (icon is not null) root.AddChild(icon);
            root.AddChild(label);

            int capturedIndex = i;
            Action<DisplayObjectEvent> clickHandler = _ => HandleSegmentClick(capturedIndex);
            hit.OnClick += clickHandler;

            _segments.Add(new SegmentVisual(capturedIndex, root, hit, label, icon, clickHandler));
            _segmentsContainer.AddChild(root);
        }

        UpdateTextColors();
        UpdateIndicatorLayout(animate: false);
    }

    private void DetachSegmentHandlers()
    {
        foreach (var segment in _segments)
        {
            segment.HitArea.OnClick -= segment.ClickHandler;
        }
    }

    private static Sprite? CreateRenderableSprite(Sprite? source)
    {
        if (source?.Bitmap is null) return null;
        return new Sprite(source.Bitmap, disposeBitmapWithSprite: false)
        {
            ScaleX = source.ScaleX,
            ScaleY = source.ScaleY,
            Rotation = source.Rotation,
            Alpha = source.Alpha,
        };
    }

    private void LayoutSegmentContent(float segmentWidth, float segmentHeight, Text label, Sprite? icon)
    {
        float textWidth = 0f;
        float textHeight = 0f;
        if (!string.IsNullOrEmpty(label.Content))
        {
            var metrics = _textFactory.MeasureText(label.Content, Math.Max(1f, label.MaxWidth));
            textWidth = metrics.Width;
            textHeight = metrics.Height > 0 ? metrics.Height : _textFactory.FontSize;
            label.Visible = true;
        }
        else
        {
            label.Visible = false;
        }

        float iconWidth = 0f;
        float iconHeight = 0f;
        if (icon is not null)
        {
            iconWidth = icon.Width * icon.ScaleX;
            iconHeight = icon.Height * icon.ScaleY;
            if (iconWidth > 0f && iconHeight > 0f)
            {
                float maxIcon = Math.Max(8f, segmentHeight - 12f);
                float scaleDown = Math.Min(1f, Math.Min(maxIcon / iconWidth, maxIcon / iconHeight));
                if (scaleDown < 1f)
                {
                    icon.ScaleX *= scaleDown;
                    icon.ScaleY *= scaleDown;
                    iconWidth = icon.Width * icon.ScaleX;
                    iconHeight = icon.Height * icon.ScaleY;
                }
            }
        }

        bool hasIcon = icon is not null;
        bool hasText = label.Visible;
        float gap = (hasIcon && hasText) ? _contentGap : 0f;
        float totalWidth = iconWidth + gap + textWidth;
        float startX = Math.Max(0f, (segmentWidth - totalWidth) / 2f);

        if (hasIcon && icon is not null)
        {
            icon.X = startX;
            icon.Y = Math.Max(0f, (segmentHeight - iconHeight) / 2f);
        }

        if (hasText)
        {
            label.X = startX + iconWidth + gap;
            label.Y = Math.Max(0f, (segmentHeight - textHeight) / 2f);
        }
    }

    private void HandleSegmentClick(int index)
    {
        if (index < 0 || index >= _options.Length) return;

        bool changed = index != _selectedIndex;
        if (changed)
        {
            SetSelectedIndexCore(index, animate: true, raiseChangedEvent: true);
        }

        OnButtonClick?.Invoke(_options[index].Name);
    }

    private void RedrawTrack()
    {
        _track.Clear();
        _track.FillColor = _trackColor;
        _track.StrokeColor = _borderColor;
        _track.StrokeWidth = _borderWidth;
        float radius = Math.Max(0f, Math.Min(_cornerRadius, Math.Min(_controlWidth, _controlHeight) / 2f));
        _track.DrawRoundedRectangle(0, 0, _controlWidth, _controlHeight, radius, radius);
    }

    private void UpdateIndicatorLayout(bool animate)
    {
        if (_options.Length == 0 || _selectedIndex < 0 || _selectedIndex >= _options.Length)
        {
            _indicatorAnimator?.Stop();
            _indicatorAnimator = null;
            _indicator.Visible = false;
            UpdateTextColors();
            return;
        }

        float segmentWidth = _controlWidth / _options.Length;
        float indicatorWidth = Math.Max(2f, segmentWidth - (_indicatorPadding * 2f));
        float indicatorHeight = Math.Max(2f, _controlHeight - (_indicatorPadding * 2f));
        float targetX = (_selectedIndex * segmentWidth) + _indicatorPadding;

        _indicator.Clear();
        _indicator.FillColor = _indicatorColor;
        float radius = Math.Max(0f, Math.Min(_cornerRadius - _indicatorPadding, Math.Min(indicatorWidth, indicatorHeight) / 2f));
        _indicator.DrawRoundedRectangle(0, 0, indicatorWidth, indicatorHeight, radius, radius);
        _indicator.Y = _indicatorPadding;
        _indicator.Visible = true;

        _indicatorAnimator?.Stop();
        _indicatorAnimator = null;

        if (animate && AnimationDuration > 0.001f)
        {
            // _indicatorAnimator = _indicator.MoveXTo(targetX, AnimationDuration, AnimationEasing);
            _indicatorAnimator = new Animator(target: _indicator, properties: null, duration: AnimationDuration, easing: AnimationEasing);
            _indicatorAnimator.Animating += (_, f) =>
            {
                float startX = (_selectedIndex * segmentWidth) + _indicatorPadding;
                float endX = targetX;
                _indicator.X = startX + (endX - startX) * f;
            };
        }
        else
        {
            _indicator.X = targetX;
        }

        UpdateTextColors();
    }

    private void UpdateTextColors()
    {
        foreach (var segment in _segments)
        {
            segment.Label.FillColor = (segment.Index == _selectedIndex) ? _selectedTextColor : _textColor;
        }
    }
}
