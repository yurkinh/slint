<!-- Copyright © SixtyFPS GmbH <info@slint.dev> -->
<!-- SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0 -->
# Slint.Markup (Preview)

Write a [Slint](https://slint.dev) UI in C#, without a `.slint` file.
The builders write `.slint` code, which Slint compiles once per page and caches on disk,
so a window starts as fast as one from a `.slint` file.

```csharp
using Slint.Markup;

using var app = new Counter();
app.Run();

class Counter : Component
{
    readonly Signal<int> count = 0;

    protected override Element Render() => new VerticalBox
    {
        new Text(() => $"Count: {count.Value}").FontSize(24),
        new Button("Add", () => count.Value++),
    };
}
```

- A builder exists for each built-in element and standard widget, with their properties, callbacks, and enums.
- A lambda, such as `() => $"Count: {count.Value}"`, becomes a property that the page sets again after a change.
- After a handler, or a change of a `Signal<T>` or `SignalList<T>` field, the component renders again.
  `If` and `ForEach` show and repeat parts without compiling another page.
- `Component<TState>` has a `State` and `SetState`.
- `<SlintFile Include="ui/card.slint" Markup="true" />` generates builders for the components of your own `.slint` files.
- Hot reload of the C# code renders the window again.

The package works with trimming and Native AOT.
See the [README of Slint for .NET](https://github.com/yurkinh/slint/blob/dotnet_dev/api/dotnet/README.md#ui-written-in-c-preview) for more.
