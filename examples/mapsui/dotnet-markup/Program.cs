// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

// A Mapsui map in a UI written in C# with Slint.Markup: the MapsuiView builder comes from mapsui.slint.

using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Slint;
using MapsuiMarkup;
using Slint.Markup;

internal static class Program
{
    // Windows needs a single-threaded apartment for the window.
    [STAThread]
    private static void Main()
    {
        using var app = new MapApp();
        using var map = MapControl.Attach(app.Page.Instance);
        app.Connect(map);
        app.Run();
    }
}

internal sealed record Place(string Name, double Longitude, double Latitude);

internal sealed class MapApp : Component
{
    private static readonly Place[] s_places =
    [
        new("Kyiv", 30.5234, 50.4501),
        new("Lviv", 24.0297, 49.8397),
        new("Odesa", 30.7233, 46.4825),
        new("Kharkiv", 36.2304, 49.9935),
    ];

    private readonly Signal<string> _shown = "Kyiv";
    private MapControl? _map;

    public void Connect(MapControl map)
    {
        _map = map;
        map.Map.Layers.Add(OpenStreetMap.CreateTileLayer("Mapsui.UI.Slint.Sample"));
        map.Map.ViewportInitialized += (_, _) => GoTo(s_places[0], animate: false);
    }

    protected override Element Render() => new WindowElement
    {
        new VerticalBox
        {
            new HorizontalBox
            {
                new Button("Kyiv", () => GoTo(s_places[0], animate: true)),
                new Button("Lviv", () => GoTo(s_places[1], animate: true)),
                new Button("Odesa", () => GoTo(s_places[2], animate: true)),
                new Button("Kharkiv", () => GoTo(s_places[3], animate: true)),
                new Text(() => $"Showing {_shown.Value}").VerticalAlignment(TextVerticalAlignment.Center),
            },
            new MapsuiView().VerticalStretch(1),
        },
    }.Title("Mapsui, in C#").PreferredWidth(900).PreferredHeight(640);

    private void GoTo(Place place, bool animate)
    {
        if (_map is null)
        {
            return;
        }
        var (x, y) = SphericalMercator.FromLonLat(place.Longitude, place.Latitude);
        _map.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), _map.Map.Navigator.Resolutions[12], animate ? 500 : -1);
        _shown.Value = place.Name;
    }
}
