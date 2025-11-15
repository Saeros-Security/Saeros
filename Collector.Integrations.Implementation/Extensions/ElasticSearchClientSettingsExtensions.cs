using System.Security.Cryptography.X509Certificates;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Shared.Integrations.ElasticSearch;
using Shared.Integrations.OpenSearch;

namespace Collector.Integrations.Implementation.Extensions;

internal static class ElasticSearchClientSettingsExtensions
{
    public static ElasticsearchClientSettings WithAuthentication(this ElasticsearchClientSettings settings, ElasticSearchSettings searchSettings)
    {
        if (string.IsNullOrWhiteSpace(searchSettings.Username) || string.IsNullOrWhiteSpace(searchSettings.Password)) return settings;
        return settings.Authentication(new BasicAuthentication(searchSettings.Username, searchSettings.Password));
    }
    
    public static ElasticsearchClientSettings WithCertificate(this ElasticsearchClientSettings settings, ElasticSearchSettings searchSettings)
    {
        if (string.IsNullOrWhiteSpace(searchSettings.CertificateBase64)) return settings;
        return settings.ClientCertificate(new X509Certificate2(Convert.FromBase64String(searchSettings.CertificateBase64)));
    }
    
    public static ElasticsearchClientSettings WithAuthentication(this ElasticsearchClientSettings settings, OpenSearchSettings searchSettings)
    {
        if (string.IsNullOrWhiteSpace(searchSettings.Username) || string.IsNullOrWhiteSpace(searchSettings.Password)) return settings;
        return settings.Authentication(new BasicAuthentication(searchSettings.Username, searchSettings.Password));
    }
    
    public static ElasticsearchClientSettings WithCertificate(this ElasticsearchClientSettings settings, OpenSearchSettings searchSettings)
    {
        if (string.IsNullOrWhiteSpace(searchSettings.CertificateBase64)) return settings;
        return settings.ClientCertificate(new X509Certificate2(Convert.FromBase64String(searchSettings.CertificateBase64)));
    }
}