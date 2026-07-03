# CODE_REVIEW.md Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all 29 findings in `CODE_REVIEW.md` (BH9000-001 through BH9000-029) while keeping `dotnet test BlogHelper9000.sln` green throughout.

**Architecture:** Each task is scoped to one subsystem (Core, Imaging, CLI, Nvim, Tui, Build/CI) so it can be implemented and verified independently. Tasks are ordered so that foundational Core changes (path safety, YAML semantics, atomic writes) land before the CLI/TUI/MCP callers that depend on them. Every behavior change gets a failing test first, per this repo's xUnit v3 + FluentAssertions + NSubstitute + `MockFileSystem` conventions (see `CLAUDE.md`).

**Tech Stack:** .NET 10 / C#, xUnit v3, FluentAssertions, NSubstitute, System.IO.Abstractions (`MockFileSystem`), `JekyllBlogFilesystemBuilder` test helper.

**Full finding details (Prompt / Evidence / Risk / Suggested follow-up) already live in `CODE_REVIEW.md` — this plan does not repeat them verbatim, it sequences the fix work and pins down the exact code changes.**

---

## Task 1: BlogPathResolver — containment guard (BH9000-001)

**Files:**
- Create: `BlogHelper9000.Core/Helpers/BlogPathResolver.cs`
- Modify: `BlogHelper9000.Core/Helpers/PostManager.cs`
- Test: `BlogHelper9000.Tests/Helpers/BlogPathResolverTests.cs` (new)

- [ ] Write failing tests for a new `BlogPathResolver` class with signature:
  ```csharp
  public class BlogPathResolver(IFileSystem fileSystem, string baseDirectory)
  {
      public bool TryResolveWithinBase(string candidatePath, out string resolvedPath);
  }
  ```
  Test cases (`MockFileSystem`, base dir e.g. `/blog`):
  - Absolute path outside base (`/etc/passwd`) → returns `false`.
  - Relative path with `../` traversal (`../../etc/passwd`) → returns `false`.
  - Plain filename (`post.md`) resolved against a subdirectory root passed in → returns `true` with the joined, normalized path.
  - Path already inside base (`/blog/_posts/post.md`) → returns `true`, unchanged.
- [ ] Implement `BlogPathResolver` using `fileSystem.Path.GetFullPath` to normalize both the candidate and the base directory, then check `resolvedPath.StartsWith(normalizedBase + separator, StringComparison.Ordinal)` (plus equality with the base itself for directory lookups).
- [ ] Wire it into `PostManager.TryFindPost`, `TryFindAuthorBranding`, `CreateDraftPath`, and `CreatePostPath`: any path supplied by a caller (post name, branding name) is resolved against `Drafts`/`Posts`/`Images` and rejected (return `false` / throw `ArgumentException` for the `Create*Path` case, matching each method's existing return contract) if it resolves outside `BasePath`.
- [ ] Slugify titles in `MakeFileName` so traversal characters can't reach the filesystem: replace `title` with lowercase, then strip everything except `[a-z0-9-]` after replacing whitespace/`/`/`\` with `-`, collapsing repeated `-`.
  ```csharp
  private string MakeFileName(string title)
  {
      var slug = title.Trim().ToLowerInvariant();
      slug = Regex.Replace(slug, @"[\s/\\]+", "-");
      slug = Regex.Replace(slug, @"[^a-z0-9-]", "");
      slug = Regex.Replace(slug, @"-{2,}", "-").Trim('-');
      return slug;
  }
  ```
  Add tests: title `"../../evil"` → slug contains no `/` or `..`; title `"My Post!"` → `"my-post"`.
- [ ] Run `dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj` — new tests pass, existing `PostManager`/`AddCommand` tests still pass (update any test that asserted the old un-slugified filename behavior).

## Task 2: YAML header semantics — Extras preservation + published/date fix (BH9000-002, BH9000-025)

**Files:**
- Modify: `BlogHelper9000.Core/YamlParsing/YamlHeader.cs`
- Modify: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs`
- Modify: `BlogHelper9000.Core/YamlParsing/YamlSerialiser.cs`
- Modify: `BlogHelper9000.Core/YamlParsing/SerialiserBase.cs`
- Test: `BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs` (existing — add cases)

- [ ] Write failing round-trip test: deserialize a front-matter block containing an unknown key (e.g. `permalink: /foo/`), serialize it back, assert the output still contains `permalink: /foo/`.
- [ ] Write failing test: deserialize `published: false` and `date: 2026-01-15`, serialize, assert output contains `published: false` (not a date) and `date: 2026-01-15` (ISO, not `dd/MM/yyyy`).
- [ ] Change `YamlHeader`:
  ```csharp
  [YamlName("published")]
  public bool? IsPublished { get; set; }

  [YamlName("date")]
  public DateTime? PublishedOn { get; set; }
  ```
  Remove the old unattributed `IsPublished`/`PublishedOn` declarations (they collapse into the two above — there is exactly one `published` bool and one `date` datetime now).
- [ ] In `SerialiserBase`, change `DateFormat` to `"yyyy-MM-dd"` (ISO 8601 date-only, matching Jekyll's expected front-matter date format).
- [ ] In `YamlSerialiser.Serialise`, after building the known-property dictionary, append any entries from `header.Extras` that were not already re-mapped to a typed property, before the closing delimiter:
  ```csharp
  foreach (var (key, value) in header.Extras)
  {
      if (string.IsNullOrEmpty(value)) continue;
      builder.AppendLine($"{key}: {value}");
  }
  ```
  (Extras values are already raw strings captured verbatim by the deserializer, so they can be re-emitted as-is.)
- [ ] Confirm `YamlDeserialiser.GetPropertyInfo` still resolves `"published"`/`"date"` correctly against the new attributes (it already checks `YamlNameAttribute` first via reflection over all properties — no change needed there, but add a unit test asserting `published`/`date` map to the right properties).
- [ ] Run `dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlConvertTests"` — all pass, including new ones.
- [ ] Update any existing test/fixture that asserted the old `dd/MM/yyyy` format or the old unattributed `published`/`date` behavior (search: `grep -rn "published\|PublishedOn\|dd/MM/yyyy" BlogHelper9000.Tests BlogHelper9000.Tui.Tests BlogHelper9000.TestHelpers`).
- [ ] Run full `dotnet test BlogHelper9000.sln` to catch downstream breakage in `BlogService`/`PublishCommand`/`FixCommand`, which all read/write `PublishedOn`/`IsPublished` — fix any call sites that assumed `IsPublished` was a `DateTime?` (there are none; the property names are unchanged, only the YAML name attributes moved, so C# call sites compile unchanged).

## Task 3: MarkdownHandler — atomic write + delimiter validation (BH9000-003)

**Files:**
- Modify: `BlogHelper9000.Core/Helpers/MarkdownHandler.cs`
- Modify: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs`
- Test: `BlogHelper9000.Tests/Helpers/MarkdownHandlerTests.cs` (new)

- [ ] Write failing test: `UpdateFile` on a post whose new header is **shorter** than the old header+body no longer leaves trailing bytes (assert exact file content equals expected, using `MockFileSystem`).
- [ ] Write failing test: `Deserialise` throws `YamlConvertException` when fewer than two `---` delimiters are present in the file content.
- [ ] Write failing test: a body containing a literal `---` line (after the header) is preserved verbatim, not treated as a second header boundary.
- [ ] In `YamlDeserialiser.Deserialise`, track delimiter count explicitly and throw if the second delimiter is never found:
  ```csharp
  public YamlHeader Deserialise(string[] fileContent)
  {
      var delimiterCount = 0;
      var yamlBlock = new List<string>();
      foreach (var line in fileContent)
      {
          if (line.Trim() == FrontMatterDelimiter)
          {
              delimiterCount++;
              if (delimiterCount == 2) break;
              continue;
          }
          if (delimiterCount == 1) yamlBlock.Add(line);
      }
      if (delimiterCount < 2)
          throw new YamlConvertException("Front matter is missing a closing '---' delimiter.");
      return ParseYamlHeader(yamlBlock);
  }
  ```
- [ ] Rewrite `MarkdownHandler.UpdateFile` to materialize the body once (avoid repeated `Count()`/`ElementAt()`), validate delimiters before mutating, and truncate on write:
  ```csharp
  public void UpdateFile(MarkdownFile file)
  {
      var markerCount = 0;
      var body = new List<string>();
      foreach (var line in EnumerateLines(file.FilePath))
      {
          if (markerCount < 2)
          {
              if (line == "---") markerCount++;
              continue;
          }
          body.Add(line);
      }

      if (markerCount < 2)
          throw new YamlConvertException($"'{file.FilePath}' is missing front-matter delimiters; refusing to overwrite.");

      var lines = new List<string> { _yamlConvert.Serialise(file.Metadata).TrimEnd('\n', '\r') };
      lines.AddRange(body);

      using var writer = new StreamWriter(_fileSystem.File.Open(file.FilePath, FileMode.Create, FileAccess.Write));
      for (var i = 0; i < lines.Count; i++)
      {
          if (i == lines.Count - 1) writer.Write(lines[i]);
          else writer.WriteLine(lines[i]);
      }
  }
  ```
  Note: `FileMode.Create` truncates any existing file before writing, fixing the stale-bytes bug. Use `_fileSystem.File.Open(...)` (already available via the injected `IFileSystem`) rather than `_fileSystem.FileInfo.New(...).OpenWrite()`, since `IFileSystem` file streams support `FileMode` directly.
- [ ] Run `dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj` — new tests pass, all existing `MarkdownHandler`/`PostManager`/`BlogService` tests still pass.

## Task 4: PostManager — IFileSystem consistency + Extras collisions + swapped locals (BH9000-029)

**Files:**
- Modify: `BlogHelper9000.Core/Helpers/PostManager.cs`
- Test: `BlogHelper9000.Tests/Helpers/PostManagerTests.cs` (existing — add cases)

- [ ] Write failing test: `LoadYamlHeaderForAllPosts` on a post whose front matter already contains an `originalFilename` or `lastUpdated` key does not throw.
- [ ] Write failing test: `LoadYamlHeaderForAllPosts` returns headers for files under both `_drafts` and `_posts` with correct counts when the two folders have different numbers of files (catches the swapped-variable bug, which currently still works by accident since both lists get added to the same `allPosts` — add a test asserting draft-only headers carry through even when `_drafts` has more files than `_posts`, and rename to make the swap-fix explicit).
- [ ] In `LoadYamlHeaderForAllPosts`, rename locals so `drafts` maps to `Drafts` and `posts` maps to `Posts` (fixing the swap — behavior is currently accidentally correct since both are unioned, but the fix prevents future bugs if the two loops diverge):
  ```csharp
  var drafts = FileSystem.Directory.EnumerateFiles(Drafts, "*.md", SearchOption.AllDirectories).ToList();
  var posts = FileSystem.Directory.EnumerateFiles(Posts, "*.md", SearchOption.AllDirectories).ToList();
  allPosts.AddRange(drafts.Select(GetHeaderWithOriginalFilename));
  allPosts.AddRange(posts.Select(GetHeaderWithOriginalFilename));
  ```
- [ ] Replace `new FileInfo(f)` with `FileSystem.FileInfo.New(f)` in the local `GetHeaderWithOriginalFilename` function so tests using `MockFileSystem` get correct timestamps.
- [ ] Replace `header.Extras.Add(...)` with indexer assignment so pre-existing keys are overwritten instead of throwing:
  ```csharp
  header.Extras["originalFilename"] = fileInfo.Name;
  header.Extras["lastUpdated"] = $"{fileInfo.LastWriteTime:dd/MM/yyyy hh:mm:ss}";
  ```
- [ ] Run `dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~PostManagerTests"` — all pass.

## Task 5: BlogService — transactional publish, idempotency, tags, collisions, tolerant batch fixes (BH9000-008, 009, 011, 026, 027)

**Files:**
- Modify: `BlogHelper9000.Core/Services/BlogService.cs`
- Modify: `BlogHelper9000.Core/Services/IBlogService.cs`
- Test: `BlogHelper9000.Tests/Services/BlogServiceTests.cs` (existing — add cases; create if it doesn't exist yet, check first)

- [ ] Check whether `BlogHelper9000.Tests/Services/BlogServiceTests.cs` already exists (`ls BlogHelper9000.Tests/Services/`); if not, create it following the pattern of other test files (constructor sets up `MockFileSystem` + `JekyllBlogFilesystemBuilder`, `Options.Create(new BlogHelperOptions{...})`, `Substitute.For<ILogger<BlogService>>()`, and a `FakeTimeProvider` or `TimeProvider` test double already used elsewhere in the suite — search `grep -rn "TimeProvider" BlogHelper9000.Tests` for the existing pattern before introducing a new one).
- [ ] Write failing test: `AddPost(title, isDraft, tags: ["csharp", "dotnet"])` produces a file whose deserialized `Tags` equals `["csharp", "dotnet"]`.
- [ ] Write failing test: `AddPost` on a title that slugifies to an existing file path returns `null` (or throws `InvalidOperationException` — pick one contract, document it in the XML doc comment) instead of appending to the existing file.
- [ ] Write failing test: `PublishPost` on a post whose filename already has a `yyyy-MM-dd-` prefix (i.e., already published) returns `null` without moving/rewriting anything.
- [ ] Write failing test: `PublishPost` computes the target path and checks for a collision (`_fileSystem.File.Exists(replacementPath)`) before mutating metadata; if the target exists, return `null` and leave the draft untouched.
- [ ] Write failing test: `PublishPost` captures `_timeProvider.GetLocalNow()` once (assert via a time-provider test double whose call count is asserted to be 1, or by asserting the folder year and filename date always agree even when crossing a fake midnight boundary).
- [ ] Write failing test: `FixMetadata` continues processing remaining files when one file's `FixPublishedStatus` throws (e.g., a post filename with no leading date), and the result reports which files were skipped.
- [ ] Update `IBlogService`:
  ```csharp
  string? AddPost(string title, bool isDraft, bool isFeatured = false, bool isHidden = false, string? featuredImage = null, IReadOnlyList<string>? tags = null);
  FixMetadataResult FixMetadata(bool fixStatus, bool fixDescription, bool fixTags);
  ```
  Add a new small result type in `BlogHelper9000.Core/Models/FixMetadataResult.cs`:
  ```csharp
  namespace BlogHelper9000.Core.Models;

  public class FixMetadataResult
  {
      public List<string> Updated { get; } = [];
      public List<string> Skipped { get; } = [];
  }
  ```
- [ ] Rewrite `BlogService.AddPost`:
  ```csharp
  public string? AddPost(string title, bool isDraft, bool isFeatured = false, bool isHidden = false, string? featuredImage = null, IReadOnlyList<string>? tags = null)
  {
      var filePath = isDraft ? _postManager.CreateDraftPath(title) : _postManager.CreatePostPath(title);

      if (_fileSystem.File.Exists(filePath))
      {
          _logger.LogError("A post already exists at {File}", filePath);
          return null;
      }

      var yamlHeader = new YamlHeader
      {
          Title = title,
          Tags = tags?.ToList() ?? [],
          FeaturedImage = featuredImage ?? string.Empty,
          IsFeatured = isFeatured,
          IsHidden = isHidden,
          IsPublished = !isDraft
      };

      var yamlHeaderText = _postManager.YamlConvert.Serialise(yamlHeader);
      _fileSystem.File.AppendAllText(filePath, yamlHeaderText);

      _logger.LogInformation("Added new post at {File}", filePath);
      return filePath;
  }
  ```
- [ ] Rewrite `BlogService.PublishPost` to capture time once, check the already-published/collision cases, and validate before mutating:
  ```csharp
  public string? PublishPost(string postName)
  {
      if (!_postManager.TryFindPost(postName, out var postMarkdown))
      {
          _logger.LogError("Could not find {Post} to publish", postName);
          return null;
      }

      var currentPath = postMarkdown.FilePath;
      var fileName = _fileSystem.Path.GetFileName(currentPath);

      if (Regex.IsMatch(fileName, @"^\d{4}-\d{2}-\d{2}-"))
      {
          _logger.LogWarning("{Post} already appears to be published", postName);
          return null;
      }

      var now = _timeProvider.GetLocalNow().DateTime;
      var publishedFilename = $"{now:yyyy-MM-dd}-{fileName}";
      var targetFolder = _fileSystem.Path.Combine(_postManager.Posts, $"{now:yyyy}");
      var replacementPath = _fileSystem.Path.Combine(targetFolder, publishedFilename);

      if (_fileSystem.File.Exists(replacementPath))
      {
          _logger.LogError("A published post already exists at {Target}", replacementPath);
          return null;
      }

      postMarkdown.Metadata.IsPublished = true;
      postMarkdown.Metadata.PublishedOn = now;
      _postManager.Markdown.UpdateFile(postMarkdown);

      if (!_fileSystem.Directory.Exists(targetFolder))
          _fileSystem.Directory.CreateDirectory(targetFolder);

      _logger.LogInformation("Publishing {PublishedFileName} to {TargetFolder}", publishedFilename, targetFolder);
      _fileSystem.File.Move(currentPath, replacementPath);

      return replacementPath;
  }
  ```
  (Drops the redundant `File.Delete(currentPath)` after `Move` — `Move` already removes the source.)
- [ ] Rewrite `BlogService.FixMetadata` to catch per-file and return a result:
  ```csharp
  public FixMetadataResult FixMetadata(bool fixStatus, bool fixDescription, bool fixTags)
  {
      var result = new FixMetadataResult();
      foreach (var file in _postManager.GetAllPosts())
      {
          try
          {
              _logger.LogInformation("Updating metadata for {PostTitle}", file.Metadata.Title);
              if (fixStatus) FixPublishedStatus(file);
              if (fixDescription) FixDescription(file);
              if (fixTags) FixTagsOnFile(file);
              _postManager.Markdown.UpdateFile(file);
              result.Updated.Add(file.FilePath);
          }
          catch (Exception ex)
          {
              _logger.LogWarning(ex, "Skipping {File} — could not fix metadata", file.FilePath);
              result.Skipped.Add(file.FilePath);
          }
      }
      return result;
  }
  ```
- [ ] Use `IFileSystem.Path.GetFileName` (already the case) and `DateTime.TryParseExact` in `FixPublishedStatus` instead of `[..10]` + `ParseExact`, so malformed names are caught by the per-file try/catch above rather than needing a separate guard — but make the failure explicit rather than silently skipping the date fix:
  ```csharp
  private static void FixPublishedStatus(MarkdownFile file)
  {
      var rawFileName = file.FilePath.Split('/').Last();
      var datePart = rawFileName.Length >= 10 ? rawFileName[..10] : rawFileName;
      if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var publishedOn))
          throw new FormatException($"Could not extract a yyyy-MM-dd date from filename '{rawFileName}'.");

      file.Metadata.PublishedOn = publishedOn;
      file.Metadata.IsPublished = true;
      file.Metadata.IsHidden = false;
  }
  ```
- [ ] Fix `GetBlogInfo`: use `_timeProvider.GetLocalNow().DateTime` instead of `DateTime.Now`, and only enumerate `_drafts`/`_posts` if they exist (delegate to `PostManager.LoadYamlHeaderForAllPosts`, which should itself tolerate missing directories — see next bullet).
- [ ] In `PostManager.LoadYamlHeaderForAllPosts` (touched again here, same file as Task 4 — combine the edit), guard both `EnumerateFiles` calls with `FileSystem.Directory.Exists(...)`, returning an empty list for a missing folder instead of throwing.
- [ ] In `BlogService.GetBlogInfo`, represent "no last post" explicitly: change `BlogMetaInformation.DaysSinceLastPost` to `TimeSpan?` (modify `BlogHelper9000.Core/Models/BlogMetaInformation.cs`) and only set it when `LastPost` is non-null; leave it `null` otherwise.
- [ ] Update `BlogHelper9000.Mcp/Tools/GetBlogInfoTool.cs` to serialize `DaysSinceLastPost = info.DaysSinceLastPost?.Days` (nullable int in the JSON) instead of unconditionally reading `.Days`.
- [ ] Update `BlogHelper9000.Mcp/Tools/AddPostTool.cs` to accept an optional `tags` parameter (comma-separated string, split on `,` and trimmed) and pass it through to `blogService.AddPost`.
- [ ] Run `dotnet test BlogHelper9000.sln` — fix any compile errors in CLI (`PublishCommand`, `FixCommand`, `AddCommand` will be replaced to call `IBlogService` in Task 6, so expect them to still reference the old duplicated logic until then; don't break `dotnet build` — if `BlogMetaInformation.DaysSinceLastPost` becoming nullable breaks a CLI/TUI call site, fix it inline, e.g. `BlogHelper9000.Tui/Commands/BlogCommands.cs`'s info-display code should null-check before formatting).

## Task 6: CLI — DI alignment + delegate to IBlogService (BH9000-007, 018)

**Files:**
- Modify: `BlogHelper9000/Program.cs`
- Modify: `BlogHelper9000/Commands/PublishCommand.cs`
- Modify: `BlogHelper9000/Commands/FixCommand.cs`
- Modify: `BlogHelper9000/Commands/AddCommand.cs`
- Test: `BlogHelper9000.Tests/Commands/PublishCommandTests.cs`, `FixCommandTests.cs`, `AddCommandTests.cs` (existing — update to match new handler bodies)

- [ ] In `BlogHelper9000/Program.cs`, register the same services as TUI/MCP so the CLI host can resolve every command handler:
  ```csharp
  builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
  builder.Services.AddSingleton<IBlogService, BlogService>();
  builder.Services.AddSingleton<IUnsplashClient>(sp =>
      new UnsplashClient(sp.GetRequiredService<ILoggerFactory>().CreateLogger<UnsplashClient>()));
  builder.Services.AddSingleton<IImageProcessor>(sp => new ImageProcessor(
      sp.GetRequiredService<ILoggerFactory>().CreateLogger<ImageProcessor>(),
      sp.GetRequiredService<PostManager>()));
  ```
  (Add the corresponding `using BlogHelper9000.Core.Services;` and `using BlogHelper9000.Imaging;`.)
- [ ] Rewrite `PublishCommand.Handler` to be a thin wrapper:
  ```csharp
  public class Handler(ILogger<Handler> logger, IBlogService blogService) : ICommandHandler<PublishCommand, Unit>
  {
      public ValueTask<Unit> Handle(PublishCommand request, CancellationToken cancellationToken)
      {
          var result = blogService.PublishPost(request.Post);
          if (result is null)
              logger.LogError("Could not publish {Post}", request.Post);
          else
              logger.LogInformation("Published to {Result}", result);
          return default;
      }
  }
  ```
- [ ] Rewrite `FixCommand.Handler` similarly, delegating to `blogService.FixMetadata(request.Status, request.Description, request.Tags)` and logging `result.Updated.Count` updated / `result.Skipped.Count` skipped.
- [ ] Rewrite `AddCommand.Handler` to delegate to `blogService.AddPost(request.Title, request.IsDraft, request.IsFeatured, request.IsHidden, request.FeaturedImage, tags)`, splitting `request.Tags` on `,` (trim, remove empty entries) before passing it through — this also fixes BH9000-026's dropped-tags bug on the CLI surface.
- [ ] Update the three command test files to substitute `IBlogService` instead of constructing `PostManager` directly (follow whichever mocking pattern (`NSubstitute`) the existing MCP tool tests already use as a template — check `BlogHelper9000.Tests` for any existing `IBlogService` substitute usage first).
- [ ] Run `dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj` — all pass.

## Task 7: UnsplashClient fixes (BH9000-005)

**Files:**
- Modify: `BlogHelper9000.Imaging/UnsplashClient.cs`
- Test: `BlogHelper9000.Tests/../` — check whether Imaging has its own test project; if not, add tests under `BlogHelper9000.Tests/Imaging/UnsplashClientTests.cs` referencing `BlogHelper9000.Imaging` (confirm the Imaging project reference exists in `BlogHelper9000.Tests.csproj`, add it if missing).

- [ ] Write failing test using a fake `HttpMessageHandler` (`DelegatingHandler` subclass capturing the request `Uri`) asserting the request URL contains `query=<the supplied query>` URL-encoded, not the hardcoded `programming`.
- [ ] Write failing test asserting the same injected `HttpClient` (with `Accept-Version` header) is used for both the search call and the image download — i.e. no second `HttpClient` is constructed.
- [ ] Write failing test: when the credentials file exists but its `UnsplashCredentials` value doesn't contain a `:`, `LoadImageAsync` throws a clear exception (or returns a failure result — pick the contract from Task 9) instead of an unhandled `IndexOutOfRangeException`.
- [ ] Refactor `UnsplashClient` to take an injected `HttpClient` via constructor (registered with `Accept-Version` header once at DI composition time) instead of building a second client per call:
  ```csharp
  public class UnsplashClient(HttpClient httpClient, ILogger logger) : IUnsplashClient
  {
      private const string UnsplashApiUrl = "https://api.unsplash.com/photos/random";

      public async Task<Stream> LoadImageAsync(string query, CancellationToken cancellationToken = default)
      {
          var credentials = LoadCredentials();
          if (credentials is null)
          {
              logger.LogError("Could not create Unsplash client because credentials are missing");
              return Stream.Null;
          }

          var credentialParts = credentials.UnsplashCredentials.Split(':');
          if (credentialParts.Length < 1 || string.IsNullOrWhiteSpace(credentialParts[0]))
          {
              logger.LogError("Unsplash credentials file is malformed");
              return Stream.Null;
          }
          var clientId = credentialParts[0];

          var fullUrl = $"{UnsplashApiUrl}?query={Uri.EscapeDataString(query)}&client_id={clientId}";
          logger.LogInformation("Loading random Unsplash image for the query '{ImageQuery}'", query);

          using var response = await httpClient.GetAsync(fullUrl, cancellationToken);
          response.EnsureSuccessStatusCode();
          var unsplashData = await response.Content.ReadFromJsonAsync<UnsplashData>(cancellationToken: cancellationToken);
          if (unsplashData is null) return Stream.Null;

          var imageUrl = $"{unsplashData.Urls.Raw}&w=1280&h=720&fit=min";
          using var imageResponse = await httpClient.GetAsync(imageUrl, cancellationToken);
          imageResponse.EnsureSuccessStatusCode();
          return await imageResponse.Content.ReadAsStreamAsync(cancellationToken);
      }
      // LoadCredentials unchanged for now (fixed for storage safety in Task 8)
  }
  ```
  Remove `IDisposable`/the owned `_httpClient` field — client lifetime is now the DI container's responsibility.
- [ ] Update `IUnsplashClient` interface to add the optional `CancellationToken` parameter.
- [ ] Update DI registrations in `BlogHelper9000/Program.cs`, `BlogHelper9000.Tui/Program.cs`, `BlogHelper9000.Mcp/Program.cs` to register a named/plain `HttpClient` with the `Accept-Version: v1` header via `AddHttpClient` (or construct one `HttpClient` singleton with the header set once and pass it into `UnsplashClient`'s constructor):
  ```csharp
  builder.Services.AddHttpClient<IUnsplashClient, UnsplashClient>(client =>
  {
      client.DefaultRequestHeaders.Add("Accept-Version", "v1");
  });
  ```
  (This replaces the existing manual `sp => new UnsplashClient(...)` factory registrations in all three `Program.cs` files — `AddHttpClient<TInterface, TImplementation>` also registers `IUnsplashClient` itself, so the manual factory lambda can be deleted.)
- [ ] Run the new Unsplash tests plus `dotnet test BlogHelper9000.sln` — everything green.

## Task 8: Credential storage safety (BH9000-006)

**Files:**
- Modify: `BlogHelper9000/Commands/UnsplashCredentialsCommand.cs`
- Modify: `BlogHelper9000.Imaging/UnsplashClient.cs`

- [ ] Move the credentials file from `~/Documents/bloghelper9000.json` to the platform app-data directory: `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` (Windows: `%APPDATA%`; on Linux/macOS via .NET this resolves to `~/.config`), under a `BlogHelper9000` subfolder, creating it if missing.
- [ ] After writing the file in `UnsplashCredentialsCommand.Handler`, restrict permissions to the owner where supported:
  ```csharp
  var path = GetCredentialsPath(fileSystem);
  fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(path)!);
  fileSystem.File.WriteAllText(path, json);
  if (!OperatingSystem.IsWindows())
  {
      File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }
  ```
  Extract `GetCredentialsPath` as a small shared static helper (e.g. on `AppDataModel` or a new `CredentialsPaths` static class in `BlogHelper9000.Core`) so both `UnsplashCredentialsCommand` and `UnsplashClient.LoadCredentials` use the identical path — this also lets `UnsplashClient` go through `IFileSystem` instead of raw `System.IO.File`, closing the abstraction gap called out in the finding.
- [ ] `UnsplashClient.LoadCredentials` takes an injected `IFileSystem` (already available via DI everywhere `UnsplashClient` is constructed) and reads via `fileSystem.File.Exists` / `fileSystem.File.ReadAllText` instead of static `File`.
- [ ] Run `dotnet test BlogHelper9000.sln`.

## Task 9: ImageProcessor — disposal, save-before-metadata, directory creation (BH9000-012, BH9000-013)

**Files:**
- Modify: `BlogHelper9000.Imaging/ImageProcessor.cs`
- Modify: `BlogHelper9000.Mcp/Tools/AddFeaturedImageTool.cs`
- Modify: `BlogHelper9000/Commands/AddImageCommand.cs`
- Modify: `BlogHelper9000.Tui/Commands/BlogCommands.cs`

- [ ] Wrap `Image.LoadAsync` results in `await using` in `ImageProcessor.Process` (both the base image and the branding logo).
- [ ] Reorder `SaveImage` so the WebP file is written first, and post metadata is only updated (and the markdown file rewritten) after a successful save:
  ```csharp
  private async Task SaveImage(MarkdownFile postMarkdown, Image baseImage)
  {
      var (fileName, savePath) = postManager.CreateImageFilePathForPost(postMarkdown);
      var directory = postManager.FileSystem.Path.GetDirectoryName(savePath);
      if (directory is not null && !postManager.FileSystem.Directory.Exists(directory))
          postManager.FileSystem.Directory.CreateDirectory(directory);

      await baseImage.SaveAsWebpAsync(savePath);

      var markdownPath = $"/assets/images/{fileName}";
      postMarkdown.Metadata.FeaturedImage = markdownPath;
      postMarkdown.Metadata.Image = markdownPath;
      postManager.UpdateMarkdown(postMarkdown);
  }
  ```
- [ ] Replace the `Stream.Null` failure sentinel with a nullable return: change `IUnsplashClient.LoadImageAsync` to return `Task<Stream?>`, returning `null` on failure instead of `Stream.Null`. Update the three call sites (`BlogHelper9000.Mcp/Tools/AddFeaturedImageTool.cs`, `BlogHelper9000/Commands/AddImageCommand.cs`, `BlogHelper9000.Tui/Commands/BlogCommands.cs`) to check `is null` instead of `== Stream.Null`, and `ImageProcessor.Process` to accept `Stream` as before (callers guard before calling `Process`, so `Process` itself doesn't need to change signature) — MCP tool already checks explicitly and returns a message, so just change its comparison operator; TUI/CLI need an added null-check with a logged error where none existed before.
- [ ] Run `dotnet test BlogHelper9000.sln`.

## Task 10: FontManager — idempotent/thread-safe (BH9000-019)

**Files:**
- Modify: `BlogHelper9000.Imaging/FontManager.cs`
- Test: add `BlogHelper9000.Tests/Imaging/FontManagerTests.cs` if an Imaging test location doesn't already exist (reuse from Task 7).

- [ ] Write failing test: constructing `FontManager` twice does not throw and `GetFont` still succeeds after both constructions (guards against `FontCollection.Add` throwing/duplicating on repeat registration).
- [ ] Make font loading happen exactly once via `Lazy<FontCollection>`:
  ```csharp
  public class FontManager
  {
      private readonly ILogger _logger;
      private static readonly Assembly Assembly = typeof(FontManager).Assembly;
      private static readonly Lazy<FontCollection> LazyFontCollection = new(LoadFonts, LazyThreadSafetyMode.ExecutionAndPublication);

      public FontManager(ILogger logger)
      {
          _logger = logger;
      }

      private static FontCollection LoadFonts()
      {
          var collection = new FontCollection();
          foreach (var ttf in Assembly.GetManifestResourceNames().Where(x => x.EndsWith(".ttf")))
          {
              using var stream = Assembly.GetManifestResourceStream(ttf);
              if (stream != null) collection.Add(stream);
          }
          return collection;
      }

      public Font GetFont(string fontName, int fontSize = 125, FontStyle style = FontStyle.Bold)
      {
          if (LazyFontCollection.Value.TryGet(fontName, out var family))
          {
              _logger.LogDebug("Loading {FontName} with size {FontSize}", fontName, fontSize);
              return family.CreateFont(fontSize, style);
          }
          throw new ArgumentException($"Could not create {fontName} font.", nameof(fontName));
      }
  }
  ```
- [ ] Run the new test plus `dotnet test BlogHelper9000.sln`.

## Task 11: Nvim RPC — timeouts, serialized writes, resolver narrowing (BH9000-014, BH9000-015)

**Files:**
- Modify: `BlogHelper9000.Nvim/Rpc/MsgPackRpcClient.cs`
- Test: `BlogHelper9000.Nvim.Tests/Rpc/MsgPackRpcClientTests.cs` (existing — add cases)

- [ ] Write failing test: `RequestAsync` with a timeout parameter throws `TimeoutException` (or `OperationCanceledException`) if no response arrives within the timeout, and the pending request is removed from the internal dictionary afterward (assert via a second identical-msgid-shaped response arriving late does not throw / is safely ignored).
- [ ] Write failing test: two concurrent `RequestAsync` calls against a stream double that records write order never interleave partial writes (assert the recorded byte buffers are each a complete, valid MsgPack frame — i.e. writes are serialized, not concurrent).
- [ ] Add a `private readonly SemaphoreSlim _writeLock = new(1, 1);` and wrap the write section of `RequestAsync`:
  ```csharp
  public async Task<object?> RequestAsync(string method, TimeSpan? timeout = null, params object[] args)
  {
      var msgId = Interlocked.Increment(ref _nextMsgId);
      var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
      _pendingRequests[msgId] = tcs;

      try
      {
          var bytes = SerializeRequest(msgId, method, args);
          _logger.LogTrace("RPC request [{MsgId}]: {Method}", msgId, method);

          await _writeLock.WaitAsync(_cts.Token);
          try
          {
              await _input.WriteAsync(bytes, _cts.Token);
              await _input.FlushAsync(_cts.Token);
          }
          finally
          {
              _writeLock.Release();
          }

          using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
          await using var registration = linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token));

          return await tcs.Task;
      }
      finally
      {
          _pendingRequests.TryRemove(msgId, out _);
      }
  }
  ```
  Note the call-site signature change (`timeout` inserted before `params args`) — check all callers of `RequestAsync` in `BlogHelper9000.Nvim/NvimClient.cs` and update them; since `timeout` is optional with a default, most call sites (`RequestAsync(method, arg1, arg2)`) need to pass `args` via an explicit array or become `RequestAsync(method, args: [arg1, arg2])` to keep working with a `params` array following an optional parameter — **C# does not allow an optional parameter before a `params` parameter to be skipped positionally**, so instead add a *second overload* rather than inserting a parameter in the middle:
  ```csharp
  public Task<object?> RequestAsync(string method, params object[] args) => RequestAsync(method, timeout: null, args);
  public async Task<object?> RequestAsync(string method, TimeSpan? timeout, params object[] args) { /* body above */ }
  ```
  This keeps all existing call sites source-compatible.
- [ ] Serialize notification writes too (`SerializeNotification` callers) through the same `_writeLock` if `NvimClient` calls `_input.WriteAsync` directly for notifications — check `BlogHelper9000.Nvim/NvimClient.cs` for a notification-send path and route it through a shared internal `WriteFrameAsync(byte[])` method on `MsgPackRpcClient` that takes the lock, rather than writing to `_input` from two places.
- [ ] Replace `TypelessObjectResolver` with a narrower composite resolver that doesn't instantiate arbitrary CLR types — use `MessagePack.Resolvers.StandardResolver.Instance` composed with the existing `NvimExtensionResolver.Instance`:
  ```csharp
  private static readonly MessagePackSerializerOptions TypelessOptions =
      MessagePackSerializerOptions.Standard.WithResolver(
          MessagePack.Resolvers.CompositeResolver.Create(
              NvimExtensionResolver.Instance,
              MessagePack.Resolvers.StandardResolver.Instance));
  ```
  Since the deserialize target is `object?` (`MessagePackSerializer.Deserialize<object?>`), confirm `StandardResolver` can still deserialize into primitive/array/map `object` shapes (it can, via `PrimitiveObjectResolver` which `StandardResolver` includes) — run the existing Nvim UI-event parsing tests to confirm redraw messages (arrays/maps/extension types) still deserialize correctly.
- [ ] Run `dotnet test BlogHelper9000.Nvim.Tests/BlogHelper9000.Nvim.Tests.csproj` — all pass, including new timeout/serialization tests.

## Task 12: NvimEditorView — command-injection-safe file open (BH9000-004)

**Files:**
- Modify: `BlogHelper9000.Tui/Views/NvimEditorView.cs`
- Modify: `BlogHelper9000.Nvim/NvimClient.cs` (add an `EditFileAsync` API if one doesn't already exist)
- Test: `BlogHelper9000.Nvim.Tests/` or `BlogHelper9000.Tui.Tests/` — add a test asserting the RPC call used to open a file passes the path as a data argument, not embedded in a command string.

- [ ] Replace the `:e {escaped}` string-command approach with `nvim_command`'s safer sibling `nvim_cmd` (structured command, MsgPack-RPC `nvim_cmd(cmd, opts)` where `cmd` is a dict `{cmd: "edit", args: [path]}`), or simpler: call Vimscript's `fnameescape()` on the *server* side by sending the raw path as an RPC argument to a small helper function invoked via `nvim_call_function("BlogHelperEditFile", [path])`, registering that function once at startup. Simplest robust option given the existing `NvimClient.CommandAsync(string)` surface: use `nvim_command` with the path escaped via a dedicated Lua/Vimscript call instead of string interpolation:
  ```csharp
  public async Task OpenFileAsync(string path)
  {
      if (!_started) return;
      await _nvim.EditFileAsync(path);
  }
  ```
  Add to `NvimClient`:
  ```csharp
  public async Task EditFileAsync(string path)
  {
      await RequestAsync("nvim_command", $"execute 'edit ' . fnameescape({VimEscapeQuotedString(path)})");
  }

  private static string VimEscapeQuotedString(string path) =>
      "'" + path.Replace("'", "''") + "'";
  ```
  This passes the path as a single-quoted Vimscript string literal (Vimscript single-quoted strings have exactly one escape rule: `''` for a literal `'`, no other character is special — unlike double-quoted strings or shell strings), then lets Neovim's own `fnameescape()` handle safe expansion for the `:edit` command. This avoids interpreting `|`, quotes, backslashes, or newlines as command separators, because the only text substituted into the outer `execute` command is a syntactically-safe single-quoted literal.
- [ ] Add tests covering paths containing spaces, single quotes, `|`, backslashes, brackets, and (if representable in a test string) embedded newlines — assert the generated command string always wraps the path in a properly `''`-doubled single-quoted literal and never places raw pipe/quote characters where they'd terminate the string early.
- [ ] Run `dotnet test BlogHelper9000.Nvim.Tests/BlogHelper9000.Nvim.Tests.csproj BlogHelper9000.Tui.Tests/BlogHelper9000.Tui.Tests.csproj`.

## Task 13: NvimGrid — bounds-checking + thread synchronization (BH9000-016, BH9000-028)

**Files:**
- Modify: `BlogHelper9000.Nvim/Grid/NvimGrid.cs`
- Modify: `BlogHelper9000.Tui/Views/NvimEditorView.cs`
- Test: `BlogHelper9000.Nvim.Tests/Grid/NvimGridTests.cs` (existing — add cases)

- [ ] Write failing test: `ApplyLine` with a `Row` outside `[0, Height)` does not throw (it's ignored/logged, not applied).
- [ ] Write failing test: `ApplyScroll` with `Top`/`Bottom`/`Left`/`Right` outside grid bounds does not throw (clamps to grid bounds before applying).
- [ ] Add bounds checks to `ApplyLine`:
  ```csharp
  internal void ApplyLine(GridLineEvent line)
  {
      if (line.Row < 0 || line.Row >= Height) return;
      var col = line.ColStart;
      var currentHlId = 0;
      foreach (var cell in line.Cells)
      {
          var hlId = cell.HlId ?? currentHlId;
          currentHlId = hlId;
          for (var r = 0; r < cell.Repeat; r++)
          {
              if (col >= 0 && col < Width)
              {
                  _cells[line.Row, col] = new NvimGridCell(cell.Text, hlId);
                  col++;
              }
          }
      }
      _dirtyRows.Add(line.Row);
  }
  ```
- [ ] Clamp `ApplyScroll`'s `Top`/`Bottom`/`Left`/`Right` to `[0, Height]`/`[0, Width]` at the top of the method before use, returning early if the clamped region is empty or inverted.
- [ ] Add a `private readonly object _gridLock = new();` in `NvimGrid` and wrap the bodies of `Clear`, `Resize`, `ApplyLine`, `ApplyScroll`, and the `this[row, col]` indexer getter in `lock (_gridLock)` — this fixes BH9000-028 (torn reads/writes between the background event-loop task and the UI draw thread) at the data-structure level rather than requiring `NvimEditorView` to coordinate threads itself.
- [ ] In `NvimEditorView.ProcessUiEvents`, wrap only the per-event `switch` body in its own `try/catch` (log and `continue`) instead of catching around the whole `await foreach` loop, so one malformed event doesn't end event processing for the rest of the session:
  ```csharp
  await foreach (var evt in _nvim.UiEvents.ReadAllAsync())
  {
      try
      {
          switch (evt) { /* existing cases */ }
      }
      catch (Exception ex)
      {
          _logger.LogWarning(ex, "Failed to apply UI event {EventType}", evt.GetType().Name);
      }
  }
  ```
- [ ] Run `dotnet test BlogHelper9000.Nvim.Tests/BlogHelper9000.Nvim.Tests.csproj` and `BlogHelper9000.Tui.Tests/BlogHelper9000.Tui.Tests.csproj`.

## Task 14: EditorSurface — save support (BH9000-017)

**Files:**
- Modify: `BlogHelper9000.Tui/Views/EditorSurface.cs`
- Test: `BlogHelper9000.Tui.Tests/Views/EditorSurfaceTests.cs` (new)

- [ ] Write failing test: after `LoadFile` then mutating `_textView.Text` and calling a new `Save()` method, `fileSystem.File.ReadAllText(path)` reflects the new content.
- [ ] Write failing test: `IsModified` is `false` right after `LoadFile`, becomes `true` after the text changes, and returns to `false` after `Save()`.
- [ ] Add save/dirty-tracking to `EditorSurface`:
  ```csharp
  public bool IsModified => _textView.HasHistoryChanges;

  public void Save()
  {
      if (_currentFilePath is null) return;
      _fileSystem.File.WriteAllText(_currentFilePath, _textView.Text?.ToString() ?? string.Empty);
  }
  ```
  (Check Terminal.Gui v2's `TextView` API for the actual "has unsaved changes" property name — it may be `HasHistoryChanges` or similar; grep the Terminal.Gui package sources / existing usages in the codebase, e.g. `grep -rn "HasHistoryChanges\|IsDirty" BlogHelper9000.Tui` and the Terminal.Gui NuGet package's public API, and use whichever exists. If no such property exists on `TextView`, track modification manually via the `TextView.ContentsChanged` event, setting an internal `_isModified` flag on change and clearing it in `Save()`/`LoadFile()`.)
- [ ] Wire a Ctrl+S (or existing save keybinding pattern used by `NvimEditorView`'s save flow via `FileSaved` event — check how `BlogWorkspaceWindow` currently reacts to Nvim's `FileSaved` event) so the no-nvim mode also triggers a save and updates the file browser / title bar the same way. At minimum, expose `Save()` publicly so `BlogWorkspaceWindow` can call it from a menu command or keybinding in no-nvim mode; check `BlogHelper9000.Tui/Views/BlogWorkspaceWindow.cs` for where Nvim's save path is wired and add an equivalent branch for `EditorSurface`.
- [ ] Run `dotnet test BlogHelper9000.Tui.Tests/BlogHelper9000.Tui.Tests.csproj`.

## Task 15: TUI fire-and-forget task tracking + de-flake tests (BH9000-020)

**Files:**
- Modify: `BlogHelper9000.Tui/Commands/BlogCommands.cs`
- Modify: `BlogHelper9000.Tui.Tests/Commands/BlogCommandsAddImageTests.cs`

- [ ] In `BlogCommands.AddImageAsync`, keep a reference to the launched `Task` on a field (e.g. `internal Task? LastAddImageTask;`) instead of discarding it with `_ =`, so tests can `await` it deterministically instead of `Task.Delay(500)`.
  ```csharp
  internal Task? LastAddImageTask { get; private set; }

  internal void AddImageAsync(MarkdownFile markdownFile, string query)
  {
      LastAddImageTask = Task.Run(async () => { /* existing body */ });
  }
  ```
- [ ] Update `BlogCommandsAddImageTests.cs` to `await commands.LastAddImageTask!;` instead of `await Task.Delay(500);` at both call sites (lines 95 and 143 per the review).
- [ ] Run `dotnet test BlogHelper9000.Tui.Tests/BlogHelper9000.Tui.Tests.csproj` and confirm the two tests pass without the fixed delay (and ideally run noticeably faster).

## Task 16: Build/docs/CI hygiene (BH9000-021, 022, 023)

**Files:**
- Modify: `README.md`
- Modify: `.github/workflows/main.yml`
- Modify: `.github/dependabot.yml`
- Modify: `.gitignore`
- Remove from git: `BlogHelper9000.sln.DotSettings.user`, `.vscode/launch.json`, `.vscode/tasks.json`, `BlogHelper9000/.vscode/launch.json`, `BlogHelper9000/.vscode/tasks.json`

- [ ] Update the README tech-stack table to match actual `PackageReference` versions (Terminal.Gui `2.4.16`, SixLabors.ImageSharp `4.0.0`, SixLabors.Fonts `3.0.0`, System.IO.Abstractions `22.1.1`, TimeWarp.Nuru — check current version in `BlogHelper9000/BlogHelper9000.csproj`, Spectre.Console — check current version too).
- [ ] Remove `continue-on-error: true` from the `Setup .NET 10` step in `.github/workflows/main.yml`, since it currently masks SDK setup failures.
- [ ] Add a dependency-audit step to `.github/workflows/main.yml` after restore/build:
  ```yaml
  - name: Check for vulnerable packages
    run: dotnet list BlogHelper9000.sln package --vulnerable --include-transitive
  ```
- [ ] Add a `github-actions` ecosystem entry to `.github/dependabot.yml`:
  ```yaml
    - package-ecosystem: "github-actions"
      directory: "/"
      schedule:
        interval: "daily"
  ```
- [ ] Add `SIXLABORS_LICENSE_KEY` handling notes as a comment in `.github/workflows/main.yml` near the Cake step, and confirm/set the secret is referenced there if the build needs it in CI (`env: SIXLABORS_LICENSE_KEY: ${{ secrets.SIXLABORS_LICENSE_KEY }}`) — check whether this is already set up before adding a duplicate.
- [ ] Add entries to `.gitignore` for `*.DotSettings.user` and `.vscode/` (both root and nested), then untrack the currently-committed files:
  ```bash
  git rm --cached BlogHelper9000.sln.DotSettings.user .vscode/launch.json .vscode/tasks.json BlogHelper9000/.vscode/launch.json BlogHelper9000/.vscode/tasks.json
  ```
  (Do not delete the working-tree copies — only untrack them — since local dev setups may depend on the `.vscode` files existing on disk.)
- [ ] Run `dotnet build BlogHelper9000.sln` to confirm nothing depended on the removed CI leniency, and `dotnet test BlogHelper9000.sln` for a final full pass.

## Task 17: Test suite cleanup (BH9000-024)

**Files:**
- Modify: `BlogHelper9000.Tests/Commands/FixCommandTests.cs`

- [ ] Remove (not just comment out) the vacuous `Should_Output_Help` test and the `Should_Output_Options` theory whose bodies are fully commented out — these were replaced in intent by the real `FixCommand`-via-`IBlogService` tests added in Task 6. If CLI-host-level help-text tests are wanted, write real ones against the Nuru host (`app.RunAsync(["fix", "-h"])` capturing console output) instead of leaving commented-out placeholders; otherwise just delete them.
- [ ] Search for other analyzer-warning sources flagged by the review (`dotnet test BlogHelper9000.sln --no-restore` and inspect warnings for unused theory parameters / unused variables) and fix each one at the source (remove unused parameters, name-fix `_` discards, etc.).
- [ ] Run `dotnet test BlogHelper9000.sln` one final time and confirm the warning count for these specific items has dropped.

---

## Final Verification

- [ ] Run `dotnet build BlogHelper9000.sln` — zero errors.
- [ ] Run `dotnet test BlogHelper9000.sln` — all tests pass (compare pass/skip counts against the review's baseline: MCP 13, Nvim 41, CLI/Core 36 passed + 2 skipped, TUI 77 — investigate any count that dropped instead of grew).
- [ ] Re-read `CODE_REVIEW.md` top to bottom and confirm each of the 29 findings' "Suggested follow-up" bullets has a corresponding change in the tasks above; note any deliberately deferred item (e.g. full YamlDotNet migration for BH9000-010 is intentionally out of scope for this plan — Task 2/3 close the highest-severity semantic and safety gaps in the custom parser without a full rewrite; add a short note to `CODE_REVIEW.md` or a follow-up issue recording this scope decision).
