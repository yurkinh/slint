// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using Slint.Android;

// The tiles of OpenStreetMap.
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]

namespace MapsuiSample;

[Activity(Label = "Mapsui in Slint", MainLauncher = true, Exported = true, ConfigurationChanges = SlintActivity.ConfigurationChanges)]
[MetaData(SlintActivity.LibraryNameKey, Value = SlintActivity.LibraryName)]
public class MainActivity : SlintActivity
{
    protected override void SlintMain()
    {
        using var window = MainWindow.Create();
        using var map = SampleMap.Attach(window);
        using var timer = SampleMap.ShowFrameTimes(window, map);
        window.Run();
    }
}
