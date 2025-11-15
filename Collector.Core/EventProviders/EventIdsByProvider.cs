using Collector.Core.Extensions;

namespace Collector.Core.EventProviders;

public sealed class EventIdsByProvider(IDictionary<ProviderKey, ISet<int>> items, ISet<string> properties)
{
    public IDictionary<ProviderKey, ISet<int>> Items { get; } = items;
    public ISet<string> Properties { get; } = properties;

    public IDictionary<ProviderKey, HashSet<int>> GetFilteredEventIdsByProvider(ProviderType providerType, IDictionary<ProviderKey, ISet<int>> eventIdsByProviderKey)
    {
        var filteredEventIds = Items.Where(item => item.Key.ProviderType == providerType).GroupBy(item => item.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.SelectMany(v => v.Value).ToHashSet());
        foreach (var kvp in eventIdsByProviderKey)
        {
            if (kvp.Key.ProviderType == providerType)
            {
                if (filteredEventIds.TryGetValue(kvp.Key, out var value))
                {
                    value.AddRange(kvp.Value);
                }
                else
                {
                    filteredEventIds[kvp.Key] = new HashSet<int>(kvp.Value);
                }
            }
        }

        return filteredEventIds;
    }
}