// Copyright (c) .NET Foundation and Contributors
// SPDX-License-Identifier: MIT

// The GalleryPageHelpers of the CometControlsGallery sample of https://github.com/dotnet/maui-labs, ported to Slint.Markup.

using Slint.Markup;
using Color = Slint.Color;

namespace ControlsGallery;

internal static class Colors
{
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Grey = new(0x80, 0x80, 0x80);
    public static readonly Color DodgerBlue = new(0x1E, 0x90, 0xFF);
    public static readonly Color Crimson = new(0xDC, 0x14, 0x3C);
    public static readonly Color CornflowerBlue = new(0x64, 0x95, 0xED);
    public static readonly Color MediumSeaGreen = new(0x3C, 0xB3, 0x71);
    public static readonly Color MediumPurple = new(0x93, 0x70, 0xDB);
    public static readonly Color Orange = new(0xFF, 0xA5, 0x00);
    public static readonly Color SidebarBackground = new(0xF2, 0xF2, 0xF7);
    public static readonly Color SelectedAccent = new(0x00, 0x7A, 0xFF);
}

internal static class GalleryPageHelpers
{
    public static Element Scaffold(string title, params Element[] sections)
    {
        var content = new VerticalBox { new Text(title).FontSize(28).FontWeight(700) };
        foreach (var section in sections)
        {
            content.Add(section);
        }
        return new ScrollView { content.Alignment(LayoutAlignment.Start) };
    }

    public static Element Section(string title, params Element[] content) => Section(new Text(title), content);

    public static Element Section(Func<string> title, params Element[] content) => Section(new Text(title), content);

    private static Element Section(Text header, Element[] content)
    {
        var section = new VerticalLayout { header.FontSize(16).FontWeight(700).Color(Colors.CornflowerBlue) };
        foreach (var element in content)
        {
            section.Add(element);
        }
        section.Add(new Rectangle().Height(1).Background(Colors.Grey).Opacity(0.3));
        return section.Spacing(10);
    }

    public static Element ButtonRow(params Element[] buttons)
    {
        var row = new HorizontalLayout();
        foreach (var button in buttons)
        {
            row.Add(button);
        }
        return row.Spacing(12).Alignment(LayoutAlignment.Start);
    }

    public static Text BodyText(string value) => new Text(value).FontSize(14);

    public static Text Caption(string value) => new Text(value).FontSize(12).Color(Colors.Grey).Wrap(TextWrap.WordWrap);
}
