// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;

namespace Slint.Markup;

/// <summary>
/// A window made from a tree of elements.
/// The page compiles the tree's <c>.slint</c> source once, and reuses it for another tree with the same source.
/// After a handler runs, the page reads its lambdas again and sets what changed; call <see cref="Update"/> after other changes.
/// </summary>
public sealed class Page : IDisposable
{
    private static readonly Dictionary<string, ComponentDefinition> Cache = new(StringComparer.Ordinal);
    private static Compiler? s_compiler;

    private readonly List<Binding> _bindings;
    private readonly object?[] _values;
    private readonly List<IDisposable> _watches = [];
    private static readonly object Unset = new();

    private Page(ComponentInstance instance, string source, List<Binding> bindings, List<Slot> slots, TimeSpan compileTime)
    {
        Instance = instance;
        Source = source;
        CompileTime = compileTime;
        _bindings = bindings;
        _values = Enumerable.Repeat(Unset, bindings.Count).ToArray();
        foreach (var slot in slots)
        {
            instance.SetCallback(slot.Name, args =>
            {
                slot.Handler(args);
                Update();
            });
        }
        Update();
        for (var i = 0; i < bindings.Count; i++)
        {
            var (binding, index) = (bindings[i], i);
            if (binding.Set == null)
            {
                continue;
            }
            _watches.Add(instance.WatchProperty(binding.Name, () =>
            {
                var value = instance.GetProperty(binding.Name);
                _values[index] = value;
                binding.Set(value);
                Update();
            }));
        }
    }

    /// <summary>The component instance, such as for its <see cref="ComponentInstance.Window"/>.</summary>
    public ComponentInstance Instance { get; }

    /// <summary>The <c>.slint</c> source that the tree turned into.</summary>
    public string Source { get; }

    /// <summary>How long compiling the source took, or zero when the page reused a compiled source.</summary>
    public TimeSpan CompileTime { get; }

    /// <summary>Creates a page from <paramref name="root"/>, a <see cref="WindowElement"/> or an element that the page puts in one.</summary>
    /// <exception cref="SlintException">The source doesn't compile, such as for an expression with an error.</exception>
    public static Page Create(Element root)
    {
        var writer = new SourceWriter();
        var source = writer.Write(root);
        var compileTime = TimeSpan.Zero;
        if (!Cache.TryGetValue(source, out var definition))
        {
            var stopwatch = Stopwatch.StartNew();
            s_compiler ??= new Compiler();
            using var result = s_compiler.BuildFromSource(source, "markup.slint");
            if (result.HasErrors)
            {
                throw new SlintException(
                    "The page doesn't compile:\n" + string.Join("\n", result.Diagnostics) + "\n" + source);
            }
            definition = result.Component(SourceWriter.ComponentName)!;
            compileTime = stopwatch.Elapsed;
            Cache[source] = definition;
        }
        return new Page(definition.Create(), source, writer.Bindings, writer.Slots, compileTime);
    }

    /// <summary>Forgets the compiled sources, such as after a change of the code that builds the trees.</summary>
    public static void ClearCache()
    {
        foreach (var definition in Cache.Values)
        {
            definition.Dispose();
        }
        Cache.Clear();
    }

    /// <summary>Reads the lambdas of the tree again, and sets the properties whose values changed.</summary>
    public void Update()
    {
        for (var i = 0; i < _bindings.Count; i++)
        {
            var binding = _bindings[i];
            var value = Convert(binding.Scalar, binding.Get());
            if (!Same(_values[i], value))
            {
                _values[i] = value;
                Instance.SetProperty(binding.Name, value);
            }
        }
    }

    /// <summary>Shows the window and runs the event loop until it closes.</summary>
    public void Run() => Instance.Run();

    /// <summary>Shows the window.</summary>
    public void Show() => Instance.Show();

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var watch in _watches)
        {
            watch.Dispose();
        }
        Instance.Dispose();
    }

    private static object? Convert(SlintType? type, object? value) =>
        type == SlintType.Length || type == SlintType.Float || type == SlintType.Angle
            ? System.Convert.ToDouble(value, CultureInfo.InvariantCulture)
            : type == SlintType.Int ? System.Convert.ToInt32(value, CultureInfo.InvariantCulture)
            : value;

    private static bool Same(object? a, object? b) => (a, b) switch
    {
        (IList x, IList y) => x.Count == y.Count && Enumerable.Range(0, x.Count).All(i => Same(x[i], y[i])),
        (Struct x, Struct y) => x.Count == y.Count && x.All(field => y.TryGetValue(field.Key, out var other) && Equals(field.Value, other)),
        _ => Equals(a, b),
    };
}
