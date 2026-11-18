// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using Mapsui.Logging;
using MapsuiSample;

// Mapsui reports the errors of fetching tiles here.
Logger.LogDelegate += (level, message, exception) =>
{
    if (level <= LogLevel.Warning)
    {
        Console.WriteLine($"Mapsui {level}: {message} {exception?.Message}");
    }
};

using var window = MainWindow.Create();
using var map = SampleMap.Attach(window);
using var timer = SampleMap.ShowFrameTimes(window, map);

// Prints the viewport to the console when it changes, for the steps of steps/run.json.
var last = "";
using var viewport = new Slint.Timer();
viewport.Start(Slint.TimerMode.Repeated, TimeSpan.FromMilliseconds(500), () =>
{
    var v = map.Map.Navigator.Viewport;
    var text = $"viewport: resolution {v.Resolution:F1}, rotation {v.Rotation:F0}, center {v.CenterX:F0} {v.CenterY:F0}, frame {map.FrameSize.Width}x{map.FrameSize.Height}";
    if (text != last)
    {
        Console.WriteLine(text);
        last = text;
    }
});
Console.WriteLine("ready");
// The browser's thread can't block, so Run would throw.
await window.RunAsync();
