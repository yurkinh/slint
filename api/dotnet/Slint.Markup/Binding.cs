// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Windows.Input;

namespace Slint.Markup;

/// <summary>A property of a builder, such as <c>LineEdit.TextProperty</c>, for <see cref="MarkupExtensions.Bind{TElement, TViewModel, TValue}(TElement, Property{TValue}, Func{TViewModel, TValue})"/>.</summary>
public sealed class Property<TValue>
{
    /// <summary>The property <paramref name="name"/> in <c>.slint</c> code, of the type <paramref name="type"/>.</summary>
    public Property(string name, SlintType type)
    {
        Name = name;
        Type = type;
    }

    internal string Name { get; }
    internal SlintType Type { get; }
}

/// <summary>
/// Bindings to a view model, like those of CommunityToolkit.Maui.Markup:
/// an element sets the view model with <see cref="BindingContext"/>, and its descendants bind to it.
/// The page updates after the view model raises <c>PropertyChanged</c> or <c>ErrorsChanged</c>, a list <c>CollectionChanged</c>,
/// or a command <c>CanExecuteChanged</c>, from any thread.
/// </summary>
public static class MarkupExtensions
{
    /// <summary>Sets the view model that the bindings of the element and its descendants read.</summary>
    public static TElement BindingContext<TElement>(this TElement element, object viewModel) where TElement : Element
    {
        element.Context = viewModel;
        return element;
    }

    /// <summary>Binds <paramref name="property"/> to a value of the view model, such as <c>(MainViewModel vm) => vm.Title</c>.</summary>
    public static TElement Bind<TElement, TViewModel, TValue>(this TElement element, Property<TValue> property, Func<TViewModel, TValue> get)
        where TElement : Element
    {
        element.SetContextBinding(property.Name, property.Type, context => get((TViewModel)context), null);
        return element;
    }

    /// <summary>Binds <paramref name="property"/> both ways: <paramref name="set"/> receives the changes of the UI.</summary>
    public static TElement Bind<TElement, TViewModel, TValue>(
        this TElement element, Property<TValue> property, Func<TViewModel, TValue> get, Action<TViewModel, TValue> set)
        where TElement : Element
    {
        element.SetContextBinding(property.Name, property.Type, context => get((TViewModel)context),
            (context, value) => set((TViewModel)context, Slint.Generated.Conversion.FromSlint<TValue>(value)));
        return element;
    }

    /// <summary>
    /// Runs a command of the view model on the callback <paramref name="callback"/>,
    /// and binds the boolean property <paramref name="enabled"/>, unless it's <c>null</c>, to whether the command can run.
    /// The builders with a <c>clicked</c> callback have a <c>BindCommand</c> of their own, which knows whether they have <c>enabled</c>.
    /// </summary>
    public static TElement BindCommand<TElement, TViewModel>(
        this TElement element, Func<TViewModel, ICommand> command, string callback, string? enabled, Func<TViewModel, object?>? parameter = null)
        where TElement : Element
    {
        element.SetCommand(callback, enabled, context => command((TViewModel)context),
            parameter == null ? null : context => parameter((TViewModel)context));
        return element;
    }
}
