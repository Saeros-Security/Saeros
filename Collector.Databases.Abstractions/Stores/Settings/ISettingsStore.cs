using System.Reactive.Subjects;
using Collector.Databases.Abstractions.Domain.Settings;
using Shared.Models.Console.Responses;

namespace Collector.Databases.Abstractions.Stores.Settings;

public interface ISettingsStore : IDisposable
{
    TimeSpan Retention { get; set; }
    DetectionProfile Profile { get; set; }
    bool OverrideAuditPolicies { get; set; }
    Subject<DomainRecord> DomainAddedOrUpdated { get; }
}