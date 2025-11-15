using System.Text.Json.Serialization;

namespace Collector.Detection.Rules;

public readonly struct RuleMetadata
{
    public RuleMetadata(string id, string title, string date, string? modified, string author, string? details, string? description, string level, string status, IEnumerable<string> tags, IEnumerable<string> references, IEnumerable<string> falsePositives, TimeSpan? correlationOrAggregationTimeSpan)
    {
        Id = id;
        Title = title;
        Date = date;
        Modified = modified;
        Author = author;
        Details = details;
        Description = description;
        Level = level;
        Status = status;
        Tags = tags;
        References = references;
        FalsePositives = falsePositives;
        CorrelationOrAggregationTimeSpan = correlationOrAggregationTimeSpan;
    }

    [JsonConstructor]
    public RuleMetadata(string id, string title, string date, string? modified, string author, string? details, string? description, string level, string status, TimeSpan? correlationOrAggregationTimeSpan) : this(id, title, date, modified, author, details, description, level, status, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), correlationOrAggregationTimeSpan)
    {
        
    }
    
    public string Id { get; }
    public string Title { get; }
    public string Date { get; }
    public string? Modified { get; }
    public string Author { get; }
    public string? Details { get; }
    public string? Description { get; }
    public string Level { get; }
    public string Status { get; }
    public IEnumerable<string> Tags { get; }
    public IEnumerable<string> References { get; }
    public IEnumerable<string> FalsePositives { get; }
    public TimeSpan? CorrelationOrAggregationTimeSpan { get; }
}