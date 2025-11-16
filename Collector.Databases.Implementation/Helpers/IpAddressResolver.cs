using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;
using Shared;
using Shared.Extensions;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Helpers;

public static class IpAddressResolver
{
    private static readonly IPAddress LocalIpAddress = GetLocalIpAddress();

    private static IPAddress GetLocalIpAddress()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            if (networkInterfaces.Length == 0) return IPAddress.Loopback;
            return networkInterfaces.Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
                .Where(networkInterface => networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(GetIpFromUnicastAddresses)
                .Aggregate((left, right) => IPAddress.IsLoopback(left) ? right : left);
        }
        catch (Exception)
        {
            return IPAddress.Loopback;
        }
    }

    private static IEnumerable<IPAddress> GetIpFromUnicastAddresses(NetworkInterface networkInterface)
    {
        var properties = networkInterface.GetIPProperties();
        if (properties.GatewayAddresses.Count == 0)
        {
            yield return IPAddress.Loopback;
            yield break;
        }
    
        var ipv4Addresses = properties.UnicastAddresses.Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork && Array.FindIndex(unicast.Address.GetAddressBytes(), b => b != 0) >= 0).ToList();
        if (ipv4Addresses.Count > 0)
        {
            foreach (var ipv4 in ipv4Addresses)
            {
                yield return ipv4.Address;
            }

            yield break; 
        }
    
        var ipv6Addresses = properties.UnicastAddresses.Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetworkV6).ToList();
        if (ipv6Addresses.Count > 0)
        {
            foreach (var ipv6 in ipv6Addresses)
            {
                yield return ipv6.Address;
            }

            yield break;
        }
    
        yield return IPAddress.Loopback;
    }
    
    private static async Task<string> GetIpAddressCoreAsync(string workstationName, CancellationToken cancellationToken)
    {
        var ipAddresses = await GetIpAddressesFromDnsAsync(workstationName, cancellationToken);
        if (ipAddresses.Contains(LocalIpAddress)) return LocalIpAddress.ToString();
        if (ipAddresses.Count == 0)
        {
            return (await GetInternetIpAddressAsync(cancellationToken))?.ToString() ?? "N/A";
        }

        return ipAddresses.First().ToString();
    }

    private static async Task<IList<IPAddress>> GetIpAddressesFromDnsAsync(string machineName, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var ipHostEntry = await Dns.GetHostEntryAsync(machineName, cts.Token);
            return GetIpAddresses(ipHostEntry.AddressList).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IEnumerable<IPAddress> GetIpAddresses(IPAddress[] ipAddresses)
    {
        foreach (var ipAddress in ipAddresses.Where(ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ipAddress)))
        {
            yield return ipAddress;
        }
        
        foreach (var ipAddress in ipAddresses.Where(ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(ipAddress)))
        {
            yield return ipAddress;
        }
    }

    private static async Task<IPAddress?> GetInternetIpAddressAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, protocolType: 0);
            await socket.ConnectAsync("8.8.8.8", 65530, cts.Token);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static ValueTask<string> GetIpAddressAsync(string workstationName, CancellationToken cancellationToken)
    {
        return Lrus.IpAddressByWorkstationName.GetOrAddAsync(workstationName, valueFactory: key => GetIpAddressCoreAsync(key, cancellationToken));
    }

    public static ValueTask<string> GetIpAddressFrom4624Async(WinEvent winEvent, CancellationToken cancellationToken)
    {
        if (winEvent.EventData.TryGetValue(nameof(Logon4624.SubjectUserName), out var subjectUserName) && subjectUserName.EndsWith('$'))
        {
            return Lrus.IpAddressByWorkstationName.GetOrAddAsync(subjectUserName.Replace("$", string.Empty), valueFactory: key => GetIpAddressCoreAsync(key, cancellationToken));
        }
        
        if (winEvent.EventData.TryGetValue(nameof(Logon4624.SubjectUserSid), out var subjectUserSid) &&
            winEvent.EventData.TryGetValue(nameof(Logon4624.SubjectDomainName), out var subjectDomainName) &&
            !subjectDomainName.Equals("-", StringComparison.Ordinal) &&
            !DomainHelper.DomainName.Contains(subjectDomainName, StringComparison.OrdinalIgnoreCase) && 
            !WellKnownSids.TryFindByBigramOrSid(subjectUserSid, out _))
        {
            return Lrus.IpAddressByWorkstationName.GetOrAddAsync(subjectDomainName, valueFactory: key => GetIpAddressCoreAsync(key, cancellationToken));
        }

        if (winEvent.EventData.TryGetValue(nameof(Logon4624.TargetUserName), out var targetUserName) && targetUserName.EndsWith('$'))
        {
            return Lrus.IpAddressByWorkstationName.GetOrAddAsync(targetUserName.Replace("$", string.Empty), valueFactory: key => GetIpAddressCoreAsync(key, cancellationToken));
        }
        
        if (winEvent.EventData.TryGetValue(nameof(Process4688.TargetUserSid), out var targetUserSid) &&
            winEvent.EventData.TryGetValue(nameof(Process4688.TargetDomainName), out var targetDomainName) &&
            !targetDomainName.Equals("-", StringComparison.Ordinal) &&
            !DomainHelper.DomainName.Contains(targetDomainName, StringComparison.OrdinalIgnoreCase) &&
            !WellKnownSids.TryFindByBigramOrSid(targetUserSid, out _))
        {
            return Lrus.IpAddressByWorkstationName.GetOrAddAsync(targetDomainName, valueFactory: key => GetIpAddressCoreAsync(key, cancellationToken));
        }

        if (winEvent.Computer.Contains('.'))
        {
            var workstationName = winEvent.Computer.StripDomain();
            return Lrus.IpAddressByWorkstationName.GetOrAddAsync(workstationName, valueFactory: key => GetIpAddressCoreAsync(key, cancellationToken));
        }
        
        return Lrus.IpAddressByWorkstationName.GetOrAddAsync(winEvent.Computer, valueFactory: key => GetIpAddressCoreAsync(key, cancellationToken));
    }
}