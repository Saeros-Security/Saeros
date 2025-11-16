using Autofac;
using Autofac.Extensions.DependencyInjection;
using Collector.Extensions;
using Collector.Logging;
using Collector.Modules;
using ConsoleAppFramework;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Shared;
using Shared.Helpers;

await ConsoleApp.RunAsync(args, async (string mode, int? port = null) =>
{
    await MutexHelper.ExecuteOnceAsync($"{Constants.CompanyName}-{mode.ToLower()}", async () =>
    {
        try
        {
            AppContext.SetSwitch("Microsoft.AspNetCore.Server.Kestrel.EnableWindows81Http2", isEnabled: true);
            var builder = Host.CreateDefaultBuilder(args);
            using var host = builder
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureHostConfiguration(configurationBuilder => { configurationBuilder.AddEnvironmentVariables(); })
                .ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddEnvironmentVariables(Collector.Constants.Application.EnvironmentVariableNamePrefix);
                    configurationBuilder.AddCommandLine(args);
                })
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                })
                .UseSerilog((_, sp, configuration) =>
                {
                    configuration
                        .Enrich.WithAssemblyVersion()
                        .Enrich.WithSourceContext()
                        .Enrich.FromLogContext()
                        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning) 
                        .MinimumLevel.Override("System.Net", LogEventLevel.Warning) 
                        .MinimumLevel.ControlledBy(new EnvironmentVariableLoggingLevelSwitch($"{Collector.Constants.Application.EnvironmentVariableNamePrefix}LOGGING_VERBOSITY"))
                        .Filter.ByExcluding(logEvent => logEvent.Filter())
                        .WriteTo.Console(outputTemplate: Collector.Constants.Logging.LogTemplate, theme: AnsiConsoleTheme.Code)
                        .WriteTo.Async(configure => configure.File(Path.Combine(PathHelper.GetApplicationDataPath(Constants.CollectorName), "Logs", $"Collector-{mode}.log"), outputTemplate: Collector.Constants.Logging.LogTemplate, shared: true, rollOnFileSizeLimit: true), monitor: new LoggingMonitor(sp.CreateScope()));
                }, preserveStaticLogger: false, writeToProviders: true)
                .ConfigureContainer<ContainerBuilder>(containerBuilder =>
                {
                    containerBuilder.RegisterModule(new CollectorModule(mode, port));
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .UseWindowsService(options => options.ServiceName = Constants.CollectorName)
                .Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Host terminated unexpectedly: {Message}", ex.Message);
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    });
});