// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: GPL-3.0-only OR LicenseRef-Slint-Royalty-free-2.0 OR LicenseRef-Slint-Software-3.0

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Slint.Markup;

namespace Slint.Tests;

public sealed partial class MarkupViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "Ada";

    public ObservableCollection<string> Tags { get; } = ["first"];

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => Tags.Add(Name);

    private bool CanSave() => Name.Length > 0;
}

[TestClass]
public sealed class MarkupBindingTests
{
    [ClassInitialize]
    public static void KeepCompiledSourcesInMemory(TestContext _) => Page.CacheDirectory = null;

    private static Element Form(MarkupViewModel viewModel) => new VerticalBox
    {
        new LineEdit().Bind(LineEdit.TextProperty, (MarkupViewModel vm) => vm.Name, (vm, name) => vm.Name = name),
        new Text().Bind(Text.TextProperty, (MarkupViewModel vm) => $"Hello, {vm.Name}"),
        new Button("Save").BindCommand((MarkupViewModel vm) => vm.SaveCommand),
        new ForEach<string>(() => viewModel.Tags, row => new Text(row.Item)),
        new Text().Bind(Text.TextProperty, (MarkupViewModel vm) => $"{vm.Tags.Count} tags"),
    }.BindingContext(viewModel);

    [TestMethod]
    public void BindsToAViewModel() => SlintThread.Run(() =>
    {
        var viewModel = new MarkupViewModel();
        using var page = Page.Create(Form(viewModel));
        var instance = page.Instance;
        // p0: the text field, p1: the greeting, p2: whether Save is enabled, p3: the tags, p4: their count; c0: Save.
        Assert.AreEqual("Ada", instance.GetProperty("p0"));
        Assert.AreEqual("Hello, Ada", instance.GetProperty("p1"));
        Assert.IsTrue((bool)instance.GetProperty("p2")!);

        // A change of the view model, outside a handler of the page.
        viewModel.Name = "Grace";
        Assert.AreEqual("Grace", instance.GetProperty("p0"));
        Assert.AreEqual("Hello, Grace", instance.GetProperty("p1"));

        // A change of the UI.
        instance.SetProperty("p0", "Linus");
        Testing.MockElapsedTime(0);
        Assert.AreEqual("Linus", viewModel.Name);
        Assert.AreEqual("Hello, Linus", instance.GetProperty("p1"));

        // The command, and the list that it changes.
        instance.Invoke("c0");
        CollectionAssert.AreEqual(new[] { "first", "Linus" }, viewModel.Tags);
        Assert.AreEqual(2, ((Model<object?>)instance.GetProperty("p3")!).RowCount);
        Assert.AreEqual("2 tags", instance.GetProperty("p4"));
        viewModel.Tags.Clear();
        Assert.AreEqual("0 tags", instance.GetProperty("p4"));

        // CanExecute follows the name.
        viewModel.Name = "";
        Assert.IsFalse((bool)instance.GetProperty("p2")!);
    });

    [TestMethod]
    public void UpdatesOnceForTheChangesOfAnotherThread() => SlintThread.Run(() =>
    {
        var viewModel = new MarkupViewModel();
        using var page = Page.Create(Form(viewModel));
        var worker = new Thread(() =>
        {
            for (var i = 0; i < 5; i++)
            {
                viewModel.Name = $"Name {i}";
            }
            EventLoop.InvokeFromEventLoop(EventLoop.Quit);
        });
        worker.Start();
        worker.Join();
        Assert.AreEqual("Ada", page.Instance.GetProperty("p0"));
        EventLoop.Run();
        Assert.AreEqual("Name 4", page.Instance.GetProperty("p0"));
    });

    [TestMethod]
    public void PlacesChildrenInGridColumns() => SlintThread.Run(() =>
    {
        using var page = Page.Create(new GridLayout
        {
            new Text("Name").Row(0).Column(0),
            new Text("Value").Row(0).Column(1).ColumnSpan(2).RowSpan(1),
        });
        StringAssert.Contains(page.Source, "col: 1;");
        StringAssert.Contains(page.Source, "colspan: 2;");
    });

    [TestMethod]
    public void RejectsABindingWithoutAContext() => SlintThread.Run(() =>
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Page.Create(new Text().Bind(Text.TextProperty, (MarkupViewModel vm) => vm.Name)));
        StringAssert.Contains(error.Message, "BindingContext");
    });
}
