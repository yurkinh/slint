// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections;

namespace Slint.Markup;

/// <summary>The type of a property in <c>.slint</c> code.</summary>
public enum SlintType
{
    /// <summary><c>string</c></summary>
    String,
    /// <summary><c>length</c>, in logical pixels</summary>
    Length,
    /// <summary><c>float</c></summary>
    Float,
    /// <summary><c>int</c></summary>
    Int,
    /// <summary><c>bool</c></summary>
    Bool,
    /// <summary><c>brush</c>, from a <see cref="Slint.Color"/></summary>
    Brush,
}

/// <summary>A <c>.slint</c> expression, such as <c>parent.width / 2</c>, for what the builders don't cover.</summary>
public sealed record Expr(string Code);

/// <summary>
/// An element of a page: a built-in element, a widget, or a component from a <c>.slint</c> file.
/// Add children with a collection initializer.
/// </summary>
public abstract class Element : IEnumerable<Element>
{
    internal readonly List<PropertyValue> Properties = [];
    internal readonly List<CallbackHandler> Callbacks = [];
    internal readonly List<Element> Children = [];

    /// <param name="type">The element's name in <c>.slint</c> code, such as <c>Text</c>.</param>
    /// <param name="import">The file that exports it, such as <c>std-widgets.slint</c>, or <c>null</c> for a built-in element.</param>
    protected Element(string type, string? import = null)
    {
        Type = type;
        Import = import;
    }

    internal string Type { get; }
    internal string? Import { get; }

    /// <summary>Adds a child.</summary>
    public void Add(Element child) => Children.Add(child);

    IEnumerator<Element> IEnumerable<Element>.GetEnumerator() => Children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Children.GetEnumerator();

    /// <summary>Sets the property <paramref name="name"/> to a value that doesn't change.</summary>
    protected void SetValue(string name, SlintType type, object value) =>
        Properties.Add(new PropertyValue(name, type, value, null, null));

    /// <summary>Binds the property <paramref name="name"/> to <paramref name="get"/>, which the page reads again on each update.</summary>
    protected void SetBinding(string name, SlintType type, Func<object?> get, Action<object?>? set = null) =>
        Properties.Add(new PropertyValue(name, type, null, get, set));

    /// <summary>Sets the property <paramref name="name"/> to a <c>.slint</c> expression.</summary>
    protected void SetExpression(string name, SlintType type, Expr expression) =>
        Properties.Add(new PropertyValue(name, type, expression, null, null));

    /// <summary>Handles the callback <paramref name="name"/>, whose arguments have the types <paramref name="parameters"/>.</summary>
    protected void SetHandler(string name, SlintType[] parameters, Action<object?[]> handler) =>
        Callbacks.Add(new CallbackHandler(name, parameters, handler, null));

    /// <summary>Handles the callback <paramref name="name"/> in a row of <see cref="ForEach{T}"/>.</summary>
    protected void SetHandler(string name, RowAction handler) =>
        Callbacks.Add(new CallbackHandler(name, [], _ => { }, handler));

    internal sealed record PropertyValue(string Name, SlintType Type, object? Value, Func<object?>? Get, Action<object?>? Set);

    internal sealed record CallbackHandler(string Name, SlintType[] Parameters, Action<object?[]> Handler, RowAction? Row);
}

/// <summary>The base of the builders, with the properties that every element has.</summary>
public abstract class Element<TSelf> : Element where TSelf : Element<TSelf>
{
    /// <inheritdoc />
    protected Element(string type, string? import = null) : base(type, import) { }

    private TSelf Self => (TSelf)this;

    /// <summary>Sets a property that the builder has no method for.</summary>
    public TSelf Property(string name, SlintType type, object value) { SetValue(name, type, value); return Self; }

    /// <summary>Binds a property that the builder has no method for.</summary>
    public TSelf Property<T>(string name, SlintType type, Func<T> get) { SetBinding(name, type, () => get()); return Self; }

    /// <summary>Sets a property to a <c>.slint</c> expression.</summary>
    public TSelf Property(string name, SlintType type, Expr expression) { SetExpression(name, type, expression); return Self; }

    /// <summary>The width, in logical pixels.</summary>
    public TSelf Width(double width) { SetValue("width", SlintType.Length, width); return Self; }

    /// <summary>The width, in logical pixels.</summary>
    public TSelf Width(Func<double> width) { SetBinding("width", SlintType.Length, () => width()); return Self; }

    /// <summary>The height, in logical pixels.</summary>
    public TSelf Height(double height) { SetValue("height", SlintType.Length, height); return Self; }

    /// <summary>The height, in logical pixels.</summary>
    public TSelf Height(Func<double> height) { SetBinding("height", SlintType.Length, () => height()); return Self; }

    /// <summary>Whether a parent layout stretches the element horizontally, from 0.</summary>
    public TSelf HorizontalStretch(double stretch) { SetValue("horizontal-stretch", SlintType.Float, stretch); return Self; }

    /// <summary>Whether a parent layout stretches the element vertically, from 0.</summary>
    public TSelf VerticalStretch(double stretch) { SetValue("vertical-stretch", SlintType.Float, stretch); return Self; }
}
