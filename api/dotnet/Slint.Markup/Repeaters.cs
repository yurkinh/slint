// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Slint.Markup;

/// <summary>
/// Shows its child only while <c>condition</c> is true.
/// It's Slint's <c>if</c>, so the page doesn't compile again when the condition changes.
/// </summary>
public sealed class If(Func<bool> condition) : Element("if")
{
    internal Func<bool> Condition { get; } = condition;
}

/// <summary>A callback handler in a row of <see cref="ForEach{T}"/>, which receives the row's item.</summary>
public sealed class RowAction
{
    private readonly Action<int> _invoke;

    internal RowAction(Action<int> invoke) => _invoke = invoke;

    internal void Invoke(int row) => _invoke(row);
}

/// <summary>The row of a <see cref="ForEach{T}"/>, for the template to refer to the item's fields.</summary>
public sealed class Row<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>
{
    private readonly ForEach<T> _owner;

    internal Row(ForEach<T> owner, string variable, string index)
    {
        _owner = owner;
        Variable = variable;
        Index = new Expr(index);
    }

    internal string Variable { get; }

    /// <summary>The row's index.</summary>
    public Expr Index { get; }

    /// <summary>The row's item, for items such as strings, which have no fields.</summary>
    public Expr Item => new(Variable);

    /// <summary>
    /// The field <paramref name="field"/> of the row's item, such as <c>row.Get(t => t.Title)</c>,
    /// or of a record in it, such as <c>row.Get(t => t.Owner.Name)</c>.
    /// </summary>
    public Expr Get<TField>(Expression<Func<T, TField>> field)
    {
        var path = new List<string>();
        var body = field.Body;
        while (body is MemberExpression { Member: PropertyInfo property } member)
        {
            path.Insert(0, Names.Kebab(property.Name));
            body = member.Expression;
        }
        return body is ParameterExpression && path.Count > 0
            ? new Expr($"{Variable}.{string.Join('.', path)}")
            : throw new ArgumentException("Pass properties of the item, such as t => t.Title", nameof(field));
    }

    /// <summary>A handler that receives the row's item.</summary>
    public RowAction Do(Action<T> action) => new(row => action(_owner.Current[row]));

    /// <summary>
    /// Shows the field <paramref name="field"/> of the row's item in a property that the user changes, such as <c>Checked</c>,
    /// and passes the change to <paramref name="set"/> with the item; the page updates after it.
    /// </summary>
    public RowBinding<TField> Bind<TField>(Expression<Func<T, TField>> field, Action<T, TField> set) =>
        new(Get(field), (row, value) => set(_owner.Current[row], Slint.Generated.Conversion.FromSlint<TField>(value)));
}

/// <summary>A field of the row of a <see cref="ForEach{T}"/> that a property shows and the user changes; see <see cref="Row{T}.Bind"/>.</summary>
public class RowBinding
{
    internal RowBinding(Expr expression, Action<int, object?> set)
    {
        Expression = expression;
        Set = set;
    }

    internal Expr Expression { get; }
    internal Action<int, object?> Set { get; }
}

/// <summary>A <see cref="RowBinding"/> of a field of type <typeparamref name="TValue"/>.</summary>
public sealed class RowBinding<TValue> : RowBinding
{
    internal RowBinding(Expr expression, Action<int, object?> set) : base(expression, set) { }
}

/// <summary>A <see cref="ForEach{T}"/> of any item type.</summary>
internal interface IRepeater
{
    string SlintType { get; }
    object? Read();
    Element Template(string variable, string index);
}

/// <summary>
/// Repeats the template for each item, with Slint's <c>for</c>, so the page doesn't compile again when the items change.
/// The page reads <c>items</c> again on each update. <typeparamref name="T"/>'s public properties become the fields of a struct,
/// and may be <c>string</c>, <c>bool</c>, <c>int</c>, <c>float</c>, <c>double</c>, <see cref="Color"/>, <see cref="TimeSpan"/>,
/// <see cref="Image"/>, an enum of the builders, or another type with such properties, which becomes a nested struct.
/// </summary>
public sealed class ForEach<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(Func<IEnumerable<T>> items, Func<Row<T>, Element> template) : Element("for"), IRepeater
{
    /// <summary>Repeats the template for each item of <paramref name="items"/>, such as a <see cref="SignalList{T}"/>, which the page reads again on each update.</summary>
    public ForEach(IEnumerable<T> items, Func<Row<T>, Element> template) : this(() => items, template) { }

    internal List<T> Current { get; private set; } = [];

    Element IRepeater.Template(string variable, string index) => template(new Row<T>(this, variable, index));

    string IRepeater.SlintType => $"[{StructFields.SlintType(typeof(T))}]";

    object? IRepeater.Read()
    {
        Current = [.. items()];
        return Current.Select(item => StructFields.Value(typeof(T), item)).ToList();
    }
}

/// <summary>Maps the public properties of a C# type to the fields of a <c>.slint</c> struct.</summary>
internal static class StructFields
{
    // The struct type and its values come from the same properties, so one that trimming removed is missing from both.
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The type and the values of a struct agree on the properties that trimming keeps.")]
    private static PropertyInfo[] Fields(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static bool IsStruct(Type type) =>
        !type.IsPrimitive && !type.IsEnum && type != typeof(string) && type != typeof(Color) && type != typeof(TimeSpan)
        && type != typeof(Image) && type != typeof(decimal);

    public static string SlintType(Type type) =>
        type == typeof(string) ? "string"
        : type == typeof(bool) ? "bool"
        : type == typeof(int) ? "int"
        : type == typeof(float) || type == typeof(double) ? "float"
        : type == typeof(Color) ? "color"
        : type == typeof(TimeSpan) ? "duration"
        : type == typeof(Image) ? "image"
        : type.IsEnum ? EnumName(type)
        : IsStruct(type) ? "{" + string.Join(", ", Fields(type).Select(f => $"{Names.Kebab(f.Name)}: {SlintType(f.PropertyType)}")) + "}"
        : throw new NotSupportedException($"The field type {type} isn't supported in ForEach");

    public static object? Value(Type type, object? value)
    {
        if (value == null || !IsStruct(type))
        {
            return value;
        }
        var row = new Struct();
        foreach (var field in Fields(type))
        {
            row[Names.Kebab(field.Name)] = Value(field.PropertyType, field.GetValue(value));
        }
        return row;
    }

    /// <summary>The <c>.slint</c> name of an enum that the builders, or the generated code, registered.</summary>
    private static string EnumName(Type type) =>
        Enum.GetValuesAsUnderlyingType(type) is { Length: > 0 } values
        && Slint.Generated.Conversion.TryToSlint(Enum.ToObject(type, values.GetValue(0)!), out var converted)
        && converted is EnumValue value
            ? value.EnumName
            : throw new NotSupportedException($"The enum {type} isn't one of a .slint file");
}
