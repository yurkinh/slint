#if (markup)
using Slint.Markup;

#endif
namespace SlintApp;

internal static class Program
{
    // Windows needs a single-threaded apartment for the window.
    [STAThread]
    private static void Main()
    {
#if (markup)
#if (style != "default")
        Page.Style = "SLINT_STYLE";
#endif
        using var app = new AppWindow();
#else
        using var app = AppWindow.Create();
#if (mvvm)
        // Binds the members of the view model to the properties and callbacks of the same name.
        using var binding = app.Bind(new MainViewModel());
#else
        app.OnRequestIncreaseValue(() => app.Counter++);
#endif
#endif
        app.Run();
    }
}
#if (markup)

/// <summary>The window, written in C#. It renders again after a change of a signal, and after a hot reload of this code.</summary>
internal sealed class AppWindow : Component
{
    private readonly Signal<int> _counter = 42;

    protected override Element Render() => new WindowElement
    {
        new VerticalBox
        {
            new Text(() => $"Counter: {_counter.Value}").FontSize(24).HorizontalAlignment(TextHorizontalAlignment.Center),
            new Button("Increase value", () => _counter.Value++),
        }.Alignment(LayoutAlignment.Center),
    }.Title("SlintApp").PreferredWidth(320).PreferredHeight(200);
}
#endif
