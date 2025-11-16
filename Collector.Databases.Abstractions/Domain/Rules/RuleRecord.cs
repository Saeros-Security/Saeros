namespace Collector.Databases.Abstractions.Domain.Rules;

public sealed record RuleRecord(string RuleId, string Description, string? MitreId, string? MitreTactic, string? MitreTechnique, string? MitreSubTechnique);