using Collector.Detection.Converters;
using Collector.Detection.Rules.Extensions;
using Collector.Tests.Conversion.Rules;
using Detection.Helpers;
using Detection.Yaml;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Collector.Tests.Conversion;

public class SigmaRuleConverterTests
{
    [Theory]
    [ClassData(typeof(SigmaRuleTestData<DetectionRule>))]
    [ClassData(typeof(SigmaRuleTestData<HayabusaRule>))]
    [ClassData(typeof(SigmaRuleTestData<Rules.f3f3a972_f982_40ad_b63c_bca6afdfad7c.SigmaRule>))]
    [ClassData(typeof(SigmaRuleTestData<Rules._93ff0ceb_e0ef_4586_8cd8_a6c277d738e3.SigmaRule>))]
    [ClassData(typeof(SigmaRuleTestData<Rules._5570c4d9_8fdd_4622_965b_403a5a101aa0.SigmaRule>))]
    [ClassData(typeof(SigmaRuleTestData<Rules.a762e74f_4dce_477c_b023_4ed81df600f9.SigmaRule>))]
    [ClassData(typeof(SigmaRuleTestData<Rules._19b041f6_e583_40dc_b842_d6fa8011493f.SigmaRule>))]
    [ClassData(typeof(SigmaRuleTestData<Rules._4f86b304_3e02_40e3_aa5d_e88a167c9617.SigmaRule>))]
    public void SigmaRuleConverter_Should_Convert(string sigmaRule, string expected)
    {
        SigmaRuleConverter.TryConvertSigmaRule(NullLogger.Instance, sigmaRule, sysmonInstalled: true, out var convertedSigmaRule, out _).Should().BeTrue();
        convertedSigmaRule.Should().NotBeNullOrEmpty();
        convertedSigmaRule?.Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
        Helper.TryGetRule(convertedSigmaRule!, out _, out _, out _).Should().BeTrue();
    }
    
    [Theory]
    [ClassData(typeof(SigmaRuleTestData<Rules._763b1967_8120_4b86_b35f_00e6ec31ff21.SigmaRule>))]
    public void SigmaRuleConverter_Should_Convert_Non_Sysmon_Rule_With_Sysmon_Not_Installed(string sigmaRule, string expected)
    {
        SigmaRuleConverter.TryConvertSigmaRule(NullLogger.Instance, sigmaRule, sysmonInstalled: false, out var convertedSigmaRule, out _).Should().BeTrue();
        convertedSigmaRule.Should().NotBeNullOrEmpty();
        convertedSigmaRule?.Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
        Helper.TryGetRule(convertedSigmaRule!, out _, out _, out _).Should().BeTrue();
    }
    
    [Theory]
    [ClassData(typeof(SigmaRuleTestData<TemporalCorrelationRule>))]
    public void SigmaRuleConverter_Should_Not_Convert(string sigmaRule, string expected)
    {
        SigmaRuleConverter.TryConvertSigmaRule(NullLogger.Instance, sigmaRule, sysmonInstalled: true, out _, out var error).Should().BeFalse();
        error.Should().Be("This rule has unsupported correlation type");
    }
    
    [Fact]
    public async Task EnumerateBuiltinRule_Should_Enumerate()
    {
        await foreach (var yaml in RuleHelper.EnumerateSigmaBuiltinRules(CancellationToken.None))
        {
            var rules = YamlParser.DeserializeMany<YamlRule>(yaml);
            foreach (var rule in rules)
            {
                var metadata = rule.ToMetadata();
                metadata.Should().NotBeNull();
                metadata.Id.Should().NotBeNullOrEmpty();
            }
        }
    }
}