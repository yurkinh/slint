# Mapsui for Slint

Show maps of [Mapsui](https://github.com/Mapsui/Mapsui) in a [Slint](https://slint.dev) UI for .NET:
OpenStreetMap and other tile servers, vector layers, styles, and Mapsui's widgets,
which you pan, zoom, and rotate with the mouse, the wheel, a trackpad, or two fingers.
It runs on Windows, macOS, Linux, Android, iOS, and in the browser.

## Get Started

Add the package, which brings Slint's package of the same version:

```sh
dotnet add package Slint.Mapsui
```

Put a `MapsuiView` in a `.slint` file, and export `MapsuiHost` from the file of the window:

```slint
import { MapsuiView } from "@mapsui";
export { MapsuiHost } from "@mapsui";

export component MainWindow inherits Window {
    preferred-width: 800px;
    preferred-height: 600px;
    MapsuiView { }
}
```

Attach a `MapControl` to the window, and add layers to its `Map`:

```csharp
using Mapsui.Tiling;
using Mapsui.UI.Slint;

using var window = MainWindow.Create();
using var map = MapControl.Attach(window.Instance);
map.Map.Layers.Add(OpenStreetMap.CreateTileLayer("MyApp"));
window.Run();
```

`MapControl` is Mapsui's `IMapControl`, as in Mapsui's controls for other UI frameworks,
so Mapsui's documentation and samples apply.
Name your app in `CreateTileLayer`, since OpenStreetMap blocks the default user agent on some platforms.

## Several Maps

Give each `MapsuiView` its own `view-id`, and attach a control to each:

```csharp
using var overview = MapControl.Attach(window.Instance, viewId: 0);
using var detail = MapControl.Attach(window.Instance, viewId: 1);
```

## UI Written in C#

From the version after 1.19.0-preview.13, with the package `Slint.Markup`, set `MapsuiSlintMarkup` to get a `MapsuiView` builder in your project's namespace:

```xml
<PropertyGroup>
  <MapsuiSlintMarkup>true</MapsuiSlintMarkup>
</PropertyGroup>
```

A page with a `MapsuiView` exports `MapsuiHost`, so attach the control to the page's instance:
`MapControl.Attach(component.Page.Instance)`.

## License

MIT. `MapControl.cs` and `RenderController.cs` are from Mapsui, under its MIT license.
