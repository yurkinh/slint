// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

// A Mapsui map in a Slint window, next to Slint widgets: OpenStreetMap tiles, panning with the mouse, and zooming with the wheel.

using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Slint;
using MapsuiSample;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var window = MainWindow.Create();
        using var map = MapControl.Attach(window.Instance);
        map.Map.Layers.Add(OpenStreetMap.CreateTileLayer());

        // Kyiv, at a zoom where the city fills the view, once the view has a size.
        var (x, y) = SphericalMercator.FromLonLat(30.5234, 50.4501);
        var home = new MPoint(x, y);
        map.Map.ViewportInitialized += (_, _) => map.Map.Navigator.CenterOnAndZoomTo(home, map.Map.Navigator.Resolutions[12]);

        window.OnZoomIn(() => map.Map.Navigator.ZoomIn());
        window.OnZoomOut(() => map.Map.Navigator.ZoomOut());
        window.OnHome(() => map.Map.Navigator.CenterOnAndZoomTo(home, map.Map.Navigator.Resolutions[12]));

        if (Environment.GetEnvironmentVariable("MAPSUI_BENCH") != null)
        {
            Bench(window, map);
            return;
        }

        // Shows the time of the last frame, drawing it and copying it into an image.
        using var timer = new Slint.Timer();
        timer.Start(Slint.TimerMode.Repeated, TimeSpan.FromMilliseconds(500), () =>
            window.Status = $"Frame: draw {map.LastFrame.Draw.TotalMilliseconds:F1} ms, copy {map.LastFrame.Copy.TotalMilliseconds:F1} ms");

        window.Run();
    }

    /// <summary>
    /// Pans through the view's own pointer callbacks, as a drag of the mouse does, and measures the frames;
    /// then zooms with the wheel callback. For the measurements of the plan, with the window shown.
    /// </summary>
    private static void Bench(MainWindow window, MapControl map)
    {
        window.Show();
        var instance = window.Instance;
        void Pointer(int kind, double x, double y) => instance.InvokeGlobal("MapsuiHost", "pointer", 0, kind, 0, false, x, y, false);
        // Lets the tiles load first.
        Slint.Timer.SingleShot(TimeSpan.FromSeconds(5), () =>
        {
            var before = map.Map.Navigator.Viewport.CenterX;
            var draws = new List<double>();
            var copies = new List<double>();
            Pointer(0, 400, 300);
            for (var i = 1; i <= 60; i++)
            {
                Pointer(2, 400 - i * 5, 300);
                map.DrawNow();
                draws.Add(map.LastFrame.Draw.TotalMilliseconds);
                copies.Add(map.LastFrame.Copy.TotalMilliseconds);
            }
            Pointer(1, 100, 300);
            var panned = map.Map.Navigator.Viewport.CenterX - before;
            var resolution = map.Map.Navigator.Viewport.Resolution;
            instance.InvokeGlobal("MapsuiHost", "wheel", 0, 400, 300, 120);
            draws.Sort();
            copies.Sort();
            Console.WriteLine($"scale {window.Window.ScaleFactor}, frames of {map.FrameSize.Width}x{map.FrameSize.Height} px");
            Console.WriteLine($"pan: map center moved {panned:F0} m east: {panned > 0}");
            Console.WriteLine($"draw while panning: median {draws[draws.Count / 2]:F2} ms, max {draws[^1]:F2} ms");
            Console.WriteLine($"copy into an image: median {copies[copies.Count / 2]:F2} ms, max {copies[^1]:F2} ms");
            // The wheel zooms with an animation.
            Slint.Timer.SingleShot(TimeSpan.FromSeconds(1), () =>
            {
                Console.WriteLine($"wheel zooms in: {map.Map.Navigator.Viewport.Resolution < resolution}");
                Slint.EventLoop.Quit();
            });
        });
        Slint.EventLoop.Run();
    }
}
