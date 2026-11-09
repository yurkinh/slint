// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// The CometTaskApp sample of https://github.com/dotnet/maui-labs, ported to Slint.Markup:
// tabs, a navigation stack for the tasks, a list with search and filters, forms, and statistics.

using Slint.Markup;
using Tasks;

// The colors of the sample are for a light theme.
Page.Style = "fluent-light";
using var app = new TaskApp();
app.Run();

internal sealed class TaskApp : Component
{
    // The pages are fields, so that they keep their state, such as the pages of the stack.
    private readonly Navigation _tasks = new(new TaskListPage());
    private readonly StatsPage _stats = new();
    private readonly SettingsPage _settings = new();
    private readonly Signal<int> _tab = 0;

    protected override Element Render() => new WindowElement
    {
        new TabWidget
        {
            new Tab("Tasks") { _tasks },
            new Tab("Stats") { _stats },
            new Tab("Settings") { _settings },
        }.CurrentIndex(() => _tab.Value, index => _tab.Value = index),
    }.Title("Tasks").Width(480).Height(720);
}
