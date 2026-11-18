// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

// A UI in C# that binds to a view model, in the style of CommunityToolkit.Maui.Markup:
// .Bind() of a property to a member, .BindCommand() of a button to a command, and .Row() and .Column() in a grid.

using Contacts;
using Slint.Markup;

internal static class Program
{
    // Windows needs a single-threaded apartment for the window.
    [STAThread]
    private static void Main()
    {
        var viewModel = new ContactsViewModel();
        using var page = Page.Create(new WindowElement
        {
            new VerticalBox
            {
                new Text("Contacts").FontSize(24).FontWeight(700),
                new GridLayout
                {
                    new Text("Name").Row(0).Column(0).Width(64).VerticalAlignment(TextVerticalAlignment.Center),
                    new LineEdit().PlaceholderText("At least two letters").Row(0).Column(1).HorizontalStretch(1)
                        .Bind(LineEdit.TextProperty, (ContactsViewModel vm) => vm.Name, (vm, name) => vm.Name = name),
                    new Text().Row(1).Column(1).FontSize(12).Color(new Slint.Color(0xD3, 0x2F, 0x2F))
                        .Bind(Text.TextProperty, (ContactsViewModel vm) => vm.Errors(nameof(vm.Name))),
                    new Text("Email").Row(2).Column(0).Width(64).VerticalAlignment(TextVerticalAlignment.Center),
                    new LineEdit().PlaceholderText("name@example.com").Row(2).Column(1).HorizontalStretch(1)
                        .Bind(LineEdit.TextProperty, (ContactsViewModel vm) => vm.Email, (vm, email) => vm.Email = email),
                    new Text().Row(3).Column(1).FontSize(12).Color(new Slint.Color(0xD3, 0x2F, 0x2F))
                        .Bind(Text.TextProperty, (ContactsViewModel vm) => vm.Errors(nameof(vm.Email))),
                    new Button("Add").Row(4).Column(1).Primary(true).BindCommand((ContactsViewModel vm) => vm.AddCommand),
                }.Spacing(8),
                new LineEdit().PlaceholderText("Search")
                    .Bind(LineEdit.TextProperty, (ContactsViewModel vm) => vm.Search, (vm, search) => vm.Search = search),
                new ListView
                {
                    new ForEach<Contact>(() => viewModel.FilteredContacts, row => new HorizontalLayout
                    {
                        new VerticalLayout
                        {
                            new Text(row.Get(c => c.Name)).FontSize(15).FontWeight(600),
                            new Text(row.Get(c => c.Email)).FontSize(12).Color(new Slint.Color(0x80, 0x80, 0x80)),
                        }.HorizontalStretch(1),
                        new Button("Remove").OnClicked(row.Do(contact => viewModel.RemoveCommand.Execute(contact))),
                    }.Padding(6).Spacing(8)),
                }.VerticalStretch(1),
                new HorizontalLayout
                {
                    new Button("Refresh").BindCommand((ContactsViewModel vm) => vm.RefreshCommand),
                    new Spinner().Indeterminate(true).Width(24).Height(24)
                        .Bind(Spinner.VisibleProperty, (ContactsViewModel vm) => vm.RefreshCommand.IsRunning),
                    new Text().VerticalAlignment(TextVerticalAlignment.Center).Bind(Text.TextProperty, (ContactsViewModel vm) => vm.Status),
                }.Spacing(8).Alignment(LayoutAlignment.Start),
            },
        }.Title("Contacts").Width(480).Height(640).BindingContext(viewModel));
        page.Run();
    }
}
