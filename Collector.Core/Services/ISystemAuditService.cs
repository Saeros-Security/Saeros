using System.Diagnostics.CodeAnalysis;
using Collector.Core.SystemAudits;
using Streaming;

namespace Collector.Core.Services;

public interface ISystemAuditService : IDisposable
{
    Task ExecuteAsync(CancellationToken cancellationToken);
    void Add(SystemAuditKey key, AuditStatus status);
    void ServerConnected(string domain, string serverName);
    bool TryGetContract(KeyValuePair<SystemAuditKey, AuditStatus> pair, [MaybeNullWhen(false)] out SystemAuditContract systemAuditContract);
    bool TryGetNameExplanation(SystemAuditKey key, AuditStatus status, [MaybeNullWhen(false)] out string name, [MaybeNullWhen(false)] out string explanation);
    void DeleteDomain(string domain);
    IObservable<string> OnServerConnected { get; }
}