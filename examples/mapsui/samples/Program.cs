// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

// Mapsui's sample collection in a Slint window: pick a category and a sample, and the map shows it.

using Mapsui.Logging;
using Mapsui.Samples.Common;
using Mapsui.Samples.Common.Extensions;
using Mapsui.Samples.Slint;
using Mapsui.UI.Slint;
using Slint;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        SampleConfiguration.ApplyRendererConfig();
        Mapsui.Samples.Common.Samples.Register();

        using var window = MainWindow.Create();
        using var map = MapControl.Attach(window.Instance);
        map.Map.Navigator.RotationLock = false;

        var all = AllSamples.GetSamples().ToList();
        var categories = all.Select(s => s.Category).Distinct().Order().ToList();
        var shown = new List<ISampleBase>();
        var names = new ListModel<string>();
        window.Categories = new ListModel<string>(categories);
        window.Samples = names;

        // Mapsui's errors, such as of fetching tiles, go to the console.
        var errors = 0;
        Logger.LogDelegate += (level, message, exception) =>
        {
            if (level == LogLevel.Error)
            {
                errors++;
                Console.WriteLine($"Mapsui: {message} {exception?.Message}");
            }
        };

        async Task<bool> Show(ISampleBase sample)
        {
            window.Status = $"{sample.Category}: {sample.Name}";
            try
            {
                map.Map.Layers.ClearAllGroups();
                await sample.SetupAsync(map);
                return true;
            }
            catch (Exception e)
            {
                window.Status = $"{sample.Name} failed: {e.Message}";
                Console.WriteLine($"failed: {sample.Category}/{sample.Name}: {e}");
                return false;
            }
        }

        void ShowCategory(int index, bool showFirst = true)
        {
            shown.Clear();
            shown.AddRange(all.Where(s => s.Category == categories[index]).OrderBy(s => s.Name));
            names.Clear();
            foreach (var sample in shown)
            {
                names.Add(sample.Name);
            }
            window.SampleIndex = 0;
            if (showFirst)
            {
                _ = Show(shown[0]);
            }
        }

        window.OnCategorySelected(index => ShowCategory(index));
        window.OnSampleSelected(index => _ = Show(shown[index]));

        // --sample <category>/<name> starts with that sample.
        var start = args.SkipWhile(a => a != "--sample").Skip(1).FirstOrDefault();
        var first = start is null ? null : all.FirstOrDefault(s => $"{s.Category}/{s.Name}" == start);
        var category = first is null ? 0 : categories.IndexOf(first.Category);
        window.CategoryIndex = category;
        ShowCategory(category);
        if (first is not null)
        {
            window.SampleIndex = shown.IndexOf(first);
            _ = Show(first);
        }

        // --check shows every sample in turn, draws it, and quits with the number of samples that failed.
        if (args.Contains("--check"))
        {
            global::Slint.Timer.SingleShot(TimeSpan.FromSeconds(1), async () =>
            {
                var failed = new List<string>();
                for (var c = 0; c < categories.Count; c++)
                {
                    window.CategoryIndex = c;
                    ShowCategory(c, showFirst: false);
                    for (var i = 0; i < shown.Count; i++)
                    {
                        window.SampleIndex = i;
                        var errorsBefore = errors;
                        var ok = await Show(shown[i]);
                        await Task.Delay(300);
                        try
                        {
                            map.DrawNow();
                        }
                        catch (Exception e)
                        {
                            ok = false;
                            Console.WriteLine($"failed to draw: {shown[i].Category}/{shown[i].Name}: {e}");
                        }
                        if (!ok || errors != errorsBefore)
                        {
                            failed.Add($"{shown[i].Category}/{shown[i].Name}");
                        }
                    }
                }
                Console.WriteLine($"checked {all.Count} samples, {failed.Count} failed: {string.Join(", ", failed)}");
                EventLoop.Quit();
            });
        }

        window.Run();
    }
}
