using CsvHelper.Configuration.Attributes;

namespace Collector.Services.Implementation.Geolocation;

internal sealed class CountryRecord : IEquatable<CountryRecord>
{
    [Index(1)] 
    public string Alpha2 { get; set; } = string.Empty;

    [Index(2)]
    public string Alpha3 { get; set; } = string.Empty;

    public bool Equals(CountryRecord? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Alpha2 == other.Alpha2 && Alpha3 == other.Alpha3;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is CountryRecord other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Alpha2, Alpha3);
    }
}