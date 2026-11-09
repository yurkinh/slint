// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Slint.Markup;

/// <summary>A value whose changes make the <see cref="Component"/> that has it as a field render again.</summary>
public interface ISignal
{
    /// <summary>Raised after the value changes, on the thread that changed it.</summary>
    event Action? Changed;
}

/// <summary>
/// A value of a <see cref="Component"/>, such as <c>readonly Signal&lt;int&gt; count = 0;</c>.
/// Setting <see cref="Value"/> to another value renders the component again.
/// </summary>
public sealed class Signal<T>(T value) : ISignal
{
    private T _value = value;

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>The value.</summary>
    public T Value
    {
        get => _value;
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
    public override string ToString() => _value?.ToString() ?? "";
}

/// <summary>
/// A list of a <see cref="Component"/>, whose changes render the component again, such as for a <see cref="ForEach{T}"/>.
/// </summary>
public sealed class SignalList<T> : Collection<T>, ISignal
{
    /// <summary>An empty list.</summary>
    public SignalList() { }

    /// <summary>A list with <paramref name="items"/>.</summary>
    public SignalList(IEnumerable<T> items) : base([.. items]) { }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    protected override void InsertItem(int index, T item) { base.InsertItem(index, item); Changed?.Invoke(); }

    /// <inheritdoc />
    protected override void SetItem(int index, T item) { base.SetItem(index, item); Changed?.Invoke(); }

    /// <inheritdoc />
    protected override void RemoveItem(int index) { base.RemoveItem(index); Changed?.Invoke(); }

    /// <inheritdoc />
    protected override void ClearItems() { base.ClearItems(); Changed?.Invoke(); }
}

/// <summary>
/// A window whose UI <see cref="Render"/> returns, like a view of Comet.
/// After a handler of the UI, or a change of a <see cref="Signal{T}"/> or <see cref="SignalList{T}"/> field,
/// the component renders again; the fields are the ones of the component's class, and the ones it inherits that aren't private. A tree with the same <c>.slint</c> source only updates the values;
/// a C# condition that changes the tree compiles another source, so prefer <see cref="If"/> and <see cref="ForEach{T}"/>.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
public abstract class Component : IDisposable
{
    private Page? _page;
    private int _slintThread;
    private bool _dirty;
    private bool _rendering;

    /// <summary>Returns the UI, from the state of the component.</summary>
    protected abstract Element Render();

    /// <summary>The page, which the first use creates.</summary>
    public Page Page => _page ?? CreatePage();

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
        if (_page == null)
        {
            return;
        }
        if (Environment.CurrentManagedThreadId != _slintThread)
        {
            EventLoop.InvokeFromEventLoop(Invalidate);
            return;
        }
        if (_page.Handling || _rendering)
        {
            _dirty = true;
            return;
        }
        Refresh();
    }

    /// <summary>Hides the window, and releases the page.</summary>
    public void Dispose()
    {
        _page?.Dispose();
        GC.SuppressFinalize(this);
    }

    private Page CreatePage()
    {
        _slintThread = Environment.CurrentManagedThreadId;
        foreach (var signal in Signals())
        {
            signal.Changed += Invalidate;
        }
        _rendering = true;
        try
        {
            _page = Page.Create(Render);
        }
        finally
        {
            _rendering = false;
        }
        _page.AfterHandler = Refresh;
        return _page;
    }

    private void Refresh()
    {
        _dirty = false;
        var before = _page!.Source;
        _rendering = true;
        try
        {
            _page.Rebuild();
        }
        finally
        {
            _rendering = false;
        }
        if (_page.Source != before && s_warnOnRecompile)
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

    /// <summary>The signals in the fields of the component's class, and the fields it inherits that aren't private.</summary>
    private IEnumerable<ISignal> Signals() =>
        GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => typeof(ISignal).IsAssignableFrom(field.FieldType))
            .Select(field => field.GetValue(this)).OfType<ISignal>();
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
