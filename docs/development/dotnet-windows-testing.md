<!-- cSpell: ignore longpaths yurkinh -->
# Testing the .NET Bindings on Windows

A checklist for an agent, or a person, who tests the .NET bindings on a Windows machine,
after the work on macOS, Android, and iOS that [the plan](dotnet-bindings.md) describes.
Work on the branch `dotnet_dev` of the fork `yurkinh/slint`, and record the results in the plan.

The commands are for PowerShell.
Set an environment variable for one command with `$env:NAME = "value"`, and remove it with `Remove-Item Env:NAME`.

## Prerequisites

- Visual Studio 2022 Build Tools with the workload "Desktop development with C++", for Rust's MSVC target and for Native AOT.
- Rust, with `rustup` and the `x86_64-pc-windows-msvc` toolchain, or `aarch64-pc-windows-msvc` on an ARM machine.
- The .NET 10 SDK; `dotnet --version` shows 10.0.
- Git with long paths: `git config --global core.longpaths true`.
- About 40 GB of free disk space for `target\`.

```powershell
git clone https://github.com/yurkinh/slint.git
cd slint
git switch dotnet_dev
```

## Build

```powershell
cargo build -p slint-compiler
cargo build -p slint-dotnet --features testing
```

The `testing` feature matters for the tests: without it, `SLINT_BACKEND=testing` falls back to winit, which aborts the test host.
For measurements, build `cargo build --release -p slint-dotnet`,
and point an app at it with `$env:SLINT_DOTNET_NATIVE_LIBRARY = "$PWD\target\release\slint_dotnet.dll"`.

## Checks

Run them in this order; each one builds on the one before.

1. The tests of the library, 137 or more, with one skipped:

   ```powershell
   dotnet run --project api/dotnet/Slint.Tests
   ```

2. The `csharp` blocks of `tests/cases`, about a thousand cases; a filter runs a part, such as `$env:SLINT_TEST_FILTER = "models"`:

   ```powershell
   cargo test --manifest-path tests/Cargo.toml -p test-driver-dotnet
   ```

3. The examples and demos build, and keep running for 5 seconds:

   ```powershell
   dotnet build examples/dotnet.slnx
   dotnet format examples/dotnet.slnx --verify-no-changes
   dotnet run examples/dotnet-smoke-test.cs -- examples/dotnet.slnx
   ```

4. The UI written in C#: run each `examples\*\dotnet-markup` project with `dotnet run`, and take a screenshot of its window.
   `todo`, `contacts`, `digits`, `tasks`, and `controls-gallery` should look as on macOS;
   in the gallery, the Pickers page's buttons open the date and time pickers.

5. Native AOT of an app with `Slint.Markup`, where `PublishAot` goes in the project file, not on the command line,
   which would also apply it to the Slint libraries:

   ```powershell
   cd examples\todo\dotnet-markup
   # Add <PublishAot>true</PublishAot> to TodoMarkup.csproj, publish, and undo the change after.
   dotnet publish -c Release -r win-x64 -o $env:TEMP\todo-aot
   & $env:TEMP\todo-aot\TodoMarkup.exe
   ```

   The publish shows no `IL` warning, and the window opens.

6. The Mapsui prototype of item 28, with tiles from OpenStreetMap, which needs the network:

   ```powershell
   dotnet run --project examples\mapsui\Mapsui.UI.Slint.Tests
   cd examples\mapsui\dotnet
   dotnet run
   $env:MAPSUI_BENCH = "1"; dotnet run; Remove-Item Env:MAPSUI_BENCH
   ```

   The tests of the control pass without a screen; the window shows Kyiv, and dragging with the mouse pans the map and the wheel zooms it.
   The scripted run prints the scale, the frame size, whether panning and the wheel work, and the draw and copy times;
   record them with the scale of the display, and again with `$env:SLINT_SCALE_FACTOR = "2"`.

7. The packages of nuget.org, once a preview is published, in an empty directory:

   ```powershell
   dotnet new install yurkinh.slint.net.Templates::<version>
   dotnet new slint -o Desktop; dotnet run --project Desktop
   dotnet new slint -o Markup --markup; dotnet run --project Markup
   dotnet publish Markup -c Release -r win-x64
   ```

## What's Different on Windows

- A window needs a single-threaded apartment: an app's `Main` has `[STAThread]`,
  and `Create` and `Run` throw a clear exception without it, item 11 of the plan.
- Paths use `\`: the resources of units are named `slint-unit:` and the runtime path with `\`,
  and `Element.ApplicationFile` of `Slint.Markup` writes `/` for the imports of a page.
- `OutputType` `WinExe`, as in the templates, opens no console, and drops what the app writes to it;
  run with `Exe` while debugging a check.
- Windows Defender can slow the first run of a new executable by seconds; measure a second run.

## Screenshots

PowerShell can capture the screen without other tools:

```powershell
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
[System.Drawing.Graphics]::FromImage($bitmap).CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bitmap.Save("$env:TEMP\screen.png")
```

## Report

Add a dated line to each item of [the plan](dotnet-bindings.md) that a check covers, such as items 26, 27, and 28,
with what passed, the numbers, and what failed with its error.
Fix a failure on `dotnet_dev`, unless it fails the CI of the upstream pull request, which takes fixes on `dotnet_bindings`.
