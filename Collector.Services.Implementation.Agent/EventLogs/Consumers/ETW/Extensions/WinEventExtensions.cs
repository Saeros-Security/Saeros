using Collector.Databases.Implementation.Caching.LRU;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;
using Collector.Services.Abstractions.Processes;
using Shared;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Extensions;

internal static class WinEventExtensions
{
    private const string FileVersion = nameof(FileVersion);
    private const string Description = nameof(Description);
    private const string Product = nameof(Product);
    private const string Company = nameof(Company);
    private const string OriginalFileName = nameof(OriginalFileName);
    private const string CurrentDirectory = nameof(CurrentDirectory);
    private const string Hashes = nameof(Hashes);
    private const string ParentCommandLine = nameof(ParentCommandLine);
    private static readonly TimeSpan Expiration = TimeSpan.FromSeconds(5);
    private static DateTimeOffset _lastTrimming = DateTimeOffset.MinValue;

    public static void EnrichProcesses(this WinEvent winEvent, IPeService peService)
    {
        if (winEvent.EventId is not 4688) return;
        if (winEvent.EventData.TryGetValue(nameof(Process4688.NewProcessName), out var processName))
        {
            var peRecord = peService.Scan(processName);
            winEvent.EventData[FileVersion] = peRecord.Version;
            winEvent.EventData[Description] = peRecord.Description;
            winEvent.EventData[Product] = peRecord.Product;
            winEvent.EventData[Company] = peRecord.Company;
            winEvent.EventData[OriginalFileName] = peRecord.OriginalFilename;
            winEvent.EventData[CurrentDirectory] = Path.GetDirectoryName(processName) ?? string.Empty;
            winEvent.EventData[Hashes] = $"SHA1={string.Empty},MD5={string.Empty},SHA256={string.Empty},IMPHASH={peRecord.ImpHash.ToUpper()}"; // MD5/SHA1/SHA256 are costly to compute
        }

        if (_lastTrimming + Expiration <= DateTimeOffset.UtcNow)
        {
            Lrus.ParentProcessIdByProcessId.Policy.ExpireAfterWrite.Value?.TrimExpired();
            Lrus.CommandLineByProcessId.Policy.ExpireAfterWrite.Value?.TrimExpired();
            _lastTrimming = DateTimeOffset.UtcNow;
        }

        if (winEvent.EventData.TryGetValue(nameof(Process4688.NewProcessId), out var newProcessId) && Lrus.ParentProcessIdByProcessId.TryGet(newProcessId, out var parentId))
        {
            if (Lrus.CommandLineByProcessId.TryGet(parentId, out var parentCommandLine))
            {
                winEvent.EventData[ParentCommandLine] = parentCommandLine;
            }
        }
    }
}