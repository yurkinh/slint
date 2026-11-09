// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

namespace Slint.Markup;

// Elements that only exist as children of a GridLayout, a TabWidget, or a RadioGroup, which the compiler lists with their parent.

/// <summary>
/// A row of a <see cref="GridLayout"/> or <see cref="GridBox"/>: its children go into the next row, from the first column.
/// Set <c>Row</c> and <c>Col</c> on children instead to place them anywhere.
/// </summary>
public sealed class GridRow : Element<GridRow>
{
    /// <summary>A row of a grid.</summary>
    public GridRow() : base("Row") { }
}

/// <summary>A tab of a <see cref="TabWidget"/>, which shows its child while the tab is the current one.</summary>
public sealed class Tab : Element<Tab>
{
    /// <summary>A tab with the title <paramref name="title"/>.</summary>
    public Tab(string title) : base("Tab") => SetValue("title", SlintType.String, title);

    /// <summary>A tab with a title that the page reads again on each update.</summary>
    public Tab(Func<string> title) : base("Tab") => SetBinding("title", SlintType.String, () => title());
}

/// <summary>An option of a <see cref="RadioGroup"/>, which reports the text of the option that the user checks with <c>OnSelected</c>.</summary>
public sealed class RadioButton : Element<RadioButton>
{
    /// <summary>An option with the text <paramref name="text"/>.</summary>
    public RadioButton(string text) : base("RadioButton") => SetValue("text", SlintType.String, text);

    /// <summary>Whether the user can check it.</summary>
    public RadioButton Enabled(Func<bool> enabled) { SetBinding("enabled", SlintType.Bool, () => enabled()); return this; }

    /// <summary>Whether it's checked, which the page reads again on each update.</summary>
    public RadioButton Checked(Func<bool> isChecked) { SetBinding("checked", SlintType.Bool, () => isChecked()); return this; }

    /// <summary>Runs <paramref name="toggled"/> when the user checks it; the page updates after it.</summary>
    public RadioButton OnToggled(Action toggled) { SetHandler("toggled", [], _ => toggled()); return this; }
}
