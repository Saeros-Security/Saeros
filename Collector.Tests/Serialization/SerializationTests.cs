using Collector.Detection;
using Collector.Detection.Rules;
using Collector.Detection.Rules.Serializers;
using Collector.Tests.Serialization.Rules;
using Collector.Tests.Serialization.Rules.f224a2b6_2db1_a1a2_42d4_25df0c460915;
using FluentAssertions;
using Shared;

namespace Collector.Tests.Serialization;

public class SerializationTests
{
    [Theory]
    [ClassData(typeof(RuleTestData<TestData_Unmatched>))]
    public void RemoteRuleSerializer_Should_Serialize(string yamlRule, IList<WinEvent> winEvents, bool match, string? details)
    {
        Helper.TryGetRule(yamlRule, out var rule, out _, out var error).Should().BeTrue();
        error.Should().BeNullOrEmpty();
        if (rule is StandardRule standardRule)
        {
            using var serialized = standardRule.Serialize();
            serialized.Seek(0, SeekOrigin.Begin);
            if (serialized.Deserialize(RuleType.Standard) is StandardRule deserialized)
            {
                foreach (var winEvent in winEvents)
                {
                    deserialized.TryMatch(winEvent, out var ruleMatch).Should().Be(match);
                    if (match)
                    {
                        ruleMatch.WinEvent.Should().BeEquivalentTo(winEvent);
                        ruleMatch.Date.Should().Be(winEvent.SystemTime);
                        ruleMatch.DetectionDetails.RuleMetadata.Id.Should().Be(rule.Id);
                        ruleMatch.DetectionDetails.RuleMetadata.Should().BeEquivalentTo(rule.Metadata);
                        ruleMatch.DetectionDetails.Details.Should().Be(details); 
                    }   
                }
            }   
        }
    }
}