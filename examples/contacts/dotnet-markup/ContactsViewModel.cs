// Copyright © SixtyFPS GmbH <info@slint.dev>
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Contacts;

public sealed record Contact(string Name, string Email);

/// <summary>A view model of CommunityToolkit.Mvvm, which knows nothing about the UI.</summary>
public sealed partial class ContactsViewModel : ObservableValidator
{
    public ObservableCollection<Contact> Contacts { get; } =
    [
        new("Ada Lovelace", "ada@example.com"),
        new("Grace Hopper", "grace@example.com"),
        new("Linus Torvalds", "linus@example.com"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredContacts))]
    private string _search = "";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [Required]
    [MinLength(2)]
    private string _name = "";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [Required]
    [EmailAddress]
    private string _email = "";

    [ObservableProperty]
    private string _status = "";

    public IEnumerable<Contact> FilteredContacts =>
        Contacts.Where(c => c.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || c.Email.Contains(Search, StringComparison.OrdinalIgnoreCase));

    public string Errors(string property) => string.Join(" ", GetErrors(property).Select(e => e.ErrorMessage));

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        Contacts.Add(new Contact(Name.Trim(), Email.Trim()));
        Status = $"Added {Name.Trim()}";
        Name = "";
        Email = "";
        ClearErrors();
    }

    private bool CanAdd() => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Email) && !HasErrors;

    [RelayCommand]
    private void Remove(Contact contact)
    {
        Contacts.Remove(contact);
        Status = $"Removed {contact.Name}";
    }

    [RelayCommand]
    private async Task Refresh()
    {
        Status = "Refreshing...";
        await Task.Delay(1500); // A web request, in a real app.
        Status = $"{Contacts.Count} contacts, refreshed at {DateTime.Now:T}";
    }
}
