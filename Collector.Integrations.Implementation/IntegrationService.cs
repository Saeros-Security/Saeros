using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Core.Services;
using Collector.Core.SystemAudits;
using Collector.Databases.Abstractions.Repositories.Integrations;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Integrations.Abstractions;
using Collector.Integrations.Implementation.ElasticSearch;
using Collector.Integrations.Implementation.Extensions;
using Collector.Integrations.Implementation.OpenSearch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Integrations;
using Shared.Integrations.ElasticSearch;
using Shared.Integrations.OpenSearch;
using Streaming;

namespace Collector.Integrations.Implementation;

public sealed class IntegrationService : IIntegrationService
{
    private readonly ILogger<IntegrationService> _logger;
    private readonly IRuleRepository _ruleRepository;
    private readonly ISystemAuditService _systemAuditService;
    private readonly IIntegrationRepository _integrationRepository;
    private readonly Dictionary<IntegrationType, IIntegration> _integrations = new();
    private readonly IDisposable _integrationChangeSubscription;
    private readonly IDisposable _exportSubscription;
    private readonly DataFlowHelper.PeriodicBlock<Export> _exportBlock;
    private readonly SemaphoreSlim _semaphoreSlim = new(initialCount: 1, maxCount: 1);
    
    public IntegrationService(ILogger<IntegrationService> logger, IRuleRepository ruleRepository, ISystemAuditService systemAuditService, IIntegrationRepository integrationRepository, IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _ruleRepository = ruleRepository;
        _systemAuditService = systemAuditService;
        _integrationRepository = integrationRepository;
        _integrationChangeSubscription = integrationRepository.IntegrationChanged.Select(_ => Observable.FromAsync(ct => LoadIntegrationsAsync(init: false, ct))).Switch().Subscribe();
        _exportBlock = CreateExportBlock(applicationLifetime.ApplicationStopping, out var exportLink);
        _exportSubscription = new CompositeDisposable(exportLink);
    }
    
    private DataFlowHelper.PeriodicBlock<Export> CreateExportBlock(CancellationToken cancellationToken, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 2,
            CancellationToken = cancellationToken
        };

        var periodicBlock = DataFlowHelper.CreatePeriodicBlock<Export>(TimeSpan.FromSeconds(5), count: 1000);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var propagationBlock = new ActionBlock<IList<Export>>(async items => { await ExportAsync(items, cancellationToken); }, executionDataflow);
        disposableLink = periodicBlock.LinkTo(propagationBlock, options);
        return periodicBlock;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return LoadIntegrationsAsync(init: true, cancellationToken);
    }

    public void Export(DetectionContract detectionContract, WinEvent winEvent, string tactic, string technique, string subTechnique)
    {
        var export = detectionContract.ToExport(winEvent, tactic, technique, subTechnique);
        if (_ruleRepository.TryGetDescription(detectionContract.RuleId, out var description))
        {
            export.Description = description;
        }
        
        if (!_exportBlock.Post(export))
        {
            _logger.Throttle(nameof(IntegrationService), itself => itself.LogError("Could not process an export"), expiration: TimeSpan.FromMinutes(1));
        }
    }

    private async Task ExportAsync(IList<Export> exports, CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            await Task.WhenAll(_integrations.Values.Select(async integration => { await integration.SendAsync(exports, cancellationToken); }));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private async Task LoadIntegrationsAsync(bool init, CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            foreach (var integration in await _integrationRepository.GetIntegrationsAsync(cancellationToken))
            {
                if (!integration.Enabled)
                {
                    if (!init)
                    {
                        _integrations.Remove(integration.IntegrationType);
                        _systemAuditService.Add(new SystemAuditKey(SystemAuditType.Integration, integration.Name), AuditStatus.Success);
                    }
                    
                    continue;
                }
                
                if (integration.IntegrationType == IntegrationType.ElasticSearch && integration is ElasticSearchIntegration elasticSearchIntegration)
                {
                    _integrations[integration.IntegrationType] = new ElasticSearchIntegrationService(_logger, integration.Id, integration.Name, integration.Severities.ToHashSet(), _systemAuditService, _integrationRepository, elasticSearchIntegration.ToSettings(elasticSearchIntegration.Settings));
                    _systemAuditService.Add(new SystemAuditKey(SystemAuditType.Integration, integration.Name), AuditStatus.Success);
                }
                else if (integration.IntegrationType == IntegrationType.OpenSearch && integration is OpenSearchIntegration openSearchIntegration)
                {
                    _integrations[integration.IntegrationType] = new OpenSearchIntegrationService(_logger, integration.Id, integration.Name, integration.Severities.ToHashSet(), _systemAuditService, _integrationRepository, openSearchIntegration.ToSettings(openSearchIntegration.Settings));
                    _systemAuditService.Add(new SystemAuditKey(SystemAuditType.Integration, integration.Name), AuditStatus.Success);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _integrationChangeSubscription.Dispose();
        _exportSubscription.Dispose();
        _semaphoreSlim.Dispose();
        await _exportBlock.DisposeAsync();
    }
}