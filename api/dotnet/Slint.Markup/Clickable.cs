// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

namespace Slint.Markup;

/// <summary>
/// A rectangle with a centered text that the user clicks, for buttons with their own colors and shapes,
/// such as round tiles or a gradient, which the style's <see cref="Button"/> doesn't have.
/// It darkens a little while the pointer is over it and while it's pressed.
/// Add children, such as an image, to show them under the text.
/// </summary>
public sealed class Clickable : Element<Clickable>
{
    private readonly Text _text;
    private readonly TouchArea _touch = new();
    private readonly Rectangle _feedback = new();

    /// <summary>A clickable rectangle with a text that doesn't change, which runs <paramref name="clicked"/> on a click.</summary>
    public Clickable(string text, Action? clicked = null) : this(new Text(text), clicked) { }

    /// <summary>A clickable rectangle with a text that the page reads again on each update.</summary>
    public Clickable(Func<string> text, Action? clicked = null) : this(new Text(text), clicked) { }

    /// <summary>A clickable rectangle with a text from a <c>.slint</c> expression, such as a field of a row.</summary>
    public Clickable(Expr text) : this(new Text(text), null) { }

    private Clickable(Text text, Action? clicked) : base("Rectangle")
    {
        _text = text
            .HorizontalAlignment(TextHorizontalAlignment.Center).VerticalAlignment(TextVerticalAlignment.Center)
            .Width(new Expr("parent.width")).Height(new Expr("parent.height"));
        _feedback
            .Width(new Expr("parent.width")).Height(new Expr("parent.height"))
            .Background(new Expr("parent.pressed ? #00000030 : parent.has-hover ? #00000014 : transparent"));
        _touch.Add(_feedback);
        if (clicked != null)
        {
            _touch.OnClicked(clicked);
        }
        Children.Add(_text);
        Children.Add(_touch);
    }

    /// <summary>Adds a child under the text, such as an image.</summary>
    public new void Add(Element child) => Children.Insert(Children.Count - 2, child);

    /// <summary>Runs <paramref name="clicked"/> on a click; the page updates after it.</summary>
    public Clickable OnClicked(Action clicked) { _touch.OnClicked(clicked); return this; }

    /// <summary>Runs <paramref name="clicked"/> with the row's item on a click, in a row of a <see cref="ForEach{T}"/>.</summary>
    public Clickable OnClicked(RowAction clicked) { _touch.OnClicked(clicked); return this; }

    /// <summary>Runs <c>.slint</c> statements on a click, such as <c>popup.show()</c>.</summary>
    public Clickable OnClicked(Expr statements) { _touch.OnClicked(statements); return this; }

    /// <summary>The background.</summary>
    public Clickable Background(Color color) { SetValue("background", SlintType.Brush, color); return this; }

    /// <summary>The background, which the page reads again on each update.</summary>
    public Clickable Background(Func<Color> color) { SetBinding("background", SlintType.Brush, () => color()); return this; }

    /// <summary>The background from a <c>.slint</c> expression, such as <c>@linear-gradient(90deg, red, blue)</c>.</summary>
    public Clickable Background(Expr color) { SetExpression("background", SlintType.Brush, color); return this; }

    /// <summary>The corner radius, in logical pixels; half the height makes a pill or a circle.</summary>
    public Clickable BorderRadius(double radius)
    {
        SetValue("border-radius", SlintType.Length, radius);
        _feedback.BorderRadius(radius);
        return this;
    }

    /// <summary>The color of the border.</summary>
    public Clickable BorderColor(Color color) { SetValue("border-color", SlintType.Brush, color); return this; }

    /// <summary>The color of the border, which the page reads again on each update.</summary>
    public Clickable BorderColor(Func<Color> color) { SetBinding("border-color", SlintType.Brush, () => color()); return this; }

    /// <summary>The width of the border, in logical pixels.</summary>
    public Clickable BorderWidth(double width) { SetValue("border-width", SlintType.Length, width); return this; }

    /// <summary>The width of the border, which the page reads again on each update.</summary>
    public Clickable BorderWidth(Func<double> width) { SetBinding("border-width", SlintType.Length, () => width()); return this; }

    /// <summary>The color of the text.</summary>
    public Clickable TextColor(Color color) { _text.Color(color); return this; }

    /// <summary>The color of the text, which the page reads again on each update.</summary>
    public Clickable TextColor(Func<Color> color) { _text.Color(color); return this; }

    /// <summary>The font size of the text, in logical pixels.</summary>
    public Clickable FontSize(double size) { _text.FontSize(size); return this; }

    /// <summary>The weight of the text's font, such as 700 for bold.</summary>
    public Clickable FontWeight(int weight) { _text.FontWeight(weight); return this; }
}
