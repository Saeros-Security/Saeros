using System.Collections.Concurrent;
using System.Diagnostics;
using Collector.Core.Extensions;
using Collector.Databases.Abstractions.Caching.LRU;
using Collector.Services.Abstractions.Processes;
using Microsoft.Extensions.Logging;
using PeNet;

namespace Collector.Services.Implementation.Agent.Processes;

public sealed class PeService(ILogger<PeService> logger) : IPeService
{
    private static readonly ConcurrentDictionary<string, PeRecord> _cache = new(StringComparer.OrdinalIgnoreCase);
    
    public PeRecord Scan(string processPath)
    {
        return _cache.GetOrAdd(processPath, path =>
        {
            try
            {
                if (!File.Exists(path)) return new PeRecord(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty); 
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (PeFile.TryParse(stream, out var peFile) && peFile is not null)
                {
                    var impHash = peFile.ImpHash;
                    var stringTable = peFile.Resources?.VsVersionInfo?.StringFileInfo.StringTable.FirstOrDefault();
                    if (stringTable is not null)
                    {
                        var version = stringTable.FileVersion;
                        var description = stringTable.FileDescription;
                        var product = stringTable.ProductName;
                        var company = stringTable.CompanyName;
                        var originalFilename = stringTable.OriginalFilename;
                        return new PeRecord(impHash ?? string.Empty, version ?? string.Empty, description ?? string.Empty, product ?? string.Empty, company ?? string.Empty, originalFilename ?? string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Throttle(nameof(Scan), itself => itself.LogError(ex, "Could not scan file {File}", processPath), expiration: TimeSpan.FromMinutes(1));
            }

            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                return new PeRecord(string.Empty, info.FileVersion ?? string.Empty, info.FileDescription ?? string.Empty, info.ProductName ?? string.Empty, info.CompanyName ?? string.Empty, info.OriginalFilename ?? string.Empty);
            }
            catch (FileNotFoundException)
            {
                return new PeRecord(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            }
        });
    }
}