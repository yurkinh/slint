// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections;

namespace Slint.Markup;

/// <summary>The type of a property in <c>.slint</c> code, which decides how C# values become <c>.slint</c> values.</summary>
public sealed class SlintType
{
    private SlintType(string name, bool literal, string? import = null)
    {
        Name = name;
        HasLiteral = literal;
        Import = import;
    }

    internal string Name { get; }

    /// <summary>The file that a page imports the type from, for an enum of an application's <c>.slint</c> file.</summary>
    internal string? Import { get; }

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
    /// <summary><c>[string]</c>, such as the items of a <c>ComboBox</c>, from a list of strings</summary>
    public static SlintType StringArray { get; } = new("[string]", true);
    /// <summary><c>image</c>, from a <see cref="Slint.Image"/></summary>
    public static SlintType Image { get; } = new("image", false);

    /// <summary>
    /// The struct <paramref name="name"/>, such as <c>Date</c>, from a <see cref="Slint.Struct"/>,
    /// which a page imports from <paramref name="import"/> unless it's built in.
    /// </summary>
    public static SlintType Struct(string name, string? import = null) => new(name, false, import);

    /// <summary>
    /// The enum <paramref name="name"/>, from a C# enum that the builders generate,
    /// which a page imports from <paramref name="import"/> unless it's built in.
    /// </summary>
    public static SlintType Enum(string name, string? import = null) => new(name, false, import);
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

    /// <summary>
    /// The path of <paramref name="path"/> where the application loads its <c>.slint</c> files from:
    /// its directory, or with <c>SlintEmbedFiles</c> the files that the assembly embeds.
    /// The generated builders of an application's <c>.slint</c> files import their components from there.
    /// </summary>
    public static string ApplicationFile(string path) =>
        Path.GetFullPath(Path.Combine(Slint.Generated.GeneratedApi.BaseDirectory, path)).Replace('\\', '/');

    /// <summary>Adds a child.</summary>
    public void Add(Element child) => Children.Add(child);

    /// <summary>
    /// Adds the UI of <paramref name="component"/>, which keeps its own state, such as a field of the parent component.
    /// Its changes render the window that shows it again.
    /// </summary>
    public void Add(Component component) => Children.Add(new NestedComponent(component));

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

    /// <summary>
    /// Sets the property <paramref name="name"/> to a field of the row of a <see cref="ForEach{T}"/>,
    /// and passes the changes of the UI to the binding.
    /// </summary>
    protected void SetRowBinding(string name, SlintType type, RowBinding binding) =>
        Properties.Add(new PropertyValue(name, type, binding.Expression, null, null) { Row = binding });

    /// <summary>Handles the callback <paramref name="name"/>, whose arguments have the types <paramref name="parameters"/>.</summary>
    protected void SetHandler(string name, SlintType[] parameters, Action<object?[]> handler) =>
        Callbacks.Add(new CallbackHandler(name, parameters, handler, null));

    /// <summary>Handles the callback <paramref name="name"/> in a row of <see cref="ForEach{T}"/>.</summary>
    protected void SetHandler(string name, RowAction handler) =>
        Callbacks.Add(new CallbackHandler(name, [], _ => { }, handler));

    /// <summary>
    /// Handles the callback <paramref name="name"/> with <c>.slint</c> statements, which run without C#
    /// and name the callback's arguments <paramref name="arguments"/>.
    /// </summary>
    protected void SetHandler(string name, string[] arguments, Expr statements) =>
        Callbacks.Add(new CallbackHandler(name, [], _ => { }, null) { Statements = statements, ArgumentNames = arguments });

    /// <summary>The name that <c>.slint</c> code refers to this element by, or <c>null</c>.</summary>
    internal string? ElementId { get; set; }

    internal sealed record PropertyValue(string Name, SlintType Type, object? Value, Func<object?>? Get, Action<object?>? Set)
    {
        public RowBinding? Row { get; init; }

        /// <summary>Reads the value from the binding context, for <see cref="MarkupExtensions.Bind{TElement, TViewModel, TValue}(TElement, Property{TValue}, Func{TViewModel, TValue})"/>.</summary>
        public Func<object, object?>? ContextGet { get; init; }

        public Action<object, object?>? ContextSet { get; init; }
    }

    /// <summary>The object that the bindings of this element and its descendants read, such as a view model.</summary>
    internal object? Context { get; set; }

    /// <summary>Binds the property to the binding context.</summary>
    internal void SetContextBinding(string name, SlintType type, Func<object, object?> get, Action<object, object?>? set) =>
        Properties.Add(new PropertyValue(name, type, null, null, null) { ContextGet = get, ContextSet = set });

    /// <summary>
    /// Runs the command that <paramref name="command"/> reads from the binding context on <paramref name="callback"/>,
    /// and binds <paramref name="enabled"/>, if any, to whether it can run.
    /// </summary>
    internal void SetCommand(string callback, string? enabled, Func<object, System.Windows.Input.ICommand> command, Func<object, object?>? parameter)
    {
        Callbacks.Add(new CallbackHandler(callback, [], _ => { }, null) { Command = command, CommandParameter = parameter });
        if (enabled != null)
        {
            SetContextBinding(enabled, SlintType.Bool, context => command(context).CanExecute(parameter?.Invoke(context)), null);
        }
    }

    internal sealed record CallbackHandler(string Name, SlintType[] Parameters, Action<object?[]> Handler, RowAction? Row)
    {
        public Expr? Statements { get; init; }
        public string[] ArgumentNames { get; init; } = [];
        public Func<object, System.Windows.Input.ICommand>? Command { get; init; }
        public Func<object, object?>? CommandParameter { get; init; }
    }
}

/// <summary>The base of the builders; the generated part has the properties that every element has.</summary>
public abstract partial class Element<TSelf> : Element where TSelf : Element<TSelf>
{
    /// <inheritdoc />
    protected Element(string type, string? import = null) : base(type, import) { }

    /// <summary>
    /// Names the element, so that <c>.slint</c> expressions and statements can refer to it, such as <c>new Expr("picker.show()")</c>.
    /// The names must be unique in a page; the root of a page is <c>root</c>.
    /// </summary>
    public TSelf Id(string id) { ElementId = id; return (TSelf)this; }

    /// <summary>The column of the element in a <see cref="GridLayout"/>, like <c>Col</c>.</summary>
    public TSelf Column(int column) => Col(column);

    /// <summary>How many columns of a <see cref="GridLayout"/> the element spans, like <c>Colspan</c>.</summary>
    public TSelf ColumnSpan(int span) => Colspan(span);

    /// <summary>How many rows of a <see cref="GridLayout"/> the element spans, like <c>Rowspan</c>.</summary>
    public TSelf RowSpan(int span) => Rowspan(span);

    /// <summary>Sets a property that the builder has no method for.</summary>
    public TSelf Property(string name, SlintType type, object value) { SetValue(name, type, value); return (TSelf)this; }

    /// <summary>Binds a property that the builder has no method for.</summary>
    public TSelf Property<T>(string name, SlintType type, Func<T> get) { SetBinding(name, type, () => get()); return (TSelf)this; }

    /// <summary>Sets a property to a <c>.slint</c> expression.</summary>
    public TSelf Property(string name, SlintType type, Expr expression) { SetExpression(name, type, expression); return (TSelf)this; }
}

/// <summary>The place of a <see cref="Component"/> inside the tree of another, whose page renders it.</summary>
internal sealed class NestedComponent(Component component) : Element("component")
{
    public Component Component { get; } = component;
}
