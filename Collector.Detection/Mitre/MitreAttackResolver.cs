using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Detection.Helpers;

namespace Collector.Detection.Mitre;

public static partial class MitreAttackResolver
{
    private sealed record Component([property:JsonPropertyName("Sub-Technique")]string SubTechnique, [property:JsonPropertyName("Tactic")]string Tactic,[property:JsonPropertyName("Technique")] string Technique);

    private static int Order(string input)
    {
        if (TechniqueRegex().IsMatch(input)) return -1;
        return 0;
    }
    
    public static IEnumerable<MitreComponent> GetComponents(IEnumerable<string> input)
    {
        var regex = AttackRegex();
        foreach (var item in input.OrderBy(Order))
        {
            var match = regex.Match(item); 
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = match.Groups[2].Value;
                }
                
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = match.Groups[3].Value;
                }

                if (string.IsNullOrWhiteSpace(value)) continue;
                if (Components.TryGetValue(value, out var component))
                {
                    yield return component;
                }
                else
                {
                    var sentence = value.Replace("-", " ");
                    foreach (var mitreComponent in Components.Values)
                    {
                        if (mitreComponent.Tactic.Equals(sentence, StringComparison.OrdinalIgnoreCase) &&
                            mitreComponent.SubTechnique.Equals("-") &&
                            mitreComponent.Technique.Equals("-"))
                        {
                            yield return mitreComponent;
                        }
                        else if (mitreComponent.Technique.Equals(sentence, StringComparison.OrdinalIgnoreCase))
                        {
                            yield return mitreComponent;
                        }
                        else if (mitreComponent.SubTechnique.Equals(sentence, StringComparison.OrdinalIgnoreCase))
                        {
                            yield return mitreComponent;
                        }
                    }
                }
            }
        }
    }

    static MitreAttackResolver()
    {
        var elements = JsonSerializer.Deserialize<Dictionary<string, Component>>(MitreHelper.GetMitreAttack());
        Components = elements!.ToDictionary(kvp => kvp.Key, kvp => new MitreComponent(kvp.Key, kvp.Value.Tactic, kvp.Value.Technique, kvp.Value.SubTechnique), StringComparer.OrdinalIgnoreCase);
    }
    
    public static readonly Dictionary<string, MitreComponent> Components;

    [GeneratedRegex("^attack\\.(t\\d+\\.?\\d+)?$|^attack\\.(.*)?$|Rule: Attack=(t\\d+\\.?\\d+)?", RegexOptions.IgnoreCase)]
    private static partial Regex AttackRegex();
    
    [GeneratedRegex("^attack\\.(t\\d+\\.?\\d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex TechniqueRegex();
}