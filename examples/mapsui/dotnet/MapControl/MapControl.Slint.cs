// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Mapsui.Extensions;
using Mapsui.Manipulations;
using SkiaSharp;
using Slint;
using SlintImage = Slint.Image;

namespace Mapsui.UI.Slint;

/// <summary>
/// The Slint part of Mapsui's map control: draws the map with SkiaSharp into the frames of a <c>MapsuiView</c> of <c>mapsui.slint</c>,
/// and passes the view's size and input to Mapsui.
/// </summary>
public partial class MapControl : IMapControl
{
    private const string Global = "MapsuiHost";

    private readonly ComponentInstance _component;
    private readonly int _viewId;
    private readonly int _slintThread = Environment.CurrentManagedThreadId;
    private readonly ListModel<SlintImage> _frames = new();
    private readonly ManipulationTracker _manipulationTracker = new();
    private SKSurface? _surface;
    private SlintImage? _frame;
    private double _width;
    private double _height;
    private bool _pressed;
    private bool _shiftPressed;
    private int _drawPending;

    private MapControl(ComponentInstance component, int viewId)
    {
        _component = component;
        _viewId = viewId;
        while (_frames.Count <= viewId)
        {
            _frames.Add(new SlintImage());
        }
        component.SetGlobalProperty(Global, "frames", _frames);
        component.SetGlobalCallback(Global, "resized", args =>
        {
            if (IsMine(args))
            {
                Resize(Number(args[1]), Number(args[2]));
            }
        });
        component.SetGlobalCallback(Global, "pointer", args =>
        {
            if (IsMine(args))
            {
                OnPointer(Convert.ToInt32(args[1]), new ScreenPosition(Number(args[4]), Number(args[5])), (bool)args[6]!);
            }
        });
        component.SetGlobalCallback(Global, "wheel", args =>
        {
            if (IsMine(args) && Number(args[3]) is var delta && delta != 0)
            {
                Map.Navigator.MouseWheelZoom(delta > 0 ? 1 : -1, new ScreenPosition(Number(args[1]), Number(args[2])));
            }
        });
        SharedConstructor();
        component.SetGlobalProperty(Global, "attached", Convert.ToInt32(component.GetGlobalProperty(Global, "attached")) + 1);
    }

    /// <summary>The size of the last frame, in physical pixels.</summary>
    public (int Width, int Height) FrameSize { get; private set; }

    /// <summary>Shows a map in the <c>MapsuiView</c> of <paramref name="component"/> with <paramref name="viewId"/>.</summary>
    public static MapControl Attach(ComponentInstance component, int viewId = 0) => new(component, viewId);

    /// <summary>How long drawing the last frame took, and copying it into a Slint image.</summary>
    public (TimeSpan Draw, TimeSpan Copy) LastFrame { get; private set; }

    /// <summary>Asks for a frame, which the Slint thread draws once for the requests before it gets to it.</summary>
    public void InvalidateCanvas()
    {
        if (Interlocked.Exchange(ref _drawPending, 1) == 1)
        {
            return;
        }
        if (Environment.CurrentManagedThreadId == _slintThread)
        {
            global::Slint.Timer.SingleShot(TimeSpan.Zero, Draw);
        }
        else
        {
            EventLoop.InvokeFromEventLoop(Draw);
        }
    }

    public void OpenInBrowser(string url) =>
        Catch.TaskRun(() =>
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        });

    public float? GetPixelDensity() => _component.Window.ScaleFactor;

    private bool GetShiftPressed() => _shiftPressed;

    private bool IsMine(object?[] args) => Convert.ToInt32(args[0]) == _viewId;

    private static double Number(object? value) => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

    private void Resize(double width, double height)
    {
        (_width, _height) = (width, height);
        SharedOnSizeChanged(width, height);
        InvalidateCanvas();
    }

    private void OnPointer(int kind, ScreenPosition position, bool shift)
    {
        _shiftPressed = shift;
        switch (kind)
        {
            case 0:
                _pressed = true;
                _manipulationTracker.Restart([position]);
                OnPointerPressed([position]);
                break;
            case 1:
                _pressed = false;
                OnPointerReleased([position]);
                break;
            case 2:
                if (!OnPointerMoved([position], isHovering: !_pressed) && _pressed)
                {
                    _manipulationTracker.Manipulate([position], Map.Navigator.Manipulate);
                }
                break;
            default:
                _pressed = false;
                break;
        }
    }

    /// <summary>Draws a frame now, instead of when the Slint thread gets to a request.</summary>
    public void DrawNow() => Draw();

    private void Draw()
    {
        Volatile.Write(ref _drawPending, 0);
        var scale = GetPixelDensity() ?? 1;
        var width = (int)Math.Ceiling(_width * scale);
        var height = (int)Math.Ceiling(_height * scale);
        if (width <= 0 || height <= 0 || _renderController == null)
        {
            return;
        }
        if (_surface == null || _surface.Canvas.DeviceClipBounds.Width != width || _surface.Canvas.DeviceClipBounds.Height != height)
        {
            _surface?.Dispose();
            _surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        }
        var started = Stopwatch.GetTimestamp();
        var canvas = _surface.Canvas;
        canvas.Save();
        canvas.Scale(scale, scale);
        _renderController.Render(canvas);
        canvas.Restore();
        canvas.Flush();
        var drawn = Stopwatch.GetTimestamp();
        using (var pixmap = _surface.PeekPixels())
        {
            var frame = SlintImage.FromPremultipliedRgba8(width, height, pixmap.GetPixelSpan());
            _frames[_viewId] = frame;
            _frame?.Dispose();
            _frame = frame;
        }
        LastFrame = (Stopwatch.GetElapsedTime(started, drawn), Stopwatch.GetElapsedTime(drawn));
        FrameSize = (width, height);
    }

    public void Dispose()
    {
        SharedDispose(true);
        _map?.Dispose();
        _surface?.Dispose();
        _frame?.Dispose();
        GC.SuppressFinalize(this);
    }
}
