<!-- cSpell: ignore Basilicata Shipibo Conibo yurkinh -->
# Draft: A Mapsui Control for Slint

A draft of an issue for [Mapsui's repository](https://github.com/Mapsui/Mapsui/issues), the decision of phase 5 of item 28 in [the plan](dotnet-bindings.md).
It isn't posted; the maintainer of the fork posts it from their account.
The text below the line is the issue.

---

**Title:** A Mapsui control for Slint: in Mapsui's repository, or as a package of its own?

Hi, we've built a Mapsui control for [Slint](https://slint.dev), a declarative UI toolkit, through the community .NET bindings of Slint (`yurkinh.slint.net` on nuget.org).
We'd like to know whether you'd want it in Mapsui's repository, next to the controls for Avalonia, Eto, and the others, or whether it should stay a package of its own.

**What it is**

- `Mapsui.UI.Slint`, 310 lines of C# and 80 of `.slint` for Slint, plus Mapsui's own `MapControl.cs` and `RenderController.cs` of `Mapsui.UI.Shared`, unchanged except for an `#elif __SLINT__` for the namespace, and `&& !__SLINT__` on the `__ANDROID__` branch, since the control also builds for Android.
- It draws with SkiaSharp into a raster surface, and shows the frame in a Slint `Image`; at 2x, a frame of 1768×822 pixels takes about 2 ms to draw and 0.08 ms to hand to Slint.
- Mouse, wheel, trackpad, touch, pinch, and rotation go through the shared code and `Navigator.Manipulate`, as in the other controls; taps raise `Info`, and widgets and links work.
- It runs on macOS, Windows, Linux, Android, iOS, and in the browser on WebAssembly, where SkiaSharp's library links into the same app as Slint's.
- A sample browser like `Mapsui.Samples.Eto` runs `Mapsui.Samples.Common` unchanged: 147 of its 153 samples show without an error, and the other 6 depend on servers that are gone or need a local server.
- Code: https://github.com/yurkinh/slint/tree/dotnet_dev/examples/mapsui

**What it would take in Mapsui's repository**

- Slint for .NET needs .NET 10, while `global.json` pins the .NET 9 SDK, so the project would need the .NET 10 SDK to build.
- Its package would depend on a community package of Slint, until Slint publishes official .NET packages.

If you'd rather not take that on yet, we'll publish it as a package of its own, which depends on Mapsui's packages, and keep the copy of the shared files in sync.

**Things we noticed along the way**

- With the .NET 10 SDK, `Mapsui.Samples.Common` moves `cities.shp.sidx` and the other `*.shp.*` resources into a satellite assembly, since MSBuild takes `shp` for the culture Shipibo-Conibo, so `ShapefileSample` throws "EmbeddedResource not found".
  `WithCulture="false"` on those `EmbeddedResource` items fixes it.
- With the .NET 10 SDK, `EnforceCodeStyleInBuild` fails Mapsui's projects on IDE0055 formatting.
- `Navigator.TransformState` passes `Viewport.Rotation + manipulation.TotalRotationChange` as the virtual rotation to `RotationSnapper`, where `Viewport.Rotation` has already changed during the gesture.
  So a two-finger rotation back towards 0° can overshoot, such as from 36° by -36° to -36° instead of 0°.
  Is the rotation at the start of the manipulation meant there?
- On Android, `HttpClientTools.GetDefaultApplicationUserAgent()` comes from the process name, which OpenStreetMap blocked in our tests, so our samples pass their own user agent to `CreateTileLayer`.
