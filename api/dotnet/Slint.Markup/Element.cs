// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections;

namespace Slint.Markup;

/// <summary>The type of a property in <c>.slint</c> code, which decides how C# values become <c>.slint</c> values.</summary>
public sealed class SlintType
{
    private SlintType(string name, bool literal)
    {
        Name = name;
        HasLiteral = literal;
    }

    internal string Name { get; }

    /// <summary>Whether a value of this type has a <c>.slint</c> literal; others become properties that C# sets once.</summary>
    internal bool HasLiteral { get; }

    /// <summary><c>string</c></summary>
    public static SlintType String { get; } = new("string", true);
    /// <summary><c>length</c>, from a <c>double</c> in logical pixels</summary>
    public static SlintType Length { get; } = new("length", true);
    /// <summary><c>float</c>, from a <c>double</c></summary>
    public static SlintType Float { get; } = new("float", true);
    /// <summary><c>int</c></summary>
    public static SlintType Int { get; } = new("int", true);
    /// <summary><c>bool</c></summary>
    public static SlintType Bool { get; } = new("bool", true);
    /// <summary><c>color</c>, from a <see cref="Slint.Color"/></summary>
    public static SlintType Color { get; } = new("color", true);
    /// <summary><c>brush</c>, from a <see cref="Slint.Color"/></summary>
    public static SlintType Brush { get; } = new("brush", true);
    /// <summary><c>duration</c>, from a <see cref="TimeSpan"/></summary>
    public static SlintType Duration { get; } = new("duration", true);
    /// <summary><c>angle</c>, from a <c>double</c> in degrees</summary>
    public static SlintType Angle { get; } = new("angle", true);
    /// <summary><c>image</c>, from a <see cref="Slint.Image"/></summary>
    public static SlintType Image { get; } = new("image", false);

    /// <summary>The enum <paramref name="name"/>, from a C# enum that the builders generate.</summary>
    public static SlintType Enum(string name) => new(name, false);
}

/// <summary>A <c>.slint</c> expression, such as <c>parent.width / 2</c>, for what the builders don't cover.</summary>
public sealed record Expr(string Code);

/// <summary>
/// An element of a page: a built-in element, a widget, or a component from a <c>.slint</c> file.
/// Add children with a collection initializer.
/// </summary>
public abstract partial class Element : IEnumerable<Element>
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
    protected void SetValue(string name, SlintType type, object value)
    {
        if (type.HasLiteral)
        {
            Properties.Add(new PropertyValue(name, type, value, null, null));
        }
        else
        {
            SetBinding(name, type, () => value);
        }
    }

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

/// <summary>The base of the builders; the generated part has the properties that every element has.</summary>
public abstract partial class Element<TSelf> : Element where TSelf : Element<TSelf>
{
    /// <inheritdoc />
    protected Element(string type, string? import = null) : base(type, import) { }

    /// <summary>Sets a property that the builder has no method for.</summary>
    public TSelf Property(string name, SlintType type, object value) { SetValue(name, type, value); return (TSelf)this; }

    /// <summary>Binds a property that the builder has no method for.</summary>
    public TSelf Property<T>(string name, SlintType type, Func<T> get) { SetBinding(name, type, () => get()); return (TSelf)this; }

    /// <summary>Sets a property to a <c>.slint</c> expression.</summary>
    public TSelf Property(string name, SlintType type, Expr expression) { SetExpression(name, type, expression); return (TSelf)this; }
}
