using System.IO.Compression;
using Collector.Detection.Rules;
using Collector.Tests.Hayabusa.Converters;
using Collector.Tests.Hayabusa.EventRecords;
using Detection.Helpers;
using Detection.Yaml.Deserializers;
using Detection.Yaml.Extensions;
using Detection.Yaml.Resolvers;
using FluentAssertions;
using Shared;
using Shared.Extensions;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeTypeResolvers;
using YAXLib;
using YAXLib.Enums;
using YAXLib.Exceptions;
using YAXLib.Options;

namespace Collector.Tests.Hayabusa;

public class HayabusaTests(ITestOutputHelper testOutputHelper) : IAsyncLifetime
{
    private readonly IDictionary<string, StandardRule> _standardRules = new Dictionary<string, StandardRule>();
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithTypeConverter(new DynamicObjectConverter())
        .WithNodeTypeResolver(new MappingNodeResolver(), ls => ls.InsteadOf<DefaultContainersNodeTypeResolver>())
        .WithNodeDeserializer(new ListsAcceptScalarDeserializer())
        .WithNodeDeserializer(new ForceEmptyListsOnDeserialization())
        .Build();

    [Fact]
    public async Task Hayabusa_Rules_Should_Match()
    {
        var serializer = new YAXSerializer<YaxEventRecord>(new SerializerOptions { SerializationOptions = YAXSerializationOptions.DontSerializeNullObjects });
        using var client = new HttpClient();
        await using var response = await client.GetStreamAsync("https://github.com/Yamato-Security/hayabusa-rules/archive/refs/heads/main.zip", CancellationToken.None);
        await using var archive = new ZipArchive(response);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.Contains("hayabusa/builtin", StringComparison.OrdinalIgnoreCase) && !entry.FullName.Contains("sigma/builtin", StringComparison.OrdinalIgnoreCase)) continue;
            await using var stream = await entry.OpenAsync();
            using var streamReader = new StreamReader(stream);
            foreach (var value in _deserializer.DeserializeMany<IDictionary<string, object>>(streamReader))
            {
                if ((!value.TryGetValue("hayabusa-sample-evtx", out var sample) && !value.TryGetValue("sample-evtx", out sample)) || sample is not string evtx || string.IsNullOrWhiteSpace(evtx)) continue;
                try
                {
                    var deserialized = serializer.Deserialize(evtx);
                    if (deserialized?.System is null) continue;
                    if (deserialized.EventData?.Items is null) continue;
                    if (deserialized.EventData.Items.Any(item => string.IsNullOrWhiteSpace(item.Name))) continue;
                    if (deserialized.EventData.Items.Any(item => string.IsNullOrWhiteSpace(item.Value))) continue;
                    if (value.TryGetValue("id", out var ruleId) && ruleId is string id && _standardRules.TryGetValue(id, out var rule))
                    {
                        var system = new Dictionary<string, string>
                        {
                            { WinEventExtensions.ProviderNameKey, deserialized.System.Provider?.Name ?? throw new ArgumentNullException(WinEventExtensions.ProviderNameKey) },
                            { WinEventExtensions.EventIdKey, deserialized.System.EventID.ToString() },
                            { WinEventExtensions.ChannelKey, deserialized.System.Channel ?? throw new ArgumentNullException(WinEventExtensions.ChannelKey) },
                            { WinEventExtensions.ComputerKey, deserialized.System.Computer ?? throw new ArgumentNullException(WinEventExtensions.ComputerKey) }
                        };

                        var eventData = new Dictionary<string, string>();
                        foreach (var node in deserialized.EventData.Items)
                        {
                            if (!string.IsNullOrWhiteSpace(node.Name) && !string.IsNullOrWhiteSpace(node.Value))
                            {
                                eventData.Add(node.Name, node.Value);
                            }
                        }

                        var winEvent = new WinEvent(system, eventData);
                        rule.TryMatch(winEvent, out var match).Should().BeTrue();
                        testOutputHelper.WriteLine($"[{rule.Metadata.Title}] {match.DetectionDetails.Details}");
                    }
                }
                catch (YAXBadlyFormedXML)
                {
                    continue;
                }
            }
        }
    }

    public async Task InitializeAsync()
    {
        await foreach (var yaml in RuleHelper.EnumerateSigmaBuiltinRules(CancellationToken.None))
        {
            Helper.TryGetRule(yaml, out var rule, out _, out var error).Should().BeTrue();
            error.Should().BeNullOrEmpty();
            if (rule is StandardRule standardRule && !standardRule.Metadata.Tags.Contains("sysmon", StringComparer.OrdinalIgnoreCase))
            {
                _standardRules.TryAdd(standardRule.Id, standardRule);
            }
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}