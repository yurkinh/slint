// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using MapsuiSample;

internal static class Program
{
    private static void Main()
    {
        using var window = MainWindow.Create();
        using var map = SampleMap.Attach(window);
        using var timer = SampleMap.ShowFrameTimes(window, map);
        // Slint starts UIKit, and Run doesn't return.
        window.Run();
    }
}
