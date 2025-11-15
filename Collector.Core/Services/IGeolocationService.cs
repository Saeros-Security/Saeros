using System.Diagnostics.CodeAnalysis;

namespace Collector.Core.Services;

public interface IGeolocationService : IDisposable
{
    bool TryResolve(string ip, [MaybeNullWhen(false)] out string countryCode, [MaybeNullWhen(false)] out string asn);
}