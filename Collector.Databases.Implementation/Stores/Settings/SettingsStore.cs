using System.Reactive.Subjects;
using Collector.Databases.Abstractions.Domain.Settings;
using Collector.Databases.Abstractions.Stores.Settings;
using Shared.Models.Console.Responses;

namespace Collector.Databases.Implementation.Stores.Settings;

public sealed class SettingsStore : ISettingsStore
{
    public TimeSpan Retention { get; set; }
    public DetectionProfile Profile { get; set; }
    public bool OverrideAuditPolicies { get; set; }
    public Subject<DomainRecord> DomainAddedOrUpdated { get; } = new();

    public void Dispose()
    {
        DomainAddedOrUpdated.Dispose();
    }
}