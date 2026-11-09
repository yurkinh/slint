// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Slint.Markup.HotReload))]

namespace Slint.Markup;

/// <summary>Where a page's compiled source came from.</summary>
public enum PageOrigin
{
    /// <summary>The page compiled its source.</summary>
    Compiled,
    /// <summary>The page read its source, compiled by an earlier run, from <see cref="Page.CacheDirectory"/>.</summary>
    DiskCache,
    /// <summary>An earlier page of this process had the same source.</summary>
    MemoryCache,
}

/// <summary>
/// A window made from a tree of elements.
/// The page compiles the tree's <c>.slint</c> source once, and reuses it for another tree with the same source,
/// in this process and, through <see cref="CacheDirectory"/>, in the next ones.
/// After a handler runs, the page reads its lambdas again and sets what changed; call <see cref="Update"/> after other changes.
/// </summary>
public sealed class Page : IDisposable
{
    private const string FileName = "markup.slint";
    private const int MaxCachedFiles = 256;
    private static readonly Dictionary<string, ComponentDefinition> Memory = new(StringComparer.Ordinal);
    private static readonly List<WeakReference<Page>> Pages = [];
    private static readonly object Unset = new();
    private static Compiler? s_compiler;
    private static bool s_embedded;

    private readonly Func<Element>? _render;
    private List<Binding> _bindings = [];
    private Dictionary<string, Action<object?[]>> _handlers = [];
    private object?[] _values = [];
    private readonly List<IDisposable> _watches = [];

    private Page(Func<Element>? render, Element root)
    {
        _render = render;
        var writer = new SourceWriter();
        Source = writer.Write(root);
        var (definition, origin, time) = Load(Source, writer.ImportedFiles);
        Origin = origin;
        CompileTime = time;
        Instance = definition.Create();
        Attach(writer);
        if (render != null)
        {
            lock (Pages)
            {
                Pages.Add(new WeakReference<Page>(this));
            }
        }
    }

    /// <summary>What runs after a handler of the page, or a change of the UI that a two-way binding passes on: <see cref="Update"/> unless a <see cref="Component"/> sets it.</summary>
    internal Action? AfterHandler { get; set; }

    /// <summary>The component that renders the page, which follows the signals that the lambdas read.</summary>
    internal Component? Owner { get; set; }

    /// <summary>What a hot reload runs instead of <see cref="Rebuild"/>, for a <see cref="Component"/>.</summary>
    internal Action? Reload { get; set; }

    /// <summary>Whether a handler of the page is running.</summary>
    internal bool Handling { get; private set; }

    /// <summary>The component instance, such as for its <see cref="ComponentInstance.Window"/>; a rebuild can replace it.</summary>
    public ComponentInstance Instance { get; private set; }

    /// <summary>The <c>.slint</c> source that the tree turned into.</summary>
    public string Source { get; private set; }

    /// <summary>Where the compiled source came from.</summary>
    public PageOrigin Origin { get; private set; }

    /// <summary>How long compiling or reading the compiled source took; zero for <see cref="PageOrigin.MemoryCache"/>.</summary>
    public TimeSpan CompileTime { get; private set; }

    /// <summary>
    /// The directory of the compiled sources that later runs read instead of compiling, or <c>null</c> to keep them in memory only.
    /// Defaults to a <c>Slint/markup</c> directory in the user's local application data.
    /// </summary>
    public static string? CacheDirectory { get; set; } = DefaultCacheDirectory();

    /// <summary>
    /// The style of the widgets, such as <c>fluent</c> or <c>material</c>, or <c>null</c> for the platform's style.
    /// Set it before the first page; pages of a process share their compiled sources.
    /// </summary>
    public static string? Style { get; set; }

    /// <summary>Creates a page from <paramref name="root"/>, a <see cref="WindowElement"/> or an element that the page puts in one.</summary>
    /// <exception cref="SlintException">The source doesn't compile, such as for an expression with an error.</exception>
    public static Page Create(Element root) => new(null, root);

    /// <summary>
    /// Creates a page from the tree that <paramref name="render"/> returns.
    /// After a hot reload of the C# code, the page calls it again; see <see cref="Rebuild"/>.
    /// </summary>
    /// <exception cref="SlintException">The source doesn't compile, such as for an expression with an error.</exception>
    public static Page Create(Func<Element> render) => new(render, render());

    /// <summary>Forgets the compiled sources of this process; the files in <see cref="CacheDirectory"/> stay.</summary>
    public static void ClearCache()
    {
        foreach (var definition in Memory.Values)
        {
            definition.Dispose();
        }
        Memory.Clear();
    }

    /// <summary>
    /// Builds the tree again with the function that the page was created with.
    /// With the same source, the page takes the new lambdas and handlers; with another,
    /// it replaces its instance, and a visible window keeps its position and size.
    /// </summary>
    public void Rebuild()
    {
        if (_render == null)
        {
            return;
        }
        var writer = new SourceWriter();
        var source = writer.Write(_render());
        if (source == Source)
        {
            Attach(writer, keepValues: true);
            return;
        }
        var (definition, origin, time) = Load(source, writer.ImportedFiles);
        var old = Instance;
        var visible = old.Window.IsVisible;
        var (position, size) = (old.Window.Position, old.Window.Size);
        DisposeWatches();
        Instance = definition.Create();
        (Source, Origin, CompileTime) = (source, origin, time);
        Attach(writer);
        if (visible)
        {
            Instance.Window.Position = position;
            Instance.Window.Size = size;
            // Showing the new window first keeps the event loop from ending with its last window.
            Instance.Show();
            old.Hide();
        }
        old.Dispose();
    }

    /// <summary>Reads the lambdas of the tree again, and sets the properties whose values changed.</summary>
    public void Update()
    {
        if (Owner != null)
        {
            Owner.Tracking(() =>
            {
                SetChangedValues();
                return 0;
            });
        }
        else
        {
            SetChangedValues();
        }
    }

    private void SetChangedValues()
    {
        for (var i = 0; i < _bindings.Count; i++)
        {
            var binding = _bindings[i];
            var value = ToSlint(binding.Scalar, binding.Get());
            if (!Same(_values[i], value))
            {
                _values[i] = value;
                Instance.SetProperty(binding.Name, value);
            }
        }
    }

    /// <summary>Shows the window and runs the event loop until the last window closes.</summary>
    public void Run()
    {
        Instance.Show();
        EventLoop.Run();
    }

    /// <summary>Shows the window.</summary>
    public void Show() => Instance.Show();

    /// <summary>Hides the window, and releases the instance.</summary>
    public void Dispose()
    {
        DisposeWatches();
        if (Instance.Window.IsVisible)
        {
            Instance.Hide();
        }
        Instance.Dispose();
        lock (Pages)
        {
            Pages.RemoveAll(p => !p.TryGetTarget(out var page) || page == this);
        }
    }

    /// <summary>Rebuilds every page created with a function, after a hot reload.</summary>
    internal static void RebuildAll()
    {
        List<Page> pages;
        lock (Pages)
        {
            pages = [.. Pages.Select(p => p.TryGetTarget(out var page) ? page : null).OfType<Page>()];
        }
        foreach (var page in pages)
        {
            (page.Reload ?? page.Rebuild)();
        }
    }

    /// <summary>
    /// Connects the bindings and callbacks of <paramref name="writer"/> to the instance.
    /// For the same source on the same instance, it swaps the C# side only, and sets the values that differ from the ones already set:
    /// Slint doesn't allow replacing the handler of a callback while it runs, such as when a handler changes the state of a <see cref="Component"/>.
    /// </summary>
    private void Attach(SourceWriter writer, bool keepValues = false)
    {
        _bindings = writer.Bindings;
        _handlers = writer.Slots.ToDictionary(s => s.Name, s => s.Handler);
        if (keepValues && _values.Length == _bindings.Count)
        {
            Update();
            return;
        }
        DisposeWatches();
        _values = Enumerable.Repeat(Unset, _bindings.Count).ToArray();
        var instance = Instance;
        foreach (var name in _handlers.Keys)
        {
            instance.SetCallback(name, args => Handle(() => _handlers[name](args)));
        }
        Update();
        for (var i = 0; i < _bindings.Count; i++)
        {
            var index = i;
            if (_bindings[index].Set == null)
            {
                continue;
            }
            _watches.Add(instance.WatchProperty(_bindings[index].Name, () =>
            {
                var value = instance.GetProperty(_bindings[index].Name);
                // Slint returns numbers as doubles; keep the value as Update converts it, so it compares equal.
                _values[index] = ToSlint(_bindings[index].Scalar, value);
                Handle(() => _bindings[index].Set!(value));
            }));
        }
    }

    private void Handle(Action handler)
    {
        Handling = true;
        try
        {
            handler();
        }
        finally
        {
            Handling = false;
        }
        (AfterHandler ?? Update)();
    }

    private void DisposeWatches()
    {
        foreach (var watch in _watches)
        {
            watch.Dispose();
        }
        _watches.Clear();
    }

    private static (ComponentDefinition, PageOrigin, TimeSpan) Load(string source, IEnumerable<string> imports)
    {
        if (Memory.TryGetValue(source, out var cached))
        {
            return (cached, PageOrigin.MemoryCache, TimeSpan.Zero);
        }
        var stopwatch = Stopwatch.StartNew();
        if (s_compiler == null)
        {
            s_compiler = new Compiler();
            s_embedded = Slint.Generated.GeneratedApi.UseEmbeddedFiles(s_compiler);
        }
        if (Style != null && s_compiler.Style != Style)
        {
            s_compiler.Style = Style;
        }
        var file = CacheDirectory is { } directory ? Path.Combine(directory, Key(source, imports) + ".unit") : null;
        var origin = PageOrigin.DiskCache;
        var result = file != null ? ReadCached(file) : null;
        if (result == null)
        {
            origin = PageOrigin.Compiled;
            var unit = s_compiler.BuildUnit(source, FileName) ?? throw CompileError(source);
            if (file != null)
            {
                WriteCached(file, unit);
            }
            result = CompilationResult.FromUnit(unit)!;
        }
        using (result)
        {
            var definition = result.Component(SourceWriter.ComponentName)!;
            Memory[source] = definition;
            return (definition, origin, stopwatch.Elapsed);
        }
    }

    /// <summary>The diagnostics of a source that doesn't compile, which the compiler reports when compiling it again.</summary>
    private static SlintException CompileError(string source)
    {
        using var result = s_compiler!.BuildFromSource(source, FileName);
        var diagnostics = result.Diagnostics.Where(d => d.Level is DiagnosticLevel.Error or DiagnosticLevel.Warning);
        return new SlintException("The page doesn't compile:\n" + string.Join("\n", diagnostics) + "\n" + source);
    }

    /// <summary>
    /// The name of the cache file of <paramref name="source"/>, from what the compiled source depends on:
    /// the Slint version, the style, the source, and the <c>.slint</c> files around the files it imports,
    /// which those may import in turn, or the build of the application when it embeds them.
    /// </summary>
    private static string Key(string source, IEnumerable<string> imports)
    {
        var style = s_compiler!.Style ?? Environment.GetEnvironmentVariable("SLINT_STYLE") ?? "";
        var version = typeof(Compiler).Assembly.GetName().Version;
        var key = new StringBuilder($"{version}\n{style}\n{source}");
        // Embedded files change only with a build of the application.
        if (s_embedded)
        {
            key.Append('\n').Append(System.Reflection.Assembly.GetEntryAssembly()?.ManifestModule.ModuleVersionId);
        }
        foreach (var directory in imports.Select(Path.GetDirectoryName).OfType<string>().Distinct().Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.slint", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var info = new FileInfo(file);
                key.Append('\n').Append(file).Append(' ').Append(info.Length).Append(' ').Append(info.LastWriteTimeUtc.Ticks);
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString())));
    }

    // The cache is an optimization: a file that can't be read or written means compiling again.
    private static CompilationResult? ReadCached(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            var result = CompilationResult.FromUnit(File.ReadAllBytes(file));
            if (result == null)
            {
                File.Delete(file);
            }
            else
            {
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
            }
            return result;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static void WriteCached(string file, byte[] unit)
    {
        try
        {
            var directory = Path.GetDirectoryName(file)!;
            Directory.CreateDirectory(directory);
            var temporary = file + "." + Environment.ProcessId + ".tmp";
            File.WriteAllBytes(temporary, unit);
            File.Move(temporary, file, overwrite: true);
            var files = new DirectoryInfo(directory).GetFiles("*.unit");
            foreach (var old in files.OrderByDescending(f => f.LastWriteTimeUtc).Skip(MaxCachedFiles))
            {
                old.Delete();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string? DefaultCacheDirectory()
    {
        var data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(data) ? null : Path.Combine(data, "Slint", "markup");
    }

    private static object? ToSlint(SlintType? type, object? value) =>
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

/// <summary>Rebuilds the pages after .NET hot reload applies a change of the code.</summary>
internal static class HotReload
{
    public static void UpdateApplication(Type[]? types)
    {
        _ = types;
        // Hot reload calls this on another thread; pages belong to the Slint thread.
        EventLoop.InvokeFromEventLoop(Page.RebuildAll);
    }
}
