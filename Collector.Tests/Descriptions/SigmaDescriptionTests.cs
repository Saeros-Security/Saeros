using Detection.Helpers;
using Detection.Yaml;
using FluentAssertions;

namespace Collector.Tests.Descriptions;

public class SigmaDescriptionTests
{
    [Fact]
    public async Task SigmaDescriptionHelper_Should_Get_Descriptions()
    {
        var descriptions = DescriptionHelper.GetDescriptions();
        await foreach (var yaml in RuleHelper.EnumerateSigmaBuiltinRules(CancellationToken.None))
        {
            var rules = YamlParser.DeserializeMany<YamlRule>(yaml);
            foreach (var rule in rules)
            {
                if (rule.Id == "76355548-fa5a-4310-9610-0de4b11f4688") continue; // NULL
                if (rule.Id == "15d042c1-07c6-4e16-ae7d-e0e556ccd9a8") continue; // NULL
                descriptions.TryGetValue(rule.Id, out var description).Should().BeTrue();
                description.Should().NotBeNullOrEmpty();
            }
        }
    }
}