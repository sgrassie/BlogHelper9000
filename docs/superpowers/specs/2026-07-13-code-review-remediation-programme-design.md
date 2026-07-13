# Code Review Remediation Programme Design

**Date:** 2026-07-13

**Status:** Approved

## Purpose

This programme addresses the security, data-integrity, performance, MCP, CLI, TUI, and Neovim findings from the July 2026 code review. The work is deliberately split into four implementation plans so each stage leaves the solution working and independently reviewable.

The central design decision is that safety rules belong in Core. CLI, MCP, and TUI must consume the same path validation, publication orchestration, persistence, schedule, and front-matter contracts instead of applying host-specific fixes around unsafe Core behaviour.

## Programme structure

The work will be specified and implemented in this order:

1. Core security, persistence, scheduling, performance, and removal of the xlsx importer.
2. MCP startup policy, protocol correctness, safe results, limits, and stdio integration tests.
3. CLI routes, configuration, exit codes, credentials, and schedule-aware mutation guards.
4. TUI and Neovim editing safety, concurrency, shutdown, logging, and rendering.

Each stage must pass its targeted tests and the full solution suite before the next begins. Later stages may rely on interfaces introduced by earlier stages, but no stage may leave a host using a compatibility shim that bypasses the Core policy.

## Shared constraints

- Target .NET 10 and C# latest.
- Continue using `System.IO.Abstractions` for ordinary filesystem tests.
- Add real-filesystem tests for symbolic-link, atomic-replacement, and permission behaviour that `MockFileSystem` cannot represent.
- Use xunit v3, FluentAssertions, and NSubstitute consistently with the existing suite.
- Keep `AGENTS.md` and `CLAUDE.md` identical whenever either changes.
- Do not expose exception messages, absolute host paths, credentials, or MCP framing data through public results.
- Do not create `.bloghelper.db` implicitly. Its absence represents damaged or incomplete blog state.
- Preserve dry-run-first behaviour for MCP bulk metadata changes.
- Prefer typed outcomes for expected failures. Exceptions are reserved for unexpected or infrastructure failures and are translated at host boundaries.
- All new mutation paths must be safe under concurrent MCP calls.

## 1. Core and scheduling foundation

### 1.1 Canonical post paths

Post lookup will accept only literal Markdown files beneath canonical `_drafts` or `_posts` directories.

The resolver must:

- reject `*`, `?`, null characters, and other search-pattern input;
- require the `.md` extension;
- resolve rooted and relative paths without allowing `..` or sibling-prefix escapes;
- reject any existing symbolic-link or reparse-point component for mutation targets;
- validate the final existing file and the output parent immediately before opening;
- search bare filenames by ordinal filename comparison rather than passing user input as an enumeration pattern;
- return an ambiguity result when more than one exact basename exists;
- distinguish not found, outside blog, unsupported extension, link escape, ambiguous name, and malformed post outcomes.

Explicit paths are limited to `_drafts` and `_posts`. A front-matter file elsewhere in the blog is not a post.

Reads may follow the same strict policy as writes. There is no requirement to expose arbitrary blog-root files through post APIs.

### 1.2 Front-matter abstraction and supported subset

The hand-written YAML implementation remains in place. It will sit behind an `IFrontMatterCodec` interface consumed by `MarkdownHandler` and post-loading services. The concrete implementation will be named `YamlFrontMatterCodec` and will continue to use focused hand-written parsing and serialisation code.

The supported format is intentionally narrower than complete YAML:

- one top-level mapping;
- scalar string values;
- nullable booleans;
- publication dates in the blog's existing format;
- `published: false`, `published: true`, and `published: draft` placeholders;
- tag sequences in the existing flow form and a simple block-sequence form;
- blank lines and comments between mapping entries.

The implementation will not add nested mappings, anchors, aliases, arbitrary YAML tags, merge keys, or general comment/format round-tripping. If compatibility testing finds that the real blog requires those features, work on that task stops and the design is revisited with YamlDotNet as the replacement. The custom parser must not grow into a partial general-purpose YAML implementation.

Serialisation must quote and escape every generated string and tag value. Metadata accepted through Core, MCP, or CLI must reject CR, LF, and front-matter delimiter injection when the field contract is single-line. Duplicate keys, malformed delimiters, unsupported structures, and invalid typed values return a typed parse error that names the file and safe reason.

Publication placeholders remain distinct from dates during a read/write round trip. Setting a real publication date clears the placeholder. A metadata or image rewrite must never convert `published: false` into `01/01/0001` or otherwise change its meaning.

File parsing will stream only through the closing front-matter delimiter. It will handle LF, CRLF, and CR consistently. Front matter is limited to 256 KiB and 512 lines. Exceeding either limit returns a typed size error.

Aggregate operations such as blog information and post listing isolate malformed or unsupported files. They return valid entries plus per-file diagnostics instead of failing the whole request.

### 1.3 Atomic persistence

Core will provide one atomic file-writing abstraction used by post creation, metadata/body replacement, publishing, and image metadata updates.

For replacement, the writer creates a sibling temporary file, writes and flushes the complete content, preserves relevant permissions, and atomically replaces or renames the destination. Temporary files are removed after failure. Creation uses create-new semantics so two same-title calls cannot append or overwrite one another.

Publishing stages the complete published file in the target year directory before committing it. The source draft is removed only after the target is durable. If source removal fails, the result reports a recoverable duplicate rather than losing either copy.

Image generation stages the WebP before replacing an existing image. Metadata is prepared before the final commit, and failures clean up staged files. Because two filesystem files cannot be committed in one portable transaction, the operation favours preserving the previous post and image over hiding a partial failure.

### 1.4 Publication orchestration

Core will expose one publication orchestration service used by CLI, MCP, and TUI. It coordinates post publication and schedule marking while preserving the distinction between them.

The result records:

- publication outcome and blog-relative published path;
- source cleanup outcome;
- schedule outcome;
- whether retrying schedule marking is safe.

A successful file publication is always reported as such even if schedule marking fails. Hosts must not translate a schedule failure into "post was not published". Schedule marking remains idempotent.

Publishing requires an existing valid schedule database. The orchestration service checks this before staging or mutating the post.

Creating a non-draft uses the same dated publication logic. It produces `_posts/<year>/yyyy-MM-dd-<slug>.md` with a matching publication value; it never creates `_posts/<slug>.md` and reports success.

### 1.5 Schedule database integrity

Schedule operations use short-lived connections rather than one process-lifetime `SqliteConnection`. Multi-statement changes use explicit transactions. Connection strings are built with `SqliteConnectionStringBuilder`.

Schema version 2 adds a global unique constraint for the normalised draft filename using the intended case behaviour. Migration preflights duplicate filenames and fails without changing the database if duplicates exist. The error lists the conflicting filenames and series; migration never chooses a winner.

Adding a series and its first entry is one transaction. Position allocation and duplicate protection rely on database constraints, with constraint failures mapped to typed outcomes. Dashboard and next-post reads avoid one query per series and do not load every schedule row merely to select one result.

Database dates use invariant `yyyy-MM-dd` formatting and exact invariant parsing. A database with a future unsupported schema version is rejected rather than opened optimistically.

Missing `.bloghelper.db` is always an error. Core exposes a validation operation that distinguishes missing, unreadable, corrupt, migration-conflict, and unsupported-version states. No read or write operation creates the database as a side effect.

### 1.6 Remove the one-time schedule importer

The following production surface is removed:

- `ScheduleImportCommand` and its Nuru route;
- `ScheduleXlsxImporter`, `ImportSummary`, and all importer tests;
- ExcelDataReader and ExcelDataReader.DataSet references;
- the explicit System.Text.Encoding.CodePages reference used by the importer;
- ClosedXML from the test project when no remaining test uses it;
- documentation and MCP text telling users to run `schedule-import`;
- importer-specific gotchas in `AGENTS.md` and `CLAUDE.md`.

Missing-database messages instruct the user to restore `.bloghelper.db` from the authoritative backup or repository state. They do not offer a creation or import command.

### 1.7 SQLite package advisory

Plan 1 upgrades `Microsoft.Data.Sqlite` from 10.0.0 to 10.0.9 for normal servicing. It does not directly override SQLitePCLRaw or claim to remediate `GHSA-2m69-gcr7-jv3q`.

The unresolved native advisory is accepted technical debt until the Microsoft package adopts a safe native bundle. The exact advisory receives a documented `NuGetAuditSuppress`; high and critical audit warnings other than that suppression fail CI. The suppression must name the advisory and explain the removal condition. It must not suppress a package, severity, or warning class broadly.

## 2. MCP server

### 2.1 Startup validation

Before starting the stdio protocol loop, the MCP host validates:

- the configured base directory;
- that the directory contains `_drafts` or `_posts`;
- that `.bloghelper.db` exists;
- that the database is readable, structurally valid, migrated, and not newer than the supported schema.

Any failure writes a concise diagnostic to stderr and exits non-zero. The host does not initialise MCP or advertise tools in a degraded state. Publishing without schedule tracking is therefore impossible through MCP.

### 2.2 Protocol-native results

Tool methods use a shared result factory that returns `CallToolResult` with structured content. Successful calls set or imply `isError: false`. Validation, conflict, missing-state, stale-version, parsing, size, and other business failures set `isError: true` and include a stable public error code.

Unexpected exceptions are logged with full detail to stderr and return a generic safe error. Tool code does not catch an exception merely to return `ex.Message`.

Success and error results use blog-relative paths. The configured absolute base directory is not returned by ordinary tools. A future explicit local diagnostic command may expose it, but that is outside this programme.

### 2.3 Optimistic concurrency

`get_post` returns a SHA-256 version derived from the exact file bytes. `update_post`, `publish_post`, and `add_featured_image` require `expectedVersion`. Core rechecks the version immediately before mutation and returns a stale-version error when it differs.

This is an accepted breaking change to the pre-1.0 MCP tool contract. Server instructions describe the required get, review, mutate flow.

Bulk metadata remains dry-run-first. Its apply mode accepts a preview token derived from the selected options and the versions of files in the preview, so an apply call cannot silently operate on a changed set.

### 2.4 Limits and cancellation

The MCP boundary enforces these fixed limits at runtime:

- post body: 8 MiB of UTF-8;
- title and image query: 256 characters;
- description and notes: 4,096 characters;
- tags: 100 entries, each no more than 128 characters;
- list page size: 1 to 100;
- remote image response: 25 MiB;
- image dimensions: no more than 8,192 pixels on either axis and no more than 40 megapixels decoded.

An existing local post larger than the MCP body limit remains usable by CLI and TUI. MCP returns a size error and does not partially return or mutate it.

Image HTTP calls use response-headers-read streaming, validate the returned host against the expected Unsplash/CDN hosts, and stop at the byte limit. Cancellation flows through download, decode, processing, save, and tool execution.

### 2.5 Tool metadata and integration tests

`publish_post` is marked destructive. All read-only, destructive, idempotent, and open-world annotations are reviewed against actual behaviour and locked by tests.

An stdio integration-test fixture starts the real MCP executable and verifies:

- missing-database startup failure and stderr diagnostics;
- successful initialisation and tool discovery;
- input/output schemas and annotations;
- structured success and `isError` failure results;
- no stdout contamination;
- blog-relative result paths;
- cancellation and size failures;
- add, get, stale update rejection, successful update, publish, and automatic schedule marking.

This programme does not add MCP resources, prompts, HTTP transport, or experimental tasks.

## 3. CLI

### 3.1 Configuration

The CLI resolves its blog directory in this order:

1. `--base-directory` or `--base-directory=<path>`;
2. `BLOG_BASE_DIRECTORY`;
3. `BlogHelperOptions:BaseDirectory` from configuration;
4. the current directory.

The resolved path is validated before Core post services are constructed. Empty, inaccessible, or non-blog paths return a concise non-zero error rather than an unhandled exception.

### 3.2 Routes and exit outcomes

Explicit Nuru routes are restored for `add-image`, `fix`, `info`, and `unsplash-credentials`. `schedule-import` is removed. A host-level test enumerates the intended public command set so handler-only tests cannot mask missing routes.

Expected command failures use a typed CLI outcome or exception translated once at the host boundary. Exit codes are stable:

- `0`: success;
- `1`: operational failure, conflict, missing post, missing database, or failed mutation;
- `2`: invalid arguments, date, path, or other usage error.

Errors are printed once. Logging an error and returning `Unit` is not a failure mechanism.

### 3.3 Schedule policy

The CLI remains available without `.bloghelper.db` for non-schedule maintenance.

The following validate the database before any file mutation:

- `publish`;
- `add --series`;
- `schedule-list`;
- `schedule-show`;
- `schedule-stats`;
- `schedule-mark`.

`add` without a series, `info`, `fix`, `add-image`, and `unsplash-credentials` remain available. Dates use exact invariant `yyyy-MM-dd` parsing. Invalid dates never become today or null silently.

### 3.4 Credentials

The unused Unsplash secret is removed. The command stores only the access key consumed by `UnsplashClient`.

The access key comes from one of:

- `UNSPLASH_ACCESS_KEY`;
- redirected stdin;
- a masked interactive prompt.

It is never a positional command argument. On Unix, the directory and file are created with restrictive permissions before content is written. Windows uses a user-only file location and the current user's ACL. No credential value is logged.

### 3.5 CLI integration tests

Host-level tests execute each route through Nuru and verify configuration precedence, exit codes, exact date validation, missing-database guards before mutation, valid non-draft creation, credential input without command-line exposure, and removal of `schedule-import`.

## 4. TUI and Neovim

### 4.1 Shared editor session

Neovim and the fallback editor implement one editor-session contract exposing:

- active path;
- dirty state;
- save;
- reopen or rebind to a new path;
- close validation;
- disposal.

Publishing an open draft prompts save, discard, or cancel before mutation. Save writes the latest buffer first; discard publishes the current on-disk file; cancel makes no change. After successful publication, the editor reopens the published path and the file browser refreshes.

Quit applies the same save/discard/cancel policy to every modified buffer. Neovim does not receive `qa!` until the user has resolved modified buffers. The fallback editor follows the same behaviour.

TUI publishing uses the shared Core publication orchestrator and requires a valid schedule database. Editing, `fix`, `add-image`, and other non-schedule maintenance remain usable without it.

### 4.2 Ordered Neovim input

All Neovim input and edit actions enter one bounded FIFO channel consumed by one sender task. The queue preserves order and applies backpressure instead of creating one `Task.Run` per keystroke. Shutdown completes or cancels the sender deterministically.

### 4.3 UI-thread redraw ownership

The notification processor parses Neovim events off-thread but accumulates redraw operations until the `flush` event. It then schedules one batch on the Terminal.Gui thread. Grid dimensions, cells, highlight attributes, cursor, colours, and mode state are mutated and read only on that thread.

Resize requests are coalesced. A resize cannot interleave with drawing a previous grid snapshot.

### 4.4 Shutdown, logging, and long operations

Shutdown uses one shared three-second deadline for the quit RPC and process exit. When the deadline expires, disposal terminates the process tree. A thirty-second RPC timeout cannot run before the shutdown deadline.

Console logging is disabled while Terminal.Gui owns the terminal. Logs go to a terminal-safe file sink, with user-visible errors and progress shown in the TUI.

Bulk metadata and image work run asynchronously with cancellation and visible progress. Conflicting mutations are serialised. Returned streams are disposed by their owning command.

### 4.5 Rendering and navigation

Grid text is decoded with `System.Text.Rune` rather than indexing a UTF-16 code unit. Continuation cells continue to control wide-character layout.

Key translation composes Ctrl, Alt, Shift, and Unicode consistently. Ctrl+Alt and Alt+Unicode receive explicit tests.

The file browser shows paths relative to `_posts` or `_drafts` when basenames collide, so open, move, and delete targets are distinguishable.

### 4.6 TUI and Neovim tests

Tests cover:

- dirty-buffer publish and quit in both editor modes;
- published-path rebinding;
- schedule marking and missing-database refusal;
- rapid ordered input and bounded-queue backpressure;
- redraw, flush, draw, and resize interleaving;
- shared shutdown deadline and force-kill fallback;
- terminal-safe logging;
- cancellation and progress for long operations;
- emoji, other non-BMP text, modifier combinations, and duplicate basenames.

## Verification and delivery

Every implementation task follows a red, green, refactor cycle and ends with a focused commit. Every plan ends with:

1. targeted project tests;
2. `dotnet test BlogHelper9000.sln`;
3. NuGet vulnerability audit with only the documented SQLite advisory suppressed;
4. a host-specific smoke test;
5. confirmation that unrelated working-tree changes remain untouched.

The programme's final verification also:

- packs both .NET tools in Release configuration;
- runs an end-to-end temporary-blog add, read, update, publish, and schedule workflow;
- verifies MCP stdout contains only JSON-RPC framing;
- verifies missing `.bloghelper.db` blocks MCP startup;
- verifies missing `.bloghelper.db` blocks every schedule-aware CLI/TUI mutation before file changes;
- verifies permitted non-schedule CLI/TUI maintenance still works;
- verifies all importer code, packages, commands, tests, and documentation references are gone.

## Explicit non-goals

- Replacing the hand-written front-matter codec with YamlDotNet in this programme.
- Implementing complete YAML.
- Recreating schedule import or adding another database bootstrap command.
- Adding MCP resources, prompts, HTTP transport, or experimental tasks.
- Adding schedule management UI to the TUI.
- Fixing the native SQLite advisory through a direct provider override before the Microsoft package adopts a suitable bundle.
- Refactoring unrelated CLI, TUI, or Neovim architecture.
