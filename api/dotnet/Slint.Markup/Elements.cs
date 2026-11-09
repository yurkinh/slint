// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

namespace Slint.Markup;

// The spike's builders, written by hand; the core generates them from the compiler's elements and widgets.

/// <summary>The page's window. A page whose root is another element gets a window around it.</summary>
public sealed class Window : Element<Window>
{
    /// <summary>A window.</summary>
    public Window() : base("Window") { }

    /// <summary>The title.</summary>
    public Window Title(string title) { SetValue("title", SlintType.String, title); return this; }

    /// <summary>The title.</summary>
    public Window Title(Func<string> title) { SetBinding("title", SlintType.String, () => title()); return this; }

    /// <summary>The background.</summary>
    public Window Background(Color color) { SetValue("background", SlintType.Brush, color); return this; }
}

/// <summary>Text.</summary>
public sealed class Text : Element<Text>
{
    /// <summary>Text that doesn't change.</summary>
    public Text(string text) : base("Text") => SetValue("text", SlintType.String, text);

    /// <summary>Text that the page reads again on each update.</summary>
    public Text(Func<string> text) : base("Text") => SetBinding("text", SlintType.String, () => text());

    /// <summary>Text from a row of <see cref="ForEach{T}"/>, or another <c>.slint</c> expression.</summary>
    public Text(Expr text) : base("Text") => SetExpression("text", SlintType.String, text);

    /// <summary>The font size, in logical pixels.</summary>
    public Text FontSize(double size) { SetValue("font-size", SlintType.Length, size); return this; }

    /// <summary>The color.</summary>
    public Text Color(Color color) { SetValue("color", SlintType.Brush, color); return this; }

    /// <summary>The color.</summary>
    public Text Color(Func<Color> color) { SetBinding("color", SlintType.Brush, () => color()); return this; }
}

/// <summary>A rectangle, which can have a background and children.</summary>
public sealed class Rectangle : Element<Rectangle>
{
    /// <summary>A rectangle.</summary>
    public Rectangle() : base("Rectangle") { }

    /// <summary>The background.</summary>
    public Rectangle Background(Color color) { SetValue("background", SlintType.Brush, color); return this; }

    /// <summary>The background.</summary>
    public Rectangle Background(Func<Color> color) { SetBinding("background", SlintType.Brush, () => color()); return this; }

    /// <summary>The corner radius, in logical pixels.</summary>
    public Rectangle BorderRadius(double radius) { SetValue("border-radius", SlintType.Length, radius); return this; }
}

/// <summary>A layout that puts its children below each other, without the style's spacing.</summary>
public sealed class VerticalLayout : Element<VerticalLayout>
{
    /// <summary>A vertical layout.</summary>
    public VerticalLayout() : base("VerticalLayout") { }

    /// <summary>The space between children, in logical pixels.</summary>
    public VerticalLayout Spacing(double spacing) { SetValue("spacing", SlintType.Length, spacing); return this; }

    /// <summary>The space around the children, in logical pixels.</summary>
    public VerticalLayout Padding(double padding) { SetValue("padding", SlintType.Length, padding); return this; }
}

/// <summary>A layout that puts its children next to each other, without the style's spacing.</summary>
public sealed class HorizontalLayout : Element<HorizontalLayout>
{
    /// <summary>A horizontal layout.</summary>
    public HorizontalLayout() : base("HorizontalLayout") { }

    /// <summary>The space between children, in logical pixels.</summary>
    public HorizontalLayout Spacing(double spacing) { SetValue("spacing", SlintType.Length, spacing); return this; }
}

/// <summary>A vertical layout with the style's spacing and padding.</summary>
public sealed class VerticalBox : Element<VerticalBox>
{
    /// <summary>A vertical box.</summary>
    public VerticalBox() : base("VerticalBox", "std-widgets.slint") { }
}

/// <summary>A horizontal layout with the style's spacing and padding.</summary>
public sealed class HorizontalBox : Element<HorizontalBox>
{
    /// <summary>A horizontal box.</summary>
    public HorizontalBox() : base("HorizontalBox", "std-widgets.slint") { }
}

/// <summary>A button of the style.</summary>
public sealed class Button : Element<Button>
{
    /// <summary>A button with a text that doesn't change.</summary>
    public Button(string text) : base("Button", "std-widgets.slint") => SetValue("text", SlintType.String, text);

    /// <summary>A button with a text that the page reads again on each update.</summary>
    public Button(Func<string> text) : base("Button", "std-widgets.slint") => SetBinding("text", SlintType.String, () => text());

    /// <summary>Whether the user can click it.</summary>
    public Button Enabled(Func<bool> enabled) { SetBinding("enabled", SlintType.Bool, () => enabled()); return this; }

    /// <summary>Runs <paramref name="clicked"/> on a click; the page updates after it.</summary>
    public Button OnClicked(Action clicked) { SetHandler("clicked", [], _ => clicked()); return this; }

    /// <summary>Runs <paramref name="clicked"/> with the row's item, in a row of <see cref="ForEach{T}"/>.</summary>
    public Button OnClicked(RowAction clicked) { SetHandler("clicked", clicked); return this; }
}

/// <summary>A text field of the style.</summary>
public sealed class LineEdit : Element<LineEdit>
{
    /// <summary>A text field.</summary>
    public LineEdit() : base("LineEdit", "std-widgets.slint") { }

    /// <summary>Binds the text both ways: <paramref name="set"/> receives what the user types.</summary>
    public LineEdit Text(Func<string> get, Action<string> set)
    {
        SetBinding("text", SlintType.String, () => get(), value => set((string)value!));
        return this;
    }

    /// <summary>The text shown while the field is empty.</summary>
    public LineEdit PlaceholderText(string text) { SetValue("placeholder-text", SlintType.String, text); return this; }

    /// <summary>Runs <paramref name="accepted"/> with the text when the user presses Enter; the page updates after it.</summary>
    public LineEdit OnAccepted(Action<string> accepted)
    {
        SetHandler("accepted", [SlintType.String], args => accepted((string)args[0]!));
        return this;
    }
}

/// <summary>A check box of the style.</summary>
public sealed class CheckBox : Element<CheckBox>
{
    /// <summary>A check box with a text that doesn't change.</summary>
    public CheckBox(string text) : base("CheckBox", "std-widgets.slint") => SetValue("text", SlintType.String, text);

    /// <summary>A check box with a text from a row of <see cref="ForEach{T}"/>, or another <c>.slint</c> expression.</summary>
    public CheckBox(Expr text) : base("CheckBox", "std-widgets.slint") => SetExpression("text", SlintType.String, text);

    /// <summary>Whether it's checked, from a row of <see cref="ForEach{T}"/>, or another <c>.slint</c> expression.</summary>
    public CheckBox Checked(Expr isChecked) { SetExpression("checked", SlintType.Bool, isChecked); return this; }

    /// <summary>Binds whether it's checked both ways: <paramref name="set"/> receives the user's change.</summary>
    public CheckBox Checked(Func<bool> get, Action<bool> set)
    {
        SetBinding("checked", SlintType.Bool, () => get(), value => set((bool)value!));
        return this;
    }

    /// <summary>Runs <paramref name="toggled"/> with the row's item when the user toggles it, in a row of <see cref="ForEach{T}"/>.</summary>
    public CheckBox OnToggled(RowAction toggled) { SetHandler("toggled", toggled); return this; }
}
