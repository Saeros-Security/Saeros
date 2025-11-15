using System.Text.Json.Serialization;
using Shared.Models.Detections;

namespace Collector.Integrations.Abstractions;

public sealed class EventExport(string title, IDictionary<string, string> system, IDictionary<string, string> data)
{
    [JsonPropertyName("Title")] 
    public string Title { get; } = title;
    
    [JsonPropertyName("System")] 
    public IDictionary<string, string> System { get; } = system;
    
    [JsonPropertyName("Data")] 
    public IDictionary<string, string> Data { get; } = data;
}

public sealed class MitreExport(string tactic, string technique, string subTechnique)
{
    [JsonPropertyName("Tactic")] 
    public string Tactic { get; } = tactic;
    
    [JsonPropertyName("Technique")] 
    public string Technique { get; } = technique;
    
    [JsonPropertyName("SubTechnique")] 
    public string SubTechnique { get; } = subTechnique;
}

public sealed class Export(string ruleId, string title, string computer, DateTimeOffset date, DetectionSeverity severity, IDictionary<string, string> details, EventExport @event, MitreExport mitre)
{
    [JsonPropertyName("RuleId")] 
    public string RuleId { get; } = ruleId;
    
    [JsonPropertyName("Title")] 
    public string Title { get; } = title;
    
    [JsonPropertyName("Description")] 
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Computer")]
    public string Computer { get; } = computer;

    [JsonPropertyName("Date")] 
    public DateTimeOffset Date { get; } = date;

    [JsonPropertyName("Severity")]
    public DetectionSeverity Severity { get; } = severity;

    [JsonPropertyName("Details")]
    public IDictionary<string, string> Details { get; } = details;

    [JsonPropertyName("Event")]
    public EventExport Event { get; } = @event;
    
    [JsonPropertyName("Mitre")]
    public MitreExport Mitre { get; } = @mitre;
}