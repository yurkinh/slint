// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using Slint.Markup;

namespace Slint.Tests;

[TestClass]
public sealed class MarkupTests
{
    // Pages compile to memory only, except in the test of the cache directory.
    [ClassInitialize]
    public static void KeepCompiledSourcesInMemory(TestContext _) => Page.CacheDirectory = null;

    private sealed record Todo(string Title, bool Done);

    private static Element Counter(Func<int> count, Action add) =>
        new VerticalBox
        {
            new Text(() => $"Count: {count()}").FontSize(24),
            new Button("Add").OnClicked(add),
        };

    [TestMethod]
    public void WritesTheSource() => SlintThread.Run(() =>
    {
        var count = 0;
        using var page = Page.Create(Counter(() => count, () => count++));
        Assert.AreEqual(
            """
            import { Button, VerticalBox } from "std-widgets.slint";
            export component MarkupPage inherits Window {
                in property <string> p0;
                callback c0();
                VerticalBox {
                    Text {
                        text: root.p0;
                        font-size: 24px;
                    }
                    Button {
                        text: "Add";
                        clicked => { root.c0(); }
                    }
                }
            }

            """.ReplaceLineEndings(),
            page.Source.ReplaceLineEndings());
    });

    [TestMethod]
    public void UpdatesAfterAHandler() => SlintThread.Run(() =>
    {
        var count = 0;
        using var page = Page.Create(Counter(() => count, () => count++));
        Assert.AreEqual("Count: 0", page.Instance.GetProperty("p0"));
        page.Instance.Invoke("c0");
        page.Instance.Invoke("c0");
        Assert.AreEqual(2, count);
        Assert.AreEqual("Count: 2", page.Instance.GetProperty("p0"));

        count = 10;
        page.Update();
        Assert.AreEqual("Count: 10", page.Instance.GetProperty("p0"));
    });

    [TestMethod]
    public void ReusesTheCompiledSource() => SlintThread.Run(() =>
    {
        Page.ClearCache();
        int first = 0, second = 5;
        using var a = Page.Create(Counter(() => first, () => first++));
        using var b = Page.Create(Counter(() => second, () => second++));
        Assert.AreNotEqual(TimeSpan.Zero, a.CompileTime);
        Assert.AreEqual(TimeSpan.Zero, b.CompileTime);
        b.Instance.Invoke("c0");
        Assert.AreEqual("Count: 0", a.Instance.GetProperty("p0"));
        Assert.AreEqual("Count: 6", b.Instance.GetProperty("p0"));
    });

    [TestMethod]
    public void BindsBothWays() => SlintThread.Run(() =>
    {
        var name = "Ada";
        var accepted = "";
        using var page = Page.Create(new WindowElement
        {
            new VerticalLayout
            {
                new LineEdit().Text(() => name, value => name = value).OnAccepted(text => accepted = text),
                new Text(() => $"Hello, {name}"),
            },
        }.Title("Greeting"));

        Assert.AreEqual("Ada", page.Instance.GetProperty("p0"));
        page.Instance.SetProperty("p0", "Grace");
        Testing.MockElapsedTime(0);
        Assert.AreEqual("Grace", name);
        Assert.AreEqual("Hello, Grace", page.Instance.GetProperty("p1"));

        name = "Linus";
        page.Update();
        Assert.AreEqual("Linus", page.Instance.GetProperty("p0"));

        page.Instance.Invoke("c0", "Linus");
        Assert.AreEqual("Linus", accepted);
    });

    [TestMethod]
    public void RepeatsRowsAndShowsConditionally() => SlintThread.Run(() =>
    {
        var todos = new List<Todo> { new("Write", false), new("Test", true) };
        using var page = Page.Create(new VerticalLayout
        {
            new ForEach<Todo>(() => todos, row => new HorizontalLayout
            {
                new CheckBox(row.Get(t => t.Title)).Checked(row.Get(t => t.Done)),
                new Button("Remove").OnClicked(row.Do(todo => todos.Remove(todo))),
            }),
            new If(() => todos.Count == 0)
            {
                new Text("Nothing to do"),
            },
        });

        StringAssert.Contains(page.Source, "in property <[{title: string, done: bool}]> p0;");
        StringAssert.Contains(page.Source, "for row0[index0] in root.p0 :");
        StringAssert.Contains(page.Source, "clicked => { root.c0(index0); }");
        Assert.AreEqual(2, ((Model<object?>)page.Instance.GetProperty("p0")!).RowCount);
        Assert.IsFalse((bool)page.Instance.GetProperty("p1")!);

        page.Instance.Invoke("c0", 1);
        CollectionAssert.AreEqual(new[] { new Todo("Write", false) }, todos);
        page.Instance.Invoke("c0", 0);
        Assert.AreEqual(0, ((Model<object?>)page.Instance.GetProperty("p0")!).RowCount);
        Assert.IsTrue((bool)page.Instance.GetProperty("p1")!);
    });

    [TestMethod]
    public void EscapesStrings() => SlintThread.Run(() =>
    {
        using var page = Page.Create(new Text("Say \"{hi}\" \\ bye"));
        StringAssert.Contains(page.Source, "text: \"Say \\\"{hi}\\\" \\\\ bye\";");
    });

    [TestMethod]
    public void ReportsErrorsWithTheSource() => SlintThread.Run(() =>
    {
        var error = Assert.ThrowsExactly<SlintException>(() =>
            Page.Create(new Text(new Expr("no-such-thing"))));
        StringAssert.Contains(error.Message, "no-such-thing");
        StringAssert.Contains(error.Message, "export component MarkupPage");
        Assert.DoesNotContain("edition", error.Message);
    });

    [TestMethod]
    public void ReadsCompiledSourcesFromTheCacheDirectory() => SlintThread.Run(() =>
    {
        var directory = Directory.CreateTempSubdirectory("slint-markup-").FullName;
        var previous = Page.CacheDirectory;
        Page.CacheDirectory = directory;
        try
        {
            var count = 0;
            // A text that no other test uses, so the page isn't in the memory cache.
            Element Tree() => new VerticalBox { new Text(() => $"Cached {count}"), new Button("Cache").OnClicked(() => count++) };
            Page.ClearCache();
            using (var first = Page.Create(Tree()))
            {
                Assert.AreEqual(PageOrigin.Compiled, first.Origin);
            }
            Assert.HasCount(1, Directory.GetFiles(directory, "*.unit"));

            Page.ClearCache();
            using var second = Page.Create(Tree());
            Assert.AreEqual(PageOrigin.DiskCache, second.Origin);
            second.Instance.Invoke("c0");
            Assert.AreEqual("Cached 1", second.Instance.GetProperty("p0"));

            using var third = Page.Create(Tree());
            Assert.AreEqual(PageOrigin.MemoryCache, third.Origin);

            // A file that isn't a unit is compiled again and replaced.
            var file = Directory.GetFiles(directory, "*.unit").Single();
            File.WriteAllBytes(file, [1, 2, 3]);
            Page.ClearCache();
            using var fourth = Page.Create(Tree());
            Assert.AreEqual(PageOrigin.Compiled, fourth.Origin);
            Assert.IsGreaterThan(3L, new FileInfo(file).Length);
        }
        finally
        {
            Page.CacheDirectory = previous;
            Directory.Delete(directory, true);
        }
    });

    [TestMethod]
    public void RebuildsWithNewLambdasOrANewSource() => SlintThread.Run(() =>
    {
        var greeting = "Hello";
        var withButton = false;
        var clicks = 0;
        Element Render() => withButton
            ? new VerticalBox { new Text(() => greeting + "!"), new Button("Click").OnClicked(() => clicks++) }
            : new VerticalBox { new Text(() => greeting) };

        var page = Page.Create(Render);
        Assert.AreEqual("Hello", page.Instance.GetProperty("p0"));

        // A change of the C# code that keeps the source: the same instance takes the new lambda.
        var instance = page.Instance;
        greeting = "Hi";
        page.Rebuild();
        Assert.AreSame(instance, page.Instance);
        Assert.AreEqual("Hi", page.Instance.GetProperty("p0"));

        // A change that adds an element: a new source, and a new instance, which takes the place of the visible window.
        page.Show();
        withButton = true;
        page.Rebuild();
        Assert.AreNotSame(instance, page.Instance);
        Assert.IsTrue(page.Instance.Window.IsVisible);
        Assert.AreEqual("Hi!", page.Instance.GetProperty("p0"));
        page.Instance.Invoke("c0");
        Assert.AreEqual(1, clicks);

        // Disposing hides the window, the last one, which queues a quit of the event loop.
        page.Dispose();
        WindowTests.TakeQueuedQuit();
    });

    [TestMethod]
    public void SetsEnumsUnitsAndTheMembersOfEveryElement() => SlintThread.Run(() =>
    {
        var alignment = TextHorizontalAlignment.Left;
        using var page = Page.Create(new VerticalLayout
        {
            new Text("Centered").HorizontalAlignment(() => alignment).Opacity(0.5).Width(120),
            new Rectangle().Background(new Color(255, 0, 0)).TransformRotation(45).Height(10),
        });

        StringAssert.Contains(page.Source, "in property <TextHorizontalAlignment> p0;");
        StringAssert.Contains(page.Source, "opacity: 0.5;");
        StringAssert.Contains(page.Source, "width: 120px;");
        StringAssert.Contains(page.Source, "background: #ff0000ff;");
        StringAssert.Contains(page.Source, "transform-rotation: 45deg;");
        Assert.AreEqual(new EnumValue("TextHorizontalAlignment", "left"), page.Instance.GetProperty("p0"));
        alignment = TextHorizontalAlignment.Center;
        page.Update();
        Assert.AreEqual(new EnumValue("TextHorizontalAlignment", "center"), page.Instance.GetProperty("p0"));
    });

    [TestMethod]
    public void PassesTypedCallbackArgumentsAndBindsWidgetsBothWays() => SlintThread.Run(() =>
    {
        var volume = 20.0;
        var changes = new List<double>();
        var touched = 0;
        using var page = Page.Create(new VerticalBox
        {
            new Slider().Maximum(100).Value(() => volume, v => volume = v).OnChanged(changes.Add),
            // pointer-event passes a struct that the builders have no C# type for, so the handler gets no arguments.
            new TouchArea().OnPointerEvent(() => touched++),
        });

        StringAssert.Contains(page.Source, "value <=> root.p0;");
        StringAssert.Contains(page.Source, "changed(a0) => { root.c0(a0); }");
        StringAssert.Contains(page.Source, "pointer-event => { root.c1(); }");

        page.Instance.SetProperty("p0", 75.0);
        Testing.MockElapsedTime(0);
        Assert.AreEqual(75.0, volume);

        page.Instance.Invoke("c0", 42.5);
        CollectionAssert.AreEqual(new[] { 42.5 }, changes);
        page.Instance.Invoke("c1");
        Assert.AreEqual(1, touched);
    });

    [TestMethod]
    public void HasNoTypeNamedLikeATypeOfTheSlintOrTheImplicitlyImportedNamespaces()
    {
        // The namespaces of ImplicitUsings in a console project, which apps import with Slint.Markup.
        string[] imported = ["Slint", "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks"];
        var slint = new[] { typeof(ComponentInstance), typeof(object), typeof(List<>), typeof(File), typeof(Enumerable), typeof(HttpClient), typeof(Thread), typeof(Task) }
            .Select(t => t.Assembly).Distinct().SelectMany(a => a.GetExportedTypes())
            .Where(t => imported.Contains(t.Namespace)).Select(t => t.Name).ToHashSet();
        var clashes = typeof(Page).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == "Slint.Markup" && slint.Contains(t.Name)).Select(t => t.Name).ToList();
        Assert.IsEmpty(clashes, string.Join(", ", clashes));
    }
}
