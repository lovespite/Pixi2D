
using Pixi2D.Components;
using Pixi2D.Core;
using SharpDX.Direct2D1;

namespace Pixi2D.Controls;

public class Number : Container
{
    public const string FORMAT_DEFAULT = "0.##";
    public const string FORMAT_INTEGER = "0";
    public const string FORMAT_COMMA = "#,##0.##";
    public const string FORMAT_COMMA_INTEGER = "#,##0";

    private readonly Text _text;
    private decimal _value;
    private decimal _targetValue;
    private string _format;
    private bool _isDirty;
    private Animator? _animator = null;

    public Number(Text text, decimal initValue = 0m, string format = FORMAT_COMMA)
    {
        _text = text;
        _value = _targetValue = initValue;
        _text.Content = _value.ToString(format);
        _format = format;
        Size = text.Size;
        AddChild(text);
    }

    public string Prefix { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;

    public string Format
    {
        get => _format;
        set
        {
            _format = value;
            _isDirty = true;
        }
    }

    public decimal Value
    {
        get => _targetValue;
        set
        {
            _targetValue = value;
            _isDirty = true;
        }
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (!_isDirty) return;

        _isDirty = false;
        _animator?.Stop();
        _animator = this.Animate(duration: 0.8f, AnimatingUpdate, EasingFunction.CircleEaseInOut, delay: 0.1f);
    }

    private void AnimatingUpdate(Animator _, float factor)
    {
        _value += (_targetValue - _value) * (decimal)factor;
        UpdateText();
    }

    private void UpdateText()
    {
        _text.Content = Prefix + _value.ToString(_format) + Suffix;
    }
}
