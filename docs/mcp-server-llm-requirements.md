# BlogHelper9000.Mcp — LLM Integration Requirements

Date: 2026-07-03
Status: Implemented (2026-07-03) — all 12 requirements (MCP-1 through MCP-12) are in place; `dotnet test BlogHelper9000.sln` is green (Mcp.Tests 33/33, full solution 233/233 + 2 pre-existing skips). One out-of-scope gap was found and spun off separately: `PostManager.TryFindPost` cannot resolve a bare filename against a post nested in `_posts/<year>/`, only its full path — see the flagged follow-up task.
Audience: an implementing engineer or coding agent. Every requirement includes acceptance criteria; implementation notes give exact file paths and code sketches. Follow the repo's existing conventions: xUnit v3 + FluentAssertions + NSubstitute, `MockFileSystem` via `System.IO.Abstractions`, thin tools delegating to `IBlogService`/`PostManager` in Core.

---

## 1. Background and evidence

`BlogHelper9000.Mcp` is a stdio MCP server (ModelContextProtocol **1.4.0**, `Host.CreateApplicationBuilder`, `WithToolsFromAssembly`) exposing six tools: `add_post`, `publish_post`, `fix_metadata`, `get_blog_info`, `list_drafts`, `add_featured_image`. It is packed as a dotnet tool (`bloghelper-mcp`, nupkg in `BlogHelper9000.Mcp/releases/`).

The investigation inspected the tool schemas as an actual LLM client sees them (via a live MCP connection to the packaged server) and the source in `BlogHelper9000.Mcp/`. Key evidence:

- The live server's `add_post` schema **lacks the `tags` parameter that exists in current source** — the packaged 1.0.0 nupkg is stale relative to the repo (see MCP-10).
- `Program.cs` never configures logging, so `Host.CreateApplicationBuilder`'s default console provider **logs to stdout — the same stream carrying JSON-RPC frames** (see MCP-1).
- Tool return values are an inconsistent mix of prose (`"Created draft at: …"`), bare JSON, and **state-dependent shapes** (`list_drafts` returns the prose string `"No drafts found."` when empty but a JSON array otherwise) (see MCP-3).
- `publish_post` reports every `null` from `BlogService.PublishPost` as *"Could not find post"*, but since the 2026-07-03 remediation `null` also means *already published* and *target filename collision* — the tool actively misleads the model (see MCP-4).
- No tool carries MCP annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`), no server instructions are set, and the version is hardcoded `"1.0.0"` (see MCP-2, MCP-6).
- **An LLM cannot read or write post content through this server.** `add_post` writes front matter only; nothing returns a post's body; `MarkdownFile` in Core does not even model the body (see MCP-7, MCP-8).

SDK capability check (reflected over ModelContextProtocol 1.4.0, so all features below are real):
`McpServerToolAttribute` supports `Name`, `Title`, `Destructive`, `Idempotent`, `OpenWorld`, `ReadOnly`, `UseStructuredContent`, `OutputSchemaType`; `McpServerOptions` supports `ServerInstructions`.

## 2. Goals and non-goals

**Goal:** an LLM agent connected to this server can (a) discover what the server operates on and how, (b) perform the full draft → write → publish → decorate workflow without filesystem access, (c) always receive machine-parseable results that distinguish success from each failure mode, and (d) never corrupt or hang the protocol.

**Non-goals:** HTTP/SSE transport, authentication, MCP resources/prompts (may be considered later), multi-blog support, changing the Jekyll front-matter schema (the `published:`-as-date convention is deliberate — see the Remediation Status note in `CODE_REVIEW.md`).

---

## 3. Requirements

Priorities: **MUST** (correctness / protocol safety), **SHOULD** (major ergonomics), **MAY** (nice-to-have).

### MCP-1 (MUST): Route all logging to stderr

**Problem:** stdio MCP servers exchange JSON-RPC on stdout. `Host.CreateApplicationBuilder(args)` registers a console logger that writes to stdout by default, and the DI graph passes real loggers into `UnsplashClient`, `ImageProcessor`, and `BlogService`. Any log line emitted during a tool call can interleave with a JSON-RPC frame and desynchronize the client.

**Requirement:** In `BlogHelper9000.Mcp/Program.cs`, immediately after creating the builder:

```csharp
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
```

**Acceptance criteria:**
- Running the server and invoking a tool that logs (e.g. `add_post`) writes nothing to stdout except JSON-RPC frames; log lines appear on stderr.
- A test is impractical here; verify manually with `BLOG_BASE_DIRECTORY=/tmp/some-blog dotnet run --project BlogHelper9000.Mcp < /dev/null 1>/tmp/out 2>/tmp/err` and confirm `/tmp/out` contains no log text.

### MCP-2 (MUST): Server identity, instructions, and version

**Problem:** the server offers no orientation. An LLM discovering the tools cannot know the blog's location, the front-matter conventions (e.g. that `published:` holds a date), or which operations are batch vs single-post. The version is hardcoded and already wrong relative to behavior.

**Requirement:** in `Program.cs`:

1. Set `options.ServerInstructions` to a concise operating manual (≤ ~250 words). It must state:
   - what the server manages (a Jekyll blog at a configured base directory; drafts in `_drafts/`, published posts in `_posts/<year>/`),
   - the typical workflow (`get_blog_info` → `list_drafts`/`list_posts` → `add_post` → `get_post`/`update_post` → `publish_post` → `add_featured_image`),
   - conventions: post identifiers are filenames (e.g. `my-post.md`) or repo-absolute paths; titles are slugified to `lowercase-hyphenated` filenames; tags are comma-separated on input,
   - cautions: `fix_metadata` rewrites every post in `_posts/` (use `dryRun` first — see MCP-5); `add_featured_image` requires Unsplash credentials configured on the host machine and performs network calls.
2. Replace the hardcoded version with the assembly's informational version:

```csharp
options.ServerInfo = new()
{
    Name = "BlogHelper9000",
    Version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0"
};
```

(Anchor `typeof(Program)` by adding `internal sealed partial class Program;` at the bottom of the top-level-statements file, or use `Assembly.GetExecutingAssembly()`.)

**Acceptance criteria:** initialize response contains non-empty `instructions`; `serverInfo.version` changes when the csproj/MinVer version changes without editing `Program.cs`.

### MCP-3 (MUST): One structured response envelope for every tool

**Problem:** current outputs force the model to parse three different formats and cannot be branched on reliably. `list_drafts` even changes *shape* depending on state. Failure text (`"Failed to fix metadata: {ex.Message}"`) is indistinguishable from success without natural-language inference.

**Requirement:**

1. Create `BlogHelper9000.Mcp/ToolResponses.cs` with a generic envelope, serialized camelCase:

```csharp
public sealed record ToolResponse<T>(bool Success, string? Error, T? Data)
{
    public static ToolResponse<T> Ok(T data) => new(true, null, data);
    public static ToolResponse<T> Fail(string error) => new(false, error, default);
}
```

2. Every tool returns its typed data through this envelope and opts into MCP structured content so clients get both `structuredContent` and an auto-generated `outputSchema`:

```csharp
[McpServerTool(Name = "list_drafts", UseStructuredContent = true, ReadOnly = true, Idempotent = true)]
public static ToolResponse<DraftListResult> ListDrafts(IBlogService blogService) { ... }
```

3. Shapes per tool (define these records in `ToolResponses.cs`):
   - `list_drafts` → `DraftListResult(IReadOnlyList<string> Drafts)` — an empty list is a **success** with `drafts: []`, never prose.
   - `add_post` → `AddPostResult(string FilePath, bool IsDraft)`; on collision return `Fail("A post already exists at <path>. Choose a different title or edit the existing post.")`.
   - `get_blog_info` → `BlogInfoResult` mirroring today's anonymous object **plus** `BaseDirectory` (see MCP-9), with `DaysSinceLastPost` as `int?` (null = no published posts, and say so in the property's `[Description]`).
   - `publish_post` → `PublishResult(string? PublishedPath)` with outcome-specific errors (see MCP-4).
   - `fix_metadata` → see MCP-5.
   - `add_featured_image` → `AddImageResult(string PostTitle, string ImagePath)`.
4. Expected-failure paths (post not found, collision, missing credentials) return `Success = false` envelopes — do **not** throw, so the model always receives the schema it was promised. Genuinely unexpected exceptions may propagate (the SDK converts them to `isError` results).

**Acceptance criteria:** every tool's happy path and every documented failure path deserializes into the same envelope; `BlogHelper9000.Mcp.Tests` updated accordingly (assert on deserialized records, not substrings, wherever possible). No tool returns bare prose.

### MCP-4 (MUST): `publish_post` must distinguish its failure modes

**Problem:** `BlogService.PublishPost` returns `null` for three different reasons — post not found, already published (filename already has a `yyyy-MM-dd-` prefix), target collision — and the tool reports all of them as "Could not find post". An LLM told "not found" will retry with different names, when the truthful answer may be "already published; stop".

**Requirement:** introduce a discriminated result in Core and surface it:

1. `BlogHelper9000.Core/Models/PublishOutcome.cs`:

```csharp
public enum PublishOutcome { Published, NotFound, AlreadyPublished, TargetExists }
public sealed record PublishPostResult(PublishOutcome Outcome, string? PublishedPath);
```

2. Add `PublishPostResult PublishPostDetailed(string postName)` to `IBlogService`/`BlogService` containing the existing logic (each early-return maps to an outcome). Keep the existing `string? PublishPost(string postName)` as a one-line wrapper over it so the CLI/TUI call sites and all existing tests compile and pass unchanged.
3. `PublishPostTool` calls `PublishPostDetailed` and maps outcomes to envelope errors:
   - `NotFound` → `"No draft named '<name>' was found in _drafts/ or _posts/."`
   - `AlreadyPublished` → `"'<name>' already has a date prefix and appears to be published. Publishing is idempotent-safe: no action was taken."`
   - `TargetExists` → `"A published post already exists at the target path for today's date."`
4. Update the tool's `[Description]` to enumerate the outcomes.

**Acceptance criteria:** four unit tests in `BlogHelper9000.Mcp.Tests/Tools/PublishPostToolTests.cs`, one per outcome, asserting the envelope's `Success`/`Error`/`Data`. Existing CLI/Core publish tests still pass.

### MCP-5 (MUST): Make `fix_metadata` safe for agents — dry-run + per-file report

**Problem:** `fix_metadata` rewrites every file under `_posts/` in one call, returns only "completed successfully", and silently skips files that fail (`BlogService.FixMetadata` catches per-file and logs). A blog-wide mutation with no preview and no report is the most dangerous tool for an autonomous agent to hold.

**Requirement:**

1. In Core, add `BlogHelper9000.Core/Models/FixMetadataResult.cs`:

```csharp
public sealed class FixMetadataResult
{
    public List<string> Updated { get; } = [];
    public List<FixMetadataSkip> Skipped { get; } = [];
}
public sealed record FixMetadataSkip(string FilePath, string Reason);
```

2. Change `BlogService.FixMetadata(bool, bool, bool)` to `FixMetadataResult FixMetadata(bool fixStatus, bool fixDescription, bool fixTags, bool dryRun = false)`. When `dryRun` is true, run the per-file fix logic but **skip the `UpdateFile` write**, still recording what *would* be updated. The existing try/catch per file populates `Skipped` with `ex.Message` instead of only logging. Update `IBlogService` and the two callers (`FixCommand`, `FixMetadataTool`); the CLI can log counts.
3. `FixMetadataTool` gains a `dryRun` parameter (default **`true`** — the agent must opt in to mutation) and returns the result in the envelope. Tool `[Description]` must state: "Rewrites front matter across ALL posts in _posts/. Call with dryRun=true first to preview; set dryRun=false to apply."
4. Annotate the tool `Destructive = true, Idempotent = true`.

**Acceptance criteria:** tests prove (a) `dryRun: true` changes no file contents (`MockFileSystem` byte-compare) yet lists prospective updates; (b) `dryRun: false` applies them; (c) a malformed filename lands in `Skipped` with a reason while other files are updated. `FixMetadataToolTests` updated for the envelope.

### MCP-6 (MUST): Tool annotations and titles on every tool

**Problem:** without `readOnlyHint`/`destructiveHint`/`idempotentHint`, clients must treat every call as potentially destructive; agent frameworks use these hints for auto-approval policies.

**Requirement:** set attribute properties on each tool (existing and new):

| Tool | ReadOnly | Destructive | Idempotent | OpenWorld | Title |
|---|---|---|---|---|---|
| `get_blog_info` | true | — | true | false | Get blog statistics |
| `list_drafts` | true | — | true | false | List draft posts |
| `list_posts` (new) | true | — | true | false | List published posts |
| `get_post` (new) | true | — | true | false | Read a post |
| `add_post` | false | false | false | false | Create a post or draft |
| `update_post` (new) | false | true | true | false | Update a post's front matter/body |
| `publish_post` | false | false | false | false | Publish a draft |
| `fix_metadata` | false | true | true | false | Batch-fix front matter |
| `add_featured_image` | false | true | false | **true** (network) | Generate featured image |

**Acceptance criteria:** `tools/list` shows the annotations (verify one read-only and one destructive tool in an integration-style test if the SDK exposes the metadata via `McpServerTool.ProtocolTool`, otherwise verify by attribute reflection in a unit test).

### MCP-7 (SHOULD): `get_post` and `list_posts` — give the model eyes

**Problem:** the model can create and publish posts it can never read back. It cannot check what `add_post` actually wrote, review a draft before publishing, or answer "what did I write about X?".

**Requirement:**

1. Core support: `MarkdownFile` gains no changes; instead add to `PostManager` (or a small new Core helper) a method `string GetPostBody(string path)` that reads the file and returns everything after the closing front-matter delimiter (reuse the delimiter-walking logic pattern from `MarkdownHandler.UpdateFile`; missing delimiters → throw `YamlConvertException`, which the tool maps to a failure envelope). Path resolution **must** go through `PostManager.TryFindPost` so `BlogPathResolver` containment keeps applying.
2. New tool `GetPostTool` (`BlogHelper9000.Mcp/Tools/GetPostTool.cs`):
   - Input: `postPath` (`[Description("Filename (e.g. 'my-post.md') or path of the post/draft to read")]`).
   - Output data: `GetPostResult(string FilePath, bool IsDraft, Dictionary<string,string?> FrontMatter, string Body)` where `FrontMatter` includes the typed fields (title, tags as a list is fine too) and `Extras`.
3. New tool `ListPostsTool`:
   - Input: optional `int limit = 20` (`[Description("Maximum number of posts to return, newest first")]`).
   - Output data: list of `PostSummary(string FileName, string? Title, DateTime? PublishedOn, IReadOnlyList<string> Tags)` built from `PostManager.LoadYamlHeaderForAllPosts()` filtered to published posts, ordered newest first.

**Acceptance criteria:** tests using `JekyllBlogFilesystemBuilder`: `get_post` round-trips a post created by `add_post` (front matter + body match); `get_post` on a missing name returns a failure envelope; `list_posts` respects `limit` and ordering; a `postPath` outside the blog root returns a failure envelope (containment).

### MCP-8 (SHOULD): Content support — `add_post` body and `update_post`

**Problem:** the flagship agent workflow — "draft a blog post about X" — is impossible: `add_post` writes an empty-bodied file and nothing can write body text.

**Requirement:**

1. `add_post` gains `string? content = null` (`[Description("Optional markdown body for the post, written below the front matter")]`). Plumb through `IBlogService.AddPost` as an optional parameter appended after the serialized header followed by a blank line. Existing call sites compile unchanged (optional parameter).
2. New tool `UpdatePostTool`:
   - Inputs: `postPath` (required); all optional: `title`, `description`, `tags` (comma-separated, replaces existing), `body` (replaces the entire markdown body).
   - Behavior: resolve via `TryFindPost`; apply only supplied fields to `MarkdownFile.Metadata`; if `body` supplied, write header + new body (add a Core method `MarkdownHandler.UpdateFile(MarkdownFile file, string newBody)` overload that substitutes the body instead of preserving it — reuse the same `FileMode.Create` write path); otherwise call the existing `UpdateFile`. Unknown front-matter keys must survive (guaranteed by the Extras-preserving serializer).
   - Output: `UpdatePostResult(string FilePath, IReadOnlyList<string> UpdatedFields)`.
   - `[Description]` must warn: "body replaces the entire post body; call get_post first to read the current content."
3. Round-trip guarantee: `add_post(content: X)` then `get_post` returns `Body == X` (modulo trailing-newline normalization — pick one behavior and assert it).

**Acceptance criteria:** tests for: create-with-content round-trip; update tags only leaves body and extras untouched; update body only leaves front matter untouched; unsupplied fields never change; failure envelope for unknown post.

### MCP-9 (SHOULD): Base-directory validation and discoverability

**Problem:** the base directory silently falls back to the process CWD (`Program.cs:14-16`). A misconfigured client gets a server that reports zero posts (or worse, `add_post` writes into an unrelated directory) with no way for the model to notice.

**Requirement:**

1. At startup, after resolving `baseDirectory`, log (stderr, per MCP-1) a warning if `<base>/_posts` and `<base>/_drafts` are both missing: `"'{baseDirectory}' does not look like a Jekyll blog (_posts/_drafts not found). Set BLOG_BASE_DIRECTORY or pass the blog path as the first argument."` Do **not** exit (a brand-new blog is legitimate).
2. `get_blog_info` includes `BaseDirectory` (absolute path) and `LooksLikeJekyllBlog` (bool, the same check) in its result so the agent can self-diagnose misconfiguration as step one of any session.
3. Document the three configuration sources and their precedence in the tool description of `get_blog_info` or the server instructions (MCP-2).

**Acceptance criteria:** unit test on `GetBlogInfoTool` asserting `BaseDirectory` appears in the envelope; manual check that a bogus directory logs the warning to stderr.

### MCP-10 (SHOULD): Release hygiene so the shipped server matches the source

**Problem:** the connected server's schemas predate today's source (`add_post` has no `tags` parameter in the live schema). LLM clients bind to whatever nupkg is installed; stale packages silently withhold features.

**Requirement:**
1. Version the package from MinVer like the CLI project (add the `MinVer` `PackageReference` with `PrivateAssets=all` to `BlogHelper9000.Mcp.csproj`), so every pack produces a distinguishable version, surfaced via MCP-2's `serverInfo.version`.
2. Add a short "Installing / upgrading the MCP server" section to `README.md`: `dotnet pack BlogHelper9000.Mcp -c Release` then `dotnet tool update --global --add-source BlogHelper9000.Mcp/releases bloghelper-mcp`, and note that MCP clients must restart the server process to pick up changes.
3. Delete the stale `BlogHelper9000.Mcp/releases/BlogHelper9000.Mcp.1.0.0.nupkg` from the repo and add `BlogHelper9000.Mcp/releases/` to `.gitignore` (packages don't belong in git).

**Acceptance criteria:** fresh pack produces a MinVer-derived version; `releases/` is untracked; README documents the upgrade path.

### MCP-11 (MAY): Cancellation and long-call ergonomics

`add_featured_image` performs two network calls plus CPU-bound image work. Tools may accept a `CancellationToken` parameter (the SDK injects the request token — it is not part of the JSON schema). Thread it through `IUnsplashClient.LoadImageAsync` (which already accepts one) and, if straightforward, `IImageProcessor.Process`. Also change the `imageQuery` default from `"programming"` to deriving from the post title when omitted (`[Description]` updated to say so) — a title-derived image is almost always what the agent wants.

### MCP-12 (MAY): Richer parameter descriptions

Audit every `[Description]` to include: value format with an example, default behavior, and what happens on failure. Specific fixes: `postName`/`postPath` descriptions should state that bare filenames are resolved against `_drafts/` then `_posts/` and that absolute paths outside the blog are rejected; `tags` should say `"Comma-separated, e.g. 'csharp, dotnet'"`; date-bearing outputs should state their format. Keep each under two sentences.

---

## 4. Suggested implementation order

| Order | Requirement | Reason |
|---|---|---|
| 1 | MCP-1 | Protocol safety; one-line change; everything else is tested through the protocol |
| 2 | MCP-3 | The envelope is the substrate every other change returns through |
| 3 | MCP-4, MCP-5 | Core result types; unblock truthful tool responses |
| 4 | MCP-6, MCP-2 | Metadata/annotations; cheap once tools are stable |
| 5 | MCP-7, MCP-8 | New tools (read, then write — get_post is a test dependency of update_post) |
| 6 | MCP-9, MCP-10 | Config discoverability and release hygiene |
| 7 | MCP-11, MCP-12 | Polish |

Constraints for the implementer:
- `dotnet test BlogHelper9000.sln` must stay green after every step; the CLI and TUI share `IBlogService`, so Core signature changes must be additive (optional parameters / new methods with thin wrappers), as specified above.
- Follow the path-containment rule: **every** tool that accepts a post identifier resolves it via `PostManager.TryFindPost` (never raw `Path.Combine` on user input).
- Update `BlogHelper9000.Mcp.Tests` in the same commit as each tool change; new tools need new test files mirroring the existing `Tools/*Tests.cs` layout.
- Do not add MCP resources/prompts, HTTP transport, or new NuGet dependencies beyond MinVer.
