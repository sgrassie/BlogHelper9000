# BlogHelper9000

**A CLI + TUI toolkit for Jekyll blogging with an embedded Neovim editor**

![CI Status](https://github.com/sgrassie/BlogHelper9000/actions/workflows/main.yml/badge.svg)
[![Coverage Status](https://coveralls.io/repos/github/sgrassie/BlogHelper9000/badge.svg?branch=main)](https://coveralls.io/github/sgrassie/BlogHelper9000?branch=main)
[![CodeQL Advanced](https://github.com/sgrassie/BlogHelper9000/actions/workflows/codeql.yml/badge.svg)](https://github.com/sgrassie/BlogHelper9000/actions/workflows/codeql.yml)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-blue)

---

BlogHelper9000 is a .NET toolset for managing [Jekyll](https://jekyllrb.com/) blog posts. It ships as two executables: a **CLI tool** (`bloghelper`) for scripting and quick operations, and a **TUI workspace** with an embedded Neovim editor for an integrated writing experience — all from your terminal.

## Features

- **CLI tool** — Create posts & drafts, publish drafts, inspect blog stats, and batch-fix YAML front matter
- **TUI workspace** — Full terminal UI with file browser, command palette, and embedded Neovim editor
- **Embedded Neovim** — Real Neovim running headless via MsgPack-RPC, rendered as a Terminal.Gui view with full mode/cursor support
- **Featured image generation** — Search Unsplash, download images, and generate branded featured images with ImageSharp
- **YAML front matter management** — Parse, serialise, and bulk-fix front matter (published status, descriptions, tags)
- **Safe mode** — Run the TUI with `--no-nvim` for a plain-text editor fallback when Neovim isn't available

## Installation

### As a .NET global tool

```bash
dotnet tool install --global BlogHelper9000
```

Once installed, the `bloghelper` command is available globally.

### Building from source

```bash
git clone https://github.com/sgrassie/BlogHelper9000.git
cd BlogHelper9000
./build.sh
```

## Quick Start

All CLI commands operate on a Jekyll blog directory. Pass `--base-directory` to point at your blog root, or configure it in `bloghelper9000.json`.

### Get blog info

```bash
bloghelper info --base-directory /path/to/blog
```

Shows total post count, drafts, days since last post, and recent posts.

### Create a new draft

```bash
bloghelper add "My New Post" --base-directory /path/to/blog --is-draft
```

### Create a published post

```bash
bloghelper add "My New Post" --base-directory /path/to/blog
```

### Publish a draft

```bash
bloghelper publish "my-draft-post" --base-directory /path/to/blog
```

Moves the draft from `_drafts/` into `_posts/<year>/`, sets the published date, and updates the YAML front matter.

### Fix metadata across all posts

```bash
# Fix published status (extract dates from filenames)
bloghelper fix --base-directory /path/to/blog --status

# Migrate legacy descriptions
bloghelper fix --base-directory /path/to/blog --description

# Normalize and de-duplicate tags
bloghelper fix --base-directory /path/to/blog --tags
```

### Add a featured image from Unsplash

```bash
bloghelper add-image "my-post" --image-query "mountains" --base-directory /path/to/blog
```

### Configure Unsplash credentials

```bash
bloghelper unsplash-credentials --access-key YOUR_KEY --secret-key YOUR_SECRET
```

Credentials are stored in `~/Documents/bloghelper9000.json`.

## TUI Workspace

The TUI provides a full terminal workspace for writing and managing blog posts. It uses [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) v2 and embeds a real Neovim instance for editing.

### Launch

```bash
dotnet run --project BlogHelper9000.Tui -- --base-directory /path/to/blog
```

### Launch without Neovim (safe mode)

```bash
dotnet run --project BlogHelper9000.Tui -- --no-nvim --base-directory /path/to/blog
```

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+B` | Toggle file browser |
| `Ctrl+P` | Open command palette |
| `Ctrl+Q` | Quit |

### Command Palette

Press `Ctrl+P` to open the command palette. It provides filtered access to blog operations:

| Command | Description |
|---------|-------------|
| New Draft | Create a new draft post |
| New Post | Create a new published post |
| Publish Draft | Publish an existing draft |
| Blog Info | Show blog statistics |
| Fix Metadata: Status | Fix published status on all posts |
| Fix Metadata: Description | Migrate legacy descriptions |
| Fix Metadata: Tags | Fix and normalize tags |

## Solution Architecture

```
BlogHelper9000.sln
├── BlogHelper9000.Core/           Core domain logic
├── BlogHelper9000.Imaging/        Image processing
├── BlogHelper9000/                CLI executable
├── BlogHelper9000.Nvim/           Neovim client
├── BlogHelper9000.Tui/            TUI executable
├── BlogHelper9000.Tests/          CLI tests
├── BlogHelper9000.Nvim.Tests/     Neovim tests
└── BlogHelper9000.TestHelpers/    Shared test infra
```

| Project | Type | Description |
|---------|------|-------------|
| **BlogHelper9000.Core** | classlib | Domain logic — `PostManager`, `MarkdownHandler`, `YamlConvert`, `BlogService`. No UI dependencies. |
| **BlogHelper9000.Imaging** | classlib | Featured image generation — `ImageProcessor`, `UnsplashClient`, `FontManager`. Uses SixLabors.ImageSharp. |
| **BlogHelper9000** | exe | CLI tool using TimeWarp.Nuru mediator pattern. Packaged as a `dotnet tool`. |
| **BlogHelper9000.Nvim** | classlib | Embedded Neovim client. `NvimProcess` manages `nvim --embed --headless`. `MsgPackRpcClient` handles MsgPack-RPC framing. `NvimGrid` maintains a 2D screen buffer. |
| **BlogHelper9000.Tui** | exe | Terminal.Gui v2 workspace. `NvimEditorView` renders the Neovim grid. `CommandPalette` exposes blog operations. `KeyTranslator` maps Terminal.Gui keys to Neovim notation. |
| **BlogHelper9000.Tests** | test | xUnit tests for CLI commands |
| **BlogHelper9000.Nvim.Tests** | test | Tests for grid operations, MsgPack serialization, and UI event parsing (no nvim required) |
| **BlogHelper9000.TestHelpers** | classlib | `JekyllBlogFilesystemBuilder` and shared test infrastructure using `MockFileSystem` |

## Tech Stack

| Dependency | Version | Purpose |
|------------|---------|---------|
| .NET | 10.0 | Runtime & SDK |
| C# | latest | Language version with nullable reference types |
| Terminal.Gui | 2.0.0-develop.5027 | TUI framework (v2 develop track) |
| MessagePack | v3 | MsgPack-RPC serialization for Neovim communication |
| SixLabors.ImageSharp | 3.1.12 | Image processing & featured image generation |
| SixLabors.Fonts | 2.1.3 | Font rendering for image overlays (bundled Ubuntu fonts) |
| TimeWarp.Nuru | 2.1.0-beta.32 | Mediator pattern for CLI command routing |
| Spectre.Console | 0.54.0 | CLI console output formatting |
| MinVer | 7.0.0 | Git-based semantic versioning |
| Cake Build | — | Build orchestration |
| xUnit v3 | — | Test framework |
| FluentAssertions | — | Test assertion library |
| NSubstitute | — | Mocking framework |
| System.IO.Abstractions | 22.1.0 | File system abstraction for testability |

## Building & Testing

BlogHelper9000 uses [Cake](https://cakebuild.net/) for build orchestration.

```bash
# Build
./build.sh

# Build and run tests
./build.sh --target=tests

# Build, test, and pack as NuGet tool
./build.sh --target=pack --configuration=release

# Run all tests directly
dotnet test BlogHelper9000.sln

# Run specific test projects
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj
dotnet test BlogHelper9000.Nvim.Tests/BlogHelper9000.Nvim.Tests.csproj

# Run a single test by name
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlConvertTests.Can_Serialise"
```

## Configuration

### Unsplash API Credentials

To use featured image generation, configure your [Unsplash API](https://unsplash.com/developers) credentials:

```bash
bloghelper unsplash-credentials --access-key YOUR_ACCESS_KEY --secret-key YOUR_SECRET_KEY
```

This stores credentials in `~/Documents/bloghelper9000.json`.

### Base Directory

All commands accept `--base-directory` to specify the Jekyll blog root directory. This should point to the directory containing your `_posts/` and `_drafts/` folders.

## License

This project is licensed under the [MIT License](LICENSE.txt).

Copyright (c) 2022 Stuart Grassie

---

> See the original blog post: [Automating Jekyll with .NET](https://www.temporalcohesion.co.uk/2022/03/04/Automating-jekyll-with-dotnet)
