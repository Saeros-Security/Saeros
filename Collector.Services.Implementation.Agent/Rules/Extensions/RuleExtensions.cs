using Collector.Core;
using Collector.Core.Extensions;
using Collector.Detection.Extensions;
using Collector.Detection.Rules;
using Collector.Services.Abstractions.Rules;
using Google.Protobuf;
using Shared;
using Streaming;

namespace Collector.Services.Implementation.Agent.Rules.Extensions;

public static class RuleExtensions
{
    public static RuleContract ToContract(this RuleBase rule, RuleBuiltinType ruleBuiltinType, AuditPolicyVolume auditPolicyVolume, RuleSource ruleSource, bool enabled, string content, string? groupName)
    {
        var ruleContract = new RuleContract
        {
            Id = rule.Id,
            Title = rule.Metadata.Title,
            Date = rule.Metadata.Date,
            Author = rule.Metadata.Author,
            Level = rule.Metadata.Level,
            Status = rule.Metadata.Status
        };

        if (!string.IsNullOrWhiteSpace(rule.Metadata.Modified))
        {
            ruleContract.Modified = rule.Metadata.Modified;
        }
        
        if (!string.IsNullOrWhiteSpace(rule.Metadata.Details))
        {
            ruleContract.Details = rule.Metadata.Details;
        }
        
        if (!string.IsNullOrWhiteSpace(rule.Metadata.Description))
        {
            ruleContract.Description = rule.Metadata.Description;
        }
        
        if (rule.Metadata.Tags.Any())
        {
            ruleContract.Tags.AddRange(rule.Metadata.Tags);
        }
        
        if (rule.Metadata.References.Any())
        {
            ruleContract.References.AddRange(rule.Metadata.References);
        }
        
        if (rule.Metadata.FalsePositives.Any())
        {
            ruleContract.FalsePositives.AddRange(rule.Metadata.FalsePositives);
        }
        
        if (rule.Metadata.CorrelationOrAggregationTimeSpan.HasValue)
        {
            ruleContract.CorrelationOrAggregationTimeSpan = rule.Metadata.CorrelationOrAggregationTimeSpan.Value.Ticks;
        }

        ruleContract.Builtin = ruleBuiltinType == RuleBuiltinType.Builtin;
        ruleContract.Enabled = enabled;
        ruleContract.Content = ByteString.CopyFromUtf8(content);
        ruleContract.GroupName = groupName ?? string.Empty;
        ruleContract.Mitre.AddRange(MitreExtensions.GetMitre(rule.Metadata.Tags, mitre => new MitreContract { Id = mitre.Id, Tactic = mitre.Tactic, Technique = mitre.Technique, SubTechnique = mitre.SubTechnique }));
        ruleContract.Source = ruleSource.FromSource();
        ruleContract.Volume = (int)auditPolicyVolume;
        return ruleContract;
    }
}