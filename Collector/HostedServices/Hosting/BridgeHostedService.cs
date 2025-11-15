using System.Security.Authentication;
using App.Metrics;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Repositories.Dashboards;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Integrations;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Repositories.Users;
using Collector.Databases.Abstractions.Stores.Authentication;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Abstractions.Stores.Tracing;
using Collector.Databases.Implementation.Stores.Authentication;
using Collector.Hosting;
using Collector.Services.Abstractions.Activity;
using Collector.Services.Abstractions.Databases;
using Collector.Services.Abstractions.Domains;
using Collector.Services.Abstractions.Rules;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;
using Shared.Databases.Collector.Repositories.Licences;
using Shared.Streaming.Interfaces;

namespace Collector.HostedServices.Hosting;

public sealed class BridgeHostedService(ILogger<BridgeHostedService> logger, int port, IServiceProvider serviceProvider) : BackgroundService
{
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
                    webBuilder.UseStartup(_ => new BridgeStartup());
                    webBuilder.UseKestrel(o =>
                    {
                        var certificate = Shared.Helpers.CertificateHelper.GetCollectorCertificate();
                        o.AllowSynchronousIO = true;
                        o.ListenAnyIP(port, listenOptions =>
                        {
                            listenOptions.UseHttps(certificate, options =>
                            {
                                options.CheckCertificateRevocation = false;
                                options.SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
                                options.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                            });
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                        });
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddSerilog();
                    services.AddGrpc(options => { options.EnableDetailedErrors = true; });
                    services.AddSingleton<IAuthenticationStore, AuthenticationStore>();
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IMetricsRoot>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IRuleRepository>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IDetectionRepository>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IUserRepository>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ICollectorLicenseRepository>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IDashboardRepository>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ILicenseForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IDashboardForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ISystemAuditForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IDetectionForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IRuleForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IEventForwarder>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IRuleService>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IDatabaseExporterService>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IGeolocationService>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IIntegrationRepository>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IActivityService>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ISettingsRepository>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ITracingStore>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<ISettingsStore>());
                    services.AddSingleton(_ => serviceProvider.GetRequiredService<IDomainService>());
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