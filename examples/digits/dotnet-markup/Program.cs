// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// The CometDigitsGame sample of https://github.com/dotnet/maui-labs, ported to Slint.Markup.
// Comet renders the whole tree from the state; here the tree stays the same,
// and lambdas, If, and fixed tiles show the state, so that the page compiles once.

using Digits;
using Slint.Markup;
using Color = Slint.Color;

using var game = new DigitsGame();
game.Run();

internal sealed class DigitsGame : Component<GameState>
{
    private static readonly Color Green = new(0x5F, 0x9C, 0x49);
    private static readonly Color Error = new(0xD3, 0x2F, 0x2F);
    private static readonly Color White = new(255, 255, 255);
    private static readonly Color Black = new(0, 0, 0);
    private static readonly Color Grey = new(0xD3, 0xD3, 0xD3);

    protected override Element Render() => new WindowElement
    {
        new VerticalLayout
        {
            new HorizontalLayout
            {
                Tab("Game Board", PageView.GameBoard),
                Tab("Operations", PageView.OperationList),
            }.Spacing(10).Padding(10),
            new If(() => State.CurrentPageView == PageView.GameBoard) { GameBoard() },
            new If(() => State.CurrentPageView == PageView.OperationList) { OperationList() },
        },
    }.Title("Digits").Background(White).Width(420).Height(620);

    private Element Tab(string label, PageView view) => new Rectangle
    {
        new Text(label).FontSize(16).Color(() => State.CurrentPageView == view ? White : Black)
            .HorizontalAlignment(TextHorizontalAlignment.Center).VerticalAlignment(TextVerticalAlignment.Center)
            .Width(new Expr("parent.width")).Height(new Expr("parent.height")),
        new TouchArea().OnClicked(() => SetState(s => s.CurrentPageView = view)),
    }.Height(44).Background(() => State.CurrentPageView == view ? Green : Grey);

    private Element GameBoard() => new VerticalLayout
    {
        new Text(() => $"{State.CurrentGame.TargetValue}").FontSize(46).FontWeight(700).Color(Black)
            .HorizontalAlignment(TextHorizontalAlignment.Center),
        new HorizontalLayout
        {
            new GridLayout
            {
                new GridRow { Tile(0, 0), Tile(0, 1), Tile(0, 2) },
                new GridRow { Tile(1, 0), Tile(1, 1), Tile(1, 2) },
            }.Spacing(12),
        }.Alignment(LayoutAlignment.Center),
        new HorizontalLayout
        {
            // Comet shows an image; the fonts that Slint embeds have no undo arrow.
            Key("Undo", null, OnUndoLastOperation),
            Key("+", Operation.Add, () => OnOperationSelected(Operation.Add)),
            Key("−", Operation.Subtract, () => OnOperationSelected(Operation.Subtract)),
            Key("×", Operation.Multiply, () => OnOperationSelected(Operation.Multiply)),
            Key("÷", Operation.Divide, () => OnOperationSelected(Operation.Divide)),
        }.Spacing(12).Alignment(LayoutAlignment.Center),
        new If(() => State.OperationInError != null)
        {
            new Text("Invalid operation!").Color(Error).FontSize(16).HorizontalAlignment(TextHorizontalAlignment.Center),
        },
        new If(() => State.IsWon)
        {
            new Text("You got it!").Color(Green).FontSize(28).FontWeight(700).HorizontalAlignment(TextHorizontalAlignment.Center),
        },
    }.Spacing(16).Padding(16).Alignment(LayoutAlignment.Start);

    /// <summary>The number at a place of the board, which stays empty once an operation used it.</summary>
    private Element Tile(int row, int column)
    {
        GameNumber? Number() => State.CurrentBoard.FirstOrDefault(n => n.Position.Row == row && n.Position.Column == column && n.Value != 0);
        bool Selected() => Number() is { } n && State.CurrentNumber == n;
        bool InError() => Number() is { } n && (State.OperationInError?.Left == n || State.OperationInError?.Right == n);
        return new Rectangle
        {
            new Text(() => Number()?.Value.ToString() ?? "").FontSize(24).FontWeight(700)
                .Color(() => Selected() || InError() ? White : Black)
                .HorizontalAlignment(TextHorizontalAlignment.Center).VerticalAlignment(TextVerticalAlignment.Center)
                .Width(86).Height(86),
            new TouchArea().OnClicked(() =>
            {
                if (Number() is { } n)
                {
                    OnNumberClicked(n);
                }
            }),
        }
        .Width(86).Height(86).BorderRadius(43)
        .Visible(() => Number() != null)
        .Background(() => InError() ? Error : Selected() ? Green : White)
        .BorderColor(() => InError() ? Error : Selected() ? Green : Black)
        .BorderWidth(() => Selected() ? 0 : 3);
    }

    private Element Key(string label, Operation? operation, Action clicked) => new Rectangle
    {
        new Text(label).FontSize(operation == null ? 16 : 24).FontWeight(700).Color(operation == null ? Green : White)
            .HorizontalAlignment(TextHorizontalAlignment.Center).VerticalAlignment(TextVerticalAlignment.Center)
            .Width(60).Height(60),
        new TouchArea().OnClicked(clicked),
    }
    .Width(60).Height(60).BorderRadius(30)
    .Background(() => operation == null ? White
        : State.OperationInError?.Operation == operation ? Error
        : State.CurrentOperation == operation ? Green : Black);

    private Element OperationList() => new VerticalLayout
    {
        new If(() => State.Operations.Count == 0)
        {
            new Text("No operations yet.").Color(new Color(0x80, 0x80, 0x80)).FontSize(16).HorizontalAlignment(TextHorizontalAlignment.Center),
        },
        new If(() => State.Operations.Count > 0)
        {
            new Text("Your operations:").FontSize(24).Color(Black).HorizontalAlignment(TextHorizontalAlignment.Center),
        },
        new ForEach<string>(() => State.Operations.Reverse().Select(Describe), row =>
            new Text(row.Item).FontSize(24).Color(Black).HorizontalAlignment(TextHorizontalAlignment.Center)),
    }.Spacing(4).Padding(40).Alignment(LayoutAlignment.Start);

    private static string Describe(OperationItem op)
    {
        var sign = op.Operation switch
        {
            Operation.Add => "+",
            Operation.Subtract => "−",
            Operation.Multiply => "×",
            Operation.Divide => "÷",
            _ => "?",
        };
        return $"{op.Left.Value} {sign} {op.Right.Value} = {op.CalcValue()}";
    }

    private void OnNumberClicked(GameNumber number)
    {
        if (State.CurrentOperation == null)
        {
            SetState(s => s.CurrentNumber = s.CurrentNumber == number ? null : number);
        }
        else if (State.CurrentNumber != null)
        {
            var newOperation = new OperationItem(State.CurrentNumber, number, State.CurrentOperation.Value);
            if (!newOperation.IsValid())
            {
                SetState(s =>
                {
                    s.CurrentNumber = null;
                    s.CurrentOperation = null;
                    s.OperationInError = newOperation;
                });
                ClearErrorAfterDelay();
            }
            else
            {
                SetState(s =>
                {
                    s.CurrentNumber = null;
                    s.CurrentOperation = null;
                    s.OperationInError = null;
                });
                ApplyOperation(newOperation);
            }
        }
    }

    private void OnOperationSelected(Operation operation)
    {
        if (State.CurrentNumber != null)
        {
            SetState(s => s.CurrentOperation = operation);
        }
    }

    private void OnUndoLastOperation()
    {
        if (State.BoardStates.Count == 0)
        {
            return;
        }
        SetState(s =>
        {
            s.BoardStates.Pop();
            s.Operations.Pop();
            s.CurrentNumber = null;
            s.CurrentOperation = null;
            s.OperationInError = null;
        });
    }

    private void ApplyOperation(OperationItem newOperation) => SetState(s =>
    {
        var newBoard = s.CurrentBoard
            .Select(n =>
                n.Id == newOperation.Left.Id || n.Value == 0 ? new GameNumber(newOperation.Left.Id, newOperation.Right.Position, 0)
                : n.Id == newOperation.Right.Id ? new GameNumber(newOperation.Right.Id, newOperation.Right.Position, newOperation.CalcValue())
                : n)
            .ToArray();
        s.BoardStates.Push(newBoard);
        s.Operations.Push(newOperation);
    });

    private async void ClearErrorAfterDelay()
    {
        await Task.Delay(1000);
        SetState(s => s.OperationInError = null);
    }
}
