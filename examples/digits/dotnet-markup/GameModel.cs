// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// From the CometDigitsGame sample of https://github.com/dotnet/maui-labs, unchanged but for the namespace.

namespace Digits;

internal sealed record GameModel(int TargetValue, GameNumber[] Values)
{
    public static GameModel FirstGame { get; } = new GameModel(58,
    [
        new GameNumber(1, new GameNumberPosition(0, 0), 2),
        new GameNumber(2, new GameNumberPosition(0, 1), 7),
        new GameNumber(3, new GameNumberPosition(0, 2), 9),
        new GameNumber(4, new GameNumberPosition(1, 0), 10),
        new GameNumber(5, new GameNumberPosition(1, 1), 11),
        new GameNumber(6, new GameNumberPosition(1, 2), 25),
    ]);
}

internal sealed record GameNumberPosition(int Row, int Column);

internal sealed record GameNumber(int Id, GameNumberPosition Position, int Value);

internal enum Operation
{
    Add,
    Subtract,
    Multiply,
    Divide,
}

internal sealed record OperationItem(GameNumber Left, GameNumber Right, Operation Operation)
{
    internal int CalcValue() => Operation switch
    {
        Operation.Add => Left.Value + Right.Value,
        Operation.Subtract => Left.Value - Right.Value,
        Operation.Multiply => Left.Value * Right.Value,
        Operation.Divide => Left.Value / Right.Value,
        _ => throw new NotImplementedException(),
    };

    public bool IsValid() => Operation switch
    {
        Operation.Subtract => Left.Value >= Right.Value,
        Operation.Divide => Left.Value % Right.Value == 0,
        _ => true,
    };
}

internal enum PageView
{
    GameBoard,
    OperationList,
}

internal sealed class GameState
{
    public GameModel CurrentGame { get; set; } = GameModel.FirstGame;
    public Stack<GameNumber[]> BoardStates { get; set; } = new();
    public Stack<OperationItem> Operations { get; set; } = new();
    public Operation? CurrentOperation { get; set; }
    public GameNumber? CurrentNumber { get; set; }
    public OperationItem? OperationInError { get; set; }
    public PageView CurrentPageView { get; set; } = PageView.GameBoard;

    public GameNumber[] CurrentBoard => BoardStates.Count > 0 ? BoardStates.Peek() : CurrentGame.Values;

    public bool IsWon => CurrentBoard.Any(n => n.Value == CurrentGame.TargetValue);
}
