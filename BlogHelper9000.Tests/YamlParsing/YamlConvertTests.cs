using BlogHelper9000.YamlParsing;


namespace BlogHelper9000.Tests.YamlParsing;

public class YamlConvertTests
{
    [Fact]
    public void Should_Load_YamlHeader()
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
    }
}