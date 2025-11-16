using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Collector.Core.Services;
using CsvHelper;
using MaxMind.Db;
using Shared.Extensions;

namespace Collector.Services.Implementation.Geolocation;

public sealed class GeolocationService : IGeolocationService
{
    private readonly Reader? _countries;
    private readonly Reader? _asn;
    private readonly IDictionary<string, string> _alpha3ByAlpha2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private const string Country = "country";
    private const string IsoCode = "iso_code";
    private const string Provider = "autonomous_system_organization";
    
    public GeolocationService()
    {
        var countryStream = ReadCountryStream();
        if (countryStream is not null)
        {
            _countries = new Reader(countryStream);
        }
        
        var asnStream = ReadAsnStream();
        if (asnStream is not null)
        {
            _asn = new Reader(asnStream);
        }
        
        var countriesStream = GetCountriesStream();
        if (countriesStream is not null)
        {
            using var reader = new StreamReader(countriesStream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<CountryRecord>();
            foreach (var record in records)
            {
                _alpha3ByAlpha2[record.Alpha2] = record.Alpha3;
            }
        }
    }
    
    public bool TryResolve(string ip, [MaybeNullWhen(false)] out string countryCode, [MaybeNullWhen(false)] out string asn)
    {
        countryCode = null;
        asn = null;
        if (_countries == null) return false;
        if (_asn == null) return false;
        if (!IPAddress.TryParse(ip, out var address)) return false;
        var countryData = _countries.Find<Dictionary<string, object>>(address);
        var asnData = _asn.Find<Dictionary<string, object>>(address);
        if (countryData is not null
            && asnData is not null
            && asnData.TryGetValue(Provider, out var provider)
            && provider is string asnValue
            && countryData.TryGetValue(Country, out var country)
            && country is IDictionary<string, object> countries
            && countries.TryGetValue(IsoCode, out var alpha2)
            && alpha2 is string alpha2S
            && _alpha3ByAlpha2.TryGetValue(alpha2S, out var alpha3))
        {
            countryCode = alpha3;
            asn = asnValue;
            return true;
        }

        return false;
    }

    private static Stream? ReadCountryStream()
    {
        var assembly = typeof(GeolocationService).Assembly;
        const string key = "GeoLite2-Country.mmdb";
        var manifest = assembly.GetResourceKey(key);
        if (manifest == null)
        {
            return null;
        }

        return assembly.GetManifestResourceStream(manifest);
    }
    
    private static Stream? ReadAsnStream()
    {
        var assembly = typeof(GeolocationService).Assembly;
        const string key = "GeoLite2-ASN.mmdb";
        var manifest = assembly.GetResourceKey(key);
        if (manifest == null)
        {
            return null;
        }

        return assembly.GetManifestResourceStream(manifest);
    }

    private static Stream? GetCountriesStream()
    {
        var assembly = typeof(GeolocationService).Assembly;
        const string key = "countries.csv";
        var manifest = assembly.GetResourceKey(key);
        if (manifest == null)
        {
            return null;
        }

        return assembly.GetManifestResourceStream(manifest);
    }

    public void Dispose()
    {
        _countries?.Dispose();
        _asn?.Dispose();
    }
}