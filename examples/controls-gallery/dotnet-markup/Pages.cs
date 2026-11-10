// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// Pages of the CometControlsGallery sample of https://github.com/dotnet/maui-labs, ported to Slint.Markup:
// the controls, the pickers, and the pages about state. Comet's Reactive<T> is a Signal<T> here.

using Slint.Markup;
using static ControlsGallery.GalleryPageHelpers;

namespace ControlsGallery;

internal sealed class SignalCounterPage : Component
{
    private readonly Signal<int> _count = 0;
    private readonly Signal<string> _label = "Ready";

    protected override Element Render() => Scaffold("Signal Counter",
        Section("Basic Counter",
            new Text(() => $"Count: {_count.Value}").FontSize(48).FontWeight(700)
                .HorizontalAlignment(TextHorizontalAlignment.Center)
                .Color(() => _count.Value >= 0 ? Colors.DodgerBlue : Colors.Crimson),
            ButtonRow(
                new Button("−", () =>
                {
                    _count.Value--;
                    _label.Value = $"Decremented to {_count.Value}";
                }).Width(60).Height(44),
                new Button("Reset", () =>
                {
                    _count.Value = 0;
                    _label.Value = "Reset to zero";
                }).Width(80).Height(44),
                new Button("+", () =>
                {
                    _count.Value++;
                    _label.Value = $"Incremented to {_count.Value}";
                }).Width(60).Height(44)),
            new Text(() => _label.Value).FontSize(14).Color(Colors.Grey).HorizontalAlignment(TextHorizontalAlignment.Center)),
        Section("Rapid Update Test",
            Caption("Tap to add 10 in a tight loop — should see final value, not intermediate flicker."),
            new Button("Add 10 rapidly", () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    _count.Value++;
                }
                _label.Value = $"Added 10, now at {_count.Value}";
            })),
        Section("Validation Notes",
            BodyText("Signal<int> read and write"),
            BodyText("Negative values change text color"),
            BodyText("Multiple signals updated in same handler"),
            BodyText("Rapid loop writes coalesce to single UI update")));
}

internal sealed class ComputedDemoPage : Component
{
    private readonly Signal<string> _firstName = "Jane";
    private readonly Signal<string> _lastName = "Doe";
    private readonly Signal<double> _age = 30;
    private readonly Computed<string> _summary;
    private readonly Computed<string> _greeting;

    public ComputedDemoPage()
    {
        _summary = new(() => $"{_firstName.Value} {_lastName.Value}, age {(int)_age.Value}");
        _greeting = new(() =>
        {
            var name = _firstName.Value;
            var age = (int)_age.Value;
            return age < 18 ? $"Hey {name}!" : age < 65 ? $"Hello, {name}." : $"Good day, {name}.";
        });
    }

    protected override Element Render() => Scaffold("Computed Demo",
        Section("Input Signals",
            Caption("First Name:"),
            new LineEdit().PlaceholderText("First name...").Text(() => _firstName.Value, v => _firstName.Value = v),
            Caption("Last Name:"),
            new LineEdit().PlaceholderText("Last name...").Text(() => _lastName.Value, v => _lastName.Value = v),
            new Text(() => $"Age: {(int)_age.Value}").FontSize(12).Color(Colors.Grey),
            new Slider().Minimum(0).Maximum(100).Value(() => _age.Value, v => _age.Value = v)),
        Section("Computed Outputs",
            new Text(() => $"Summary: {_summary.Value}").FontSize(18).FontWeight(700),
            new Text(() => _greeting.Value).FontSize(16).Color(Colors.MediumSeaGreen)),
        Section("Validation Notes",
            BodyText("Computed<string> re-evaluates when any dependency changes"),
            BodyText("Two Computed values from overlapping dependencies"),
            BodyText("Conditional logic inside Computed (greeting changes by age)"),
            BodyText("No manual subscription — automatic dependency tracking")));
}

internal sealed class TwoWayBindingPage : Component
{
    private readonly Signal<string> _sharedText = "Hello Comet";
    private readonly Signal<double> _sliderValue = 50;
    private readonly Signal<bool> _toggleValue = false;

    protected override Element Render() => Scaffold("Two-Way Binding",
        Section("Shared Text Signal",
            Caption("Text field A writes to the shared text:"),
            new LineEdit().PlaceholderText("Type here...").Text(() => _sharedText.Value, v => _sharedText.Value = v),
            Caption("Text field B is bound to the same text:"),
            new LineEdit().PlaceholderText("Or type here...").Text(() => _sharedText.Value, v => _sharedText.Value = v),
            new Text(() => $"Live value: \"{_sharedText.Value}\"").FontSize(16).FontWeight(700).Color(Colors.DodgerBlue),
            new Text(() => $"Length: {_sharedText.Value.Length} characters").FontSize(14).Color(Colors.Grey)),
        Section("Slider ↔ Label",
            new Slider().Minimum(0).Maximum(100).Value(() => _sliderValue.Value, v => _sliderValue.Value = v),
            new Text(() => $"Value: {_sliderValue.Value:F1}").FontSize(16).HorizontalAlignment(TextHorizontalAlignment.Center),
            ButtonRow(
                new Button("Set 0", () => _sliderValue.Value = 0),
                new Button("Set 50", () => _sliderValue.Value = 50),
                new Button("Set 100", () => _sliderValue.Value = 100))),
        Section("Toggle ↔ Label",
            ButtonRow(
                new Switch().Checked(() => _toggleValue.Value, v => _toggleValue.Value = v),
                new Text(() => _toggleValue.Value ? "ON" : "OFF").FontSize(16)),
            new Button("Toggle programmatically", () => _toggleValue.Value = !_toggleValue.Value)),
        Section("Validation Notes",
            BodyText("Two text fields bound to the same Signal<string>"),
            BodyText("Programmatic signal writes update UI controls"),
            BodyText("Slider, Switch two-way binding verified"),
            BodyText("UI → Signal → UI round-trip")));
}

internal sealed class SignalListPage : Component
{
    private readonly SignalList<string> _items = [];
    private readonly Signal<string> _newItemText = "";
    private readonly Signal<int> _addCount = 0;

    protected override Element Render() => Scaffold("Signal List",
        Section("Add Items",
            new HorizontalLayout
            {
                new LineEdit().PlaceholderText("Item text...").Text(() => _newItemText.Value, v => _newItemText.Value = v),
                new Button("Add", () =>
                {
                    _items.Add(string.IsNullOrWhiteSpace(_newItemText.Value) ? $"Item {++_addCount.Value}" : _newItemText.Value);
                    _newItemText.Value = "";
                }).Width(60),
            }.Spacing(8),
            ButtonRow(
                new Button("Add 5", () =>
                {
                    for (var i = 0; i < 5; i++)
                    {
                        _items.Add($"Batch {++_addCount.Value}");
                    }
                }),
                new Button("Clear All", _items.Clear))),
        Section(() => $"Items ({_items.Count})",
            new If(() => _items.Count == 0)
            {
                new Text("No items — add some above!").FontSize(14).Color(Colors.Grey),
            },
            new VerticalLayout
            {
                new ForEach<string>(_items, row => new HorizontalLayout
                {
                    new Text(new Expr($"\"• \" + {row.Item.Code}")).FontSize(14).HorizontalStretch(1)
                        .VerticalAlignment(TextVerticalAlignment.Center),
                    new Button("X").OnClicked(row.Do((_, index) => _items.RemoveAt(index))).Width(32).Height(32),
                }.Padding(4)),
            }.Spacing(4)),
        Section("Validation Notes",
            BodyText("SignalList<string> Add, RemoveAt, and Clear"),
            BodyText("ForEach follows every list mutation without compiling again"),
            BodyText("Batch add (5 items in tight loop)")));
}

internal sealed class CoalescingDemoPage : Component
{
    private readonly Signal<int> _counter = 0;
    private readonly Signal<string> _status = "Ready";
    private readonly Signal<bool> _timerRunning = false;
    private int _renders;

    protected override Element Render()
    {
        _renders++;
        return Scaffold("Coalescing Demo",
            Section("Synchronous Burst",
                Caption("Writes 100 signal updates in a tight loop, which render once."),
                new Button("Write 100x synchronously", () =>
                {
                    var before = _renders;
                    for (var i = 0; i < 100; i++)
                    {
                        _counter.Value++;
                    }
                    _status.Value = $"Wrote 100x. Counter={_counter.Value}. Renders before={before}.";
                }),
                new Text(() => $"Counter: {_counter.Value}").FontSize(24).FontWeight(700).Color(Colors.DodgerBlue),
                new Text(() => $"Renders: {_renders}").FontSize(16).Color(Colors.MediumPurple),
                new Text(() => _status.Value).FontSize(12).Color(Colors.Grey)),
            Section("Async Burst",
                Caption("Fires 10 writes from a background thread; the window renders once for them."),
                new Button("Write 10x from background", () =>
                {
                    _status.Value = "Background writes started...";
                    Task.Run(() =>
                    {
                        for (var i = 0; i < 10; i++)
                        {
                            _counter.Value++;
                        }
                        Slint.EventLoop.InvokeFromEventLoop(() => _status.Value = $"Background done. Counter={_counter.Value}");
                    });
                })),
            Section("Timer Stress",
                Caption("A timer writes to the counter every 100 ms; the UI should update smoothly without freezing."),
                new Button(() => _timerRunning.Value ? "Stop Timer" : "Start Timer", () =>
                {
                    _timerRunning.Value = !_timerRunning.Value;
                    if (_timerRunning.Value)
                    {
                        StartTimer();
                    }
                }),
                new Text(() => _timerRunning.Value ? "Timer running..." : "Timer stopped").FontSize(14)
                    .Color(() => _timerRunning.Value ? Colors.MediumSeaGreen : Colors.Grey)),
            Section("Reset",
                new Button("Reset all counters", () =>
                {
                    _counter.Value = 0;
                    _renders = 0;
                    _status.Value = "Reset complete";
                })),
            Section("Validation Notes",
                BodyText("100 synchronous writes → one render"),
                BodyText("Background thread writes → one render"),
                BodyText("Timer stress test — no UI freeze"),
                BodyText("The render count tracks actual renders")));
    }

    private async void StartTimer()
    {
        while (_timerRunning.Value)
        {
            await Task.Delay(100);
            if (_timerRunning.Value)
            {
                _counter.Value++;
            }
        }
    }
}

internal sealed class StatePreservationPage : Component
{
    private readonly Signal<int> _parentCounter = 0;
    private readonly Signal<string> _parentNote = "Navigate to child, then come back.";
    private readonly DateTimeOffset _createdAt = DateTimeOffset.Now;

    protected override Element Render() => Scaffold("State Preservation",
        Section("Parent State",
            new Text(() => $"Parent Counter: {_parentCounter.Value}").FontSize(32).FontWeight(700)
                .HorizontalAlignment(TextHorizontalAlignment.Center).Color(Colors.DodgerBlue),
            ButtonRow(
                new Button("−", () => _parentCounter.Value--).Width(60).Height(44),
                new Button("+", () => _parentCounter.Value++).Width(60).Height(44)),
            new Text(() => _parentNote.Value).FontSize(14).Color(Colors.Grey),
            Caption($"Page created: {_createdAt:T}")),
        Section("Navigation Test",
            Caption("Push a child page, interact with it, then pop back. The parent counter above should retain its value."),
            new Button("Push Child Page", () =>
            {
                _parentNote.Value = $"Navigated away with counter={_parentCounter.Value}";
                Navigation!.Push(new StatePreservationChildPage(_parentCounter.Value));
            })),
        Section("Validation Notes",
            BodyText("Parent state survives child push and pop"),
            BodyText("The creation time proves the same instance"),
            BodyText("Child page has its own independent state"),
            BodyText("A popped child stops following its signals")));
}

internal sealed class StatePreservationChildPage(int parentValue) : Component
{
    private readonly Signal<int> _childCounter = 0;
    private readonly DateTimeOffset _childCreatedAt = DateTimeOffset.Now;

    protected override Element Render() => Scaffold("Child Page",
        Section("Child State",
            new Text(() => $"Child Counter: {_childCounter.Value}").FontSize(32).FontWeight(700)
                .HorizontalAlignment(TextHorizontalAlignment.Center).Color(Colors.MediumSeaGreen),
            ButtonRow(
                new Button("−", () => _childCounter.Value--).Width(60).Height(44),
                new Button("+", () => _childCounter.Value++).Width(60).Height(44))),
        Section("Context",
            BodyText($"Parent counter was {parentValue} when navigated here"),
            Caption($"Child created: {_childCreatedAt:T}"),
            new Button("Pop back to parent", () => Navigation!.Pop())));
}

internal sealed class ControlsPage : Component
{
    private readonly Signal<int> _clickCount = 0;
    private readonly Signal<string> _entryText = "";
    private readonly Signal<double> _sliderValue = 50;
    private readonly Signal<bool> _toggleValue = false;
    private readonly Signal<bool> _checkValue = false;
    private readonly Signal<int> _stepperValue = 0;
    private readonly Signal<string> _radioValue = "Option A";

    protected override Element Render() => Scaffold("Controls",
        Section("Button & ProgressBar",
            new Button("Click me!", () => _clickCount.Value++),
            // The style's Button has no background, so the gradient button is a Clickable.
            new Clickable("Gradient Button", () => _clickCount.Value++)
                .Background(new Expr("@linear-gradient(90deg, #ff4500 0%, #1e90ff 100%)"))
                .TextColor(Colors.White).FontWeight(600).BorderRadius(6).Height(36),
            new Text(() => $"Clicks: {_clickCount.Value}").FontSize(14),
            Caption("Progress (click 20x to fill):"),
            new ProgressIndicator().Progress(() => Math.Min(1.0, _clickCount.Value / 20.0))),
        Section("Button with Image",
            StarButton("Image Left (default)", "left"),
            StarButton("Image Right", "right"),
            StarButton("Image Top", "top"),
            StarButton("Image Bottom", "bottom")),
        Section("Entry",
            new LineEdit().PlaceholderText("Type here...").Text(() => _entryText.Value, v => _entryText.Value = v),
            new Text(() => $"Echo: {_entryText.Value}").FontSize(14).Color(Colors.Grey)),
        Section("Editor",
            new TextEdit().PlaceholderText("Multi-line text editor...").Height(80)),
        Section("Slider",
            new Slider().Minimum(0).Maximum(100).Value(() => _sliderValue.Value, v => _sliderValue.Value = v),
            new Text(() => $"Slider: {_sliderValue.Value:F0}").FontSize(14)),
        Section("Switch",
            ButtonRow(
                new Switch().Checked(() => _toggleValue.Value, v => _toggleValue.Value = v),
                new Text(() => _toggleValue.Value ? "On" : "Off").FontSize(14))),
        Section("CheckBox",
            ButtonRow(
                new CheckBox().Checked(() => _checkValue.Value, v => _checkValue.Value = v),
                new Text(() => _checkValue.Value ? "Checked ✓" : "Unchecked").FontSize(14))),
        Section("Stepper (increment by 5)",
            new SpinBox().Minimum(0).Maximum(50).StepSize(5).Value(() => _stepperValue.Value, v => _stepperValue.Value = v),
            new Text(() => $"Stepper: {_stepperValue.Value}").FontSize(14)),
        Section("RadioButton",
            new RadioGroup
            {
                new RadioButton("Option A").Checked(() => _radioValue.Value == "Option A"),
                new RadioButton("Option B").Checked(() => _radioValue.Value == "Option B"),
                new RadioButton("Option C").Checked(() => _radioValue.Value == "Option C"),
            }.OnSelected(value => _radioValue.Value = value),
            new Text(() => $"Selected: {_radioValue.Value}").FontSize(14).Color(Colors.DodgerBlue)));

    // Comet draws a star with a font image; the fonts that Slint embeds draw the star as text.
    private static Element StarButton(string label, string position)
    {
        var icon = new Text("★").FontSize(12).Color(Colors.Orange);
        var text = new Text(label).FontSize(14);
        return position switch
        {
            "right" => new HorizontalLayout { text, icon }.Spacing(8).Alignment(LayoutAlignment.Start),
            "top" => new VerticalLayout { icon, text }.Spacing(4),
            "bottom" => new VerticalLayout { text, icon }.Spacing(4),
            _ => new HorizontalLayout { icon, text }.Spacing(8).Alignment(LayoutAlignment.Start),
        };
    }
}

internal sealed class PickersPage : Component
{
    private static readonly string[] ColorOptions = ["Red", "Green", "Blue", "Orange", "Purple"];
    private static readonly string[] Fruits =
        ["Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape", "Honeydew", "Kiwi", "Lemon", "Mango"];

    private readonly Signal<DateTime> _selectedDate = DateTime.Today;
    private readonly Signal<TimeSpan> _selectedTime = new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0);
    private readonly Signal<int> _pickerIndex = -1;
    private readonly Signal<string> _searchText = "";

    // Slint's pickers are popups, which the buttons open with show() on their ids, and which close themselves.
    // The gallery shows this page under an If, which a popup component can't be in, so the page puts them at its root.
    protected override IEnumerable<Element> Popups() =>
    [
        new DatePickerPopup().Id("date-picker").Title("Pick a date")
            .Date(() => new Slint.Struct { ["year"] = _selectedDate.Value.Year, ["month"] = _selectedDate.Value.Month, ["day"] = _selectedDate.Value.Day })
            .OnAccepted(date => _selectedDate.Value = new DateTime(Field(date, "year"), Field(date, "month"), Field(date, "day")))
            .OnAccepted(new Expr("date-picker.close()"))
            .OnCanceled(new Expr("date-picker.close()")),
        new TimePickerPopup().Id("time-picker").Title("Pick a time")
            .Time(() => new Slint.Struct { ["hour"] = _selectedTime.Value.Hours, ["minute"] = _selectedTime.Value.Minutes, ["second"] = 0 })
            .OnAccepted(time => _selectedTime.Value = new TimeSpan(Field(time, "hour"), Field(time, "minute"), 0))
            .OnAccepted(new Expr("time-picker.close()"))
            .OnCanceled(new Expr("time-picker.close()")),
    ];

    protected override Element Render() => Scaffold("Pickers",
        Section("DatePicker",
            new Button("Pick a date").OnClicked(new Expr("date-picker.show()")),
            new Text(() => $"Selected date: {_selectedDate.Value:D}").FontSize(14)),
        Section("TimePicker",
            new Button("Pick a time").OnClicked(new Expr("time-picker.show()")),
            new Text(() => $"Selected time: {DateTime.Today.Add(_selectedTime.Value):t}").FontSize(14)),
        Section("Picker (Dropdown)",
            new ComboBox().Model(ColorOptions).CurrentIndex(() => _pickerIndex.Value, index => _pickerIndex.Value = index),
            new Text(() => _pickerIndex.Value >= 0 && _pickerIndex.Value < ColorOptions.Length
                ? $"Selected: {ColorOptions[_pickerIndex.Value]}"
                : "Selected: (none)").FontSize(14)),
        Section("SearchBar",
            new LineEdit().PlaceholderText("Search fruits...").Text(() => _searchText.Value, v => _searchText.Value = v),
            new Text(() =>
            {
                if (string.IsNullOrWhiteSpace(_searchText.Value))
                {
                    return "Search results will appear here...";
                }
                var matches = Fruits.Where(f => f.Contains(_searchText.Value, StringComparison.OrdinalIgnoreCase)).ToArray();
                return matches.Length > 0 ? $"Found: {string.Join(", ", matches)}" : "No matches found.";
            }).FontSize(14).Color(Colors.Grey)));

    // Slint passes the int fields of a struct as numbers.
    private static int Field(Slint.Struct value, string name) => Convert.ToInt32(value[name], System.Globalization.CultureInfo.InvariantCulture);
}
