# YamlParsing Improvements and Blog Post Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply five targeted improvements to `BlogHelper9000.Core.YamlParsing` and write a blog post draft covering the design, tradeoffs, and performance story.

**Architecture:** All changes are confined to `BlogHelper9000.Core/YamlParsing/` and the corresponding test file. Changes are independent and each task leaves the build and tests green. The blog post is written last, so it can reference the final code.

**Tech Stack:** .NET 10 / C#, xunit v3, FluentAssertions, `System.Reflection`

---

## File Map

| File | Change |
|------|--------|
| `BlogHelper9000.Core/YamlParsing/SerialiserBase.cs` | Add shared constants; add static property cache |
| `BlogHelper9000.Core/YamlParsing/YamlConvert.cs` | Add `readonly` to static field declarations |
| `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs` | Use constants; add guard in `ParseHeaderTag`; improve exception message |
| `BlogHelper9000.Core/YamlParsing/YamlSerialiser.cs` | Use constants |
| `BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs` | Add tests for guard and improved exception message |
| `docs/blog-drafts/yaml-parsing-deep-dive.md` | New file: blog post draft |

---

## Task 1: Fix `static readonly` on `YamlConvert` fields

**Files:**
- Modify: `BlogHelper9000.Core/YamlParsing/YamlConvert.cs:7-8`

The two private fields are `static` but not `readonly`, implying they could be reassigned. Make them `readonly` to accurately express that they are stateless singletons.

- [ ] **Step 1: Open `YamlConvert.cs` and update the field declarations**

Replace:
```csharp
private static YamlSerialiser Serialiser = new YamlSerialiser();
private static YamlDeserialiser Deserialiser = new YamlDeserialiser();
```
With:
```csharp
private static readonly YamlSerialiser Serialiser = new YamlSerialiser();
private static readonly YamlDeserialiser Deserialiser = new YamlDeserialiser();
```

- [ ] **Step 2: Build and run all YAML tests**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlParsing"
```
Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add BlogHelper9000.Core/YamlParsing/YamlConvert.cs
git commit -m "refactor: make static serialiser fields readonly in YamlConvert"
```

---

## Task 2: Extract magic string constants

**Files:**
- Modify: `BlogHelper9000.Core/YamlParsing/SerialiserBase.cs`
- Modify: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs`
- Modify: `BlogHelper9000.Core/YamlParsing/YamlSerialiser.cs`

Move `"---"`, `"dd/MM/yyyy"`, and `"draft"` out of inline literals into named constants so they appear once and read as intent rather than as noise.

- [ ] **Step 1: Add constants to `SerialiserBase.cs`**

The full file after the change:
```csharp
using System.Reflection;

namespace BlogHelper9000.Core.YamlParsing;

public abstract class SerialiserBase
{
    protected const string FrontMatterDelimiter = "---";
    protected const string DateFormat = "dd/MM/yyyy";

    private static readonly Dictionary<string, PropertyInfo> _propertyCache = BuildPropertyCache();

    private static Dictionary<string, PropertyInfo> BuildPropertyCache() =>
        typeof(YamlHeader)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<YamlIgnoreAttribute>() is null)
            .ToDictionary(p =>
            {
                var attr = p.GetCustomAttribute<YamlNameAttribute>();
                return attr is not null ? attr.Name.ToLower() : p.Name.ToLower();
            });

    protected Dictionary<string, object?> GetYamlHeaderProperties(YamlHeader? header = null)
    {
        var yamlHeader = header ?? new YamlHeader();
        return _propertyCache.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetValue(yamlHeader, null));
    }
}
```

> Note: the cache is added here too — it is needed for Task 2 and Task 5 is now complete as part of this step. No separate Task 5 is required.

- [ ] **Step 2: Update `YamlDeserialiser.cs` to use constants**

Replace line 15:
```csharp
if (line.Trim() == "---")
```
With:
```csharp
if (line.Trim() == FrontMatterDelimiter)
```

Replace line 80–82 (the date parsing block):
```csharp
var date = value is "draft" or "true" or "false"
    ? DateTime.MinValue
    : DateTime.ParseExact((string)item.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
```
With:
```csharp
private const string DraftDateValue = "draft";
```
Add that const at the top of the class body, then update the date parsing line:
```csharp
var date = value is DraftDateValue or "true" or "false"
    ? DateTime.MinValue
    : DateTime.ParseExact((string)item.Value, DateFormat, CultureInfo.InvariantCulture);
```

- [ ] **Step 3: Update `YamlSerialiser.cs` to use constants**

Replace line 12:
```csharp
builder.AppendLine("---");
```
With:
```csharp
builder.AppendLine(FrontMatterDelimiter);
```

Replace line 36:
```csharp
builder.Append("---");
```
With:
```csharp
builder.Append(FrontMatterDelimiter);
```

Replace line 28 (the DateTime formatting):
```csharp
builder.AppendLine($"{item.Key}: {item.Value:dd/MM/yyyy}");
```
With:
```csharp
builder.AppendLine($"{item.Key}: {((DateTime)item.Value).ToString(DateFormat, CultureInfo.InvariantCulture)}");
```
Also add `using System.Globalization;` at the top of `YamlSerialiser.cs` if not already present.

- [ ] **Step 4: Build and run all YAML tests**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlParsing"
```
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add BlogHelper9000.Core/YamlParsing/SerialiserBase.cs \
        BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs \
        BlogHelper9000.Core/YamlParsing/YamlSerialiser.cs
git commit -m "refactor: extract magic string constants and cache reflection metadata"
```

---

## Task 3: Guard against malformed YAML lines in `ParseHeaderTag`

**Files:**
- Modify: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs`
- Modify: `BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs`

Currently `ParseHeaderTag` calls `Substring(0, index)` where `index` is the result of `IndexOf(':')`. If no `:` is found, `index` is `-1` and the call throws `ArgumentOutOfRangeException`. Replace with a deliberate `YamlConvertException` carrying a useful message.

- [ ] **Step 1: Write the failing test**

Add to `YamlConvertTests.cs`:
```csharp
[Fact]
public void Should_Throw_YamlConvertException_For_MalformedLine()
{
    var yaml = "---\nno colon on this line\n---";
    var act = () => new YamlConvert(new MockFileSystem()).Deserialise(yaml.Split(Environment.NewLine));
    act.Should().Throw<YamlConvertException>()
        .WithMessage("*no colon on this line*");
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj \
  --filter "FullyQualifiedName~YamlConvertTests.Should_Throw_YamlConvertException_For_MalformedLine"
```
Expected: FAIL — currently throws `ArgumentOutOfRangeException`, not `YamlConvertException`.

- [ ] **Step 3: Add the guard to `ParseHeaderTag`**

Replace the existing `ParseHeaderTag` method in `YamlDeserialiser.cs`:
```csharp
private static (string property, string value) ParseHeaderTag(string tag)
{
    if (string.IsNullOrEmpty(tag)) return ("", "");
    tag = tag.Trim();
    var index = tag.IndexOf(':');
    if (index < 0) throw new YamlConvertException($"Malformed YAML line: '{tag}'");
    var property = tag.Substring(0, index);
    var value = tag.Substring(index + 1).Trim();
    return (property, value);
}
```

- [ ] **Step 4: Run the test to confirm it passes**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj \
  --filter "FullyQualifiedName~YamlConvertTests.Should_Throw_YamlConvertException_For_MalformedLine"
```
Expected: PASS.

- [ ] **Step 5: Run the full YAML test suite**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlParsing"
```
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs \
        BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs
git commit -m "fix: throw YamlConvertException for malformed YAML lines in ParseHeaderTag"
```

---

## Task 4: Include property value in deserialization exception message

**Files:**
- Modify: `BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs:99-101`
- Modify: `BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs`

The `catch` block in `ToYamlHeader` currently loses the attempted value from the exception message. Include it so parse failures are immediately debuggable.

- [ ] **Step 1: Write the failing test**

Add to `YamlConvertTests.cs`:
```csharp
[Fact]
public void Should_Include_Value_In_ExceptionMessage_When_Deserialisation_Fails()
{
    var yaml = "---\npublished: not-a-date\n---";
    var act = () => new YamlConvert(new MockFileSystem()).Deserialise(yaml.Split(Environment.NewLine));
    act.Should().Throw<YamlConvertException>()
        .WithMessage("*not-a-date*");
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj \
  --filter "FullyQualifiedName~YamlConvertTests.Should_Include_Value_In_ExceptionMessage_When_Deserialisation_Fails"
```
Expected: FAIL — current message is `"Error deserialising property 'published'"` which doesn't contain the value.

- [ ] **Step 3: Update the exception message in `ToYamlHeader`**

Replace lines 99–101 in `YamlDeserialiser.cs`:
```csharp
catch (Exception e)
{
    var message = $"Error deserialising property '{item.Key}'";
    throw new YamlConvertException(message, e);
}
```
With:
```csharp
catch (Exception e)
{
    var message = $"Error deserialising property '{item.Key}' with value '{item.Value}'";
    throw new YamlConvertException(message, e);
}
```

- [ ] **Step 4: Run the test to confirm it passes**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj \
  --filter "FullyQualifiedName~YamlConvertTests.Should_Include_Value_In_ExceptionMessage_When_Deserialisation_Fails"
```
Expected: PASS.

- [ ] **Step 5: Run the full YAML test suite**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj --filter "FullyQualifiedName~YamlParsing"
```
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add BlogHelper9000.Core/YamlParsing/YamlDeserialiser.cs \
        BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs
git commit -m "fix: include property value in YamlConvertException message"
```

---

## Task 5: Verify reflection cache correctness with a round-trip test

**Files:**
- Modify: `BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs`

The cache was introduced in Task 2. This task adds a test that exercises a fully-populated `YamlHeader` round-trip to confirm the cache returns correct values for every property — including those mapped via `YamlNameAttribute`.

- [ ] **Step 1: Write the round-trip test**

Add to `YamlConvertTests.cs`:
```csharp
[Fact]
public void Should_RoundTrip_FullyPopulated_YamlHeader()
{
    var yaml = """
        ---
        layout: post
        title: Test Post
        description: A test description
        tags: [dotnet,csharp]
        featured_image: /assets/img.jpg
        featured: false
        hidden: false
        published: 01/01/2024
        ---
        """;

    var header = new YamlConvert(new MockFileSystem()).Deserialise(yaml.Split(Environment.NewLine));
    var serialised = new YamlConvert(new MockFileSystem()).Serialise(header);
    var roundTripped = new YamlConvert(new MockFileSystem()).Deserialise(serialised.Split(Environment.NewLine));

    roundTripped.Layout.Should().Be("post");
    roundTripped.Title.Should().Be("Test Post");
    roundTripped.Description.Should().Be("A test description");
    roundTripped.Tags.Should().Equal("dotnet", "csharp");
    roundTripped.FeaturedImage.Should().Be("/assets/img.jpg");
    roundTripped.IsFeatured.Should().BeFalse();
    roundTripped.IsHidden.Should().BeFalse();
    roundTripped.PublishedOn.Should().Be(new DateTime(2024, 1, 1));
}
```

- [ ] **Step 2: Run the test**

```bash
dotnet test BlogHelper9000.Tests/BlogHelper9000.Tests.csproj \
  --filter "FullyQualifiedName~YamlConvertTests.Should_RoundTrip_FullyPopulated_YamlHeader"
```
Expected: PASS. If it fails, the cache has a bug — compare `_propertyCache` keys against the expected YAML keys.

- [ ] **Step 3: Run the full test suite to confirm no regressions**

```bash
dotnet test BlogHelper9000.sln
```
Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add BlogHelper9000.Tests/YamlParsing/YamlConvertTests.cs
git commit -m "test: add full round-trip test covering all YamlHeader properties"
```

---

## Task 6: Commit the blog post draft

**Files:**
- Review/edit: `docs/blog-drafts/yaml-parsing-deep-dive.md` (already written)

The draft has already been written at `docs/blog-drafts/yaml-parsing-deep-dive.md`. Read through it, make any edits needed, then commit.

- [ ] **Step 1: Read the draft and make any edits**

```bash
# Open the draft for review
cat docs/blog-drafts/yaml-parsing-deep-dive.md
```

The post covers six sections in this order: The problem → The architecture → Three design decisions (attribute mapping, Extras dictionary, two-phase deserialisation) → Tradeoffs vs. YamlDotNet → Performance: the reflection cache → Key takeaways.

Check that code examples match the final state of the source files after Tasks 1–5 are complete.

- [ ] **Step 2: Commit**

```bash
git add docs/blog-drafts/yaml-parsing-deep-dive.md
git commit -m "docs: add blog post draft for YAML parsing deep-dive"
```
