# BlogHelper9000 Full Solution Code Review

## Remediation Status (2026-07-03)

All 29 findings below were triaged and implemented against the plan at
[docs/superpowers/plans/2026-07-03-code-review-fixes.md](docs/superpowers/plans/2026-07-03-code-review-fixes.md).
Final state: `dotnet build BlogHelper9000.sln` — 0 errors; `dotnet test BlogHelper9000.sln` —
MCP 13/13, Nvim 55/55, CLI/Core 53/53 (+2 pre-existing skips), TUI 82/82, all passing with no regressions
against the review's original baseline (MCP 13, Nvim 41, CLI/Core 36+2 skipped, TUI 77).

Two items were deliberately **not** implemented as originally suggested, after verifying the suggested
fix conflicted with either the existing, deliberately-tested behavior of the codebase or the project's
own `.gitignore` intent. Per `superpowers:receiving-code-review` guidance, these were pushed back on
with technical reasoning rather than force-implemented:

- **BH9000-025** — The suggested fix (make `published` a bool mapped from `IsPublished`, move the date
  to a `date:` key, switch to ISO 8601) would redefine this project's own established front-matter schema.
  `YamlConvertTests`, `FixCommandTests`, `InfoCommandTests`, and `PublishCommandTests` all consistently and
  deliberately encode `published:` as the date field and `ispublished:`/`IsPublished` as the separate
  boolean — this is intentional, not accidental, and the `DraftDateValue`/`"true"`/`"false"` sentinel
  handling in `YamlDeserialiser` exists specifically to support this convention's draft-placeholder workflow.
  Swapping the mapping would break five files of currently-passing, intentional tests and silently
  reinterpret real users' existing blog post front matter. **Implemented instead:** BH9000-002's Extras
  preservation (the safe, additive part of this pair of findings), which already prevents the underlying
  data-loss risk (unknown keys silently dropped) without a schema-breaking change. The published/date
  key semantics are left as this project's existing, deliberate convention — a real redefinition of that
  schema is a product decision for the maintainer, not a bug fix.
- **BH9000-023** — Untracked `BlogHelper9000.sln.DotSettings.user` (already listed in `.gitignore`, so its
  continued tracking was a genuine inconsistency). Left `.vscode/launch.json` and `.vscode/tasks.json`
  (both root and nested under `BlogHelper9000/`) tracked: the root `.gitignore` explicitly negates
  `.vscode/*` for exactly these two filenames (`!.vscode/tasks.json`, `!.vscode/launch.json`), which is
  clear evidence of a deliberate choice to share generic, non-personal debug configs (no hardcoded
  usernames or absolute paths) with the team, not accidental "local machine state."

Review date: 2026-07-02

Scope: `BlogHelper9000.sln`, including Core, CLI, Imaging, MCP, Neovim, TUI, tests, build, and CI configuration. No production or test code was changed as part of this review.

Verification performed:

- `dotnet test BlogHelper9000.sln --no-restore` passed: MCP 13 tests, Nvim 41 tests, CLI/Core 36 passed and 2 skipped, TUI 77 tests.
- The test run emitted compiler/analyzer warnings, especially Terminal.Gui obsolete API warnings, xUnit analyzer warnings, and one unused variable warning.
- `dotnet list BlogHelper9000.sln package --vulnerable --include-transitive` did not return in the sandbox after more than two minutes and was cancelled, so dependency vulnerability status remains unverified.
- `dotnet run --no-restore --project BlogHelper9000 -- ...` smoke checks did not return in the sandbox and were cancelled, so CLI runtime behavior was assessed from source wiring rather than a completed repro.

## Executive Summary

The codebase has a healthy project split and the current test suite passes, but several high-impact risks are concentrated in shared file mutation paths. The biggest concerns are path scoping for MCP/CLI/TUI operations, YAML/front-matter preservation, markdown rewrite safety, and external-image/credential handling. The TUI and Neovim integration also has several reliability and command-injection edge cases that should be tightened before treating it as a robust editor surface.

The highest-value next actions are:

1. Centralize all blog-mutating operations behind `IBlogService` and enforce base-directory containment before reads, writes, moves, deletes, and image updates.
2. Replace the custom lossy YAML/front-matter rewrite path with a parser/serializer strategy that preserves unknown metadata, delimiters, comments where possible, and body text exactly — including correct Jekyll `published:`/`date:` semantics and ISO 8601 dates (see BH9000-025) and post-creation tag/collision handling (see BH9000-026).
3. Harden Unsplash/image handling: real query support, injected `HttpClient`, credential storage, cancellation, HTTP error handling, and image disposal.
4. Add regression tests for malformed posts, duplicate metadata, path traversal attempts, missing directories, publish collisions, and unusual filenames.
5. Resolve build/CI drift and make dependency auditing reliable.

## Validation Pass (2026-07-03)

A second, independent review verified every finding below against the source tree. All 24 findings are **Confirmed**: the cited evidence lines are accurate and the described behaviour exists in the code. The table lists corrections and amplifications discovered during validation; five additional findings (BH9000-025 to BH9000-029) are appended at the end of this document and should be treated as part of the review.

| ID | Verdict | Validation notes |
|----|---------|------------------|
| BH9000-001 | Confirmed | Also: the MCP server falls back to the current working directory as blog root when `BLOG_BASE_DIRECTORY` and args are absent (`BlogHelper9000.Mcp/Program.cs:14-16`), so a mis-launched server treats any directory as the blog. |
| BH9000-002 | Confirmed | Worse than stated: standard Jekyll `date:` is not a typed property, so it lands in `Extras` and is deleted by any update, silently changing permalinks and post ordering. `FixMetadata` also rewrites every post unconditionally, even when no fix applied (`BlogHelper9000.Core/Services/BlogService.cs:79-91`), so one `fix` run strips unknown front matter blog-wide. |
| BH9000-003 | Confirmed | The `Count()`/`ElementAt()` loop re-enumerates the `Prepend` chain on every iteration, making the rewrite O(n²) per file. |
| BH9000-004 | Confirmed | Escaping is at `BlogHelper9000.Tui/Views/NvimEditorView.cs:116-117`; only `\` and space are handled. |
| BH9000-005 | Confirmed | One nuance: `GetFromJsonAsync` throws on non-2xx internally, so API failures surface as unhandled exceptions rather than silently. Also new: `credentials.UnsplashCredentials.Split(':')` throws `NullReferenceException` when the JSON file lacks that property (`BlogHelper9000.Imaging/UnsplashClient.cs:57`). |
| BH9000-006 | Confirmed | Writer uses `IFileSystem` but reader uses `System.IO.File`, so tests cannot cover the read path. |
| BH9000-007 | Confirmed | CLI `Program.cs:13-17` registers neither `TimeProvider` nor `IUnsplashClient`/`IImageProcessor`, so `publish` and `add-image` cannot resolve their handlers at runtime. |
| BH9000-008 | Confirmed | See BH9000-027 for the redundant post-move delete and non-idempotent republish this finding did not cover. |
| BH9000-009 | Confirmed | Also: `rawFileName[..10]` throws `ArgumentOutOfRangeException` for filenames shorter than 10 characters (`BlogHelper9000.Core/Services/BlogService.cs:135`). |
| BH9000-010 | Confirmed | See BH9000-025 for concrete Jekyll `published:`/`date:` round-trip corruption, which is higher severity than the general parser-narrowness described here. |
| BH9000-011 | Confirmed | `DateTime.Now` at `BlogHelper9000.Core/Services/BlogService.cs:114`. See also BH9000-029 for the `new FileInfo(f)` abstraction bypass in the same code path. |
| BH9000-012 | Confirmed | — |
| BH9000-013 | Confirmed | TUI pass-through at `BlogHelper9000.Tui/Commands/BlogCommands.cs:367-376`; CLI at `BlogHelper9000/Commands/AddImageCommand.cs:25-26`. |
| BH9000-014 | Confirmed | Also: if `WriteAsync` throws, the pending `TaskCompletionSource` is never removed from `_pendingRequests`, leaking an entry per failed request (`BlogHelper9000.Nvim/Rpc/MsgPackRpcClient.cs:55-64`). |
| BH9000-015 | Confirmed | — |
| BH9000-016 | Confirmed | See BH9000-028 for a related but distinct cross-thread race on the same grid. |
| BH9000-017 | Confirmed | `EditorSurface` has no save/write member at all. |
| BH9000-018 | Confirmed | — |
| BH9000-019 | Confirmed | Runtime impact is limited because `ImageProcessor` is a singleton in TUI/MCP, but each construction still re-adds every embedded font face to the static collection. |
| BH9000-020 | Confirmed | Fixed delays at `BlogHelper9000.Tui.Tests/Commands/BlogCommandsAddImageTests.cs:95` and `:143`. |
| BH9000-021 | Confirmed | Concrete drift: README claims Terminal.Gui `2.0.0-develop.5027` vs csproj `2.4.16`; ImageSharp `3.1.12` vs `4.0.0`; SixLabors.Fonts `2.1.3` vs `3.0.0`; System.IO.Abstractions `22.1.0` vs `22.1.1`. |
| BH9000-022 | Confirmed | — |
| BH9000-023 | Confirmed | Tracked: `BlogHelper9000.sln.DotSettings.user`, `.vscode/launch.json`, `.vscode/tasks.json`, `BlogHelper9000/.vscode/launch.json`, `BlogHelper9000/.vscode/tasks.json`. |
| BH9000-024 | Confirmed | `FixCommandTests` help tests have fully commented-out bodies but still run and pass vacuously. |

## Critical / High Issues

### BH9000-001: MCP and CLI inputs can escape the configured blog root

Prompt: "Add a single path-resolution guard for all operations that accepts user/MCP file names, normalizes paths, resolves them against `BlogHelperOptions.BaseDirectory`, and rejects any path outside the blog root."

Evidence:

- `PostManager.TryFindPost` accepts any existing path before trying `_drafts` or `_posts`: `BlogHelper9000.Core/Helpers/PostManager.cs:96-124`.
- `TryFindAuthorBranding` similarly accepts any existing branding path: `BlogHelper9000.Core/Helpers/PostManager.cs:146-157`.
- MCP tools expose mutating operations without an additional trust boundary: `BlogHelper9000.Mcp/Tools/PublishPostTool.cs:11-18`, `BlogHelper9000.Mcp/Tools/AddFeaturedImageTool.cs:12-31`, `BlogHelper9000.Mcp/Tools/FixMetadataTool.cs:11-25`.
- `AddPost` builds filenames from raw titles with only spaces replaced, so titles containing `../`, path separators, or reserved characters can change the target path: `BlogHelper9000.Core/Helpers/PostManager.cs:177-180`, `BlogHelper9000.Core/Services/BlogService.cs:25-42`.

Risk:

An MCP client, CLI user, or future UI path can read or mutate files outside the intended Jekyll blog if it supplies an absolute path or traversal title. This is especially important for MCP because the tool is intended to be called by AI agents.

Suggested follow-up:

- Implement a `BlogPathResolver` that returns canonical full paths and verifies containment under the configured base directory.
- Add tests for absolute paths outside the blog, `../` in titles, symlink-like edge cases where supported, and basename-only lookups.
- Reject unsafe post titles or slugify them with a whitelist such as lowercase letters, digits, and hyphens.

### BH9000-002: Updating markdown drops unknown front-matter keys

Prompt: "Make front-matter updates preserve unknown YAML metadata instead of discarding everything stored in `YamlHeader.Extras`."

Evidence:

- `YamlDeserialiser` collects unknown keys into `YamlHeader.Extras`: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs:36-54`.
- `YamlSerialiser` serializes only the known property cache and never appends `Extras`: `BlogHelper9000.Core/YamlParsing/YamlSerialiser.cs:8-39`, `BlogHelper9000.Core/YamlParsing/SerialiserBase.cs:22-28`.
- `MarkdownHandler.UpdateFile` replaces the original front matter wholesale with the serialized header: `BlogHelper9000.Core/Helpers/MarkdownHandler.cs:25-62`.

Risk:

Any publish, metadata fix, or image update can silently remove Jekyll/custom fields such as `permalink`, `redirect_from`, `comments`, `canonical_url`, plugin fields, or future metadata. This is data loss, not just formatting churn.

Suggested follow-up:

- Either serialize `Extras` back out or update only the fields being changed in the original front-matter block.
- Add round-trip tests proving unknown fields survive publish, add-image, and fix-metadata operations.
- Decide how to handle conflicts when an extra key maps to a newly supported typed property.

### BH9000-003: Markdown rewrites can leave stale bytes or destroy content on malformed files

Prompt: "Replace `MarkdownHandler.UpdateFile` with an atomic truncate/write flow that validates front-matter delimiters before modifying a file."

Evidence:

- `OpenWrite()` does not truncate an existing file before writing new content: `BlogHelper9000.Core/Helpers/MarkdownHandler.cs:47-48`.
- The write loop repeatedly calls `Count()` and `ElementAt()` on an enumerable: `BlogHelper9000.Core/Helpers/MarkdownHandler.cs:49-53`.
- If fewer than two `---` delimiters are present, `withoutOriginalHeader` remains empty and the file is rewritten to only new YAML: `BlogHelper9000.Core/Helpers/MarkdownHandler.cs:27-45`.
- The parser does not explicitly reject missing front-matter delimiters: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs:10-31`.

Risk:

Shorter rewrites can leave old trailing content in the file. Malformed or delimiter-less files can be corrupted by a metadata operation. A failed write can also leave partial content because the update is not atomic.

Suggested follow-up:

- Use `FileMode.Create` or `WriteAllText` through `IFileSystem` for truncation, preferably via a temp file and atomic replace.
- Validate exactly one opening and one closing front-matter delimiter before mutating.
- Preserve body content without custom newline parsing where possible.
- Add tests for shorter metadata, missing closing delimiters, no front matter, and body text containing `---`.

### BH9000-004: Neovim file opening is vulnerable to command injection through filenames

Prompt: "Open files in Neovim using an API/escaping strategy that treats the path as data, not as part of an Ex command."

Evidence:

- `OpenFileAsync` escapes only backslashes and spaces before interpolating into `:e {escaped}`: `BlogHelper9000.Tui/Views/NvimEditorView.cs:112-118`.
- Filenames can legally contain characters meaningful to Vim commands, including `|`, quotes, and newlines.

Risk:

Opening a crafted filename from the file browser can execute unintended Ex command fragments inside the embedded Neovim process. In a local writing tool this is still meaningful because posts may be synced from external repositories or generated by tools.

Suggested follow-up:

- Use `fnameescape()` from inside Neovim, `nvim_command` with robust escaping, or an API path that does not parse the filename as command text.
- Add tests for paths containing spaces, quotes, pipes, backslashes, brackets, and newlines.

### BH9000-005: Unsplash client ignores the requested query and does not use the configured client

Prompt: "Refactor `UnsplashClient` so image searches use the supplied query, one injected/configured `HttpClient`, cancellation tokens, response validation, and testable HTTP behavior."

Evidence:

- The query helper ignores its `query` argument and always sends `query=programming`: `BlogHelper9000.Imaging/UnsplashClient.cs:35-38`.
- `CreateUnsplashClient` creates a client with `Accept-Version`, but `LoadImageAsync` uses `_httpClient` instead: `BlogHelper9000.Imaging/UnsplashClient.cs:17-27`, `BlogHelper9000.Imaging/UnsplashClient.cs:52-62`.
- The extra `HttpClient` created in `CreateUnsplashClient` is never disposed.
- There is no `EnsureSuccessStatusCode`, cancellation token, timeout policy, or typed handling for Unsplash rate-limit/error responses.

Risk:

The feature does not do what the user asks, may not satisfy Unsplash API requirements, leaks clients, and is hard to test without live network calls.

Suggested follow-up:

- Register `HttpClient` with DI or use `IHttpClientFactory`.
- URL-encode the actual query.
- Validate API and image responses before loading streams.
- Add tests with a fake message handler proving query, headers, and failure paths.

### BH9000-006: Unsplash credentials are stored as plaintext in a predictable user document file

Prompt: "Replace plaintext Unsplash credential storage with a safer platform-appropriate secret store or document the risk and restrict file permissions."

Evidence:

- `UnsplashCredentialsCommand` writes `accessKey:secretKey` into `~/Documents/bloghelper9000.json`: `BlogHelper9000/Commands/UnsplashCredentialsCommand.cs:17-25`.
- `UnsplashClient` reads the same file via `System.IO.File`, bypassing `IFileSystem`: `BlogHelper9000.Imaging/UnsplashClient.cs:69-80`.
- The secret key is collected and stored but apparently not used for random image requests: `BlogHelper9000.Imaging/UnsplashClient.cs:57-58`.

Risk:

The file is easy to discover, likely included in user backups/sync, and may have broad default permissions depending on platform. It also creates an unnecessary long-lived copy of the Unsplash secret.

Suggested follow-up:

- Prefer OS credential storage or environment variables.
- Store only the access key if the secret is not needed.
- If file storage remains, use an app-data directory, set owner-only permissions, and avoid logging paths unnecessarily.

### BH9000-007: CLI dependency injection appears incomplete for several commands

Prompt: "Audit CLI service registration and add smoke tests that resolve and execute every command through the real Nuru/DI host."

Evidence:

- CLI `Program.cs` registers `IFileSystem`, `InfoCommandReporter`, `MarkdownHandler`, and `PostManager`: `BlogHelper9000/Program.cs:13-17`.
- `PublishCommand.Handler` requires `TimeProvider`: `BlogHelper9000/Commands/PublishCommand.cs:11-14`.
- `AddImageCommand.Handler` requires `IUnsplashClient` and `IImageProcessor`: `BlogHelper9000/Commands/AddImageCommand.cs:16-19`.
- `BlogService`, `TimeProvider`, `IUnsplashClient`, and `IImageProcessor` are registered in TUI/MCP but not in CLI: `BlogHelper9000.Tui/Program.cs:35-46`, `BlogHelper9000.Mcp/Program.cs:24-30`.

Risk:

Commands can work in unit tests that instantiate handlers manually but fail at runtime when resolved through the real CLI host.

Suggested follow-up:

- Align CLI DI with TUI/MCP registrations.
- Prefer shared service registration extension methods to avoid drift.
- Add host-level tests for `add`, `publish`, `info`, `fix`, `add-image`, and `unsplash-credentials`.

## Medium Issues

### BH9000-008: Publish is not transactional and can partially modify drafts

Prompt: "Make publish update metadata and move files as one recoverable operation, with collision checks and a single captured clock value."

Evidence:

- Metadata is written before the file move: `BlogHelper9000.Core/Services/BlogService.cs:56-73`.
- Replacement path collisions are not checked before `Move`: `BlogHelper9000.Core/Services/BlogService.cs:70-73`.
- `GetLocalNow()` is called multiple times, so a midnight boundary can produce inconsistent year/date values: `BlogHelper9000.Core/Services/BlogService.cs:58-63`.
- The CLI duplicates the publish implementation separately: `BlogHelper9000/Commands/PublishCommand.cs:20-40`.

Risk:

If the move fails, the draft can remain in place but be marked published. Existing files can cause an exception after metadata has already changed. Duplicate implementations increase the chance that fixes land in only one surface.

Suggested follow-up:

- Capture `now` once.
- Compute and validate the target path before mutating metadata.
- Consider writing to a temp target and rolling back on failure.
- Have CLI call `IBlogService.PublishPost`.

### BH9000-009: Batch metadata fixes can fail mid-run on filename assumptions

Prompt: "Make metadata repair resilient per file and report skipped files instead of aborting the entire batch."

Evidence:

- `FixPublishedStatus` slices the first 10 characters of the filename and parses `yyyy-MM-dd`: `BlogHelper9000.Core/Services/BlogService.cs:132-138`.
- The CLI duplicate uses `Split("/")`, which is not portable to Windows paths: `BlogHelper9000/Commands/FixCommand.cs:41-45`.
- There is no per-file try/catch around the batch: `BlogHelper9000.Core/Services/BlogService.cs:79-90`.

Risk:

A single non-standard post filename can abort the entire batch after earlier files have already been modified.

Suggested follow-up:

- Use `IFileSystem.Path.GetFileName`.
- Validate with `DateTime.TryParseExact`.
- Return a structured result listing updated, skipped, and failed files.

### BH9000-010: YAML parsing is a narrow custom format and throws on common real-world YAML

Prompt: "Define the supported front-matter subset explicitly or switch to a YAML parser that supports common Jekyll front matter."

Evidence:

- Each line is split on the first colon, with no support for multiline values or comments: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs:134-142`.
- Duplicate keys throw through `Dictionary.Add`: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs:44-50`.
- Lists are parsed by stripping brackets and splitting commas, so quoted commas are not supported: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs:88-95`.
- Serialization emits raw scalar values without escaping: `BlogHelper9000.Core/YamlParsing/YamlSerialiser.cs:31-34`.

Risk:

Real Jekyll front matter may parse incorrectly or fail. Even when parsing succeeds, writing it back may change semantics.

Suggested follow-up:

- Evaluate `YamlDotNet` or a front-matter-specific parser.
- If keeping the custom parser, add fixtures for comments, duplicate keys, quoted commas, colons, empty values, arrays, booleans, and multiline strings.

### BH9000-011: `GetBlogInfo` can throw on missing directories and uses wall-clock time directly

Prompt: "Make blog-info tolerant of missing `_posts`/`_drafts` and use the injected `TimeProvider` consistently."

Evidence:

- `LoadYamlHeaderForAllPosts` enumerates both directories without existence checks: `BlogHelper9000.Core/Helpers/PostManager.cs:56-66`.
- `BlogService.GetBlogInfo` uses `DateTime.Now` instead of `_timeProvider`: `BlogHelper9000.Core/Services/BlogService.cs:113-114`.
- MCP serializes `DaysSinceLastPost = info.DaysSinceLastPost.Days`, which returns `0` when no post exists: `BlogHelper9000.Mcp/Tools/GetBlogInfoTool.cs:14-31`.

Risk:

Fresh/incomplete blogs can fail instead of producing an empty summary. Time-dependent tests and behavior are less deterministic.

Suggested follow-up:

- Return empty lists for missing folders.
- Use `_timeProvider.GetLocalNow()`.
- Represent "no last post" explicitly in MCP JSON.

### BH9000-012: Image processing does not dispose images and may write metadata before image save succeeds

Prompt: "Dispose ImageSharp image objects and make featured-image metadata updates happen only after the image file is successfully written."

Evidence:

- `Image.LoadAsync` results are not disposed: `BlogHelper9000.Imaging/ImageProcessor.cs:21-28`.
- Metadata is updated and the markdown file is rewritten before `SaveAsWebpAsync`: `BlogHelper9000.Imaging/ImageProcessor.cs:38-48`.
- The save directory is assumed to exist: `BlogHelper9000.Imaging/ImageProcessor.cs:42-47`.

Risk:

Repeated image operations can retain native/managed resources longer than needed. If WebP save fails, the post can point at an image that does not exist.

Suggested follow-up:

- Use `await using`/`using` around `Image` instances as appropriate for the ImageSharp version.
- Ensure the image directory exists.
- Save the image first to a temp path, then update markdown, then move/replace.

### BH9000-013: `Stream.Null` failure signaling is inconsistent

Prompt: "Replace `Stream.Null` as an image-load failure sentinel with a result type or exception contract."

Evidence:

- `UnsplashClient.LoadImageAsync` returns `Stream.Null` on failure: `BlogHelper9000.Imaging/UnsplashClient.cs:32-33`.
- MCP checks for `Stream.Null`: `BlogHelper9000.Mcp/Tools/AddFeaturedImageTool.cs:24-28`.
- CLI and TUI pass the stream directly to `ImageProcessor`, where `Image.LoadAsync(Stream.Null)` will fail later: `BlogHelper9000/Commands/AddImageCommand.cs:25-26`, `BlogHelper9000.Tui/Commands/BlogCommands.cs:367-376`.

Risk:

Failure handling differs by surface. Users may see a generic image-processing error instead of an actionable credential/network message.

Suggested follow-up:

- Return `Task<Result<Stream, ImageLoadError>>` or throw a typed exception.
- Handle the same failure contract in CLI, TUI, and MCP.

### BH9000-014: Neovim RPC requests can hang indefinitely and writes are not serialized

Prompt: "Add request timeouts/cancellation and serialize writes to the MsgPack-RPC stream."

Evidence:

- `RequestAsync` awaits the task completion source with no timeout or caller cancellation: `BlogHelper9000.Nvim/Rpc/MsgPackRpcClient.cs:51-65`.
- Concurrent key sends are started with `Task.Run` and can call `_input.WriteAsync` concurrently: `BlogHelper9000.Tui/Views/NvimEditorView.cs:197-208`, `BlogHelper9000.Tui/Views/NvimEditorView.cs:311-324`.

Risk:

If Neovim stops responding, UI operations can accumulate hung tasks. Concurrent writes to the same stream risk interleaved frames under load.

Suggested follow-up:

- Add a `SemaphoreSlim` around writes.
- Accept cancellation tokens on RPC APIs.
- Add per-request timeout and remove pending requests on timeout.

### BH9000-015: MessagePack typeless deserialization is broader than needed

Prompt: "Avoid `TypelessObjectResolver` for Neovim MsgPack-RPC data unless there is a proven need for typeless object activation."

Evidence:

- The RPC client deserializes Neovim output using `TypelessObjectResolver`: `BlogHelper9000.Nvim/Rpc/MsgPackRpcClient.cs:29-33`, `BlogHelper9000.Nvim/Rpc/MsgPackRpcClient.cs:106-108`.

Risk:

Typeless deserialization is generally inappropriate for untrusted data because it can deserialize type-annotated payloads. Neovim is local, but plugins and opened project files can influence editor behavior, so the parser should be as narrow as possible.

Suggested follow-up:

- Use primitive/object resolvers that do not instantiate arbitrary CLR types.
- Add tests for extension types and redraw messages to prove the narrower resolver still works.

### BH9000-016: Neovim grid/event handling trusts event coordinates too much

Prompt: "Bounds-check Neovim UI events before applying them to the grid and keep the event loop alive after malformed events."

Evidence:

- `ApplyLine` writes `_cells[line.Row, col]` without validating row: `BlogHelper9000.Nvim/Grid/NvimGrid.cs:92-113`.
- `ApplyScroll` trusts top/bottom/left/right coordinates: `BlogHelper9000.Nvim/Grid/NvimGrid.cs:115-150`.
- `NvimEditorView.ProcessUiEvents` catches exceptions around the whole loop, so one unexpected grid exception ends event processing: `BlogHelper9000.Tui/Views/NvimEditorView.cs:213-280`.

Risk:

An unexpected redraw event can stop rendering updates for the rest of the session.

Suggested follow-up:

- Validate event coordinates and clamp or ignore invalid events.
- Catch/log per event, not around the entire stream loop.

### BH9000-017: No-nvim editor mode appears unable to save edits

Prompt: "Either implement save support for `EditorSurface` or present it as read/edit preview only."

Evidence:

- `EditorSurface` loads file text into a `TextView` and supports editing commands: `BlogHelper9000.Tui/Views/EditorSurface.cs:38-67`.
- There is no save/write method, modified tracking, or menu command wired for the fallback editor.

Risk:

The advertised safe-mode editor can give users a place to type changes that are never persisted.

Suggested follow-up:

- Add save/reload/dirty-state support.
- Warn before navigating away from unsaved changes.
- Add integration tests for safe-mode editing persistence.

## Low / Hygiene / Performance Issues

### BH9000-018: CLI, Core service, and TUI duplicate command logic

Prompt: "Move command behavior into `IBlogService` or small application services and have CLI/TUI/MCP call the same implementation."

Evidence:

- Publish exists in both `BlogService` and `PublishCommand`: `BlogHelper9000.Core/Services/BlogService.cs:48-77`, `BlogHelper9000/Commands/PublishCommand.cs:14-50`.
- Fix metadata exists in both `BlogService` and `FixCommand`: `BlogHelper9000.Core/Services/BlogService.cs:79-174`, `BlogHelper9000/Commands/FixCommand.cs:18-93`.
- Add post exists in both `BlogService` and `AddCommand`: `BlogHelper9000.Core/Services/BlogService.cs:25-46`, `BlogHelper9000/Commands/AddCommand.cs:26-59`.

Risk:

Bug fixes and validations will drift across surfaces.

Suggested follow-up:

- Make CLI handlers thin wrappers over `IBlogService`.
- Share service-registration extension methods across CLI, TUI, and MCP.

### BH9000-019: Font loading uses a static collection and reloads resources per processor instance

Prompt: "Make font loading idempotent and thread-safe."

Evidence:

- `FontCollection` is static, but every `FontManager` constructor iterates resources and adds fonts again: `BlogHelper9000.Imaging/FontManager.cs:10-22`.

Risk:

Repeated construction can duplicate state and create avoidable startup/memory cost. Static mutable collections also need clear thread-safety boundaries.

Suggested follow-up:

- Use `Lazy<FontCollection>` or a singleton `FontManager`.
- Add a test that repeated construction does not duplicate or throw.

### BH9000-020: TUI async fire-and-forget paths make state and tests timing-dependent

Prompt: "Introduce explicit async command workflows or task tracking for TUI background operations."

Evidence:

- Several operations use `_ = Task.Run(...)`: `BlogHelper9000.Tui/Program.cs:97-102`, `BlogHelper9000.Tui/Views/BlogWorkspaceWindow.cs:230-240`, `BlogHelper9000.Tui/Views/NvimEditorView.cs:197-208`, `BlogHelper9000.Tui/Commands/BlogCommands.cs:361-385`.
- Tests wait with fixed delays: `BlogHelper9000.Tui.Tests/Commands/BlogCommandsAddImageTests.cs:92-99`, `BlogHelper9000.Tui.Tests/Commands/BlogCommandsAddImageTests.cs:140-145`.

Risk:

Failures can be visible only in logs, operations can overlap unexpectedly, and tests can be flaky on slower machines.

Suggested follow-up:

- Return tasks from command methods where possible.
- Use completion signals in tests instead of `Task.Delay`.
- Surface background failures to the UI.

### BH9000-021: Build and documentation versions are drifting

Prompt: "Centralize dependency versions and update README/CI to match project files."

Evidence:

- README tech-stack versions do not match project references for Terminal.Gui, ImageSharp, SixLabors.Fonts, TimeWarp.Nuru, Spectre.Console, and System.IO.Abstractions: `README.md:286-301` compared with project files.
- `global.json` allows latest major roll-forward and prerelease SDKs: `global.json:2-6`.
- CI uses a specific .NET 10 RC value and marks setup-dotnet `continue-on-error: true`: `.github/workflows/main.yml:23-48`.

Risk:

Developers and CI may build with different SDKs/dependencies than documented. `continue-on-error` can mask SDK setup failures.

Suggested follow-up:

- Remove `continue-on-error` from SDK setup unless there is a documented fallback.
- Align `global.json`, CI, and README.
- Consider `Directory.Packages.props` for central package management.

### BH9000-022: CI/security automation has gaps

Prompt: "Add reliable dependency auditing and make CodeQL/Cake behavior match the solution's build requirements."

Evidence:

- Dependabot is configured for NuGet only at root: `.github/dependabot.yml:6-11`.
- CodeQL C# is configured with `build-mode: none`: `.github/workflows/codeql.yml:42-50`.
- CI invokes Cake tests but does not show a vulnerability/audit step: `.github/workflows/main.yml:45-54`.
- The imaging project requires `SIXLABORS_LICENSE_KEY` at build time: `BlogHelper9000.Imaging/BlogHelper9000.Imaging.csproj:12-18`.

Risk:

Dependency vulnerabilities may be missed until manually checked. CodeQL may have less semantic context than a built analysis. CI may fail for external contributors or behave differently depending on license-key availability.

Suggested follow-up:

- Add `dotnet list package --vulnerable --include-transitive` or `dotnet restore /p:NuGetAudit=true` as a CI step.
- Decide whether CodeQL should use manual build mode.
- Document or conditionally handle the SixLabors license requirement in CI and forks.
- Add Dependabot entries for GitHub Actions and possibly dotnet tools.

### BH9000-023: Generated/user-local files are tracked

Prompt: "Review tracked IDE/user files and exclude local machine state from source control."

Evidence:

- `BlogHelper9000.sln.DotSettings.user` is tracked.
- `.vscode` files under root and `BlogHelper9000/.vscode` are tracked.

Risk:

User-specific settings can create noisy diffs or leak local assumptions. This is low severity but worth cleaning up.

Suggested follow-up:

- Decide which editor settings are intentional team defaults.
- Ignore `.DotSettings.user` and any machine-local IDE files.

### BH9000-024: Test suite passes but misses edge and integration coverage

Prompt: "Add regression tests around the risky paths discovered in this review."

Evidence:

- Current tests pass, but many tests instantiate handlers directly rather than through real DI/CLI host wiring.
- CLI help tests in `FixCommandTests` are commented out but still parameterized: `BlogHelper9000.Tests/Commands/FixCommandTests.cs:23-48`.
- Some tests have unused theory parameters or fixed sleeps, producing analyzer warnings.

Risk:

Important runtime and edge-case behavior can regress while tests remain green.

Suggested follow-up:

- Add host-level CLI command tests.
- Add path traversal, YAML preservation, malformed front matter, publish collision, and no-nvim save tests.
- Clean up skipped/commented tests and analyzer warnings.

## Additional Findings (Validation Pass, 2026-07-03)

### BH9000-025: Front-matter round-trip corrupts Jekyll `published:` and `date:` semantics (High)

Prompt: "Model `published` as Jekyll's boolean, keep post dates in `date:`, and read/write ISO 8601 dates so a round-trip through the tool preserves Jekyll semantics."

Evidence:

- `published` maps to `DateTime? PublishedOn`, not a boolean: `BlogHelper9000.Core/YamlParsing/YamlHeader.cs:24-25`.
- `published: false` (Jekyll's standard way to keep a post unpublished) is parsed to `DateTime.MinValue`: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs:79-86`.
- The serializer writes any non-null date back as `dd/MM/yyyy`: `BlogHelper9000.Core/YamlParsing/SerialiserBase.cs:8`, `BlogHelper9000.Core/YamlParsing/YamlSerialiser.cs:27-30`. So after any metadata operation, `published: false` becomes `published: 01/01/0001`.
- Jekyll treats any value other than `false` as truthy, so that rewrite silently publishes a deliberately unpublished post.
- Standard Jekyll `date:` is an unknown key, so it lands in `Extras` and is destroyed by any update (see BH9000-002), changing permalinks and ordering.
- `dd/MM/yyyy` is not ISO 8601; Jekyll parses front-matter dates as ISO, so round-tripped dates can be month/day-swapped or rejected at site build time.

Risk:

Any publish, fix, or add-image operation on a real Jekyll blog can silently take drafts live, change post URLs, or corrupt dates. This is concrete data corruption on top of the general parser narrowness in BH9000-010.

Suggested follow-up:

- Treat `published` as `bool?` and store the post date in `date:`.
- Read and write ISO 8601 (`yyyy-MM-dd` and `yyyy-MM-dd HH:mm:ss zzz` variants).
- Add round-trip fixtures using front matter copied from real Jekyll posts, asserting byte-level equivalence of semantics-bearing keys.

### BH9000-026: `add` silently discards requested tags and corrupts existing posts on title collision (High)

Prompt: "Plumb the tags parameter through post creation and refuse to create a post whose target file already exists."

Evidence:

- `AddCommand` accepts a `Tags` parameter but assigns `Tags = []` with the real code commented out: `BlogHelper9000/Commands/AddCommand.cs:12-13`, `BlogHelper9000/Commands/AddCommand.cs:49`.
- `BlogService.AddPost` has no tags parameter at all and also hardcodes `Tags = []`: `BlogHelper9000.Core/Services/BlogService.cs:25-42`.
- Both paths write with `AppendAllText`, so if the computed path already exists, a second `---` front-matter block is appended to the existing post: `BlogHelper9000/Commands/AddCommand.cs:58`, `BlogHelper9000.Core/Services/BlogService.cs:42`.
- The MCP `add_post` tool exposes no tags parameter either: `BlogHelper9000.Mcp/Tools/AddPostTool.cs`.

Risk:

User-supplied tags are silently lost on every surface. Re-adding a title that slugifies to an existing filename corrupts that post with a duplicate YAML header, which the parser then reads incorrectly.

Suggested follow-up:

- Add a tags parameter to `IBlogService.AddPost` and wire it through CLI, TUI, and MCP.
- Check for the target file and fail (or prompt) instead of appending; use create-new semantics rather than `AppendAllText`.
- Add tests for tag round-trip and duplicate-title collision.

### BH9000-027: Publish is not idempotent and contains a redundant post-move delete (Medium)

Prompt: "Make publish reject already-published posts and remove the dead `File.Delete` after the move."

Evidence:

- Both implementations call `File.Move(currentPath, replacementPath)` and then `File.Delete(currentPath)`: `BlogHelper9000.Core/Services/BlogService.cs:73-74`, `BlogHelper9000/Commands/PublishCommand.cs:39-40`. After a successful move the source no longer exists, so the delete is dead code that masks intent and would remove the wrong file in partial-failure scenarios.
- `TryFindPost` happily matches posts already under `_posts`: `BlogHelper9000.Core/Helpers/PostManager.cs:114-130`, so publishing an already-published post moves it into `_posts/<year>/` with a second date prefix (`2026-07-03-2020-01-15-title.md`) and rewrites its metadata again.

Risk:

Running publish twice (easy from MCP, where an AI agent may retry) mangles filenames, changes URLs, and relocates published history. The redundant delete complicates reasoning about failure recovery (relevant to BH9000-008).

Suggested follow-up:

- Refuse to publish a post whose filename already carries a date prefix or whose metadata says `IsPublished` with a real `PublishedOn`.
- Delete the `File.Delete(currentPath)` lines.
- Add an idempotency test: publish twice, assert the second call is a no-op or a clear error.

### BH9000-028: NvimGrid is mutated on a background thread while the UI thread draws it (Medium)

Prompt: "Synchronize NvimGrid access between the UI event loop and Terminal.Gui's draw path."

Evidence:

- Grid events are applied on a background task: `BlogHelper9000.Tui/Views/NvimEditorView.cs:106` (event loop started with `Task.Run`), `BlogHelper9000.Tui/Views/NvimEditorView.cs:271` (`_grid.ApplyEvent(evt)` runs on that task).
- `OnDrawingContent` reads `_grid` dimensions and cells on the UI thread with no synchronization: `BlogHelper9000.Tui/Views/NvimEditorView.cs:138-171`.
- `NvimGrid.Resize` swaps the `_cells` array and updates `Width`/`Height` non-atomically: `BlogHelper9000.Nvim/Grid/NvimGrid.cs:47-67`.

Risk:

A resize or heavy redraw racing with a draw pass can read torn state: `IndexOutOfRangeException` (old bounds against a new smaller array) or garbled frames. This is distinct from BH9000-016, which is about trusting event coordinates; this race exists even for well-formed events.

Suggested follow-up:

- Apply grid events on the UI thread via `Application.Invoke`, or guard grid reads/writes with a lock.
- In the draw path, snapshot width/height/cells consistently.
- Add a stress test interleaving resize events with reads.

### BH9000-029: `LoadYamlHeaderForAllPosts` bypasses `IFileSystem` and throws on duplicate extras keys (Low)

Prompt: "Use the injected `IFileSystem` for file metadata in `LoadYamlHeaderForAllPosts` and make the extras additions collision-safe."

Evidence:

- `new FileInfo(f)` bypasses the `IFileSystem` abstraction the rest of the class uses: `BlogHelper9000.Core/Helpers/PostManager.cs:72`. Under `MockFileSystem` in tests this silently returns default timestamps instead of the mock's values.
- `header.Extras.Add("originalFilename", ...)` / `Add("lastUpdated", ...)` throw `ArgumentException` if a post's front matter already contains those keys: `BlogHelper9000.Core/Helpers/PostManager.cs:73-74`.
- The local variables are swapped: `posts` enumerates `Drafts` and `drafts` enumerates `Posts`: `BlogHelper9000.Core/Helpers/PostManager.cs:60-61`.

Risk:

`info`/`list_drafts`/`get_blog_info` crash on posts containing those key names; tests assert against wrong timestamps without noticing; the swapped names invite future logic errors.

Suggested follow-up:

- Use `_fileSystem.FileInfo.New(f)`.
- Assign via the indexer (`Extras["originalFilename"] = ...`) instead of `Add`.
- Rename the swapped locals.

