// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

[assembly: DoNotParallelize]

namespace Mapsui.UI.Slint.Tests;

/// <summary>
/// Runs test bodies on one thread with Slint's testing backend, which needs no screen,
/// since Slint objects belong to the thread that first used Slint.
/// </summary>
internal static partial class SlintThread
{
    private static readonly BlockingCollection<Action> s_queue = [];

    static SlintThread()
    {
        // The native library reads the environment of the process, which Environment.SetEnvironmentVariable changes on Windows only.
        if (OperatingSystem.IsWindows())
        {
            Environment.SetEnvironmentVariable("SLINT_BACKEND", "testing");
        }
        else
        {
            setenv("SLINT_BACKEND", "testing", 1);
        }
        new Thread(() =>
        {
            foreach (var action in s_queue.GetConsumingEnumerable())
            {
                action();
            }
        }, 16 * 1024 * 1024)
        { IsBackground = true }.Start();
    }

    public static void Run(Action body)
    {
        ExceptionDispatchInfo? failure = null;
        using var done = new ManualResetEventSlim();
        s_queue.Add(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                failure = ExceptionDispatchInfo.Capture(e);
            }
            done.Set();
        });
        done.Wait();
        failure?.Throw();
    }

    /// <summary>Runs Slint's event loop for a moment, for the changed handlers of the views, such as the one that reports their size.</summary>
    public static void Pump(int milliseconds = 50)
    {
        global::Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(milliseconds), global::Slint.EventLoop.Quit);
        global::Slint.EventLoop.Run();
    }

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int setenv(string name, string value, int overwrite);
}
