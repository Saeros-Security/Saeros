using System.Text.Json.Serialization;
using Shared.Models.Console.Responses;

namespace Collector.Databases.Abstractions.Domain.Exclusions;

public sealed class Exclusion(string ruleId, IEnumerable<Computer> computers, IDictionary<string, string> attributes)
{
    [JsonPropertyName("RuleId")]
    public string RuleId { get; } = ruleId;

    [JsonPropertyName("Computers")]
    public IEnumerable<Computer> Computers { get; } = computers;

    [JsonPropertyName("Attributes")]
    public IDictionary<string, string> Attributes { get; } = attributes;
}