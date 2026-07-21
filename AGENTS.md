# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

BlogHelper9000 is a multi-project .NET solution for managing Jekyll blog posts. It provides a CLI tool (`bloghelper`), a TUI workspace with an embedded Neovim editor, and an MCP server (`bloghelper-mcp`) exposing blog operations to AI agents.

## Solution Structure

```
BlogHelper9000.sln
  BlogHelper9000.Core/           (classlib — domain logic, YAML parsing, post management)
  BlogHelper9000.Imaging/        (classlib — ImageSharp, Unsplash, featured image generation)
  BlogHelper9000/                (exe — CLI tool, references Core + Imaging)
  BlogHelper9000.Nvim/           (classlib — embedded Neovim client via MsgPack-RPC)
  BlogHelper9000.Tui/            (exe — Terminal.Gui workspace, references Core + Nvim)
  BlogHelper9000.Mcp/            (exe — stdio MCP server, references Core + Imaging)
  BlogHelper9000.Tests/          (tests for CLI commands)
  BlogHelper9000.Nvim.Tests/     (tests for Nvim grid, RPC, UI event parsing)
  BlogHelper9000.Tui.Tests/      (tests for TUI views, commands, key translation)
  BlogHelper9000.Mcp.Tests/      (tests for MCP tools)
  BlogHelper9000.TestHelpers/    (classlib — shared test infrastructure)
```

`AGENTS.md` is a copy of this file for Codex — keep the two in sync when updating either. Design specs and implementation plans live in `docs/superpowers/specs/` and `docs/superpowers/plans/`.

## Jekyll Blog Conventions (domain model)

- Drafts live in `_drafts/`; published posts live in `_posts/<year>/`.
- A post is identified by filename (e.g. `my-post.md`) or path; bare filenames resolve against `_drafts/` then `_posts/` (including nested year folders). Paths outside the blog root are rejected.
- Titles are slugified to lowercase-hyphenated filenames.
- In front matter, `published:` holds a date (or a draft/true/false placeholder); a separate internal boolean tracks publish state. Blog stats count only published posts.
- The publishing schedule (post series, per-post entries, publish ticks) lives in a SQLite database at `.bloghelper.db` in the blog root — dot-prefixed so Jekyll does not copy it into `_site`. It is created by `bloghelper schedule-import <xlsx>`; entries are keyed by draft filename (no date prefix). A series may optionally carry a cadence (day-of-week + the date of week 1), used to resolve week-numbered entries to calendar dates.

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

# Run the MCP server (blog path via env var, first positional arg, or cwd)
BLOG_BASE_DIRECTORY=/path/to/jekyll/blog dotnet run --project BlogHelper9000.Mcp
```

Cake targets are `Default` (build), `Tests`, and `Pack` (invoked lowercase as `--target=tests` / `--target=pack`).

`BlogHelper9000.Imaging` requires a `SIXLABORS_LICENSE_KEY` environment variable to build (SixLabors.ImageSharp license). Since `BlogHelper9000.Tests` and `BlogHelper9000.Mcp.Tests` reference it transitively, building or testing those projects fails without it. CI reads it from a GitHub secret; locally it's expected to live in your shell profile — if a `dotnet build`/`dotnet test` run in a non-interactive shell can't see it, re-run via a login shell (e.g. `zsh -lc '...'`) rather than assuming the key is missing.

## Architecture

### Projects

- **BlogHelper9000.Core** — Domain logic: `PostManager`, `MarkdownHandler`, `YamlConvert`, `BlogService`. No UI dependencies.
- **BlogHelper9000.Imaging** — `ImageProcessor`, `UnsplashClient`, `FontManager`. Depends on SixLabors.ImageSharp.
- **BlogHelper9000** — CLI exe using TimeWarp.Nuru mediator pattern. Commands in `Commands/`. Schedule commands: `schedule-import <xlsx> [--force]`, `schedule-list`, `schedule-show <series>`, `schedule-stats`, `schedule-mark <post> [--date] [--unmark]`; `add --series/--week/--publish-date` appends the new draft to a series and `publish` auto-ticks the matching entry. The importer reads xlsx with ExcelDataReader (CLI project only).
- **BlogHelper9000.Nvim** — Embedded Neovim client. `NvimProcess` manages `nvim --embed --headless`. `MsgPackRpcClient` handles MsgPack-RPC framing. `NvimGrid` maintains 2D screen buffer. Uses MessagePack v3.
- **BlogHelper9000.Tui** — Terminal.Gui v2 (develop track) workspace. `NvimEditorView` renders Neovim grid. `CommandPalette` (Ctrl+P) exposes blog operations. `KeyTranslator` converts Terminal.Gui keys to Neovim notation.
- **BlogHelper9000.Mcp** — MCP server over stdio using the `ModelContextProtocol` SDK. Tools live in `Tools/` (one class per tool, auto-discovered via `[McpServerTool]` and `WithToolsFromAssembly()`); shared response shapes in `ToolResponses.cs`. Packs as dotnet tool `bloghelper-mcp`. **stdout is reserved for JSON-RPC framing — never write to it; all logging must go to stderr** (`Program.cs` configures `LogToStandardErrorThreshold = Trace`). `validate_post`/`validate_blog` (`Tools/ValidatePostTool.cs`, over Core's `PostValidator`) run static, offline pre-publish checks — front matter, image existence, internal links, placeholder content — and are the recommended step before `publish_post`.

### Key Namespaces

| Namespace | Location |
|-----------|----------|
| `BlogHelper9000.Core.Helpers` | PostManager, MarkdownHandler |
| `BlogHelper9000.Core.YamlParsing` | YamlConvert, YamlHeader, attributes |
| `BlogHelper9000.Core.Models` | BlogMetaInformation, AppDataModel |
| `BlogHelper9000.Core.Services` | IBlogService, BlogService |
| `BlogHelper9000.Core.Scheduling` | ScheduleDatabase, ScheduleRepository, ScheduleService, ScheduleStats |
| `BlogHelper9000.Imaging` | ImageProcessor, UnsplashClient |
| `BlogHelper9000.Nvim.Rpc` | NvimProcess, MsgPackRpcClient |
| `BlogHelper9000.Nvim.Grid` | NvimGrid, NvimGridCell |
| `BlogHelper9000.Nvim.UiEvents` | NvimUiEvent records, UiEventParser |
| `BlogHelper9000.Tui.Views` | BlogWorkspaceWindow, NvimEditorView, FileBrowserView, CommandPalette |
| `BlogHelper9000.Tui.Input` | KeyTranslator |
| `BlogHelper9000.Tui.Commands` | BlogCommands (palette actions) |
| `BlogHelper9000.Mcp.Tools` | One class per MCP tool (AddPostTool, PublishPostTool, …) |

### TUI Keyboard Shortcuts
- `Ctrl+B` — Toggle file browser
- `Ctrl+P` — Open command palette
- `Ctrl+Q` — Quit

### Testing Patterns
- **xunit v3** with **FluentAssertions** and **NSubstitute**
- File system operations are abstracted via `System.IO.Abstractions.IFileSystem`, tested with `MockFileSystem`
- `JekyllBlogFilesystemBuilder` (in TestHelpers) constructs mock Jekyll directory structures
- Nvim tests cover grid operations, MsgPack serialization, and UI event parsing without requiring nvim
- MCP tool tests (`BlogHelper9000.Mcp.Tests/Tools/`) exercise each tool against a `MockFileSystem` — no running MCP client needed

### Gotchas
- `FixMetadataTool` rewrites every post under `_posts/` in one call — it supports `dryRun=true`; preserve that behaviour when changing it
- MCP server: never write to stdout (see Architecture above); a stray `Console.WriteLine` breaks JSON-RPC framing
- Post lookup must handle nested `_posts/<year>/` filenames — `TryFindPost` was previously broken for these (fixed in `ef99e1e`); add tests for nested paths when touching post resolution
- SQLite bypasses `IFileSystem` — schedule tests use `ScheduleDatabase.OpenInMemory()`, never `MockFileSystem`; only existence checks go through `IFileSystem`
- `schedule-import` replaces the whole schedule database (guarded by `--force`)
- The destructive MCP schedule tools (`remove_schedule_entry`, `delete_series`) default `dryRun=true`, matching `fix_metadata`; `delete_series` also refuses to delete a non-empty series regardless of `dryRun`
- The lifecycle MCP tools `delete_draft`/`unpublish_post` also default `dryRun=true`; `delete_draft` refuses anything under `_posts/` (published posts must be unpublished first). `unpublish_post` clears `published:` (drops the date) but leaves `ispublished: False` set explicitly — `GetBlogInfo`'s draft-counting relies on that explicit bool (`BlogService.GetBlogInfo`, filtering on `IsPublished == true/false`), not on the presence/absence of a published date, so do not "simplify" this away
- `ScheduleDatabase` schema migrations are sequential, `user_version`-gated steps (`MigrateTo1`, `MigrateTo2`, …) run in order on every open; add new ones as `MigrateToN` rather than editing an existing step
- TimeWarp.Nuru 3.0.0-beta.71: service registrations MUST go through `builder.ConfigureServices(...)` (touching `builder.Services` throws at startup); the lambda is inlined into generated code so it cannot capture locals; `[NuruRouteGroup]` is silently ignored, hence the hyphenated `schedule-*` route names
- ClosedXML cannot LOAD workbooks in this solution (SixLabors.Fonts 3.x conflict via Imaging) — creating/saving them in tests is fine; production xlsx reading uses ExcelDataReader
- MCP file-touching tools serialise through a global gate (`BlogHelper9000.Mcp/ToolGate.cs`) — the SDK dispatches concurrent `tools/call` requests in parallel, and `PostManager`/`MarkdownHandler` open files with no sharing strategy, so unsynchronised concurrent calls can hit file-sharing violations. New file-touching tools MUST wrap their bodies in `ToolGate.RunExclusive`/`RunExclusiveAsync`; pure-SQLite schedule tools are excluded (SQLite handles its own locking)
- Like SQLite, child processes (`IProcessRunner`/`GitDeployStateService`) bypass `IFileSystem` — tests use a recording fake runner (`RecordingProcessRunner`), never real git against a repo. Git arguments are passed as vectors (`ProcessStartInfo.ArgumentList`), never composed into a string — this is what makes the runner immune to flag/argument injection from commit messages or filenames

## Tech Stack
- .NET 10.0 / C# latest, nullable reference types enabled
- Cake Build for build orchestration
- Terminal.Gui v2 (2.0.0-develop.5027) for TUI
- MessagePack v3 for Neovim RPC
- ModelContextProtocol SDK for the MCP server (stdio transport)
- `InternalsVisibleTo` exposes internals to test projects
- MinVer for semantic versioning
- Spectre.Console for CLI console output formatting
