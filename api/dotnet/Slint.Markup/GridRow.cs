// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

namespace Slint.Markup;

/// <summary>
/// A row of a <see cref="GridLayout"/> or <see cref="GridBox"/>: its children go into the next row, from the first column.
/// Set <c>Row</c> and <c>Col</c> on children instead to place them anywhere.
/// </summary>
public sealed class GridRow : Element<GridRow>
{
    /// <summary>A row of a grid.</summary>
    public GridRow() : base("Row") { }
}
