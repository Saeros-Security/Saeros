using Microsoft.Data.Sqlite;
using Shared.Databases.Collector;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Streaming;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using App.Metrics;
using Collector.Core;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Databases.Abstractions.Domain.Computers;
using Collector.Databases.Abstractions.Domain.Profiles;
using Collector.Databases.Abstractions.Domain.Settings;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Databases.Implementation.Contexts.Settings;
using Collector.Databases.Implementation.Extensions;
using Collector.Databases.Implementation.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Databases.Implementation.Repositories.Settings;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly ILogger<SettingsRepository> _logger;
    private readonly IMetricsRoot _metrics;
    private readonly SettingsContext _context;
    private readonly ISettingsStore _settingsStore;
    private readonly DataFlowHelper.PeriodicBlock<MetricContract> _insertBlock;
    private readonly IDisposable _subscription;
    private static readonly TimeSpan InsertionInterval = TimeSpan.FromSeconds(1);

    private const string ComputerInsertion = @"INSERT OR REPLACE INTO Computers (Computer, Domain, IpAddress, OperatingSystem, UpTime, MedianCpuUsage, MedianWorkingSet, Version)
VALUES (@Computer, @Domain, @IpAddress, @OperatingSystem, @UpTime, @MedianCpuUsage, @MedianWorkingSet, @Version);";
    
    private const string DomainInsertion = @"INSERT OR REPLACE INTO Domains (Name, PrimaryDomainController, DomainControllerCount, ShouldUpdate)
VALUES (@Name, @PrimaryDomainController, @DomainControllerCount, @ShouldUpdate);";
    
    private const string ComputerSelection = "SELECT Computer, Domain, IpAddress, OperatingSystem, UpTime, MedianCpuUsage, MedianWorkingSet, Version FROM Computers;";
    private const string DomainSelection = "SELECT Name, PrimaryDomainController, DomainControllerCount, ShouldUpdate FROM Domains;";
    
    public SettingsRepository(ILogger<SettingsRepository> logger, IMetricsRoot metrics, IHostApplicationLifetime applicationLifetime, SettingsContext context, ISettingsStore settingsStore)
    {
        _logger = logger;
        _metrics = metrics;
        _context = context;
        _settingsStore = settingsStore;
        _insertBlock = CreateBlock<MetricContract>(applicationLifetime.ApplicationStopping, InsertAsync, period: InsertionInterval, out var insertionLink);
        _subscription = insertionLink;
    }
    
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _settingsStore.Retention = await GetRetentionAsync(cancellationToken);
        _settingsStore.Profile = await GetProfileAsync(cancellationToken);
        _settingsStore.OverrideAuditPolicies = await OverrideAuditPoliciesAsync(cancellationToken);
        await foreach (var domain in EnumerateDomainsAsync(cancellationToken))
        {
            _settingsStore.DomainAddedOrUpdated.OnNext(domain);
        }
    }

    public async ValueTask StoreAsync(MetricContract metricContract, CancellationToken cancellationToken)
    {
        if (!await _insertBlock.SendAsync(metricContract, cancellationToken))
        {
            _logger.Throttle(nameof(SettingsRepository), itself => itself.LogError("Could not insert metric"), expiration: TimeSpan.FromMinutes(1));
        }
    }

    public async IAsyncEnumerable<DomainRecord> EnumerateDomainsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name, PrimaryDomainController, DomainControllerCount, ShouldUpdate FROM Domains;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new DomainRecord(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3));
        }
    }

    public async Task<TimeSpan> GetRetentionAsync(CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Retention FROM Settings;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new TimeSpan(reader.GetInt64(0));
        }

        return TimeSpan.FromDays(14);
    }
    
    public async Task<Shared.Models.Console.Responses.Settings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(cancellationToken);
        var retention = await GetRetentionAsync(cancellationToken);
        var lockoutThreshold = await GetLockoutThresholdAsync(cancellationToken);
        var overrideAuditPolicies = await OverrideAuditPoliciesAsync(cancellationToken);
        var cacheSize = new DirectoryInfo(CollectorContextBase.DbPath).GetDirectorySize();
        var detectionContext = _metrics.Snapshot.GetForContext(MetricOptions.Detections.Context);
        var detections = detectionContext.Counters.Where(h => h.Name.Contains(MetricOptions.Detections.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var detectionCount = detections.Sum(source => source.Value.Count);
        var computers = new ObservableCollection<ComputerStatus>(await GetComputerStatusAsync(cancellationToken));
        return new Shared.Models.Console.Responses.Settings(new ObservableCollection<Domain>(await GetDomainsAsync(cancellationToken)), computers, profile, retention, cacheSize, detectionCount, lockoutThreshold, overrideAuditPolicies);
    }
    
    public async Task<ProfileChangeRecord> SetProfileAsync(DetectionProfile profile, CancellationToken cancellationToken)
    {
        using var connection = await _context.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText =
            $@"
            UPDATE Settings
            SET Profile = {(int)profile};
";
        await command.ExecuteNonQueryAsync(cancellationToken);
        
        var changed = _settingsStore.Profile != profile;
        _settingsStore.Profile = profile;
        return new ProfileChangeRecord(changed);
    }

    public async Task<SettingsChangeRecord> SetSettingsAsync(SetSettings settings, CancellationToken cancellationToken)
    {
        using var connection = await _context.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText =
            $@"
            UPDATE Settings
            SET Retention = {settings.Retention.Ticks};
            UPDATE Settings
            SET LockoutThreshold = {settings.LockoutThreshold.TotalMinutes};
            UPDATE Settings
            SET OverrideAuditPolicies = {(settings.OverrideAuditPolicies ? 1 : 0)};
";
        await command.ExecuteNonQueryAsync(cancellationToken);
        
        var overrideAuditPoliciesChanged = _settingsStore.OverrideAuditPolicies != settings.OverrideAuditPolicies;
        _settingsStore.OverrideAuditPolicies = settings.OverrideAuditPolicies;
        _settingsStore.Retention = settings.Retention;
        return new SettingsChangeRecord(overrideAuditPoliciesChanged);
    }

    public async Task AddDomainAsync(DomainRecord domain, CancellationToken cancellationToken)
    {
        using var connection = await _context.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText = DomainInsertion;
        var nameParameter = command.Parameters.Add("Name", SqliteType.Text);
        var primaryDomainControllerParameter = command.Parameters.Add("PrimaryDomainController", SqliteType.Text);
        var domainControllerCountParameter = command.Parameters.Add("DomainControllerCount", SqliteType.Integer);
        var shouldUpdateParameter = command.Parameters.Add("ShouldUpdate", SqliteType.Integer);
        nameParameter.Value = DatabaseHelper.GetValue(domain.Name.ToLowerInvariant());
        primaryDomainControllerParameter.Value = DatabaseHelper.GetValue(domain.PrimaryDomainController);
        domainControllerCountParameter.Value = DatabaseHelper.GetValue(domain.DomainControllerCount);
        shouldUpdateParameter.Value = DatabaseHelper.GetValue(domain.ShouldUpdate ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    public async Task DeleteDomainAsync(string name, CancellationToken cancellationToken)
    {
        using var connection = await _context.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using (var command = connection.DbConnection.CreateCommand())
        {
            var nameParameter = command.Parameters.Add("Name", SqliteType.Text);
            nameParameter.Value = DatabaseHelper.GetValue(name.ToLowerInvariant());
            command.CommandText = "DELETE FROM Domains WHERE Name = @Name;";
            command.ExecuteNonQuery();
        }

        await using (var command = connection.DbConnection.CreateCommand())
        {
            var nameParameter = command.Parameters.Add("Name", SqliteType.Text);
            nameParameter.Value = DatabaseHelper.GetValue(name.ToLowerInvariant());
            command.CommandText = "DELETE FROM Computers WHERE Domain = @Name;";
            command.ExecuteNonQuery();
        }
    }
    
    private void AddOrUpdateDomain(DomainRecord domain)
    {
        _settingsStore.DomainAddedOrUpdated.OnNext(domain);
    }
    
    private async Task<DetectionProfile> GetProfileAsync(CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Profile FROM Settings;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return (DetectionProfile)reader.GetInt32(0);
        }

        return DetectionProfile.Core;
    }
    
    private async Task<TimeSpan> GetLockoutThresholdAsync(CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT LockoutThreshold FROM Settings;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return TimeSpan.FromMinutes(reader.GetInt64(0));
        }

        return TimeSpan.FromMinutes(15);
    }
    
    private async Task<bool> OverrideAuditPoliciesAsync(CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OverrideAuditPolicies FROM Settings;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.GetBoolean(0);
        }

        return true;
    }
    
    private async Task<IEnumerable<Domain>> GetDomainsAsync(CancellationToken cancellationToken)
    {
        var domains = new List<Domain>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = DomainSelection;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            domains.Add(new Domain
            {
                Name = reader.GetString(0),
                PrimaryDomainController = reader.GetString(1),
                DomainControllerCount = reader.GetInt32(2),
                ShouldUpdate = reader.GetBoolean(3)
            });
        }

        return domains;
    }

    private async Task<IEnumerable<ComputerStatus>> GetComputerStatusAsync(CancellationToken cancellationToken)
    {
        Lrus.ActiveComputerByName.Policy.ExpireAfterWrite.Value?.TrimExpired();
        var computers = new List<ComputerStatus>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ComputerSelection;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var computer = reader.GetString(0);
            var domain = reader.IsDBNull(1) ? null : reader.GetString(1);
            var ipAddress = reader.GetString(2);
            var operatingSystem = reader.GetString(3);
            var upTime = reader.GetInt64(4);
            var medianCpuUsage = reader.GetInt64(5);
            var medianWorkingSet = reader.GetInt64(6);
            var version = reader.GetString(7);
            computers.Add(new ComputerStatus
            {
                Name = computer.ToLowerInvariant(),
                Domain = domain ?? Shared.Constants.Workgroup,
                IpAddress = ipAddress,
                OperatingSystem = operatingSystem,
                UpTime = TimeSpan.FromMilliseconds(upTime),
                MedianCpuUsage = medianCpuUsage,
                MedianWorkingSet = medianWorkingSet,
                Version = version,
                Active = Lrus.ActiveComputerByName.TryGet(computer, out _)
            });
        }

        return computers;
    }
    
    private async Task InsertAsync(IList<MetricContract> metrics, CancellationToken cancellationToken)
    {
        try
        {
            if (metrics.Count == 0) return;
            Lrus.RemovedDomainsBarrier.Policy.ExpireAfterWrite.Value?.TrimExpired();
            using var connection = await _context.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var transaction = connection.DbConnection.BeginTransaction();
            var domains = new List<DomainRecord>();
            await using (var computerCommand = connection.DbConnection.CreateCommand())
            {
                computerCommand.CommandText = ComputerInsertion;
                var computerParameter = computerCommand.Parameters.Add("Computer", SqliteType.Text);
                var domainParameter = computerCommand.Parameters.Add("Domain", SqliteType.Text);
                var ipAddressParameter = computerCommand.Parameters.Add("IpAddress", SqliteType.Text);
                var operatingSystemParameter = computerCommand.Parameters.Add("OperatingSystem", SqliteType.Text);
                var upTimeParameter = computerCommand.Parameters.Add("UpTime", SqliteType.Integer);
                var medianCpuUsageParameter = computerCommand.Parameters.Add("MedianCpuUsage", SqliteType.Integer);
                var medianWorkingSetParameter = computerCommand.Parameters.Add("MedianWorkingSet", SqliteType.Integer);
                var versionParameter = computerCommand.Parameters.Add("Version", SqliteType.Text);
                foreach (var metric in metrics.Where(metric => !Lrus.RemovedDomainsBarrier.TryGet(metric.Domain, out _)))
                {
                    Lrus.ActiveComputerByName.AddOrUpdate(metric.Computer, new ComputerRecord(metric.Version));
                    if (metric.HasDomain)
                    {
                        domains.Add(new DomainRecord(metric.Domain, metric.PrimaryDomainController, metric.DomainControllerCount, ShouldUpdate: !Assembly.GetAssembly(typeof(SettingsRepository)).GetCollectorVersion().Equals(metric.Version, StringComparison.OrdinalIgnoreCase)));
                    }

                    computerParameter.Value = DatabaseHelper.GetValue(metric.Computer);
                    domainParameter.Value = DatabaseHelper.GetValue(metric.HasDomain ? metric.Domain.ToLowerInvariant() : null);
                    ipAddressParameter.Value = DatabaseHelper.GetValue(metric.IpAddress);
                    operatingSystemParameter.Value = DatabaseHelper.GetValue(metric.OperatingSystem);
                    upTimeParameter.Value = DatabaseHelper.GetValue(metric.Uptime);
                    medianCpuUsageParameter.Value = DatabaseHelper.GetValue(metric.MedianCpuUsage);
                    medianWorkingSetParameter.Value = DatabaseHelper.GetValue(metric.MedianWorkingSet);
                    versionParameter.Value = DatabaseHelper.GetValue(metric.Version);
                    await computerCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            
            await transaction.CommitAsync(cancellationToken);
            if (domains.Count > 0)
            {
                foreach (var group in domains.GroupBy(domain => domain.Name))
                {
                    AddOrUpdateDomain(group.First());
                }
            }
        }
        catch (OperationCanceledException)
        {
            _context.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "An error has occurred");
        }
    }
    
    private static DataFlowHelper.PeriodicBlock<T> CreateBlock<T>(CancellationToken cancellationToken, Func<IList<T>, CancellationToken, Task> action, TimeSpan period, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 2,
            CancellationToken = cancellationToken
        };

        var periodicBlock = DataFlowHelper.CreatePeriodicBlock<T>(period);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var propagationBlock = new ActionBlock<IList<T>>(async items => { await action(items, cancellationToken); }, executionDataflow);
        disposableLink = periodicBlock.LinkTo(propagationBlock, options);
        return periodicBlock;
    }

    public async ValueTask DisposeAsync()
    {
        if (_subscription is IAsyncDisposable subscriptionAsyncDisposable)
            await subscriptionAsyncDisposable.DisposeAsync();
        else
            _subscription.Dispose();
        
        await _insertBlock.DisposeAsync();
    }
}