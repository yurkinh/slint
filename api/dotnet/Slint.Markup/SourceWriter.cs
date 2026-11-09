// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Globalization;
using System.Text;

namespace Slint.Markup;

internal static class Names
{
    /// <summary>Turns a C# name such as <c>IsDone</c> into a <c>.slint</c> name such as <c>is-done</c>.</summary>
    public static string Kebab(string name)
    {
        var result = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsUpper(c) && result.Length > 0)
            {
                result.Append('-');
            }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
}

/// <summary>A property of the page's root, which C# sets from a lambda of the tree.</summary>
internal sealed class Binding(string name, string type, SlintType? scalar, Func<object?> get, Action<object?>? set)
{
    public string Name { get; } = name;
    public string Type { get; } = type;
    public SlintType? Scalar { get; } = scalar;
    public Func<object?> Get { get; } = get;
    public Action<object?>? Set { get; } = set;
}

/// <summary>A callback of the page's root, which an element's handler forwards to.</summary>
internal sealed record Slot(string Name, Action<object?[]> Handler);

/// <summary>The <c>.slint</c> source of a tree of elements, with the bindings and callbacks that connect it to C#.</summary>
internal sealed class SourceWriter
{
    public const string ComponentName = "MarkupPage";

    private readonly StringBuilder _body = new();
    private readonly SortedSet<string> _widgets = new(StringComparer.Ordinal);
    private readonly List<string> _declarations = [];
    private readonly List<Element> _popups = [];
    private int _rows;

    /// <summary>The files that the source imports, other than the standard widgets.</summary>
    public IEnumerable<string> ImportedFiles => _widgets.Select(w => w.Split('|')[1]).Where(Path.IsPathRooted).Distinct();

    public List<Binding> Bindings { get; } = [];
    public List<Slot> Slots { get; } = [];

    /// <summary>Writes the tree of a page, and at its root the popups of the component that renders it and of the components in the tree.</summary>
    public string Write(Element root)
    {
        if (Component.Host is { } owner)
        {
            _popups.AddRange(owner.RenderPopups());
        }
        if (root.Type == "Window" && root.Import == null)
        {
            _context = root.Context;
            WriteContents(root, 1, null);
            _context = null;
        }
        else
        {
            WriteElement(root, 1, null);
        }
        foreach (var popup in _popups)
        {
            WriteElement(popup, 1, null);
        }

        var source = new StringBuilder();
        foreach (var import in _widgets.GroupBy(w => w.Split('|')[1]))
        {
            source.Append("import { ").AppendJoin(", ", import.Select(w => w.Split('|')[0]).Order(StringComparer.Ordinal)).Append(" } from \"").Append(import.Key).AppendLine("\";");
        }
        source.Append("export component ").Append(ComponentName).AppendLine(" inherits Window {");
        foreach (var declaration in _declarations)
        {
            source.Append("    ").AppendLine(declaration);
        }
        source.Append(_body).AppendLine("}");
        return source.ToString();
    }

    /// <summary>The objects whose notifications update the page: binding contexts, collections, and commands.</summary>
    public HashSet<object> Notifiers { get; } = new(ReferenceEqualityComparer.Instance);

    private object? _context;

    private object Context(string what) =>
        _context ?? throw new InvalidOperationException($"{what} binds to a view model, but no element around it has a BindingContext");

    private void WriteElement(Element element, int depth, string? rowIndex)
    {
        var previous = _context;
        _context = element.Context ?? _context;
        try
        {
            WriteElementInContext(element, depth, rowIndex);
        }
        finally
        {
            _context = previous;
        }
    }

    private void WriteElementInContext(Element element, int depth, string? rowIndex)
    {
        var indent = new string(' ', depth * 4);
        switch (element)
        {
            case If condition:
                {
                    var name = Bind("bool", SlintType.Bool, () => condition.Condition(), null);
                    _body.Append(indent).Append("if root.").Append(name).AppendLine(" : ");
                    WriteElement(Single(element), depth, rowIndex);
                    return;
                }
            case NestedComponent nested:
                WriteElement(nested.Component.RenderNested(), depth, rowIndex);
                _popups.AddRange(nested.Component.RenderPopups());
                return;
            case IRepeater forEach:
                {
                    var row = $"row{_rows}";
                    var index = $"index{_rows++}";
                    var name = Bind(forEach.SlintType, null, forEach.Read, null);
                    _body.Append(indent).Append("for ").Append(row).Append('[').Append(index).Append("] in root.").Append(name).AppendLine(" : ");
                    WriteElement(forEach.Template(row, index), depth, index);
                    return;
                }
        }

        if (element.Import != null)
        {
            _widgets.Add(element.Type + "|" + element.Import);
        }
        _body.Append(indent);
        if (element.ElementId != null)
        {
            _body.Append(element.ElementId).Append(" := ");
        }
        _body.Append(element.Type).AppendLine(" {");
        WriteContents(element, depth + 1, rowIndex);
        _body.Append(indent).AppendLine("}");
    }

    private void WriteContents(Element element, int depth, string? rowIndex)
    {
        var indent = new string(' ', depth * 4);
        foreach (var property in element.Properties)
        {
            _body.Append(indent).Append(property.Name);
            if (property.ContextGet is { } contextGet)
            {
                var context = Context($"'{property.Name}'");
                Notifiers.Add(context);
                var contextSet = property.ContextSet;
                var name = Bind(property.Type.Name, property.Type, () => contextGet(context),
                    contextSet == null ? null : value => contextSet(context, value));
                _body.Append(contextSet != null ? " <=> root." : ": root.").Append(name);
            }
            else if (property.Get != null)
            {
                var name = Bind(property.Type.Name, property.Type, property.Get, property.Set);
                _body.Append(property.Set != null ? " <=> root." : ": root.").Append(name);
            }
            else if (property.Value is Expr expression)
            {
                _body.Append(": ").Append(expression.Code);
                if (property.Row is { } row)
                {
                    if (rowIndex == null)
                    {
                        throw new InvalidOperationException($"'{property.Name}' is bound to a row's field, so it must be in a ForEach");
                    }
                    var slot = $"c{Slots.Count}";
                    Slots.Add(new Slot(slot, args => row.Set(Convert.ToInt32(args[0], CultureInfo.InvariantCulture), args[1])));
                    _declarations.Add($"callback {slot}(int, {property.Type.Name});");
                    // The widget's own changes, such as a click on a check box, go back to C#.
                    _body.AppendLine(";").Append(indent).Append("changed ").Append(property.Name)
                        .Append(" => { root.").Append(slot).Append('(').Append(rowIndex).Append(", self.").Append(property.Name).Append("); }");
                    _body.AppendLine();
                    continue;
                }
            }
            else
            {
                _body.Append(": ").Append(Literal(property.Type, property.Value!));
            }
            _body.AppendLine(";");
        }

        // A callback with several handlers, such as C# and then .slint statements, runs them in order.
        foreach (var handlers in element.Callbacks.GroupBy(c => c.Name))
        {
            var count = handlers.Max(h => h.Statements != null ? h.ArgumentNames.Length : h.Parameters.Length);
            var named = handlers.FirstOrDefault(h => h.Statements != null && h.ArgumentNames.Length == count);
            var parameters = named?.ArgumentNames.ToList() ?? Enumerable.Range(0, count).Select(i => $"a{i}").ToList();
            var statements = new List<string>();
            foreach (var callback in handlers)
            {
                if (callback.Statements is { } code)
                {
                    statements.Add(code.Code);
                    continue;
                }
                if (callback.Command is { } command)
                {
                    var context = Context($"The command of '{callback.Name}'");
                    var parameter = callback.CommandParameter;
                    Notifiers.Add(command(context));
                    var slot = $"c{Slots.Count}";
                    Slots.Add(new Slot(slot, _ =>
                    {
                        var value = parameter?.Invoke(context);
                        var current = command(context);
                        if (current.CanExecute(value))
                        {
                            current.Execute(value);
                        }
                    }));
                    _declarations.Add($"callback {slot}();");
                    statements.Add($"root.{slot}();");
                    continue;
                }
                foreach (var parameter in callback.Parameters.Where(p => p.Import != null))
                {
                    _widgets.Add(parameter.Name + "|" + parameter.Import);
                }
                var name = $"c{Slots.Count}";
                var types = callback.Parameters.Select(p => p.Name).ToList();
                var arguments = parameters.Take(callback.Parameters.Length).ToList();
                if (callback.Row != null)
                {
                    if (rowIndex == null)
                    {
                        throw new InvalidOperationException($"The handler of '{callback.Name}' takes a row's item, so it must be in a ForEach");
                    }
                    types.Add("int");
                    arguments.Add(rowIndex);
                    var row = callback.Row;
                    Slots.Add(new Slot(name, args => row.Invoke(Convert.ToInt32(args[^1], CultureInfo.InvariantCulture))));
                }
                else
                {
                    Slots.Add(new Slot(name, callback.Handler));
                }
                _declarations.Add($"callback {name}({string.Join(", ", types)});");
                statements.Add($"root.{name}({string.Join(", ", arguments)});");
            }
            _body.Append(indent).Append(handlers.Key);
            if (parameters.Count > 0)
            {
                _body.Append('(').AppendJoin(", ", parameters).Append(')');
            }
            _body.Append(" => { ").AppendJoin(" ", statements).AppendLine(" }");
        }

        foreach (var child in element.Children)
        {
            WriteElement(child, depth, rowIndex);
        }
    }

    private string Bind(string type, SlintType? scalar, Func<object?> get, Action<object?>? set)
    {
        if (scalar?.Import is { } import)
        {
            _widgets.Add(type + "|" + import);
        }
        var name = $"p{Bindings.Count}";
        Bindings.Add(new Binding(name, type, scalar, get, set));
        _declarations.Add($"{(set != null ? "in-out" : "in")} property <{type}> {name};");
        return name;
    }

    private static Element Single(Element element) =>
        element.Children.Count == 1
            ? element.Children[0]
            : throw new InvalidOperationException("An If needs exactly one child; put several in a layout");

    private static string Literal(SlintType type, object value) => value switch
    {
        string text when type == SlintType.String => Quote(text),
        IEnumerable<string> items when type == SlintType.StringArray => "[" + string.Join(", ", items.Select(Quote)) + "]",
        bool b when type == SlintType.Bool => b ? "true" : "false",
        Color c when type == SlintType.Color || type == SlintType.Brush => $"#{c.Red:x2}{c.Green:x2}{c.Blue:x2}{c.Alpha:x2}",
        TimeSpan d when type == SlintType.Duration => Number(d.TotalMilliseconds) + "ms",
        _ when type == SlintType.Length => Number(value) + "px",
        _ when type == SlintType.Angle => Number(value) + "deg",
        _ when type == SlintType.Float || type == SlintType.Int => Number(value),
        _ => throw new ArgumentException($"{value} isn't a {type.Name}"),
    };

    private static string Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);

    private static string Quote(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
        {
            quoted.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                _ => c.ToString(),
            });
        }
        return quoted.Append('"').ToString();
    }
}
