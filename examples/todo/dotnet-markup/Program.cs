// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

// The todo example with its UI written in C#, with Slint.Markup instead of a .slint file.

using System.Diagnostics;
using Slint.Markup;

var todos = new List<Todo> { new("Implement the .slint file", true), new("Do the Rust part", false), new("Write the C# builders", false) };
var draft = "";

void Add()
{
    if (draft.Trim().Length > 0)
    {
        todos.Add(new Todo(draft.Trim(), false));
        draft = "";
    }
}

// A function, so that hot reload of this code builds the window again.
Element Render() => new WindowElement
{
    new VerticalBox
    {
        new HorizontalBox
        {
            new LineEdit().Text(() => draft, text => draft = text).PlaceholderText("What needs to be done?").OnAccepted(_ => Add()),
            new Button("Add New Entry").Enabled(() => draft.Trim().Length > 0).OnClicked(Add),
        },
        new VerticalLayout
        {
            new ForEach<Todo>(() => todos, row => new HorizontalLayout
            {
                new CheckBox(row.Get(t => t.Title)).HorizontalStretch(1)
                    .Checked(row.Bind(t => t.Done, (todo, done) => todos[todos.IndexOf(todo)] = todo with { Done = done })),
                new Button("Remove").OnClicked(row.Do(todo => todos.Remove(todo))),
            }),
        }.Alignment(LayoutAlignment.Start).VerticalStretch(1),
        new If(() => todos.Count == 0) { new Text("Nothing to do") },
        new Text(() => $"{todos.Count(t => !t.Done)} of {todos.Count} left").Color(new Slint.Color(0x80, 0x80, 0x80)),
    },
}.Title("Todo, in C#").Width(420).Height(360);

var stopwatch = Stopwatch.StartNew();
using var page = Page.Create(Render);
Console.WriteLine($"Page.Create: {stopwatch.Elapsed.TotalMilliseconds:F1} ms, of which {page.Origin} {page.CompileTime.TotalMilliseconds:F1} ms");
if (Environment.GetEnvironmentVariable("SLINT_MARKUP_SOURCE") != null)
{
    Console.WriteLine(page.Source);
}
if (Environment.GetEnvironmentVariable("SLINT_MARKUP_BENCH") != null)
{
    // A second compile in the process, without the costs of the first call into Slint.
    for (var i = 0; i < 3; i++)
    {
        Page.ClearCache();
        stopwatch.Restart();
        using var again = Page.Create(new WindowElement { new VerticalBox { new Text($"Run {i}"), new Button("A").OnClicked(Add), new ForEach<Todo>(() => todos, row => new CheckBox(row.Get(t => t.Title))) } });
        Console.WriteLine($"Again: {stopwatch.Elapsed.TotalMilliseconds:F1} ms, of which compiling {again.CompileTime.TotalMilliseconds:F1} ms");
        stopwatch.Restart();
        using var cached = Page.Create(new WindowElement { new VerticalBox { new Text($"Run {i}"), new Button("A").OnClicked(Add), new ForEach<Todo>(() => todos, row => new CheckBox(row.Get(t => t.Title))) } });
        Console.WriteLine($"Cached: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
    }
    return;
}
page.Run();

internal sealed record Todo(string Title, bool Done);
