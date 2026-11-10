// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// The CometControlsGallery sample of https://github.com/dotnet/maui-labs, ported to Slint.Markup:
// a sidebar of pages, of which this port has the controls, the pickers, and the pages about state.
// Each page sits under an If, so the gallery compiles once, and every page keeps its state.

using ControlsGallery;
using Slint.Markup;

internal static class Program
{
    // Windows needs a single-threaded apartment for the window.
    [STAThread]
    private static void Main()
    {
        Page.Style = "fluent-light";
        using var gallery = new Gallery();
        gallery.Run();
    }
}

internal sealed class Gallery : Component
{
    private sealed record Item(string Category, string Title, Component Page);

    private readonly List<Item> _items =
    [
        new("General", "Controls", new ControlsPage()),
        new("General", "Pickers & Search", new PickersPage()),
        new("State Management", "Signal Counter", new SignalCounterPage()),
        new("State Management", "Computed Demo", new ComputedDemoPage()),
        new("State Management", "Two-Way Binding", new TwoWayBindingPage()),
        new("State Management", "Signal List", new SignalListPage()),
        new("State Management", "Coalescing Demo", new CoalescingDemoPage()),
        new("State Management", "State Preservation", new Navigation(new StatePreservationPage())),
    ];

    private readonly Signal<int> _selected = 0;

    protected override Element Render()
    {
        var sidebar = new VerticalLayout();
        foreach (var category in _items.Select(i => i.Category).Distinct())
        {
            sidebar.Add(new Text(category.ToUpperInvariant()).FontSize(11).FontWeight(600).Color(Colors.Grey));
            foreach (var index in Enumerable.Range(0, _items.Count).Where(i => _items[i].Category == category))
            {
                sidebar.Add(new Rectangle
                {
                    new Text(_items[index].Title).FontSize(14).VerticalAlignment(TextVerticalAlignment.Center)
                        .Color(() => _selected.Value == index ? Colors.White : new Slint.Color(60, 60, 67))
                        .X(12).Height(new Expr("parent.height")),
                    new TouchArea().OnClicked(() => _selected.Value = index),
                }.Height(32).BorderRadius(6).Background(() => _selected.Value == index ? Colors.SelectedAccent : Colors.SidebarBackground));
            }
        }

        var detail = new VerticalLayout();
        foreach (var index in Enumerable.Range(0, _items.Count))
        {
            detail.Add(new If(() => _selected.Value == index) { _items[index].Page });
        }

        return new WindowElement
        {
            new HorizontalLayout
            {
                new Rectangle { sidebar.Spacing(4).Padding(12).Alignment(LayoutAlignment.Start) }
                    .Width(220).Background(Colors.SidebarBackground),
                detail.HorizontalStretch(1),
            },
        }.Title("Controls Gallery").Width(960).Height(720);
    }
}
