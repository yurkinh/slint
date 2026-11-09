// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

// The todo example with its UI written in C#, with Slint.Markup instead of a .slint file.

using Slint.Markup;

using var app = new TodoApp();
app.Run();

internal sealed record Todo(string Title, bool Done);

/// <summary>
/// Renders again after each change of a field that's a signal, or after a handler of the UI.
/// Hot reload of this code renders it again too.
/// </summary>
internal sealed class TodoApp : Component
{
    private readonly SignalList<Todo> _todos =
        [new("Implement the .slint file", true), new("Do the Rust part", false), new("Write the C# builders", false)];
    private readonly Signal<string> _draft = "";

    protected override Element Render() => new WindowElement
    {
        new VerticalBox
        {
            new HorizontalBox
            {
                new LineEdit().Text(() => _draft.Value, text => _draft.Value = text)
                    .PlaceholderText("What needs to be done?").OnAccepted(_ => Add()),
                new Button("Add New Entry", Add).Enabled(() => _draft.Value.Trim().Length > 0),
            },
            new VerticalLayout
            {
                new ForEach<Todo>(_todos, row => new HorizontalLayout
                {
                    new CheckBox(row.Get(t => t.Title)).HorizontalStretch(1)
                        .Checked(row.Bind(t => t.Done, (todo, done) => _todos[_todos.IndexOf(todo)] = todo with { Done = done })),
                    new Button("Remove").OnClicked(row.Do(todo => _todos.Remove(todo))),
                }),
            }.Alignment(LayoutAlignment.Start).VerticalStretch(1),
            new If(() => _todos.Count == 0) { new Text("Nothing to do") },
            new Text(() => $"{_todos.Count(t => !t.Done)} of {_todos.Count} left").Color(new Slint.Color(0x80, 0x80, 0x80)),
        },
    }.Title("Todo, in C#").Width(420).Height(360);

    private void Add()
    {
        if (_draft.Value.Trim().Length > 0)
        {
            _todos.Add(new Todo(_draft.Value.Trim(), false));
            _draft.Value = "";
        }
    }
}
