using System.Net;
using System.Reactive.Linq;
using Collector.ActiveDirectory.Helpers;
using Collector.ActiveDirectory.Helpers.ScheduledTasks;
using Collector.ActiveDirectory.Managers;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Domain.Settings;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Services.Abstractions.Domains;
using Collector.Services.Implementation.Bridge.NamedPipes;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Bridge.Domains;

public sealed class DomainService(ILogger<DomainService> logger, ISettingsRepository settingsRepository, ISystemAuditService systemAuditService, ISettingsStore settingsStore, INamedPipeBridge namedPipeBridge) : IDomainService
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private readonly IDictionary<string, CancellationTokenSource> _cancellationTokenSources = new Dictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, DomainRecord> _domains = new Dictionary<string, DomainRecord>(StringComparer.OrdinalIgnoreCase);

    private IDisposable? _disposable;

    public async Task AddOrUpdateAsync(string username, string password, string domain, string primaryDomainController, int ldapPort, CancellationToken cancellationToken)
    {
        Lrus.RemovedDomainsBarrier.Policy.ExpireAfterWrite.Value?.TrimExpired();
        if (Lrus.RemovedDomainsBarrier.TryGet(domain, out _))
        {
            throw new Exception("Please allow up to 15 seconds before adding this domain again");
        }

        if (!ActiveDirectoryHelper.TestConnection(logger, domain, primaryDomainController, ldapPort, username, password, out var message))
        {
            throw new Exception(message);
        }

        var host = await Dns.GetHostEntryAsync(primaryDomainController, cancellationToken);
        var groupPolicyManager = new GroupPolicyManager(domain, username, password, primaryDomainController, ldapPort, logger);
        groupPolicyManager.SetGroupPolicyObject();
        var networkCredential = new NetworkCredential(username, password, domain);
        await ActiveDirectoryHelper.UpdateGroupPoliciesAsync(logger, host.HostName, networkCredential, cancellationToken);
        await ActiveDirectoryHelper.InvokeScheduledTaskAsync(logger, ScheduledTasksHelper.ScheduleTaskType.ServiceCreation, host.HostName, networkCredential, cancellationToken);
        await OnDomainAddedOrUpdated(new DomainRecord(domain, PrimaryDomainController: host.HostName, DomainControllerCount: 1, ShouldUpdate: false), cancellationToken);
    }

    public async Task DeleteAsync(string username, string password, string domain, string primaryDomainController, int ldapPort, CancellationToken cancellationToken)
    {
        if (!ActiveDirectoryHelper.TestConnection(logger, domain, primaryDomainController, ldapPort, username, password, out var message))
        {
            throw new Exception(message);
        }

        var host = await Dns.GetHostEntryAsync(primaryDomainController, cancellationToken);
        var groupPolicyManager = new GroupPolicyManager(domain, username, password, primaryDomainController, ldapPort, logger);
        groupPolicyManager.RemoveGroupPolicyObject();
        var networkCredential = new NetworkCredential(username, password, domain);
        await ActiveDirectoryHelper.UpdateGroupPoliciesAsync(logger, host.HostName, networkCredential, cancellationToken);
        await DeleteAsync(domain, cancellationToken);
    }

    public void Observe()
    {
        _disposable = settingsStore.DomainAddedOrUpdated.Select(domain => Observable.FromAsync(async ct => await OnDomainAddedOrUpdated(domain, ct))).Concat().Subscribe();
    }

    private async Task OnDomainAddedOrUpdated(DomainRecord domain, CancellationToken cancellationToken)
    {
        await AddOrUpdateAsync(domain, cancellationToken);
    }

    private async Task AddOrUpdateAsync(DomainRecord domain, CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            Lrus.RemovedDomainsBarrier.Policy.ExpireAfterWrite.Value?.TrimExpired();
            if (Lrus.RemovedDomainsBarrier.TryGet(domain.Name, out _))
            {
                return;
            }

            if (_domains.TryGetValue(domain.Name, out var domainRecord))
            {
                if (!domainRecord.PrimaryDomainController.Equals(domain.PrimaryDomainController, StringComparison.OrdinalIgnoreCase))
                {
                    if (_cancellationTokenSources.Remove(domain.Name, out var token))
                    {
                        await token.CancelAsync();
                        token.Dispose();

                        var tokenSource = new CancellationTokenSource();
                        _ = namedPipeBridge.ExecuteAsync(domain.Name, domain.PrimaryDomainController, tokenSource.Token);
                        _cancellationTokenSources.Add(domain.Name, tokenSource);
                    }
                }
            }
            else if (_domains.TryAdd(domain.Name, domain))
            {
                var tokenSource = new CancellationTokenSource();
                _ = namedPipeBridge.ExecuteAsync(domain.Name, domain.PrimaryDomainController, tokenSource.Token);
                _cancellationTokenSources.Add(domain.Name, tokenSource);
            }

            await settingsRepository.AddDomainAsync(domain, cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            Lrus.RemovedDomainsBarrier.AddOrUpdate(name, 1);
            await settingsRepository.DeleteDomainAsync(name, cancellationToken);
            systemAuditService.DeleteDomain(name);
            if (_domains.Remove(name, out _))
            {
                if (_cancellationTokenSources.Remove(name, out var token))
                {
                    await token.CancelAsync();
                    token.Dispose();
                }
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            _disposable?.Dispose();
            foreach (var kvp in _domains)
            {
                if (_cancellationTokenSources.Remove(kvp.Key, out var token))
                {
                    await token.CancelAsync();
                    token.Dispose();
                }
            }

            _domains.Clear();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}