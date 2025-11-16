using System.Text.Json.Serialization;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Implementation.Extensions;
using Streaming;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;

public class SuccessLogonTracer(long count, string targetAccount, ISet<string> targetComputer, ISet<string> logonType, ISet<string> sourceComputer, ISet<string> sourceIpAddress) : Tracer
{
    [JsonPropertyName("Count")]
    public long Count { get; } = count;
    
    [JsonPropertyName("TargetAccount")]
    public string TargetAccount { get; } = targetAccount;

    [JsonPropertyName("TargetComputer")]
    public ISet<string> TargetComputer { get; } = targetComputer;
    
    [JsonPropertyName("LogonType")]
    public ISet<string> LogonType { get; } = logonType;
    
    [JsonPropertyName("SourceComputer")]
    public ISet<string> SourceComputer { get; } = sourceComputer;

    [JsonPropertyName("SourceIpAddress")] 
    public ISet<string> SourceIpAddress { get; } = sourceIpAddress;
    
    public override TraceContract ToContract()
    {
        return new TraceContract
        {
            Content = this.FromTracer()
        };
    }
}