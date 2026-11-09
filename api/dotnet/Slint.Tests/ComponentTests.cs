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

    private sealed class SharedState
    {
        public readonly Signal<string> Name = "Ada";
    }

    private sealed class Greeting(SharedState shared) : Component
    {
        protected override Element Render() => new Text(() => $"Hello, {shared.Name.Value}");
    }

    private sealed class ListPage : Component
    {
        protected override Element Render() => new VerticalBox
        {
            new Text("List"),
            new Button("Open", () => Navigation!.Push(new DetailPage())),
        };
    }

    private sealed class DetailPage : Component
    {
        private readonly Signal<int> _views = 0;

        protected override Element Render() => new VerticalBox
        {
            new Text(() => $"Detail {_views.Value}"),
            new Button("Back", () => Navigation!.Pop()),
            new Button("View", () => _views.Value++),
        };
    }

    private sealed class Tabs : Component
    {
        public readonly Navigation First = new(new ListPage());
        private readonly Signal<int> _tab = 0;

        public int Tab => _tab.Value;

        protected override Element Render() => new TabWidget
        {
            new Tab("First") { First },
            new Tab("Second") { new ComboBox().Model(["Low", "High"]).CurrentValue("Low") },
        }.CurrentIndex(() => _tab.Value, index => _tab.Value = index);
    }

    private sealed class Person : Component
    {
        public readonly Signal<string> First = "Jane";
        public readonly Signal<int> Age = 30;
        public readonly Computed<string> Greeting;
        public int Computations { get; private set; }
        public int Renders { get; private set; }

        public Person() => Greeting = new(() =>
        {
            Computations++;
            return Age.Value < 18 ? $"Hey {First.Value}!" : $"Hello, {First.Value}.";
        });

        protected override Element Render()
        {
            Renders++;
            return new Text(() => Greeting.Value);
        }
    }

    [TestMethod]
    public void ComputesValuesFromSignals() => SlintThread.Run(() =>
    {
        using var person = new Person();
        var instance = person.Page.Instance;
        Assert.AreEqual("Hello, Jane.", instance.GetProperty("p0"));
        var computations = person.Computations;

        // Reading again without a change of a dependency doesn't compute again.
        _ = person.Greeting.Value;
        Assert.AreEqual(computations, person.Computations);

        person.Age.Value = 12;
        Assert.AreEqual("Hey Jane!", instance.GetProperty("p0"));
        person.First.Value = "Ada";
        Assert.AreEqual("Hey Ada!", instance.GetProperty("p0"));
    });

    [TestMethod]
    public void RendersOnceForTheChangesOfAnotherThread() => SlintThread.Run(() =>
    {
        using var counter = new Counter();
        var instance = counter.Page.Instance;
        var renders = counter.Renders;
        var worker = new Thread(() =>
        {
            for (var i = 0; i < 10; i++)
            {
                counter.Set(i + 1);
            }
            EventLoop.InvokeFromEventLoop(EventLoop.Quit);
        });
        worker.Start();
        worker.Join();
        EventLoop.Run();
        Assert.AreEqual("Count: 10", instance.GetProperty("p0"));
        Assert.AreEqual(renders + 1, counter.Renders);
    });

    [TestMethod]
    public void FollowsSignalsOfStateThatComponentsShare() => SlintThread.Run(() =>
    {
        var shared = new SharedState();
        using var first = new Greeting(shared);
        using var second = new Greeting(shared);
        Assert.AreEqual("Hello, Ada", first.Page.Instance.GetProperty("p0"));
        Assert.AreEqual("Hello, Ada", second.Page.Instance.GetProperty("p0"));
        shared.Name.Value = "Grace";
        Assert.AreEqual("Hello, Grace", first.Page.Instance.GetProperty("p0"));
        Assert.AreEqual("Hello, Grace", second.Page.Instance.GetProperty("p0"));
    });

    [TestMethod]
    public void NavigatesBetweenPagesInATab() => SlintThread.Run(() =>
    {
        var error = new StringWriter();
        var previous = Console.Error;
        Console.SetError(error);
        try
        {
            using var tabs = new Tabs();
            var page = tabs.Page;
            StringAssert.Contains(page.Source, "Tab {");
            StringAssert.Contains(page.Source, "model: [\"Low\", \"High\"];");
            var listSource = page.Source;

            // p0: the current index, c0: Open.
            page.Instance.SetProperty("p0", 1);
            Testing.MockElapsedTime(0);
            Assert.AreEqual(1, tabs.Tab);
            page.Instance.Invoke("c0");
            Assert.AreEqual(2, tabs.First.Depth);
            StringAssert.Contains(page.Source, "text: \"Back\";");
            // The new instance shows the tab that was current.
            Assert.AreEqual(1.0, page.Instance.GetProperty("p0"));

            // Detail: c0 Back, c1 View.
            page.Instance.Invoke("c1");
            Assert.AreEqual("Detail 1", page.Instance.GetProperty("p1"));
            page.Instance.Invoke("c0");
            Assert.AreEqual(1, tabs.First.Depth);
            Assert.AreEqual(listSource, page.Source);
            Assert.AreEqual("", error.ToString());
        }
        finally
        {
            Console.SetError(previous);
        }
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
