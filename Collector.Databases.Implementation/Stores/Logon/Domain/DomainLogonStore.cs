using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using Collector.Core.Extensions;
using Collector.Databases.Implementation.Caching.LRU;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Stores.Logon.Domain;

public sealed class DomainLogonStore(ILogger<DomainLogonStore> logger) : LogonStore(logger, ContextType.Domain)
{
    private static IEnumerable<Computer> EnumerateDomainComputers(ILogger logger, string domain, CancellationToken cancellationToken)
    {
        var policy = Policy.Handle<Exception>().WaitAndRetryForever(_ => TimeSpan.FromSeconds(1), onRetry: (ex, _) => { logger.Throttle(nameof(EnumerateDomainComputers), log => log.LogWarning(ex, "Could not enumerate domain controllers. Retrying every second..."), TimeSpan.FromMinutes(1)); });

        return policy.Execute(_ =>
        {
            var computers = new HashSet<Computer>();
            using var entry = new DirectoryEntry($"LDAP://{domain}");
            using var searcher = new DirectorySearcher(entry);
            searcher.Filter = "(objectClass=computer)";
            searcher.SizeLimit = 0;
            searcher.PageSize = 250;
            searcher.PropertiesToLoad.Add("name");
            searcher.PropertiesToLoad.Add("operatingSystem");
            searcher.PropertiesToLoad.Add("operatingSystemVersion");
            foreach (SearchResult result in searcher.FindAll())
            {
                if (result.Properties["name"].Count > 0 && result.Properties["operatingSystem"].Count > 0 && result.Properties["operatingSystemVersion"].Count > 0)
                {
                    var computerName = (string)result.Properties["name"][0];
                    var operatingSystem = (string)result.Properties["operatingSystem"][0];
                    var operatingSystemVersion = (string)result.Properties["operatingSystemVersion"][0];
                    computers.Add(new Computer(computerName, $"{operatingSystem} ({operatingSystemVersion})"));
                }
            }

            return computers;
        }, cancellationToken);
    }

    protected override async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(EnumerateDomainComputers(logger, DomainHelper.DomainName, cancellationToken).Select(async computer =>
        {
            AddComputer(computer);
            Lrus.WorkstationNameByIpAddress.AddOrUpdate(await GetIpAddressAsync(computer.Name, cancellationToken), computer.Name);
        }));
    }
}