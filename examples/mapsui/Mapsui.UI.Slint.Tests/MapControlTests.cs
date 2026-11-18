// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Utilities;
using Mapsui.Widgets;
using Mapsui.Widgets.ButtonWidgets;

namespace Mapsui.UI.Slint.Tests;

[TestClass]
public sealed class MapControlTests
{
    private static void Pointer(TestWindow window, int view, int kind, double x, double y) =>
        window.Instance.InvokeGlobal("MapsuiHost", "pointer", view, kind, 0, 0, x, y, false);

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
    public void ZoomsContinuouslyWithTheTrackpad() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        SlintThread.Pump();
        map.Map.Navigator.CenterOnAndZoomTo(new MPoint(0, 0), 1000);

        // Pixels, unlike the 60 of a line of the wheel, zoom by their amount and without the animation.
        window.Instance.InvokeGlobal("MapsuiHost", "wheel", 0, 100, 150, 100);
        Assert.AreEqual(1000 / Math.Sqrt(2), map.Map.Navigator.Viewport.Resolution, 0.001);
        window.Instance.InvokeGlobal("MapsuiHost", "wheel", 0, 100, 150, -100);
        Assert.AreEqual(1000, map.Map.Navigator.Viewport.Resolution, 0.001);
        window.Hide();
    });

    [TestMethod]
    public void ZoomsRotatesAndPansWithAPinch() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        SlintThread.Pump();
        map.Map.Navigator.CenterOnAndZoomTo(new MPoint(0, 0), 1000);
        void Pinch(int phase, double scale, double rotation, double x, double y) =>
            window.Instance.InvokeGlobal("MapsuiHost", "pinch", 0, phase, scale, rotation, x, y);

        // The first finger starts a drag, which the pinch takes over.
        Pointer(window, 0, 0, 100, 150);
        Pinch(0, 1, 0, 100, 150);
        Pinch(1, 2, 45, 100, 150);
        Assert.AreEqual(500, map.Map.Navigator.Viewport.Resolution, 0.001);
        Assert.AreEqual(45, map.Map.Navigator.Viewport.Rotation, 0.001);
        // The world under the center of the pinch stays there.
        Assert.AreEqual(0, map.Map.Navigator.Viewport.CenterX, 0.001);

        Pinch(1, 2, 45, 120, 150);
        Assert.AreEqual(-20 * 500, map.Map.Navigator.Viewport.CenterX * Math.Sqrt(2), 0.01);
        Pinch(2, 1, 0, 120, 150);
        Pointer(window, 0, 2, 200, 150);
        var center = map.Map.Navigator.Viewport.CenterX;
        Pinch(1, 3, 0, 100, 150);
        Assert.AreEqual(center, map.Map.Navigator.Viewport.CenterX, "Moves after the pinch ended change nothing");
        Assert.AreEqual(500, map.Map.Navigator.Viewport.Resolution, 0.001);
        window.Hide();
    });

    [TestMethod]
    public void FetchesTheDataOfTheViewportAfterAPinch() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        var layer = new FetchCountingLayer();
        map.Map.Layers.Add(layer);
        SlintThread.Pump();
        map.Map.Navigator.CenterOnAndZoomTo(new MPoint(0, 0), 1000);
        void Pinch(int phase, double scale) => window.Instance.InvokeGlobal("MapsuiHost", "pinch", 0, phase, scale, 0, 100, 150);

        Pinch(0, 1);
        Pinch(1, 2);
        var fetches = layer.DiscreteFetches;
        Pinch(2, 2);

        Assert.AreEqual(fetches + 1, layer.DiscreteFetches);
        window.Hide();
    });

    private sealed class FetchCountingLayer : MemoryLayer, IAsyncDataFetcher
    {
        public int DiscreteFetches { get; private set; }

        public void AbortFetch() { }

        public void ClearCache() { }

        public void RefreshData(FetchInfo fetchInfo, Action<Func<Task>> enqueueFetch)
        {
            if (fetchInfo.ChangeType == ChangeType.Discrete)
            {
                DiscreteFetches++;
            }
        }
    }

    [TestMethod]
    public void TakesNoTapFromTheFingersOfAPinch() => SlintThread.Run(() =>
    {
        using var window = TestWindow.Create();
        window.Show();
        using var map = MapControl.Attach(window.Instance, 0);
        SlintThread.Pump();
        var taps = 0;
        map.Info += (_, _) => taps++;
        void Finger(int kind, int finger) => window.Instance.InvokeGlobal("MapsuiHost", "pointer", 0, kind, 0, finger, 100, 150, false);

        // The events of Slint's touch handling: the release of the first finger, with the second one's id,
        // the pinch, and the remaining finger, pressed again when the other lifts.
        Finger(0, 1);
        Finger(1, 2);
        window.Instance.InvokeGlobal("MapsuiHost", "pinch", 0, 0, 1, 0, 100, 150);
        window.Instance.InvokeGlobal("MapsuiHost", "pinch", 0, 2, 2, 0, 100, 150);
        Finger(0, 1);
        Finger(1, 1);
        Assert.AreEqual(0, taps);

        Finger(0, 1);
        Finger(1, 1);
        Assert.AreEqual(1, taps);
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
