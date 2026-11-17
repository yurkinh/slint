<!-- cSpell: ignore yurkinh -->
# Splitting the .NET Bindings for Upstream

A plan for replacing [#14498](https://github.com/slint-ui/slint/pull/14498) with small pull requests,
and for moving everything else into the fork `yurkinh/slint`, as of 2026-11-17.
The roadmap of the bindings stays in [the plan](dotnet-bindings.md).

## What the Maintainers Said

- NigelBreslaw, 2026-11-12: they can't add more language bindings now, since everything merged is maintained indefinitely.
  They recommend maintaining the bindings out of tree,
  and would consider "a smaller patch that would help this as an enabler for other languages".
- yurkinh, 2026-11-13: offered to upstream only the bindings for the Rust backends,
  and to keep hot reload, DrawnUI, Microcharts, MVVM, and `Slint.Markup` in separate repositories and packages.
- ogoffart, 2026-11-13: "Sounds good", with small, reviewable pull requests that state their motivation.
- The automated review suggested a split into the native crate and library, the generator, the test driver, CI, and the samples,
  and named the 112 `csharp` blocks in `tests/cases` as a cost for everyone who edits those cases.

So even the smallest .NET binding is a new language, which the project declined for now.
The plan starts with patches that help any language and can merge on their own,
asks before opening the binding itself, and treats the fork as the product either way.

## Upstream Pull Requests

Each one starts from `master`, has the motivation in its description, and follows the commit template.

### 1. Enablers, Independent of .NET

These merge without anyone accepting a new language:

| Pull request | Change | Size | Where it is now |
|---|---|---|---|
| 1a | Fix `ScaleRotateGestureHandler`'s scale for two-finger pinches, which zoomed in too far and out too little | 20 lines, with a unit test | `dotnet_dev`, 71f66935f7 |
| 1b | Open the shared `NSApplication` before creating a status item on macOS, so the system tray works with the testing backend | 4 lines | `dotnet_bindings` |
| 1c | Serialize a compiled unit with its resources and fonts, `llr::bundle` and `ComponentDefinition::from_compilation_unit_with_fonts`, so an interpreted binding can skip compiling at startup | about 90 lines, needs tests of its own | `dotnet_bindings` |

1a is a bug in a released element, with a `ChangeLog:` trailer.
1c needs the strongest motivation: Python and Node.js could load precompiled units too.
Ask in chat.slint.dev before writing tests for it.

### 2. Ask First

Post in #14498, or in chat.slint.dev, before preparing anything below:
the shape of the split, the size of each part, and that the fork keeps everything else.
If the answer is no, stop after the enablers; the rest of this plan still applies to the fork.

### 3. The Binding, If They Agree

Only Windows, macOS, Linux, Android, and iOS, with the interpreter at run time, the code generator, and the MSBuild integration.

| Pull request | Contents | Estimate |
|---|---|---|
| 3a | `api/dotnet/native` without `browser.rs`, and `api/dotnet/Slint` without the browser, live preview, and MVVM parts: compiling, instances, values, models and adapters, timers, the event loop, windows, translations, testing. `Slint.Tests`, and a desktop CI job on the three systems. | 9,000 lines, 4,000 of them tests |
| 3b | `-f csharp` in `internal/compiler/generator/csharp.rs`, precompiled units, `build/Slint.targets` without live preview and the browser, and `tests/driver/dotnet`, which checks the `test` property of every case. No `csharp` blocks in `tests/cases`. | 2,000 lines |
| 3c | Android and iOS: `SlintActivity`, `android.rs`, the iOS library resolver, `scripts/build_dotnet_ios.sh`, the smoke tests, and their CI jobs | 1,000 lines |
| 3d | Docs: a trimmed `api/dotnet/README.md`, a developer guide with the design and the native ABI, and a page on the documentation website, if they want one | 600 lines |

3a is the hard one to review, and can't shrink much: the native crate needs the C# side and its tests to be reviewable.
3b depends on 1c only for precompiled units; without 1c, the generated code compiles at startup, as in preview 4.
No NuGet publishing, templates, samples, or `WHATS_NEW.md`: the Slint project would decide on packages later.

## What Stays in the Fork

Everything else, which the fork builds, tests, and publishes as `yurkinh.slint.net` and the other packages:

- The browser: `internal/backends/emscripten`, `browser.rs`, `RunAsync`, `BrowserTest`, `scripts/build_dotnet_browser.sh`, and `run_in_chrome.mjs`.
- Hot reload: live preview in the generator, `Slint.targets`, and `ComponentInstance`.
- MVVM: `ViewModelBinding`, `ObservableCollectionModel`, and `Slint.SourceGenerator`.
- `Slint.Markup`, Mapsui, DrawnUI, and Microcharts.
- The templates, the 27 sample ports, `examples/dotnet-viewer`, and `examples/dotnet.slnx`.
- `publish_nuget_package.yaml`, `Slint.Compiler`, and `WHATS_NEW.md`.
- The `csharp` blocks of `tests/cases`.

## Branches

- `dotnet/1a-pinch-scale`, `dotnet/1b-tray-testing`, `dotnet/1c-compiled-units`: the enablers, each from `master`.
- `dotnet/core`: `master` plus the binding of step 3, for the five platforms, cut from `dotnet_bindings`.
  It's the source of 3a to 3d, and keeps the core small in the fork whatever upstream decides.
- `dotnet/main`: the fork's product and default branch, which starts as a copy of `dotnet/core`.
  Git can't have a branch `dotnet` next to branches named `dotnet/…`, so it's `dotnet/main`.
  A pull request in the fork from `dotnet-fork`, which is `dotnet_dev` with `dotnet/core` merged as `ours`, into `dotnet/main`
  then shows exactly what the fork adds over the core:
  the browser, hot reload, MVVM, `Slint.Markup`, Mapsui, the samples, the templates, and publishing.
  Afterwards `dotnet/main` merges `dotnet/core`, which merges `master`.
- `dotnet_bindings` and `dotnet_dev` retire once their content is in `dotnet/core` and `dotnet/main`;
  #14498 closes with links to the pull requests that replace it.

## Pipelines

- Upstream branches: `ci.yaml` runs the desktop .NET job on Linux, macOS, and Windows, and the Android and iOS jobs,
  `ci_path_filters.yaml` lists only their paths, and `build_and_test_reusable.yaml` excludes `slint-dotnet` and `test-driver-dotnet`.
  No browser job and no publish workflow.
- The fork: the jobs that only the fork needs move into a new workflow, `dotnet_fork.yaml`:
  the browser, `Slint.Markup`, Mapsui, the templates, and the samples' smoke test.
  The fork then changes few lines of `master`'s workflows, which keeps merging `master` cheap.
  `publish_nuget_package.yaml` stays as it is, with the repository variable `SLINT_NUGET_PACKAGE_ID`.
- Merging `master` into the fork breaks the `csharp` blocks of cases that upstream changes, three in the merge of 2026-11-17.
  Moving the blocks into files of their own next to the cases, such as `focus_change.cs`, would remove the textual conflicts;
  the fork's CI still catches a block that no longer matches its case.

## Order

1. Open 1a and 1b; draft 1c, and ask about it with the question of step 2.
2. Cut `dotnet/core` from `dotnet_bindings` by removing the fork-only parts, with its CI for the five platforms.
3. Create `dotnet/main` from `dotnet/core`, add `dotnet_fork.yaml` in the pull request from `dotnet-fork`,
   merge that pull request, and make `dotnet/main` the fork's default branch.
4. If upstream agrees, open 3a to 3d from `dotnet/core`, each when the one before it is reviewed.
5. Close #14498 with links to whichever pull requests replace it.
