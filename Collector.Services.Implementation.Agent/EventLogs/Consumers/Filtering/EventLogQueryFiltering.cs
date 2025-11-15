using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using Collector.Core.EventProviders;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.Filtering;

internal static class EventLogQueryFiltering
{
    private sealed record Indexer(StringBuilder StringBuilder, int Index)
    {
        public int Index { get; set; } = Index;
    }

    private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    
    public static IDictionary<string, string> BuildQueryByChannel(IDictionary<ProviderKey, HashSet<int>> eventIdsByProviderName, Func<ProviderKey, IEnumerable<string>> channelFactory)
    {
        var queriesByChannel = new ConcurrentDictionary<string, Indexer>();
        foreach (var kvp in eventIdsByProviderName)
        {
            var channelNames = channelFactory(kvp.Key).ToHashSet();
            foreach (var channelName in channelNames)
            {
                if (string.IsNullOrEmpty(channelName)) continue;
                foreach (var query in EnumerateQueries(channelName, providerName: kvp.Key.ProviderName, kvp.Value, indexGenerator: () => queriesByChannel.TryGetValue(channelName, out var indexer) ? ++indexer.Index : 0))
                {
                    queriesByChannel.AddOrUpdate(channelName, addValueFactory: _ =>
                    {
                        var sb = new StringBuilder();
                        sb.Append(query);
                        return new Indexer(sb, Index: 0);
                    }, updateValueFactory: (_, current) =>
                    {
                        current.StringBuilder.Append(query);
                        return current;
                    });
                }
            }
        }

        return queriesByChannel.ToDictionary(kvp => kvp.Key, kvp => IndentXml($"<QueryList>{kvp.Value.StringBuilder}</QueryList>"));
    }
    
    public static IDictionary<string, string> BuildQueryByChannel(IDictionary<ProviderKey, HashSet<int>> eventIdsByProviderName, DateTime time, Func<ProviderKey, IEnumerable<string>> channelFactory)
    {
        var queriesByChannel = new ConcurrentDictionary<string, Indexer>();
        foreach (var kvp in eventIdsByProviderName)
        {
            var channelNames = channelFactory(kvp.Key).ToHashSet();
            foreach (var channelName in channelNames)
            {
                if (string.IsNullOrEmpty(channelName)) continue;
                foreach (var query in EnumerateQueries(channelName, providerName: kvp.Key.ProviderName, kvp.Value, time, indexGenerator: () => queriesByChannel.TryGetValue(channelName, out var indexer) ? ++indexer.Index : 0))
                {
                    queriesByChannel.AddOrUpdate(channelName, addValueFactory: _ =>
                    {
                        var sb = new StringBuilder();
                        sb.Append(query);
                        return new Indexer(sb, Index: 0);
                    }, updateValueFactory: (_, current) =>
                    {
                        current.StringBuilder.Append(query);
                        return current;
                    });
                }
            }
        }

        return queriesByChannel.ToDictionary(kvp => kvp.Key, kvp => IndentXml($"<QueryList>{kvp.Value.StringBuilder}</QueryList>"));
    }
    
    private static IEnumerable<string> EnumerateQueries(string channelName, string providerName, HashSet<int> eventIds, Func<int> indexGenerator)
    {
        foreach (var chunk in eventIds.Chunk(20)) // 20 is the maximum number of event Ids to filter
        {
            if (providerName.Equals("Security") || providerName.Equals("Application") || providerName.Equals("System"))
            {
                yield return $@"<Query Id=""{indexGenerator()}"" Path=""{channelName}"">
        <Select Path=""{channelName}"">*[System[({string.Join(" or ", chunk.Select(eventId => $"EventID={eventId}"))})]]</Select>
      </Query>";
            }
            else
            {
                yield return $@"<Query Id=""{indexGenerator()}"" Path=""{channelName}"">
        <Select Path=""{channelName}"">*[System[Provider[@Name='{providerName}'] and ({string.Join(" or ", chunk.Select(eventId => $"EventID={eventId}"))})]]</Select>
      </Query>";
            }
        }
    }
    
    private static IEnumerable<string> EnumerateQueries(string channelName, string providerName, HashSet<int> eventIds, DateTime time, Func<int> indexGenerator)
    {
        foreach (var chunk in eventIds.Chunk(20)) // 20 is the maximum number of event Ids to filter
        {
            if (providerName.Equals("Security") || providerName.Equals("Application") || providerName.Equals("System"))
            {
                yield return $@"<Query Id=""{indexGenerator()}"" Path=""{channelName}"">
        <Select Path=""{channelName}"">*[System[TimeCreated[@SystemTime&gt;='{time.ToString(TimeFormat)}'] and ({string.Join(" or ", chunk.Select(eventId => $"EventID={eventId}"))})]]</Select>
      </Query>";
            }
            else
            {
                yield return $@"<Query Id=""{indexGenerator()}"" Path=""{channelName}"">
        <Select Path=""{channelName}"">*[System[TimeCreated[@SystemTime&gt;='{time.ToString(TimeFormat)}'] and Provider[@Name='{providerName}'] and ({string.Join(" or ", chunk.Select(eventId => $"EventID={eventId}"))})]]</Select>
      </Query>";
            }
        }
    }

    private static string IndentXml(string query)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = true
        };
        
        var doc = new XmlDocument();
        doc.LoadXml(query);
        using (var writer = XmlWriter.Create(sb, settings))
        {
            doc.Save(writer);
        }

        return sb.ToString();
    }
}