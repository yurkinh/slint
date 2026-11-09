// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// The pages of the CometTaskApp sample of https://github.com/dotnet/maui-labs, ported to Slint.Markup.
// Each page keeps its tree the same whatever the state, with lambdas, If, and ForEach,
// so that it compiles once; Comet renders the tree again from the state instead.

using Slint.Markup;
using Color = Slint.Color;

namespace Tasks;

internal static class Theme
{
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Gray = new(0x80, 0x80, 0x80);
    public static readonly Color DarkGray = new(0xA9, 0xA9, 0xA9);
    public static readonly Color LightGray = new(0xD3, 0xD3, 0xD3);
    public static readonly Color WhiteSmoke = new(0xF5, 0xF5, 0xF5);
    public static readonly Color Green = new(0x00, 0x80, 0x00);
    public static readonly Color Orange = new(0xFF, 0xA5, 0x00);
    public static readonly Color OrangeRed = new(0xFF, 0x45, 0x00);
    public static readonly Color Red = new(0xFF, 0x00, 0x00);
    public static readonly Color Purple = new(0x80, 0x00, 0x80);
    public static readonly Color DodgerBlue = new(0x1E, 0x90, 0xFF);

    public static readonly string[] Priorities = ["Low", "Medium", "High", "Critical"];
    public static readonly string[] Categories = ["Personal", "Work", "Shopping", "Health", "Learning", "Other"];

    public static Color PriorityColor(TaskPriority p) => p switch
    {
        TaskPriority.Critical => Red,
        TaskPriority.High => OrangeRed,
        TaskPriority.Medium => Orange,
        TaskPriority.Low => Green,
        _ => Gray,
    };

    public static Text Heading(string text, double size = 24) => new Text(text).FontSize(size).FontWeight(700);

    public static Text Caption(string text) => new Text(text).FontSize(12).Color(Gray);

    /// <summary>A rounded card, which Comet makes with a background and a clip shape.</summary>
    public static Rectangle Card(Element content) => new Rectangle { content }.Background(WhiteSmoke).BorderRadius(8);
}

/// <summary>A task as a row of the list shows it, with the types that a row of a ForEach can have.</summary>
internal sealed record TaskRow(string Id, string Title, string Description, string Mark, Color Priority, Color TitleColor, int Weight);

internal sealed class TaskListPage : Component
{
    private readonly AppState _state = AppState.Instance;

    protected override Element Render() => new VerticalBox
    {
        Theme.Heading("My Tasks"),
        new LineEdit().PlaceholderText("Search tasks...").Text(() => _state.SearchText.Value, text => _state.SearchText.Value = text),
        new HorizontalLayout
        {
            FilterPill("All", null),
            FilterPill("Personal", TaskCategory.Personal),
            FilterPill("Work", TaskCategory.Work),
            FilterPill("Shopping", TaskCategory.Shopping),
            FilterPill("Health", TaskCategory.Health),
            FilterPill("Learning", TaskCategory.Learning),
        }.Spacing(8),
        new HorizontalLayout
        {
            new Text(() => $"{_state.TotalCount} total").FontSize(12).Color(Theme.DarkGray),
            new Text(() => $"{_state.CompletedCount} done").FontSize(12).Color(Theme.Green),
            new Text(() => $"{_state.PendingCount} pending").FontSize(12).Color(Theme.Orange),
        }.Spacing(16).Alignment(LayoutAlignment.Start),
        new ListView
        {
            new ForEach<TaskRow>(() => _state.GetFilteredTasks().Select(ToRow), row => new Rectangle
            {
                new TouchArea().OnClicked(row.Do(task => Navigation!.Push(new TaskDetailPage(task.Id)))),
                new HorizontalLayout
                {
                    new VerticalLayout
                    {
                        new Rectangle().Width(12).Height(12).BorderRadius(6).Background(row.Get(t => t.Priority)),
                    }.Alignment(LayoutAlignment.Center),
                    new VerticalLayout
                    {
                        new Text(row.Get(t => t.Title)).FontSize(16).FontWeight(row.Get(t => t.Weight)).Color(row.Get(t => t.TitleColor)),
                        new Text(row.Get(t => t.Description)).FontSize(13).Color(Theme.DarkGray),
                    }.HorizontalStretch(1),
                    new Rectangle
                    {
                        new Text(row.Get(t => t.Mark)).FontSize(20).VerticalAlignment(TextVerticalAlignment.Center),
                        new TouchArea().OnClicked(row.Do(task => _state.ToggleComplete(task.Id))),
                    }.Width(40),
                }.Spacing(10).Padding(8),
            }.Height(56)),
        }.VerticalStretch(1),
        new Button("+ Add Task", () => Navigation!.Push(new AddTaskPage())),
    };

    private static TaskRow ToRow(TaskItem task) => new(
        task.Id,
        $"{task.Category} {task.Title}",
        task.Description,
        task.IsCompleted ? "[x]" : "[ ]",
        Theme.PriorityColor(task.Priority),
        task.IsCompleted ? Theme.Gray : Theme.Black,
        task.IsCompleted ? 400 : 600);

    private Element FilterPill(string label, TaskCategory? category)
    {
        bool Selected() => _state.FilterCategory.Value == category;
        return new Clickable(label, () => _state.FilterCategory.Value = category).FontSize(13)
            .TextColor(() => Selected() ? Theme.White : Theme.DarkGray)
            .Background(() => Selected() ? Theme.DodgerBlue : Theme.LightGray)
            .Height(28).MinWidth(64).BorderRadius(12);
    }
}

internal sealed class TaskDetailState
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Priority { get; set; }
    public int Category { get; set; }
}

/// <summary>A task's details; Comet's props become the constructor's parameter.</summary>
internal sealed class TaskDetailPage : Component<TaskDetailState>
{
    private readonly AppState _state = AppState.Instance;
    private readonly string _taskId;

    public TaskDetailPage(string taskId)
    {
        _taskId = taskId;
        if (_state.GetTask(taskId) is { } task)
        {
            State.Title = task.Title;
            State.Description = task.Description;
            State.Priority = (int)task.Priority;
            State.Category = (int)task.Category;
        }
    }

    private TaskItem? Task => _state.GetTask(_taskId);

    protected override Element Render() => new VerticalLayout
    {
        new If(() => Task == null)
        {
            new VerticalBox
            {
                Theme.Heading("Task not found"),
                new Text("The selected task may have been removed from the list.").FontSize(14).Color(Theme.Gray),
                new Button("Back to tasks", () => Navigation!.Pop()),
            },
        },
        new If(() => Task != null)
        {
            new VerticalBox
            {
                Theme.Heading("Task Details"),
                new HorizontalLayout
                {
                    new Text(() => Task?.IsCompleted == true ? "Completed" : "Pending").FontSize(14)
                        .Color(() => Task?.IsCompleted == true ? Theme.Green : Theme.Orange).HorizontalStretch(1),
                    new Text(() => $"Created: {Task?.CreatedAt:MMM dd, yyyy}").FontSize(12).Color(Theme.Gray),
                },
                Theme.Caption("Title"),
                new LineEdit().FontSize(18).PlaceholderText("Task title...")
                    .Text(() => State.Title, value => SetState(s => s.Title = value)),
                Theme.Caption("Description"),
                new LineEdit().PlaceholderText("Task description...")
                    .Text(() => State.Description, value => SetState(s => s.Description = value)),
                Theme.Caption("Priority"),
                new ComboBox().Model(Theme.Priorities).CurrentIndex(() => State.Priority, index => SetState(s => s.Priority = index)),
                Theme.Caption("Category"),
                new ComboBox().Model(Theme.Categories).CurrentIndex(() => State.Category, index => SetState(s => s.Category = index)),
                new Button("Save Changes", SaveChanges),
                new HorizontalLayout
                {
                    new Button(() => Task?.IsCompleted == true ? "Mark Pending" : "Mark Complete", ToggleComplete),
                    new Button("Delete", DeleteTask),
                }.Spacing(12),
            }.Alignment(LayoutAlignment.Start),
        },
    };

    private void SaveChanges()
    {
        _state.UpdateTask(_taskId, task => task with
        {
            Title = State.Title,
            Description = State.Description,
            Priority = (TaskPriority)State.Priority,
            Category = (TaskCategory)State.Category,
        });
        Navigation!.Pop();
    }

    private void ToggleComplete()
    {
        _state.ToggleComplete(_taskId);
        Navigation!.Pop();
    }

    private void DeleteTask()
    {
        _state.RemoveTask(_taskId);
        Navigation!.Pop();
    }
}

internal sealed class AddTaskState
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Priority { get; set; } = 1;
    public int Category { get; set; }
    public string ErrorMessage { get; set; } = "";
}

internal sealed class AddTaskPage : Component<AddTaskState>
{
    protected override Element Render() => new VerticalBox
    {
        Theme.Heading("Create New Task"),
        new If(() => State.ErrorMessage.Length > 0)
        {
            new Text(() => State.ErrorMessage).FontSize(14).Color(Theme.Red),
        },
        Theme.Caption("Title *"),
        new LineEdit().PlaceholderText("What needs to be done?").Text(() => State.Title, value => SetState(s =>
        {
            s.Title = value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                s.ErrorMessage = "";
            }
        })),
        Theme.Caption("Description"),
        new LineEdit().PlaceholderText("Add more details...").Text(() => State.Description, value => SetState(s => s.Description = value)),
        Theme.Caption("Priority"),
        new ComboBox().Model(Theme.Priorities).CurrentIndex(() => State.Priority, index => SetState(s => s.Priority = index)),
        Theme.Caption("Category"),
        new ComboBox().Model(Theme.Categories).CurrentIndex(() => State.Category, index => SetState(s => s.Category = index)),
        new Button("Create Task", CreateTask).Primary(true),
        new Button("Cancel", () => Navigation!.Pop()),
    }.Alignment(LayoutAlignment.Start);

    private void CreateTask()
    {
        if (string.IsNullOrWhiteSpace(State.Title))
        {
            SetState(s => s.ErrorMessage = "Please enter a task title.");
            return;
        }
        AppState.Instance.AddTask(new TaskItem
        {
            Title = State.Title.Trim(),
            Description = State.Description,
            Priority = (TaskPriority)State.Priority,
            Category = (TaskCategory)State.Category,
        });
        Navigation!.Pop();
    }
}

/// <summary>A bar of a breakdown, as a row of a ForEach.</summary>
internal sealed record Bar(string Label, int Count, double Width);

internal sealed class StatsPage : Component
{
    private readonly AppState _state = AppState.Instance;

    private int Percentage => _state.TotalCount > 0 ? (int)(_state.CompletedCount * 100.0 / _state.TotalCount) : 0;

    protected override Element Render() => new ScrollView
    {
        new VerticalBox
        {
        Theme.Heading("Task Statistics"),
        new HorizontalLayout
        {
            new Rectangle
            {
                new Rectangle().X(10).Y(10).Width(100).Height(100).BorderRadius(50).Background(Theme.White),
                new Text(() => $"{Percentage}%").FontSize(28).FontWeight(700)
                    .Color(() => Percentage >= 75 ? Theme.Green : Percentage >= 50 ? Theme.Orange : Theme.Red)
                    .HorizontalAlignment(TextHorizontalAlignment.Center).VerticalAlignment(TextVerticalAlignment.Center)
                    .Width(120).Height(120),
            }.Width(120).Height(120).BorderRadius(60).Background(Theme.LightGray),
        }.Alignment(LayoutAlignment.Center),
        StatCard("Total Tasks", () => _state.TotalCount, "#", Theme.DodgerBlue),
        StatCard("Completed", () => _state.CompletedCount, "*", Theme.Green),
        StatCard("Pending", () => _state.PendingCount, "~", Theme.Orange),
        Theme.Heading("By Category", 18),
        Bars(() => Enum.GetValues<TaskCategory>().Select(c => (c.ToString(), _state.Tasks.Count(t => t.Category == c)))),
        Theme.Heading("By Priority", 18),
        Bars(() => Enum.GetValues<TaskPriority>().Select(p => (p.ToString(), _state.Tasks.Count(t => t.Priority == p)))),
        }.Alignment(LayoutAlignment.Start),
    };

    private static Element StatCard(string label, Func<int> value, string symbol, Color color) => Theme.Card(new HorizontalLayout
    {
        new Text(symbol).FontSize(28),
        new VerticalLayout
        {
            new Text(() => value().ToString()).FontSize(22).FontWeight(700).Color(color),
            new Text(label).FontSize(13).Color(Theme.Gray),
        },
    }.Spacing(12).Padding(12).Alignment(LayoutAlignment.Start));

    private Element Bars(Func<IEnumerable<(string Label, int Count)>> counts) => new VerticalLayout
    {
        new ForEach<Bar>(() => counts().Select(c => new Bar(c.Label, c.Count,
            Math.Max(4, _state.TotalCount > 0 ? c.Count * 200.0 / _state.TotalCount : 0))), row => new HorizontalLayout
        {
            new Text(row.Get(b => b.Label)).FontSize(13).Width(100),
            new Rectangle().Width(new Expr($"{row.Get(b => b.Width).Code} * 1px")).Height(16).BorderRadius(4).Background(Theme.DodgerBlue),
            new Text(new Expr($"{row.Get(b => b.Count).Code}")).FontSize(13).Color(Theme.Gray),
        }.Spacing(8).Alignment(LayoutAlignment.Start)),
    }.Spacing(6);
}

internal sealed class SettingsPage : Component
{
    private readonly AppState _state = AppState.Instance;

    protected override Element Render() => new VerticalBox
    {
        Theme.Heading("Settings"),
        SettingRow("Show Completed Tasks", "Include completed tasks in the list",
            () => _state.ShowCompletedTasks.Value ? "Showing" : "Hidden",
            () => _state.ShowCompletedTasks.Value ? Theme.Green : Theme.Red,
            () => _state.ShowCompletedTasks.Value = !_state.ShowCompletedTasks.Value),
        SettingRow("Dark Mode", "Switch between light and dark theme",
            () => _state.DarkMode.Value ? "Dark" : "Light",
            () => _state.DarkMode.Value ? Theme.Purple : Theme.Orange,
            () => _state.DarkMode.Value = !_state.DarkMode.Value),
        Theme.Heading("Danger Zone", 18).Color(Theme.Red),
        new Button("Clear All Completed", _state.ClearCompleted),
        new Button("Reset to Sample Data", _state.ResetToSampleData),
        Theme.Heading("About", 18),
        Theme.Card(new VerticalLayout
        {
            new Text("Comet Task Manager, ported to Slint").FontSize(14).FontWeight(600),
            new Text("Built with Slint.Markup for .NET").FontSize(12).Color(Theme.Gray),
            new Text("Demonstrates: Navigation, Lists, Forms, State Management").FontSize(11).Color(Theme.DarkGray),
        }.Spacing(4).Padding(12)),
    }.Alignment(LayoutAlignment.Start);

    private static Element SettingRow(string title, string subtitle, Func<string> indicator, Func<Color> color, Action toggle) => new Rectangle
    {
        new HorizontalLayout
        {
            new VerticalLayout
            {
                new Text(title).FontSize(16).FontWeight(600),
                new Text(subtitle).FontSize(12).Color(Theme.Gray),
            }.HorizontalStretch(1),
            new Text(indicator).Color(color).VerticalAlignment(TextVerticalAlignment.Center),
        }.Spacing(12).Padding(12),
        new TouchArea().OnClicked(toggle),
    }.Background(Theme.WhiteSmoke).BorderRadius(8);
}
