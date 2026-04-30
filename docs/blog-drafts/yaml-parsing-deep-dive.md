---
title: "Parsing Jekyll Front Matter in C# — Design, Tradeoffs, and Performance"
description: A deep-dive into building a custom YAML front matter parser for Jekyll blog posts in C#, covering the attribute-based design, tradeoffs vs. a library, and a concrete performance improvement using reflection caching.
tags: [csharp, dotnet, yaml, jekyll, reflection]
---

## The problem

Every Jekyll blog post starts with a YAML block called front matter — a small island of structured metadata between two `---` delimiters:

```yaml
---
layout: post
title: "My Blog Post"
tags: [csharp, dotnet]
featured_image: /assets/img.jpg
published: 01/01/2024
---
```

If you are building .NET tooling around a Jekyll blog — generating posts, updating metadata, tagging images — you need to read and write this block. The question is: do you reach for a YAML library, or do you write something tailored?

I chose to write something tailored, and it turned out to be an interesting design problem. This post walks through the result: a small, attribute-driven parser that handles everything the Jekyll format needs and nothing it does not.

---

## The architecture

The parser lives in a handful of classes with clear, separated responsibilities.

`YamlHeader` is the data model — a plain C# class with one property per Jekyll front matter field. You interact with it directly throughout the rest of the codebase, which means you get strong typing, IDE completion, and null safety.

`YamlConvert` is the public facade. It takes an `IFileSystem` for testability, exposes three methods (`Serialise`, and two overloads of `Deserialise`), and delegates all real work to `YamlSerialiser` and `YamlDeserialiser`. Callers never need to know which class actually does the parsing.

`SerialiserBase` is an abstract base that both the serialiser and deserialiser inherit from. Its one job is to provide a method that turns a `YamlHeader` instance into a `Dictionary<string, object?>` keyed by YAML field name. Keeping this in a shared base means the two workers never diverge in how they discover and name properties.

`YamlSerialiser` and `YamlDeserialiser` are sealed classes that each do one thing. The serialiser walks the property dictionary and builds a string. The deserialiser parses a string array and populates a `YamlHeader`.

---

## Three design decisions worth explaining

### 1. Attribute-driven name mapping

Jekyll uses snake_case keys. C# uses PascalCase properties. The most common approach to bridging this is a mapping configuration somewhere — a dictionary, a convention class, or a fluent builder. Instead, I put the mapping co-located with the property via a custom attribute:

```csharp
[YamlName("featured_image")]
public string? FeaturedImage { get; set; }
```

When the serialiser or deserialiser needs to know that `FeaturedImage` maps to `featured_image`, it reads the attribute at the property site. If you rename the property in a refactor, the attribute stays with it. There is no separate file to keep in sync.

A `YamlIgnoreAttribute` handles the opt-out case — properties like `Extras` (explained below) that should not appear in serialised YAML:

```csharp
[YamlIgnore]
public Dictionary<string, string> Extras { get; set; } = new();
```

### 2. The `Extras` dictionary

Jekyll front matter is an open format. Real-world posts accumulate fields that predate the current model — `categories`, `comments`, `permalink` — and a parser that throws on unknown keys is fragile when you are reading files you did not create.

The deserialiser handles this by routing any key it does not recognise into a catch-all dictionary:

```csharp
if (headerProperties.ContainsKey(propertyValue.property))
{
    parsedHeaderProperties.Add(propertyValue.property, propertyValue.value);
}
else
{
    extraHeaderProperties.Add(propertyValue.property, propertyValue.value);
}
```

`YamlHeader.Extras` then holds those pairs. When the header is serialised back out, `[YamlIgnore]` ensures they are not written — you have to handle extras explicitly if you want to preserve them. That is a deliberate choice: the model is the source of truth, not the raw YAML.

### 3. Two-phase deserialisation

The deserialiser works in two passes. First, it parses the raw YAML block into a `Dictionary<string, object>` — just string keys and string values, no type conversions yet. Then, in a second pass, it maps that dictionary onto a typed `YamlHeader`, converting values to `string`, `bool?`, `DateTime?`, or `List<string>` as the target property requires.

This separation is what makes `Extras` natural: unknown keys simply never make it into the typed pass and accumulate in their own dictionary without disrupting the main mapping.

---

## Tradeoffs: rolling your own vs. YamlDotNet

The honest answer is that this parser works well for its narrow use-case and would break in ways that matter if that scope expanded.

**What it handles correctly:** every YAML key-value pattern Jekyll actually produces. Simple strings, booleans, dates in `dd/MM/yyyy` format, and square-bracket tag lists.

**What it does not handle:** escaped characters in list values (the parser strips `[` and `]` with string replacement, so a tag containing a bracket would break), multi-document YAML, anchors, aliases, block scalars, or any format deviation from what the tool itself writes.

**YamlDotNet** would handle all of the above. It supports naming conventions natively (so you get snake_case↔PascalCase mapping without custom attributes), is actively maintained, and is the obvious choice if you are parsing YAML you do not control. The cost is a dependency and the need to configure it correctly — not a high bar, but a real one.

For a self-contained tool that owns the YAML it reads and writes, the custom parser is defensible. It is small, readable, has no external dependencies, and its failure modes are known. For anything that reads arbitrary YAML, use a library.

---

## Performance: the reflection cache

Every time `YamlConvert.Serialise` or `YamlConvert.Deserialise` is called, the original implementation called `GetType().GetProperties(...)` on `YamlHeader`, iterated each property, read its custom attributes, and built a dictionary. This happened from scratch on every single call.

Reflection is not free. The type of `YamlHeader` never changes at runtime, so there is no reason to re-discover its properties repeatedly. The fix is a `static readonly` cache populated once at startup:

```csharp
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
```

`GetYamlHeaderProperties` now consults the cache rather than reflecting each time:

```csharp
protected Dictionary<string, object?> GetYamlHeaderProperties(YamlHeader? header = null)
{
    var yamlHeader = header ?? new YamlHeader();
    return _propertyCache.ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value.GetValue(yamlHeader, null));
}
```

The per-call cost is now just value reads — `PropertyInfo.GetValue` — rather than type traversal and attribute scanning. For a tool processing hundreds of posts in a batch operation, this is a meaningful improvement. For a one-shot parse it is noise, but it is a useful pattern to have regardless.

If you want to measure the difference, [BenchmarkDotNet](https://benchmarkdotnet.org) makes this straightforward — and is a good candidate for a follow-up post.

---

## Key takeaways

- **Attribute-driven name mapping** keeps the YAML key co-located with the C# property, reducing drift during refactoring.
- **Two-phase deserialisation** — raw dictionary first, typed mapping second — makes it natural to collect unknown keys without disrupting the main model.
- **Cache reflection metadata** in a `static readonly` field when the type is fixed. The setup cost is paid once; every subsequent call is fast.
- **Rolling your own parser is defensible** when you own the format, have a narrow scope, and want zero dependencies — but be honest about the gaps, and reach for a library the moment scope widens.
