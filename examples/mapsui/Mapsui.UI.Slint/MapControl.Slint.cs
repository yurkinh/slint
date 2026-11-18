// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
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
    // A wheel reports lines, which Slint's winit backend gives as 60 logical pixels each; a trackpad reports pixels.
    private const double WheelLine = 60;
    private const double TrackpadPixelsPerZoomLevel = 200;

    private readonly ComponentInstance _component;
    private readonly MapsuiViews _views;
    private readonly int _viewId;
    private readonly int _slintThread = Environment.CurrentManagedThreadId;
    private readonly ManipulationTracker _manipulationTracker = new();
    private SKSurface? _surface;
    private SlintImage? _frame;
    private double _width;
    private double _height;
    private bool _pressed;
    private int _pressedFinger;
    private bool _twoFingers;
    private bool _shiftPressed;
    private bool _disposed;
    private int _drawPending;
    private ScreenPosition? _pinchCenter;
    private double _pinchScale;
    private double _pinchRotation;
    private double _pinchTotalRotation;

    private MapControl(ComponentInstance component, int viewId)
    {
        _component = component;
        _viewId = viewId;
        SharedConstructor();
        _views = MapsuiViews.Of(component);
        _views.Add(this);
    }

    internal int ViewId => _viewId;

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

    public void OpenInBrowser(string url)
    {
        if (OperatingSystem.IsBrowser())
        {
            BrowserWindow.Open(url, "_blank");
            return;
        }
        Catch.TaskRun(() =>
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        });
    }

    public float? GetPixelDensity() => _component.Window.ScaleFactor;

    private bool GetShiftPressed() => _shiftPressed;

    internal void Resize(double width, double height)
    {
        (_width, _height) = (width, height);
        SharedOnSizeChanged(width, height);
        InvalidateCanvas();
    }

    internal void OnWheel(double delta, ScreenPosition position)
    {
        if (delta == 0)
        {
            return;
        }
        if (Math.IEEERemainder(delta, WheelLine) == 0)
        {
            Map.Navigator.MouseWheelZoom(Math.Sign(delta), position);
        }
        else
        {
            Map.Navigator.MouseWheelZoomContinuous(Math.Pow(2, -delta / TrackpadPixelsPerZoomLevel), position);
        }
    }

    internal void OnPinch(int phase, double scale, double rotation, ScreenPosition center)
    {
        switch (phase)
        {
            case 0:
                // A drag of the mouse with a trackpad pinch.
                _pressed = false;
                (_pinchCenter, _pinchScale, _pinchRotation, _pinchTotalRotation) = (center, scale, rotation, 0);
                break;
            case 1 when _pinchCenter is { } previous:
                var rotationChange = rotation - _pinchRotation;
                _pinchTotalRotation += rotationChange;
                Map.Navigator.Manipulate(new Manipulation(center, previous, scale / _pinchScale, rotationChange, _pinchTotalRotation));
                (_pinchCenter, _pinchScale, _pinchRotation) = (center, scale, rotation);
                break;
            default:
                // Mapsui fetches the data of a new viewport on a discrete change, which a release gives a drag.
                if (_pinchCenter != null)
                {
                    Refresh();
                }
                _pinchCenter = null;
                break;
        }
    }

    internal void OnPointer(int kind, int finger, ScreenPosition position, bool shift)
    {
        _shiftPressed = shift;
        // Slint releases the first finger when a second one touches, with the second finger's id.
        // Until the last finger lifts, the fingers are a pinch, not a drag or a tap.
        if (kind == 1 && finger != _pressedFinger)
        {
            _pressed = false;
            _twoFingers = true;
            return;
        }
        if (_twoFingers)
        {
            _twoFingers = kind != 1;
            return;
        }
        switch (kind)
        {
            case 0:
                _pressed = true;
                _pressedFinger = finger;
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
        if (_disposed)
        {
            return;
        }
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
            _views.Show(_viewId, frame);
            _frame?.Dispose();
            _frame = frame;
        }
        LastFrame = (Stopwatch.GetElapsedTime(started, drawn), Stopwatch.GetElapsedTime(drawn));
        FrameSize = (width, height);
    }

    /// <summary>Stops showing the map in the view, and releases it.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _views.Remove(this);
        SharedDispose(true);
        _map?.Dispose();
        _surface?.Dispose();
        _frame?.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal static partial class BrowserWindow
{
    [JSImport("globalThis.open")]
    [SupportedOSPlatform("browser")]
    public static partial void Open(string url, string target);
}

/// <summary>
/// The views of one window: the frames model of its <c>MapsuiHost</c>, and its callbacks, which go to the control of their view.
/// A window can show several maps, each with a view id of its own.
/// </summary>
internal sealed class MapsuiViews
{
    private const string Global = "MapsuiHost";
    private static readonly ConditionalWeakTable<ComponentInstance, MapsuiViews> All = new();

    private readonly ComponentInstance _component;
    private readonly ListModel<SlintImage> _frames = new();
    private readonly Dictionary<int, MapControl> _controls = [];

    private MapsuiViews(ComponentInstance component)
    {
        _component = component;
        component.SetGlobalProperty(Global, "frames", _frames);
        component.SetGlobalCallback(Global, "resized", args => Find(args)?.Resize(Number(args[1]), Number(args[2])));
        component.SetGlobalCallback(Global, "pointer", args =>
            Find(args)?.OnPointer(Convert.ToInt32(args[1]), Convert.ToInt32(args[3]), new ScreenPosition(Number(args[4]), Number(args[5])), (bool)args[6]!));
        component.SetGlobalCallback(Global, "wheel", args =>
            Find(args)?.OnWheel(Number(args[3]), new ScreenPosition(Number(args[1]), Number(args[2]))));
        component.SetGlobalCallback(Global, "pinch", args =>
            Find(args)?.OnPinch(Convert.ToInt32(args[1]), Number(args[2]), Number(args[3]), new ScreenPosition(Number(args[4]), Number(args[5]))));
    }

    public static MapsuiViews Of(ComponentInstance component) => All.GetValue(component, c => new MapsuiViews(c));

    public void Add(MapControl control)
    {
        if (!_controls.TryAdd(control.ViewId, control))
        {
            throw new InvalidOperationException($"A MapControl already shows the view {control.ViewId}; dispose it first");
        }
        while (_frames.Count <= control.ViewId)
        {
            _frames.Add(new SlintImage());
        }
        // The views report their size again, which they did before there were handlers.
        _component.SetGlobalProperty(Global, "attached", Convert.ToInt32(_component.GetGlobalProperty(Global, "attached")) + 1);
    }

    public void Remove(MapControl control)
    {
        if (_controls.Remove(control.ViewId) && control.ViewId < _frames.Count)
        {
            _frames[control.ViewId] = new SlintImage();
        }
    }

    public void Show(int viewId, SlintImage frame) => _frames[viewId] = frame;

    private MapControl? Find(object?[] args) => _controls.GetValueOrDefault(Convert.ToInt32(args[0]));

    private static double Number(object? value) => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
}
