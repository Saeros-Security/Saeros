using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Collector.Core.Converters;
using Collector.Databases.Abstractions.Repositories.RuleConfigurations;
using Collector.Databases.Implementation.Contexts.RuleConfigurations;
using Collector.Databases.Implementation.Helpers;
using Collector.Detection.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Collector.Databases.Implementation.Repositories.RuleConfigurations;

public sealed class RuleConfigurationsRepository(RuleConfigurationContext context) : IRuleConfigurationsRepository
{
    private enum ConfigurationType
    {
        Aliases,
        EventTitles,
        PropertyMappings,
        Details,
        ChannelAbbrevations,
        ProviderAbbrevations,
        ExcludedRules,
        NoisyRules,
        ProvenRules,
        TargetEventIds
    }

    private static readonly JsonSerializerOptions Options = new() { Converters = { new ProviderEventIdConverter() } };

    private static int GetConfigurationType(ConfigurationType type)
    {
        return (int)type;
    }

    private void AddCore(ConfigurationType type, string content)
    {
        try
        {
            using var connection = context.CreateSingleConnection();
            connection.DbConnection.Open();
            using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
            INSERT OR REPLACE INTO RuleConfigurations (Type, Content)
            VALUES (@Type, @Content);
";
            command.Parameters.Add(new SqliteParameter("Type", DatabaseHelper.GetValue(GetConfigurationType(type))));
            command.Parameters.Add(new SqliteParameter("Content", DatabaseHelper.GetValue(content)));
            command.ExecuteNonQuery();
        }
        catch (OperationCanceledException)
        {
            context.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "An error has occurred");
        }
    }

    public void AddAliases(Aliases aliases)
    {
        AddCore(ConfigurationType.Aliases, JsonSerializer.Serialize(aliases, Options));
    }

    public void AddEventTitles(EventTitles eventTitles)
    {
        AddCore(ConfigurationType.EventTitles, JsonSerializer.Serialize(eventTitles, Options));
    }

    public void AddPropertyMappings(PropertyMappings propertyMappings)
    {
        AddCore(ConfigurationType.PropertyMappings, JsonSerializer.Serialize(propertyMappings, Options));
    }

    public void AddDetails(Details details)
    {
        AddCore(ConfigurationType.Details, JsonSerializer.Serialize(details, Options));
    }

    public void AddChannelAbbrevations(ChannelAbbrevations channelAbbrevations)
    {
        AddCore(ConfigurationType.ChannelAbbrevations, JsonSerializer.Serialize(channelAbbrevations, Options));
    }

    public void AddProviderAbbrevations(ProviderAbbrevations providerAbbrevations)
    {
        AddCore(ConfigurationType.ProviderAbbrevations, JsonSerializer.Serialize(providerAbbrevations, Options));
    }

    public void AddExcludedRules(ExcludedRules excludedRules)
    {
        AddCore(ConfigurationType.ExcludedRules, JsonSerializer.Serialize(excludedRules, Options));
    }

    public void AddNoisyRules(NoisyRules noisyRules)
    {
        AddCore(ConfigurationType.NoisyRules, JsonSerializer.Serialize(noisyRules, Options));
    }

    public void AddProvenRules(ProvenRules provenRules)
    {
        AddCore(ConfigurationType.ProvenRules, JsonSerializer.Serialize(provenRules, Options));
    }

    public void AddTargetEventIds(TargetEventIds targetEventIds)
    {
        AddCore(ConfigurationType.TargetEventIds, JsonSerializer.Serialize(targetEventIds, Options));
    }

    private bool TryGet(ConfigurationType type, [MaybeNullWhen(false)] out string content)
    {
        content = string.Empty;
        using var connection = context.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Content
            FROM RuleConfigurations
            WHERE Type = @Type;
";

        command.Parameters.Add(new SqliteParameter("Type", GetConfigurationType(type)));
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            content = reader.GetString(ordinal: 0);
            return !string.IsNullOrEmpty(content);
        }

        return false;
    }

    public bool TryGetAliases([MaybeNullWhen(false)] out Aliases aliases)
    {
        aliases = null;
        if (TryGet(ConfigurationType.Aliases, out var content))
        {
            aliases = JsonSerializer.Deserialize<Aliases>(content, Options);
            return aliases is not null;
        }

        return false;
    }

    public bool TryGetEventTitles([MaybeNullWhen(false)] out EventTitles eventTitles)
    {
        eventTitles = null;
        if (TryGet(ConfigurationType.EventTitles, out var content))
        {
            eventTitles = JsonSerializer.Deserialize<EventTitles>(content, Options);
            return eventTitles is not null;
        }

        return false;
    }

    public bool TryGetPropertyMappings([MaybeNullWhen(false)] out PropertyMappings propertyMappings)
    {
        propertyMappings = null;
        if (TryGet(ConfigurationType.PropertyMappings, out var content))
        {
            propertyMappings = JsonSerializer.Deserialize<PropertyMappings>(content, Options);
            return propertyMappings is not null;
        }

        return false;
    }

    public bool TryGetDetails([MaybeNullWhen(false)] out Details details)
    {
        details = null;
        if (TryGet(ConfigurationType.Details, out var content))
        {
            details = JsonSerializer.Deserialize<Details>(content, Options);
            return details is not null;
        }

        return false;
    }

    public bool TryGetChannelAbbrevations([MaybeNullWhen(false)] out ChannelAbbrevations channelAbbrevations)
    {
        channelAbbrevations = null;
        if (TryGet(ConfigurationType.ChannelAbbrevations, out var content))
        {
            channelAbbrevations = JsonSerializer.Deserialize<ChannelAbbrevations>(content, Options);
            return channelAbbrevations is not null;
        }

        return false;
    }

    public bool TryGetProviderAbbrevations([MaybeNullWhen(false)] out ProviderAbbrevations providerAbbrevations)
    {
        providerAbbrevations = null;
        if (TryGet(ConfigurationType.ProviderAbbrevations, out var content))
        {
            providerAbbrevations = JsonSerializer.Deserialize<ProviderAbbrevations>(content, Options);
            return providerAbbrevations is not null;
        }

        return false;
    }

    public bool TryGetExcludedRules([MaybeNullWhen(false)] out ExcludedRules excludedRules)
    {
        excludedRules = null;
        if (TryGet(ConfigurationType.ExcludedRules, out var content))
        {
            excludedRules = JsonSerializer.Deserialize<ExcludedRules>(content, Options);
            return excludedRules is not null;
        }

        return false;
    }

    public bool TryGetNoisyRules([MaybeNullWhen(false)] out NoisyRules noisyRules)
    {
        noisyRules = null;
        if (TryGet(ConfigurationType.NoisyRules, out var content))
        {
            noisyRules = JsonSerializer.Deserialize<NoisyRules>(content, Options);
            return noisyRules is not null;
        }

        return false;
    }

    public bool TryGetProvenRules([MaybeNullWhen(false)] out ProvenRules provenRules)
    {
        provenRules = null;
        if (TryGet(ConfigurationType.ProvenRules, out var content))
        {
            provenRules = JsonSerializer.Deserialize<ProvenRules>(content, Options);
            return provenRules is not null;
        }

        return false;
    }

    public bool TryGetTargetEventIds([MaybeNullWhen(false)] out TargetEventIds targetEventIds)
    {
        targetEventIds = null;
        if (TryGet(ConfigurationType.TargetEventIds, out var content))
        {
            targetEventIds = JsonSerializer.Deserialize<TargetEventIds>(content, Options);
            return targetEventIds is not null;
        }

        return false;
    }
}