// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Slint;

namespace MapsuiSample;

/// <summary>The map of the sample on every platform: OpenStreetMap, at Kyiv, with the buttons of the window.</summary>
internal static class SampleMap
{
    public static MapControl Attach(MainWindow window)
    {
        var map = MapControl.Attach(window.Instance);
        // OpenStreetMap blocks Mapsui's default, made from the process name, which on Android is the same for every app.
        map.Map.Layers.Add(OpenStreetMap.CreateTileLayer("Mapsui.UI.Slint.Sample"));

        // Kyiv, at a zoom where the city fills the view, once the view has a size.
        var (x, y) = SphericalMercator.FromLonLat(30.5234, 50.4501);
        var home = new MPoint(x, y);
        void GoHome() => map.Map.Navigator.CenterOnAndZoomTo(home, map.Map.Navigator.Resolutions[12]);
        map.Map.ViewportInitialized += (_, _) => GoHome();

        window.OnZoomIn(() => map.Map.Navigator.ZoomIn());
        window.OnZoomOut(() => map.Map.Navigator.ZoomOut());
        window.OnHome(() =>
        {
            GoHome();
            map.Map.Navigator.RotateTo(0);
        });
        return map;
    }

    /// <summary>Shows the time of the last frame, drawing it and copying it into an image, until the timer is disposed.</summary>
    public static Slint.Timer ShowFrameTimes(MainWindow window, MapControl map)
    {
        var timer = new Slint.Timer();
        timer.Start(Slint.TimerMode.Repeated, TimeSpan.FromMilliseconds(500), () =>
            window.Status = $"Frame: draw {map.LastFrame.Draw.TotalMilliseconds:F1} ms, copy {map.LastFrame.Copy.TotalMilliseconds:F1} ms");
        return timer;
    }
}
