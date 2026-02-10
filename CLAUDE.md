# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BlogHelper9000 is a .NET CLI tool (`bloghelper`) for managing Jekyll blog posts. It handles creating, publishing, fixing metadata, and generating featured images for posts.

## Build & Test Commands

```bash
# Build (uses Cake build system)
./build.sh

# Build and run tests
./build.sh --target=tests

# Build, test, and pack as NuGet tool
./build.sh --target=pack --configuration=release

# Run tests directly
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj

# Run a single test by name
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlConvertTests.Can_Serialise"

# Run tests with coverage
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj /p:CollectCoverage=true /p:CoverletOutput=TestResults/ /p:CoverletOutputFormat=lcov
```

## Architecture

### CLI Framework
Uses **TimeWarp.Nuru** for CLI routing and the mediator pattern. Each command is a class implementing `IRequest` with a nested `Handler` class. Routes are configured in `Program.cs` via `NuruAppBuilder`.

### Key Services
- **PostManager** (`Helpers/PostManager.cs`) — Core service managing blog post file operations, path generation, and Jekyll directory structure (`_posts/`, `_drafts/`, `assets/images/`)
- **MarkdownHandler** (`Helpers/MarkdownHandler.cs`) — Reads/writes markdown files while preserving YAML front-matter headers
- **YamlConvert** (`YamlParsing/YamlConvert.cs`) — Custom YAML serializer/deserializer (no external YAML library). Uses `[YamlName]` and `[YamlIgnore]` attributes on `YamlHeader` for property mapping
- **ImageProcessor** (`Imager/ImageProcessor.cs`) — Generates featured images using ImageSharp: composites Unsplash photos with title text and author branding, outputs WebP
- **UnsplashClient** (`Imager/UnsplashClient.cs`) — Fetches images from Unsplash API; credentials stored in `~/Documents/bloghelper9000.json`

### Commands
Commands live in `Commands/` — `InfoCommand`, `AddCommand`, `FixCommand`, `PublishCommand`, `AddImageCommand`.

### Testing Patterns
- **xunit v3** with **FluentAssertions** and **NSubstitute**
- File system operations are abstracted via `System.IO.Abstractions.IFileSystem`, tested with `MockFileSystem`
- `JekyllBlogFilesystemBuilder` (test helper) constructs mock Jekyll directory structures for test scenarios

## Tech Stack
- .NET 10.0 / C# latest, nullable reference types enabled
- Cake Build for build orchestration
- `InternalsVisibleTo` exposes internals to the test project
- MinVer for semantic versioning
- Spectre.Console for console output formatting