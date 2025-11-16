using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Authentication;
using System.Security.Principal;
using Collector.Core.Hubs.SystemAudits;
using Collector.Hosting;
using Collector.Services.Implementation.Agent.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;
using Shared.Helpers;
using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.HostedServices.Hosting;

public sealed class AgentHostedService(ILogger<AgentHostedService> logger, AgentCertificateHelper agentCertificateHelper, IServiceProvider serviceProvider) : BackgroundService
{
    private sealed class AuditHostedService(ILogger<AuditHostedService> logger, IStreamingSystemAuditHub streamingSystemAuditHub) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await UpdatePeriodicallyAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Cancellation has occurred");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error has occurred");
            }
        }

        private async Task UpdatePeriodicallyAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
                var machineName = MachineNameHelper.FullyQualifiedName.ToLowerInvariant();
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    streamingSystemAuditHub.SendSystemAudit(new SystemAuditContract
                    {
                        Date = DateTimeOffset.UtcNow.Ticks,
                        Status = AuditStatus.Success,
                        Name = machineName,
                        Explanation = $"The following machine is reachable: {machineName}."
                    });
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Cancellation has occurred");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error has occurred");
            }
        }
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            using var host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureHostConfiguration(configurationBuilder => { configurationBuilder.AddEnvironmentVariables(); })
                .ConfigureAppConfiguration((_, configurationBuilder) => { configurationBuilder.AddEnvironmentVariables(Constants.Application.EnvironmentVariableNamePrefix); })
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup(_ => new AgentStartup());
                    webBuilder.UseKestrel(o =>
                    {
                        var certificate = agentCertificateHelper.GetServerCertificate();
                        if (certificate == null) return;
                        o.AllowSynchronousIO = true;
                        o.ListenNamedPipe(Core.Constants.Application.NamedPipeName,
                            listenOptions =>
                            {
                                listenOptions.UseHttps(certificate, options =>
                                {
                                    options.CheckCertificateRevocation = false;
                                    options.SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
                                    options.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                                });
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                    });

                    webBuilder.UseNamedPipes(opts =>
                    {
                        var pipeSecurity = new PipeSecurity();
                        pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null), PipeAccessRights.FullControl, AccessControlType.Allow));
                        pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, domainSid: null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
                        opts.PipeSecurity = pipeSecurity;
                        opts.CurrentUserOnly = false;
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddSerilog();
                    services.AddGrpc(options => { options.EnableDetailedErrors = true; });
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IDetectionForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IRuleForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IProcessTreeForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ITracingForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IEventForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ISystemAuditForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IMetricForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IStreamingSystemAuditHub>());
                    services.AddHostedService<AuditHostedService>();
                })
                .Build();
            
            await host.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
    }
}
