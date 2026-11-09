// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// From the CometTaskApp sample of https://github.com/dotnet/maui-labs.
// TaskItem is a record that the state replaces, since a SignalList sees replaced items, not changed ones.

using Slint.Markup;

namespace Tasks;

internal enum TaskPriority
{
    Low,
    Medium,
    High,
    Critical,
}

internal enum TaskCategory
{
    Personal,
    Work,
    Shopping,
    Health,
    Learning,
    Other,
}

internal sealed record TaskItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsCompleted { get; init; }
    public TaskPriority Priority { get; init; }
    public TaskCategory Category { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

/// <summary>The state that the pages share; a page that reads a signal renders again when it changes.</summary>
internal sealed class AppState
{
    public static AppState Instance { get; } = new();

    public readonly SignalList<TaskItem> Tasks = new(CreateSampleTasks());
    public readonly Signal<TaskCategory?> FilterCategory = new(null);
    public readonly Signal<bool> ShowCompletedTasks = new(true);
    public readonly Signal<string> SearchText = new("");
    public readonly Signal<bool> DarkMode = new(false);

    public void AddTask(TaskItem task) => Tasks.Add(task);

    public void RemoveTask(string id) => Tasks.RemoveAll(t => t.Id == id);

    public TaskItem? GetTask(string id) => Tasks.FirstOrDefault(t => t.Id == id);

    public void ToggleComplete(string id) => UpdateTask(id, task => task with { IsCompleted = !task.IsCompleted });

    public void UpdateTask(string id, Func<TaskItem, TaskItem> update)
    {
        var index = Tasks.IndexOf(Tasks.FirstOrDefault(t => t.Id == id)!);
        if (index >= 0)
        {
            Tasks[index] = update(Tasks[index]);
        }
    }

    public void ClearCompleted() => Tasks.RemoveAll(task => task.IsCompleted);

    public void ResetToSampleData() => Tasks.Reset(CreateSampleTasks());

    public List<TaskItem> GetFilteredTasks()
    {
        var search = SearchText.Value;
        return Tasks
            .Where(t => ShowCompletedTasks.Value || !t.IsCompleted)
            .Where(t => FilterCategory.Value == null || t.Category == FilterCategory.Value)
            .Where(t => string.IsNullOrEmpty(search) ||
                t.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.IsCompleted)
            .ToList();
    }

    public int CompletedCount => Tasks.Count(t => t.IsCompleted);
    public int TotalCount => Tasks.Count;
    public int PendingCount => TotalCount - CompletedCount;

    private static List<TaskItem> CreateSampleTasks() =>
    [
        new() { Title = "Buy groceries", Description = "Milk, eggs, bread, cheese", Priority = TaskPriority.Medium, Category = TaskCategory.Shopping },
        new() { Title = "Finish project report", Description = "Q4 summary with charts", Priority = TaskPriority.High, Category = TaskCategory.Work },
        new() { Title = "Go for a run", Description = "30 minutes in the park", Priority = TaskPriority.Low, Category = TaskCategory.Health },
        new() { Title = "Read Comet docs", Description = "Learn MVU patterns for MAUI", Priority = TaskPriority.High, Category = TaskCategory.Learning },
        new() { Title = "Call dentist", Description = "Schedule annual checkup", Priority = TaskPriority.Medium, Category = TaskCategory.Personal },
        new() { Title = "Fix kitchen faucet", Description = "Leaking when turned off", Priority = TaskPriority.Critical, Category = TaskCategory.Personal },
    ];
}
