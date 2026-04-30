# Design: YamlParsing Code Review, Improvements, and Blog Post

## Context

The `BlogHelper9000.Core.YamlParsing` namespace is a hand-rolled YAML front matter parser for Jekyll blog posts. The author has an existing blog series covering its construction and has seen strong search/traffic results. The goal is to produce a fresh, standalone deep-dive blog post (Option B) for a general C# developer audience, supported by targeted code improvements that become material for the post.

## Blog Post

**Title:** "Parsing Jekyll Front Matter in C# — Design, Tradeoffs, and Performance"

**Audience:** General C# developers. No prior knowledge of the existing series required.

**Structure:**

### Section 1 — The problem and context
Introduce Jekyll front matter: a YAML block delimited by `---` at the top of every post file. Frame the question: you're building .NET tooling and need to read and write it — do you pull in a library or roll your own? Leave that open to pull the reader through.

### Section 2 — The architecture
Walk through the design top-down:
- `YamlConvert` — public facade, takes `IFileSystem` for testability, delegates to serialiser/deserialiser
- `YamlSerialiser` / `YamlDeserialiser` — the workers
- `SerialiserBase` — shared reflection utilities, property discovery
- `YamlHeader` — strongly-typed model for Jekyll metadata
- `YamlNameAttribute` — maps PascalCase C# properties to snake_case YAML keys
- `YamlIgnoreAttribute` — opts properties out of serialization

### Section 3 — Interesting design decisions
Three decisions worth a paragraph each:
1. **The `Extras` dictionary** — unknown YAML keys are captured rather than discarded, giving forward-compatibility for metadata fields the model doesn't know about yet
2. **Attribute-driven reflection** — `YamlNameAttribute` keeps the mapping co-located with the property rather than in a separate config, reducing drift
3. **Two-phase deserialization** — raw key/value dictionary first, then typed mapping — is what makes `Extras` possible and worth explaining

### Section 4 — Tradeoffs: rolling your own vs. YamlDotNet
Honest comparison:
- Custom: handles the exact needed subset, zero dependencies, full control
- Custom gaps: no escape handling in list values, fragile date parsing, no multi-document support
- YamlDotNet: handles all of the above, but adds a dependency and requires naming convention configuration
- Verdict: for a tightly-scoped tool that owns its YAML format, custom is defensible. For anything more open-ended, use a library.

### Section 5 — Performance: caching reflection
The current code re-reflects `YamlHeader` properties on every call. The fix — a `static readonly` cache built on first use — is a single illustrative change that demonstrates a broadly applicable principle. Show before/after code. Mention `BenchmarkDotNet` as the natural follow-up.

### Conclusion
Key takeaways: attribute pattern for bidirectional name mapping, two-phase parse for unknown keys, when to own a parser vs. delegate.

## Code Improvements

Five targeted changes to make before (or alongside) writing the post. All are small, safe, and independently applicable.

### 1. Cache reflected property metadata (performance)
**File:** `SerialiserBase.cs`
**Change:** Replace the per-call reflection in `GetYamlHeaderProperties()` with a `static readonly` dictionary populated once on first call.
**Why it matters:** Every serialize/deserialize call currently pays full reflection cost. This is the post's performance story.

### 2. Fix static fields in `YamlConvert` (architectural clarity)
**File:** `YamlConvert.cs`
**Change:** `Serialiser` and `Deserialiser` are declared `private static` on an instance class. Make them `private static readonly` explicitly or make the class static. They are stateless so no behaviour changes.
**Why it matters:** Removes an odd hybrid that implies instance state where there is none.

### 3. Extract magic string constants (readability)
**Files:** `YamlDeserialiser.cs`, `YamlSerialiser.cs`
**Change:** Pull `"---"`, `"dd/MM/yyyy"`, and `"draft"` into `private const string` declarations.
**Why it matters:** Small, but worth mentioning in the post as baseline hygiene.

### 4. Better exception messages in deserialization (debugging quality)
**File:** `YamlDeserialiser.cs` — the broad `try/catch` in `ToYamlHeader()`
**Change:** Include the property name and attempted value in the `YamlConvertException` message.
**Why it matters:** Currently loses context that would make debugging parse failures fast.

### 5. Guard against malformed YAML lines (robustness)
**File:** `YamlDeserialiser.cs` — `ParseHeaderTag()`
**Change:** Check that `:` is present before calling `Substring`. Throw `YamlConvertException` with a descriptive message if not.
**Why it matters:** Currently surfaces as `IndexOutOfRangeException` — confusing and hard to trace.

## Out of Scope

- List value escape handling (the `Replace("[","")` hack) — fixing properly requires format changes
- `AllowMultiple = true` on attributes — real smell, but removing it is a distraction from the post's focus
- Migration to YamlDotNet — mentioned in the tradeoffs section but not implemented

## Success Criteria

- The five code improvements build cleanly with no regressions in `BlogHelper9000.Tests`
- The blog post is a self-contained read for a C# developer with no prior knowledge of the series
- Section 5 contains a concrete before/after code example for the reflection cache