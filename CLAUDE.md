# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BlogHelper9000 is a multi-project .NET solution for managing Jekyll blog posts. It provides both a CLI tool (`bloghelper`) and a TUI workspace with an embedded Neovim editor.

## Solution Structure

```
BlogHelper9000.sln
  BlogHelper9000.Core/           (classlib — domain logic, YAML parsing, post management)
  BlogHelper9000.Imaging/        (classlib — ImageSharp, Unsplash, featured image generation)
  BlogHelper9000/                (exe — CLI tool, references Core + Imaging)
  BlogHelper9000.Nvim/           (classlib — embedded Neovim client via MsgPack-RPC)
  BlogHelper9000.Tui/            (exe — Terminal.Gui workspace, references Core + Nvim)
  BlogHelper9000.Tests/          (tests for CLI commands)
  BlogHelper9000.Nvim.Tests/     (tests for Nvim grid, RPC, UI event parsing)
  BlogHelper9000.TestHelpers/    (classlib — shared test infrastructure)
```

## Build & Test Commands

```bash
# Build (uses Cake build system)
./build.sh

# Build and run tests
./build.sh --target=tests

# Build, test, and pack as NuGet tool
./build.sh --target=pack --configuration=release

# Run all tests
dotnet test BlogHelper9000.sln

# Run specific test projects
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj
dotnet test BlogHelper9000.Nvim.Tests/BlogHelper9000.Nvim.Tests.csproj

# Run a single test by name
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlConvertTests.Can_Serialise"

# Run the TUI
dotnet run --project BlogHelper9000.Tui -- --base-directory /path/to/jekyll/blog

# Run the TUI without Neovim (safe mode, uses plain-text editor)
dotnet run --project BlogHelper9000.Tui -- --no-nvim --base-directory /path/to/jekyll/blog
```

## Architecture

### Projects

- **BlogHelper9000.Core** — Domain logic: `PostManager`, `MarkdownHandler`, `YamlConvert`, `BlogService`. No UI dependencies.
- **BlogHelper9000.Imaging** — `ImageProcessor`, `UnsplashClient`, `FontManager`. Depends on SixLabors.ImageSharp.
- **BlogHelper9000** — CLI exe using TimeWarp.Nuru mediator pattern. Commands in `Commands/`.
- **BlogHelper9000.Nvim** — Embedded Neovim client. `NvimProcess` manages `nvim --embed --headless`. `MsgPackRpcClient` handles MsgPack-RPC framing. `NvimGrid` maintains 2D screen buffer. Uses MessagePack v3.
- **BlogHelper9000.Tui** — Terminal.Gui v2 (develop track) workspace. `NvimEditorView` renders Neovim grid. `CommandPalette` (Ctrl+P) exposes blog operations. `KeyTranslator` converts Terminal.Gui keys to Neovim notation.

### Key Namespaces

| Namespace | Location |
|-----------|----------|
| `BlogHelper9000.Core.Helpers` | PostManager, MarkdownHandler |
| `BlogHelper9000.Core.YamlParsing` | YamlConvert, YamlHeader, attributes |
| `BlogHelper9000.Core.Models` | BlogMetaInformation, AppDataModel |
| `BlogHelper9000.Core.Services` | IBlogService, BlogService |
| `BlogHelper9000.Imaging` | ImageProcessor, UnsplashClient |
| `BlogHelper9000.Nvim.Rpc` | NvimProcess, MsgPackRpcClient |
| `BlogHelper9000.Nvim.Grid` | NvimGrid, NvimGridCell |
| `BlogHelper9000.Nvim.UiEvents` | NvimUiEvent records, UiEventParser |
| `BlogHelper9000.Tui.Views` | BlogWorkspaceWindow, NvimEditorView, FileBrowserView, CommandPalette |
| `BlogHelper9000.Tui.Input` | KeyTranslator |

### TUI Keyboard Shortcuts
- `Ctrl+B` — Toggle file browser
- `Ctrl+P` — Open command palette
- `Ctrl+Q` — Quit

### Testing Patterns
- **xunit v3** with **FluentAssertions** and **NSubstitute**
- File system operations are abstracted via `System.IO.Abstractions.IFileSystem`, tested with `MockFileSystem`
- `JekyllBlogFilesystemBuilder` (in TestHelpers) constructs mock Jekyll directory structures
- Nvim tests cover grid operations, MsgPack serialization, and UI event parsing without requiring nvim

## Tech Stack
- .NET 10.0 / C# latest, nullable reference types enabled
- Cake Build for build orchestration
- Terminal.Gui v2 (2.0.0-develop.5027) for TUI
- MessagePack v3 for Neovim RPC
- `InternalsVisibleTo` exposes internals to test projects
- MinVer for semantic versioning
- Spectre.Console for CLI console output formatting
