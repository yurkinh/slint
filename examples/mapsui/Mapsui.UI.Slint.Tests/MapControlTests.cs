// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using Mapsui.Manipulations;
using Mapsui.Utilities;
using Mapsui.Widgets;
using Mapsui.Widgets.ButtonWidgets;

namespace Mapsui.UI.Slint.Tests;

[TestClass]
public sealed class MapControlTests
{
    private static void Pointer(TestWindow window, int view, int kind, double x, double y) =>
        window.Instance.InvokeGlobal("MapsuiHost", "pointer", view, kind, 0, false, x, y, false);

    private static void Tap(TestWindow window, int view, double x, double y)
    {
        Pointer(window, view, 0, x, y);
        Pointer(window, view, 1, x, y);
    }

    [TestMethod]
    public void SizesTheViewportOfEachViewAndDrawsItsFrames() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var left = MapControl.Attach(window.Instance, 0);
        using var right = MapControl.Attach(window.Instance, 1);
        SlintThread.Pump();

        Assert.AreEqual(200, left.Map.Navigator.Viewport.Width);
        Assert.AreEqual(300, left.Map.Navigator.Viewport.Height);
        Assert.AreEqual(200, right.Map.Navigator.Viewport.Width);

        var scale = window.Window.ScaleFactor;
        left.DrawNow();
        Assert.AreEqual(((int)Math.Ceiling(200 * scale), (int)Math.Ceiling(300 * scale)), left.FrameSize);
        window.Hide();
    });

    [TestMethod]
    public void DrawsTheNextFrameAtANewScaleFactor() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        SlintThread.Pump();

        window.Window.DispatchEvent(new global::Slint.WindowEvent.ScaleFactorChanged(3));
        map.DrawNow();

        // The view keeps its logical size, so Mapsui's viewport stays, and the frame has 3 pixels per logical pixel.
        Assert.AreEqual(200, map.Map.Navigator.Viewport.Width);
        Assert.AreEqual((600, 900), map.FrameSize);
        window.Hide();
    });

    [TestMethod]
    public void RaisesInfoOnATap() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        SlintThread.Pump();
        MapInfoEventArgs? info = null;
        map.Info += (_, e) => info = e;

        Tap(window, 0, 100, 150);

        Assert.IsNotNull(info);
        Assert.AreEqual(100, info.ScreenPosition.X);
        window.Hide();
    });

    [TestMethod]
    public void ZoomsWithTheWheelAndPansWithADrag() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        SlintThread.Pump();
        map.Map.Navigator.CenterOnAndZoomTo(new MPoint(0, 0), 1000);
        // Without the animation, the wheel zooms at once.
        map.Map.Navigator.MouseWheelAnimation.UseContinuousMouseWheelZoom = true;

        var resolution = map.Map.Navigator.Viewport.Resolution;
        window.Instance.InvokeGlobal("MapsuiHost", "wheel", 0, 100, 150, 120);
        Assert.IsLessThan(resolution, map.Map.Navigator.Viewport.Resolution);
        window.Instance.InvokeGlobal("MapsuiHost", "wheel", 0, 100, 150, -120);
        Assert.AreEqual(resolution, map.Map.Navigator.Viewport.Resolution, resolution / 1000);

        Pointer(window, 0, 0, 100, 150);
        Pointer(window, 0, 2, 150, 150);
        Pointer(window, 0, 1, 150, 150);
        // Dragging to the right shows what's west of the center.
        Assert.IsLessThan(0, map.Map.Navigator.Viewport.CenterX);
        window.Hide();
    });

    [TestMethod]
    public void OpensTheUrlOfATappedLink() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        string? opened = null;
        PlatformUtilities.SetOpenInBrowserFunc(url => opened = url);
        map.Map.Widgets.Enqueue(new HyperlinkWidget
        {
            Text = "Slint",
            Url = "https://slint.dev",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = 100,
            Height = 40,
        });
        SlintThread.Pump();
        // Drawing places the widgets.
        map.DrawNow();

        Tap(window, 0, 20, 20);

        Assert.AreEqual("https://slint.dev", opened);
        window.Hide();
    });

    [TestMethod]
    public void ReleasesItsViewWhenDisposed() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        var first = MapControl.Attach(window.Instance, 1);
        Assert.ThrowsExactly<InvalidOperationException>(() => MapControl.Attach(window.Instance, 1));

        first.Dispose();
        var taps = 0;
        first.Info += (_, _) => taps++;
        Tap(window, 1, 50, 50);
        Assert.AreEqual(0, taps);

        using var second = MapControl.Attach(window.Instance, 1);
        SlintThread.Pump();
        Assert.AreEqual(200, second.Map.Navigator.Viewport.Width);
        window.Hide();
    });
}
