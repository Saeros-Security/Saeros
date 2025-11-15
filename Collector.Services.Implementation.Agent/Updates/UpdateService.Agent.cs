using System.Diagnostics;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Collector.ActiveDirectory.Helpers;
using Collector.ActiveDirectory.Helpers.ScheduledTasks;
using Collector.ActiveDirectory.Helpers.Sysvol;
using Collector.ActiveDirectory.Managers;
using Collector.Core.Helpers;
using Collector.Services.Abstractions.Updates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Serilog;
using Vanara.InteropServices;
using Vanara.PInvoke;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Collector.Services.Implementation.Agent.Updates;

public sealed class UpdateServiceAgent(ILogger<UpdateServiceAgent> logger, IHostApplicationLifetime applicationLifetime)
    : IUpdateService
{
    private readonly string _currentVersion = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule!.FileName).FileVersion ?? DateTime.MinValue.ToString(VersionFormat);
    private const string VersionFormat = "yyyy.MM.dd.HH";

    private static FileSystemWatcher BuildWatcher(string directory)
    {
        return new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = true,
            Filters = { "*.exe", "*.txt" },
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
        };
    }

    private IDisposable SubscribeFileChange(FileSystemWatcher fileSystemWatcher, EventLoopScheduler eventLoopScheduler, CancellationToken cancellationToken)
    {
        var observable = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => fileSystemWatcher.Changed += h, h => fileSystemWatcher.Changed -= h);
        return observable.ObserveOn(eventLoopScheduler).Select(e =>
        {
            return Observable.FromAsync(async _ =>
            {
                if (e.EventArgs.ChangeType == WatcherChangeTypes.Changed)
                {
                    if (e.EventArgs.FullPath.EndsWith(GroupPolicyManager.CollectorServiceFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        await ApplyNewVersionAsync(e.EventArgs.FullPath);
                    }
                    else if (e.EventArgs.FullPath.EndsWith(GroupPolicyManager.DeleteFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        await UninstallAsync(cancellationToken);
                    }
                }
            });
        }).Concat().Subscribe();
    }

    private async Task<bool> ApplyNewVersionAsync(string path)
    {
        try
        {
            var newVersion = FileVersionInfo.GetVersionInfo(path).FileVersion ?? DateTime.MinValue.ToString(VersionFormat);
            if (DateTime.TryParseExact(newVersion, VersionFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var newVersionDate) &&
                DateTime.TryParseExact(_currentVersion, VersionFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var currentVersionDate))
            {
                if (currentVersionDate < newVersionDate)
                {
                    logger.LogInformation($"A new version is available: {newVersion}");
                    if (UpdateServicePath(logger, path))
                    {
                        applicationLifetime.StopApplication();
                        await Log.CloseAndFlushAsync();
                        Environment.Exit(-1);
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }

        return false;
    }

    private async Task<bool> UninstallAsync(CancellationToken cancellationToken)
    {
        if (await UninstallServiceAsync(logger, cancellationToken))
        {
            applicationLifetime.StopApplication();
            await Log.CloseAndFlushAsync();
            Environment.Exit(0);
            return true;
        }

        return false;
    }

    private static bool UpdateServicePath(ILogger logger, string updatedPath)
    {
        using var hSvcMgr = AdvApi32.OpenSCManager(lpMachineName: null, lpDatabaseName: null, AdvApi32.ScManagerAccessTypes.SC_MANAGER_ALL_ACCESS);
        using var hSvc = AdvApi32.OpenService(hSvcMgr, GroupPolicyManager.CollectorServiceName, AdvApi32.ServiceAccessTypes.SERVICE_ALL_ACCESS);
        using var info = new SafeHGlobalHandle(size: 1024);
        if (!hSvc.IsNull && AdvApi32.QueryServiceConfig(hSvc, info, info.Size, out _))
        {
            if (AdvApi32.ChangeServiceConfig(hSvc,
                    AdvApi32.ServiceTypes.SERVICE_NO_CHANGE,
                    AdvApi32.ServiceStartType.SERVICE_NO_CHANGE,
                    AdvApi32.ServiceErrorControlType.SERVICE_NO_CHANGE,
                    lpBinaryPathName: $@"""{updatedPath}"" --mode {Shared.Constants.CollectorAgentMode}"))
            {
                logger.LogInformation("The service path has been updated to {Path}", updatedPath);
                return true;
            }
        }

        logger.LogError("Could not update service path to {Path}", updatedPath);
        return false;
    }

    private static async Task<bool> UninstallServiceAsync(ILogger logger, CancellationToken cancellationToken)
    {
        using var hSvcMgr = AdvApi32.OpenSCManager(lpMachineName: null, lpDatabaseName: null, AdvApi32.ScManagerAccessTypes.SC_MANAGER_ALL_ACCESS);
        using var hSvc = AdvApi32.OpenService(hSvcMgr, GroupPolicyManager.CollectorServiceName, AdvApi32.ServiceAccessTypes.SERVICE_ALL_ACCESS);
        var success = AdvApi32.DeleteService(hSvc);
        if (success)
        {
            logger.LogInformation("The service has been deleted");
            await ActiveDirectoryHelper.DeleteScheduledTaskAsync(logger, ScheduledTasksHelper.ScheduleTaskType.ServiceCreation, cancellationToken);
            return true;
        }

        return false;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var policy = Policy.Handle<DirectoryNotFoundException>().WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(10), onRetryAsync: (ex, next) =>
        {
            logger.LogWarning(ex, "Could not find the update directory. Retrying in '{Retry}s'...", next.TotalSeconds);
            return Task.CompletedTask;
        });

        await policy.ExecuteAsync(async ct =>
        {
            var companyDirectory = SysvolHelper.GetCompanyDirectory();
            foreach (var file in Directory.EnumerateFiles(companyDirectory, searchPattern: "*.exe", SearchOption.AllDirectories))
            {
                if (await ApplyNewVersionAsync(file)) return;
            }

            foreach (var file in Directory.EnumerateFiles(companyDirectory, searchPattern: "*.txt", SearchOption.AllDirectories))
            {
                if (file.EndsWith(GroupPolicyManager.DeleteFileName))
                {
                    if (await UninstallAsync(ct)) return;
                }
            }

            using var fileSystemWatcher = BuildWatcher(companyDirectory);
            using var eventLoopScheduler = new EventLoopScheduler();
            using var updateSubscription = SubscribeFileChange(fileSystemWatcher, eventLoopScheduler, ct);
            fileSystemWatcher.EnableRaisingEvents = true;
            await Task.Delay(-1, ct);
        }, cancellationToken);
    }
}