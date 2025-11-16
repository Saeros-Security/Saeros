using System.Security.Cryptography.X509Certificates;
using Collector.ActiveDirectory.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Serilog;
using Shared.Helpers;

namespace Collector.Services.Implementation.Agent.Helpers;

public sealed class AgentCertificateHelper(ILogger<AgentCertificateHelper> logger, IHostApplicationLifetime applicationLifetime)
{
    private const string PrimaryDomainControllerName = nameof(PrimaryDomainControllerName);

    private readonly RetryPolicy _policy = Policy.Handle<InvalidOperationException>().WaitAndRetry(3, _ => TimeSpan.FromSeconds(5), onRetry: (_, _, context) =>
    {
        if (context.TryGetValue(PrimaryDomainControllerName, out var name) && name is string primaryDomainControllerName)
        {
            logger.LogWarning("The certificates are not available, performing group policy update...");
            try
            {
                ActiveDirectoryHelper.UpdateGroupPolicies(logger, primaryDomainControllerName, applicationLifetime.ApplicationStopping);
            }
            catch (OperationCanceledException)
            {
                // Silent
            }
        }
    });
    
    private readonly AsyncRetryPolicy _asyncPolicy = Policy.Handle<InvalidOperationException>().WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(5), onRetryAsync: async (_, _, context) =>
    {
        if (context.TryGetValue(PrimaryDomainControllerName, out var name) && name is string primaryDomainControllerName)
        {
            logger.LogWarning("The certificates are not available, performing group policy update...");
            try
            {
                await ActiveDirectoryHelper.UpdateGroupPoliciesAsync(logger, primaryDomainControllerName, applicationLifetime.ApplicationStopping);
            }
            catch (OperationCanceledException)
            {
                // Silent
            }
        }
    });

    public X509Certificate2? GetServerCertificate()
    {
        try
        {
            if (!DomainHelper.DomainJoined) return Shared.Helpers.CertificateHelper.GetCollectorCertificate();
            applicationLifetime.ApplicationStopping.ThrowIfCancellationRequested();
            return _policy.Execute((_, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Shared.Helpers.CertificateHelper.GetCollectorCertificate();
                },
                new Context(nameof(GetServerCertificate), new Dictionary<string, object>
                {
                    { PrimaryDomainControllerName, ActiveDirectoryHelper.GetPrimaryDomainControllerDnsName(logger, DomainHelper.DomainName, applicationLifetime.ApplicationStopping) }
                }), applicationLifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, @"Error while getting server certificate. You might need to invoke ""gpupdate /force"" on the Primary Domain Controller to propagate certificates.");
            applicationLifetime.StopApplication();
            Log.CloseAndFlush();
            Environment.Exit(-1);
            return null;
        }
    }
    
    public async Task<X509Certificate2?> GetServerCertificateAsync()
    {
        try
        {
            if (!DomainHelper.DomainJoined) return Shared.Helpers.CertificateHelper.GetCollectorCertificate();
            applicationLifetime.ApplicationStopping.ThrowIfCancellationRequested();
            return await _asyncPolicy.ExecuteAsync((_, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(Shared.Helpers.CertificateHelper.GetCollectorCertificate());
                },
                new Context(nameof(GetServerCertificate), new Dictionary<string, object>
                {
                    { PrimaryDomainControllerName, ActiveDirectoryHelper.GetPrimaryDomainControllerDnsName(logger, DomainHelper.DomainName, applicationLifetime.ApplicationStopping) }
                }), applicationLifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, @"Error while getting server certificate. You might need to invoke ""gpupdate /force"" on the Primary Domain Controller to propagate certificates.");
            applicationLifetime.StopApplication();
            await Log.CloseAndFlushAsync();
            Environment.Exit(-1);
            return null;
        }
    }
}