// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Slint.Markup;

/// <summary>A value whose changes render again the <see cref="Component"/> that read it.</summary>
public interface ISignal
{
    /// <summary>Raised after the value changes, on the thread that changed it.</summary>
    event Action? Changed;
}

/// <summary>
/// A value, such as <c>readonly Signal&lt;int&gt; count = 0;</c> in a component or in state that components share.
/// A component that reads <see cref="Value"/> while it renders, or in a lambda of its tree, renders again when the value changes.
/// </summary>
public sealed class Signal<T>(T value) : ISignal
{
    private T _value = value;

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>The value.</summary>
    public T Value
    {
        get
        {
            Tracker.Track(this);
            return _value;
        }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                Changed?.Invoke();
            }
        }
    }

    /// <summary>A signal with the value <paramref name="value"/>.</summary>
    public static implicit operator Signal<T>(T value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value?.ToString() ?? "";
}

/// <summary>
/// A list, such as the items of a <see cref="ForEach{T}"/>, whose changes render again the components that read it, like a <see cref="Signal{T}"/>.
/// </summary>
public sealed class SignalList<T> : IList<T>, IReadOnlyList<T>, ISignal
{
    private readonly List<T> _items;

    /// <summary>An empty list.</summary>
    public SignalList() => _items = [];

    /// <summary>A list with <paramref name="items"/>.</summary>
    public SignalList(IEnumerable<T> items) => _items = [.. items];

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc cref="IList{T}.this" />
    public T this[int index]
    {
        get => Read()[index];
        set
        {
            _items[index] = value;
            Changed?.Invoke();
        }
    }

    /// <inheritdoc cref="ICollection{T}.Count" />
    public int Count => Read().Count;

    bool ICollection<T>.IsReadOnly => false;

    /// <inheritdoc />
    public void Add(T item)
    {
        _items.Add(item);
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public bool Remove(T item)
    {
        var removed = _items.Remove(item);
        if (removed)
        {
            Changed?.Invoke();
        }
        return removed;
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        Changed?.Invoke();
    }

    /// <summary>Removes the items that match <paramref name="match"/>, and returns how many.</summary>
    public int RemoveAll(Predicate<T> match)
    {
        var removed = _items.RemoveAll(match);
        if (removed > 0)
        {
            Changed?.Invoke();
        }
        return removed;
    }

    /// <summary>Replaces the items with <paramref name="items"/>.</summary>
    public void Reset(IEnumerable<T> items)
    {
        _items.Clear();
        _items.AddRange(items);
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Clear()
    {
        _items.Clear();
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public bool Contains(T item) => Read().Contains(item);

    /// <inheritdoc />
    public int IndexOf(T item) => Read().IndexOf(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => Read().CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => Read().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private List<T> Read()
    {
        Tracker.Track(this);
        return _items;
    }
}

/// <summary>What follows the signals that it reads: a component, or a <see cref="Computed{T}"/>.</summary>
internal interface ISignalObserver
{
    void Observe(ISignal signal);
}

/// <summary>Passes the signals that code reads to the observer that runs it, on this thread.</summary>
internal static class Tracker
{
    [ThreadStatic]
    private static ISignalObserver? t_observer;

    public static void Track(ISignal signal) => t_observer?.Observe(signal);

    public static T Run<T>(ISignalObserver observer, Func<T> action)
    {
        var previous = t_observer;
        t_observer = observer;
        try
        {
            return action();
        }
        finally
        {
            t_observer = previous;
        }
    }
}

/// <summary>
/// A value computed from signals, like Comet's: it computes again after a change of a signal that it read,
/// and a component that reads it renders again then.
/// </summary>
public sealed class Computed<T>(Func<T> compute) : ISignal, ISignalObserver
{
    private readonly HashSet<ISignal> _dependencies = [];
    private T _value = default!;
    private bool _dirty = true;

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>The value, which the first read after a change of a dependency computes.</summary>
    public T Value
    {
        get
        {
            Tracker.Track(this);
            if (_dirty)
            {
                _value = Tracker.Run(this, compute);
                _dirty = false;
            }
            return _value;
        }
    }

    void ISignalObserver.Observe(ISignal signal)
    {
        if (_dependencies.Add(signal))
        {
            signal.Changed += OnDependencyChanged;
        }
    }

    private void OnDependencyChanged()
    {
        if (!_dirty)
        {
            _dirty = true;
            Changed?.Invoke();
        }
    }

    /// <inheritdoc />
    public override string ToString() => Value?.ToString() ?? "";
}

/// <summary>
/// A window whose UI <see cref="Render"/> returns, like a view of Comet.
/// After a handler of the UI, or a change of a <see cref="Signal{T}"/> or <see cref="SignalList{T}"/> that it read
/// while it rendered or in a lambda of its tree, the component renders again.
/// A tree with the same <c>.slint</c> source only updates the values;
/// a C# condition that changes the tree compiles another source, so prefer <see cref="If"/> and <see cref="ForEach{T}"/>.
/// </summary>
public abstract class Component : IDisposable, ISignalObserver
{
    private Page? _page;
    private Component? _host;
    private int _slintThread;
    private bool _dirty;
    private bool _rendering;
    private bool _expectNewStructure;
    private readonly HashSet<ISignal> _signals = [];

    private int _posted;

    /// <summary>The component whose page renders, during that, on this thread: the host of the components inside its tree.</summary>
    [ThreadStatic]
    private static Component? t_host;

    /// <summary>Returns the UI, from the state of the component.</summary>
    protected abstract Element Render();

    /// <summary>
    /// Returns the popups of the component, such as a <c>DatePickerPopup</c> with an <c>Id</c>, which the page puts at its root.
    /// The tree refers to them by their ids, such as <c>new Expr("date-picker.show()")</c>.
    /// Slint doesn't allow a component that inherits <c>PopupWindow</c> inside an <c>if</c> or a <c>for</c>,
    /// such as the <see cref="If"/> that shows a page of a gallery, so put such popups here.
    /// </summary>
    protected virtual IEnumerable<Element> Popups() => [];

    internal IEnumerable<Element> RenderPopups() => Popups();

    /// <summary>The component whose page renders on this thread, or <c>null</c>.</summary>
    internal static Component? Host => t_host;

    /// <summary>The page, which the first use creates.</summary>
    public Page Page => _page ?? CreatePage();

    /// <summary>The <see cref="Markup.Navigation"/> that shows this component, or <c>null</c>.</summary>
    public Navigation? Navigation { get; internal set; }

    /// <summary>Shows the window.</summary>
    public void Show() => Page.Show();

    /// <summary>Shows the window and runs the event loop until the last window closes.</summary>
    public void Run() => Page.Run();

    /// <summary>
    /// Renders the component again with the current state.
    /// During a handler of the UI, it waits until the handler returns, so several changes render once.
    /// </summary>
    protected void Invalidate()
    {
        if (_host != null)
        {
            _host.Invalidate();
            return;
        }
        if (_page == null)
        {
            return;
        }
        if (Environment.CurrentManagedThreadId != _slintThread)
        {
            // The changes from other threads before the Slint thread gets to it render once.
            if (Interlocked.Exchange(ref _posted, 1) == 0)
            {
                EventLoop.InvokeFromEventLoop(() =>
                {
                    Volatile.Write(ref _posted, 0);
                    Invalidate();
                });
            }
            return;
        }
        if (_page.Handling || _rendering)
        {
            _dirty = true;
            return;
        }
        Refresh();
    }

    /// <summary>Renders the component again, where another structure is expected, such as after navigation.</summary>
    internal void InvalidateStructure()
    {
        (_host ?? this)._expectNewStructure = true;
        Invalidate();
    }

    /// <summary>Hides the window, releases the page, and stops following the signals that the component read.</summary>
    public void Dispose()
    {
        foreach (var signal in _signals)
        {
            signal.Changed -= Invalidate;
        }
        _signals.Clear();
        _page?.Dispose();
        GC.SuppressFinalize(this);
    }

    void ISignalObserver.Observe(ISignal signal)
    {
        if (_signals.Add(signal))
        {
            signal.Changed += Invalidate;
        }
    }

    /// <summary>Runs <paramref name="action"/> with this component as the one that follows the signals it reads.</summary>
    internal T Tracking<T>(Func<T> action) => Tracker.Run(this, action);

    /// <summary>Returns the UI of this component inside the component whose page renders it.</summary>
    internal Element RenderNested()
    {
        var host = t_host ?? throw new InvalidOperationException(
            $"{GetType().Name} is inside the tree of a page that no Component renders; add it to the tree of a Component");
        if (host == this)
        {
            throw new InvalidOperationException($"{GetType().Name} is inside its own tree");
        }
        _host = host;
        return Render();
    }

    private Page CreatePage()
    {
        _slintThread = Environment.CurrentManagedThreadId;
        _page = Rendering(() => Page.Create(Render));
        _page.Owner = this;
        _page.AfterHandler = Refresh;
        // A hot reload changes the code, so another structure is expected there.
        _page.Reload = () => Rendering(() =>
        {
            _page.Rebuild();
            return 0;
        });
        return _page;
    }

    private T Rendering<T>(Func<T> render)
    {
        var previous = t_host;
        t_host = this;
        _rendering = true;
        try
        {
            return Tracking(render);
        }
        finally
        {
            _rendering = false;
            t_host = previous;
        }
    }

    private void Refresh()
    {
        _dirty = false;
        var before = _page!.Source;
        var expected = _expectNewStructure;
        _expectNewStructure = false;
        Rendering(() =>
        {
            _page.Rebuild();
            return 0;
        });
        if (_page.Source != before && !expected && s_warnOnRecompile)
        {
            Console.Error.WriteLine(
                $"{GetType().Name}.Render() returned another structure after a change of state, so the page compiles another source. " +
                "Use If and ForEach for parts that come and go, so that the page compiles once.");
        }
        if (_dirty)
        {
            Refresh();
        }
    }

    // Only in Debug builds of the application, where the warning helps and costs nothing that matters.
    private static readonly bool s_warnOnRecompile =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled == true;
}

/// <summary>A <see cref="Component"/> whose state is a <typeparamref name="TState"/>, which <see cref="SetState(Action{TState})"/> changes.</summary>
public abstract class Component<TState> : Component where TState : new()
{
    /// <summary>The state.</summary>
    protected TState State { get; private set; } = new();

    /// <summary>Changes the state, and renders the component again.</summary>
    protected void SetState(Action<TState> change)
    {
        change(State);
        Invalidate();
    }

    /// <summary>Replaces the state, such as with a changed copy of a record, and renders the component again.</summary>
    protected void SetState(TState state)
    {
        State = state;
        Invalidate();
    }
}

/// <summary>
/// A stack of pages, such as the pages of a tab: it shows the top one,
/// and the pages that it shows <see cref="Push"/> others, such as a page with the details of an item, and <see cref="Pop"/> back.
/// Keep it in a field of the component that shows it, like other components inside a component.
/// Each combination of pages compiles once; the window is a new one after a change of page, at the same place.
/// </summary>
public sealed class Navigation : Component
{
    private readonly Stack<Component> _pages = new();

    /// <summary>A stack with the page <paramref name="root"/>.</summary>
    public Navigation(Component root) => Add(root);

    /// <summary>The page on top.</summary>
    public Component Current => _pages.Peek();

    /// <summary>How many pages the stack has, with the first one.</summary>
    public int Depth => _pages.Count;

    /// <summary>Shows <paramref name="page"/> on top.</summary>
    public void Push(Component page)
    {
        Add(page);
        InvalidateStructure();
    }

    /// <summary>Goes back to the page below, and releases the top one; the first page stays.</summary>
    public void Pop()
    {
        if (_pages.Count > 1)
        {
            _pages.Pop().Dispose();
            InvalidateStructure();
        }
    }

    /// <inheritdoc />
    protected override Element Render() => new VerticalLayout { Current };

    private void Add(Component page)
    {
        page.Navigation = this;
        _pages.Push(page);
    }
}
