using Collector.Core.Extensions;
using Collector.Core.Services;
using Collector.Core.SystemAudits;
using Collector.Databases.Abstractions.Repositories.Integrations;
using Collector.Integrations.Abstractions;
using Collector.Integrations.Implementation.Extensions;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Shared.Integrations;
using Shared.Integrations.OpenSearch;
using Shared.Models.Detections;
using Streaming;

namespace Collector.Integrations.Implementation.OpenSearch;

internal sealed class OpenSearchIntegrationService(ILogger logger, int integrationId, string integrationName, HashSet<DetectionSeverity> severities, ISystemAuditService systemAuditService, IIntegrationRepository integrationRepository, OpenSearchSettings searchSettings)
    : IIntegration
{
    private const string DetectionIndexName = "detection";
    private readonly ElasticsearchClient _elasticsearchClient = CreateClientInternal(searchSettings);

    public string Name { get; } = integrationName;

    private static ElasticsearchClient CreateClientInternal(OpenSearchSettings searchSettings)
    {
        var settings = new ElasticsearchClientSettings(searchSettings.Uri)
            .DefaultMappingFor<Export>(m => m
                .IndexName(DetectionIndexName)
            )
            .WithAuthentication(searchSettings)
            .WithCertificate(searchSettings)
            .ServerCertificateValidationCallback((_, _, _, _) => true)
            .IncludeServerStackTraceOnError()
            .EnableHttpPipelining()
            .EnableHttpCompression();
        return new ElasticsearchClient(settings);
    }

    public async Task SendAsync(IList<Export> exports, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested) return;
            var compatibleExports = exports.Where(export => severities.Contains(export.Severity)).ToList();
            if (compatibleExports.Count == 0) return;
            if (!(await _elasticsearchClient.Indices.ExistsAsync<Export>(cancellationToken)).Exists)
            {
                await _elasticsearchClient.Indices.CreateAsync<Export>(index => index.Index(DetectionIndexName), cancellationToken);
            }
            
            var response = await _elasticsearchClient.IndexManyAsync(compatibleExports, cancellationToken);
            if (!response.IsValidResponse || response.Errors)
            {
                logger.Throttle(nameof(OpenSearchIntegrationService), itself => itself.LogError(response.DebugInformation), expiration: TimeSpan.FromMinutes(1));
                await integrationRepository.SetStatusAsync(integrationId, IntegrationStatus.Error, cancellationToken);
                systemAuditService.Add(new SystemAuditKey(SystemAuditType.Integration, Name), AuditStatus.Failure);
            }
            else
            {
                await integrationRepository.SetStatusAsync(integrationId, IntegrationStatus.Running, cancellationToken);
                systemAuditService.Add(new SystemAuditKey(SystemAuditType.Integration, Name), AuditStatus.Success);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
            await integrationRepository.SetStatusAsync(integrationId, IntegrationStatus.Error, cancellationToken);
            systemAuditService.Add(new SystemAuditKey(SystemAuditType.Integration, Name), AuditStatus.Failure);
        }
    }
}