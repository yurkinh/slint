// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using Slint.Markup;

namespace Slint.Tests;

[TestClass]
public sealed class ComponentTests
{
    [ClassInitialize]
    public static void KeepCompiledSourcesInMemory(TestContext _) => Page.CacheDirectory = null;

    private sealed class Counter : Component
    {
        private readonly Signal<int> _count = 0;

        public int Renders { get; private set; }

        protected override Element Render()
        {
            Renders++;
            return new VerticalBox
            {
                new Text(() => $"Count: {_count.Value}"),
                new Button("Add", () => _count.Value++),
            };
        }

        public void Set(int count) => _count.Value = count;
    }

    private sealed class Form : Component<Form.Model>
    {
        public sealed class Model
        {
            public string Name { get; set; } = "";
            public int Saves { get; set; }
        }

        public int Renders { get; private set; }

        public Model Current => State;

        protected override Element Render()
        {
            Renders++;
            return new VerticalBox
            {
                new LineEdit().Text(() => State.Name, name => SetState(s => s.Name = name)),
                new Button("Save", () =>
                {
                    SetState(s => s.Saves++);
                    SetState(s => s.Name = "");
                }),
            };
        }
    }

    private sealed class Toggle : Component<Toggle.Model>
    {
        public sealed class Model
        {
            public bool Details { get; set; }
        }

        protected override Element Render() => State.Details
            ? new VerticalBox { new Text("Details"), new Button("Less", () => SetState(s => s.Details = false)) }
            : new VerticalBox { new Button("More", () => SetState(s => s.Details = true)) };
    }

    private sealed class Names : Component
    {
        public readonly SignalList<string> Items = ["Ada"];

        protected override Element Render() => new VerticalLayout
        {
            new ForEach<string>(Items, row => new Text(row.Item)),
            new Text(() => $"{Items.Count} names"),
        };
    }

    private sealed class Pair : Component
    {
        public readonly Counter Left = new();
        public readonly Counter Right = new();
        public readonly Form Details = new();

        protected override Element Render() => new HorizontalBox { Left, Right, Details };
    }

    private sealed class Orphan : Component
    {
        protected override Element Render() => new Text("Orphan");
    }

    [TestMethod]
    public void NestsComponentsWithTheirOwnState() => SlintThread.Run(() =>
    {
        using var pair = new Pair();
        var instance = pair.Page.Instance;
        // The counters render in order: c0 and p0 are the left one's, c1 and p1 the right one's.
        Assert.AreEqual("Count: 0", instance.GetProperty("p0"));
        instance.Invoke("c1");
        instance.Invoke("c1");
        Assert.AreEqual("Count: 0", instance.GetProperty("p0"));
        Assert.AreEqual("Count: 2", instance.GetProperty("p1"));
        Assert.AreSame(instance, pair.Page.Instance);

        // A change of a child's signal outside a handler renders the window that shows it.
        pair.Left.Set(5);
        Assert.AreEqual("Count: 5", instance.GetProperty("p0"));

        // So does the state of a nested Component<TState>.
        instance.SetProperty("p2", "Ada");
        Testing.MockElapsedTime(0);
        Assert.AreEqual("Ada", pair.Details.Current.Name);
        instance.Invoke("c2");
        Assert.AreEqual(1, pair.Details.Current.Saves);
        Assert.AreEqual("", instance.GetProperty("p2"));
    });

    [TestMethod]
    public void RejectsAComponentInATreeThatNoComponentRenders() => SlintThread.Run(() =>
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => Page.Create(new VerticalBox { new Orphan() }));
        StringAssert.Contains(error.Message, "Orphan");
    });

    [TestMethod]
    public void RendersAgainAfterAHandlerWithoutCompilingAgain() => SlintThread.Run(() =>
    {
        using var counter = new Counter();
        var instance = counter.Page.Instance;
        Assert.AreEqual("Count: 0", instance.GetProperty("p0"));

        instance.Invoke("c0");
        instance.Invoke("c0");
        Assert.AreEqual("Count: 2", instance.GetProperty("p0"));
        Assert.AreSame(instance, counter.Page.Instance);
        Assert.AreEqual(3, counter.Renders);

        // A change outside a handler renders at once.
        counter.Set(10);
        Assert.AreEqual("Count: 10", instance.GetProperty("p0"));
    });

    [TestMethod]
    public void RendersOnceForTheChangesOfAHandler() => SlintThread.Run(() =>
    {
        using var form = new Form();
        var instance = form.Page.Instance;
        instance.SetProperty("p0", "Ada");
        Testing.MockElapsedTime(0);
        Assert.AreEqual("Ada", form.Current.Name);

        var renders = form.Renders;
        instance.Invoke("c0");
        Assert.AreEqual(renders + 1, form.Renders);
        Assert.AreEqual(1, form.Current.Saves);
        Assert.AreEqual("", instance.GetProperty("p0"));
    });

    [TestMethod]
    public void FollowsTheChangesOfASignalList() => SlintThread.Run(() =>
    {
        using var names = new Names();
        Assert.AreEqual("1 names", names.Page.Instance.GetProperty("p1"));
        names.Items.Add("Grace");
        Assert.AreEqual("2 names", names.Page.Instance.GetProperty("p1"));
        var rows = (Model<object?>)names.Page.Instance.GetProperty("p0")!;
        Assert.AreEqual("Grace", rows.RowData(1));
        StringAssert.Contains(names.Page.Source, "text: row0;");
    });

    [TestMethod]
    public void WarnsWhenACSharpConditionChangesTheStructure() => SlintThread.Run(() =>
    {
        var error = new StringWriter();
        var previous = Console.Error;
        Console.SetError(error);
        try
        {
            using var toggle = new Toggle();
            var source = toggle.Page.Source;
            toggle.Page.Instance.Invoke("c0");
            Assert.AreNotEqual(source, toggle.Page.Source);
            StringAssert.Contains(error.ToString(), "Toggle.Render() returned another structure");
            toggle.Page.Instance.Invoke("c0");
            Assert.AreEqual(source, toggle.Page.Source);
        }
        finally
        {
            Console.SetError(previous);
        }
    });
}
