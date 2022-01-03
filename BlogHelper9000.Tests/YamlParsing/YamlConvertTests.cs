using BlogHelper9000.YamlParsing;


namespace BlogHelper9000.Tests.YamlParsing;

public class YamlConvertTests
{
    [Fact]
    public void Should_Deserialise_YamlHeader()
    {
        var yaml = @"---
layout: post
title: 'Dynamic port assignment in Octopus Deploy'
tags: ['build tools', 'octopus deploy']
featured_image: /assets/images/posts/2020/artem-sapegin-b18TRXc8UPQ-unsplash.jpg
featured: false
hidden: false
---
post content that's not parsed";
        
        var yamlObject = YamlConvert.Deserialise(yaml.Split(Environment.NewLine));

        yamlObject.Layout.Should().Be("post");
        yamlObject.Tags.Should().NotBeEmpty();
    }

    [Fact]
    public void Should_Deserialise_YamlHeader_And_CollectUnofficialPropertiesAndValues()
    {
        var yaml = @"---
title: Learning ReactiveUI for fun and profit&#58; Hello, World!
published: 17/11/2013
layout: post
categories: ReactiveUI
---";

        var header = YamlConvert.Deserialise(yaml.Split(Environment.NewLine));

        header.Extras.Should().ContainKey("categories");
    }

    [Fact]
    public void Should_Serialise_YamlHeader()
    {
        var expected = @"---
layout: post
title: 'Dynamic port assignment in Octopus Deploy'
tags: ['build tools','octopus deploy']
featured_image: /assets/images/posts/2020/artem-sapegin-b18TRXc8UPQ-unsplash.jpg
featured: False
hidden: False
---";
        var header = new YamlHeader
        {
            Layout = "post",
            Title = "'Dynamic port assignment in Octopus Deploy'",
            Tags = new List<string> { "'build tools'", "'octopus deploy'" },
            FeaturedImage = "/assets/images/posts/2020/artem-sapegin-b18TRXc8UPQ-unsplash.jpg",
            IsFeatured = false,
            IsHidden = false
        };

        var serialised = YamlConvert.Serialise(header);

        serialised.Should().Match(expected);
    }

    [Fact]
    public void Should_Deserialise_and_Serialise_AndBe_ExactlyTheSame()
    {
        var yaml = @"---
layout: post
title: ""Test Driven Development: Implementing Freecell - Part 3""
description: Developing a Freecell rules engine, using Test Driven Development in csharp - Part 3
series: ""TDD: Implementing Freecell""
---";
        var expected = @"---
layout: post
title: ""Test Driven Development: Implementing Freecell - Part 3""
description: Developing a Freecell rules engine, using Test Driven Development in csharp - Part 3
series: ""TDD: Implementing Freecell""
---";
        var header = YamlConvert.Deserialise(yaml.Split(Environment.NewLine));
        var serialised = YamlConvert.Serialise(header);

        serialised.Should().Match(yaml);
    }
}