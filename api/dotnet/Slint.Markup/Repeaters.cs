// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

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
public sealed class Row<T>
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

    /// <summary>The field <paramref name="field"/> of the row's item, such as <c>row.Get(t => t.Title)</c>.</summary>
    public Expr Get<TField>(Expression<Func<T, TField>> field) =>
        field.Body is MemberExpression { Member: PropertyInfo property }
            ? new Expr($"{Variable}.{Names.Kebab(property.Name)}")
            : throw new ArgumentException("Pass a property of the item, such as t => t.Title", nameof(field));

    /// <summary>A handler that receives the row's item.</summary>
    public RowAction Do(Action<T> action) => new(row => action(_owner.Current[row]));
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
/// and may be <c>string</c>, <c>bool</c>, <c>int</c>, <c>float</c>, or <c>double</c>.
/// </summary>
public sealed class ForEach<T>(Func<IEnumerable<T>> items, Func<Row<T>, Element> template) : Element("for"), IRepeater
{
    private static readonly PropertyInfo[] Fields = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    internal List<T> Current { get; private set; } = [];

    Element IRepeater.Template(string variable, string index) => template(new Row<T>(this, variable, index));

    string IRepeater.SlintType => "[{" + string.Join(", ", Fields.Select(f => $"{Names.Kebab(f.Name)}: {FieldType(f.PropertyType)}")) + "}]";

    object? IRepeater.Read()
    {
        Current = [.. items()];
        return Current.Select(item =>
        {
            var row = new Struct();
            foreach (var field in Fields)
            {
                row[Names.Kebab(field.Name)] = field.GetValue(item);
            }
            return row;
        }).ToList();
    }

    private static string FieldType(Type type) =>
        type == typeof(string) ? "string"
        : type == typeof(bool) ? "bool"
        : type == typeof(int) ? "int"
        : type == typeof(float) || type == typeof(double) ? "float"
        : throw new NotSupportedException($"The field type {type} isn't supported in ForEach");
}
