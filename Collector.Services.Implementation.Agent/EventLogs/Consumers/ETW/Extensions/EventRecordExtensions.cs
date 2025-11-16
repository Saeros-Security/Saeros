using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Collector.Core.Extensions;
using Collector.Detection.Rules.Builders;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using Microsoft.O365.Security.ETW;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Extensions;

internal static class EventRecordExtensions
{
    private const ushort TDH_INTYPE_UNICODESTRING = 1;
    private const ushort TDH_INTYPE_ANSISTRING = 2;
    private const ushort TDH_INTYPE_INT8 = 3;
    private const ushort TDH_INTYPE_UINT8 = 4;
    private const ushort TDH_INTYPE_INT16 = 5;
    private const ushort TDH_INTYPE_UINT16 = 6;
    private const ushort TDH_INTYPE_INT32 = 7;
    private const ushort TDH_INTYPE_UINT32 = 8;
    private const ushort TDH_INTYPE_INT64 = 9;
    private const ushort TDH_INTYPE_UINT64 = 10;
    private const ushort TDH_INTYPE_FLOAT = 11;
    private const ushort TDH_INTYPE_DOUBLE = 12;
    private const ushort TDH_INTYPE_BOOLEAN = 13;
    private const ushort TDH_INTYPE_BINARY = 14;
    private const ushort TDH_INTYPE_GUID = 15;
    private const ushort TDH_INTYPE_POINTER = 16;
    private const ushort TDH_INTYPE_FILETIME = 17;
    private const ushort TDH_INTYPE_SYSTEMTIME = 18;
    private const ushort TDH_INTYPE_SID = 19;
    private const ushort TDH_INTYPE_HEXINT32 = 20;
    private const ushort TDH_INTYPE_HEXINT64 = 21;
    private const ushort TDH_INTYPE_COUNTEDSTRING = 300;
    private const ushort TDH_INTYPE_COUNTEDANSISTRING = 301;
    private const ushort TDH_INTYPE_REVERSEDCOUNTEDSTRING = 302;
    private const ushort TDH_INTYPE_REVERSEDCOUNTEDANSISTRING = 303;
    private const ushort TDH_INTYPE_NONNULLTERMINATEDSTRING = 304;
    private const ushort TDH_INTYPE_NONNULLTERMINATEDANSISTRING = 305;
    private const ushort TDH_INTYPE_HEXDUMP = 309;
    private const ushort TDH_INTYPE_WBEMSID = 310;

    private const ushort TDH_OUTTYPE_HEXINT8 = 16;
    private const ushort TDH_OUTTYPE_HEXINT16 = 17;
    private const ushort TDH_OUTTYPE_HEXINT32 = 18;
    private const ushort TDH_OUTTYPE_HEXINT64 = 19;
    private const ushort TDH_OUTTYPE_PORT = 22;
    private const ushort TDH_OUTTYPE_IPV4 = 23;
    private const ushort TDH_OUTTYPE_IPV6 = 24;
    private const ushort TDH_OUTTYPE_ERRORCODE = 29;
    private const ushort TDH_OUTTYPE_WIN32ERROR = 30;
    private const ushort TDH_OUTTYPE_NTSTATUS = 31;
    private const ushort TDH_OUTTYPE_HRESULT = 32;

    private static readonly ConcurrentDictionary<ushort, ConcurrentHashSet<string>> BlacklistedPropertyNames = new();
    private const string NewLine = "\n";
    private const string CarriageReturn = "\r";
    private const string Tabulation = "\t";
    
    private static string RemoveTabulationsAndLineBreaks(string input)
    {
        return input.Replace(CarriageReturn, string.Empty).Replace(NewLine, string.Empty).Replace(Tabulation, string.Empty);
    }
        
    public static void FillProperties(this EventRecord record, ILogger logger, Dictionary<string, string> data, Func<EventRecord, Property, bool> isPropertyLengthUnknown)
    {
        foreach (var property in record.Properties)
        {
            try
            {
                if (isPropertyLengthUnknown(record, property))
                {
                    continue;
                }

                if (record.TryGetValue(logger, property, out var value) && !data.TryAdd(property.Name, RemoveTabulationsAndLineBreaks(value)))
                {
                    if (data.TryGetValue(property.Name, out var current))
                    {
                        if (current.Contains(Constants.AbnormalSeparator))
                        {
                            var entries = current.Split(Constants.AbnormalSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                            entries.Add(RemoveTabulationsAndLineBreaks(value));
                            data[property.Name] = string.Join(Constants.AbnormalSeparator, entries);
                        }
                        else
                        {
                            data[property.Name] = string.Join(Constants.AbnormalSeparator, new List<string> { current, RemoveTabulationsAndLineBreaks(value) });
                        }
                    }
                }
            }
            finally
            {
                property.Dispose();
            }
        }
    }
        
    // https://learn.microsoft.com/en-us/windows/win32/api/tdh/ne-tdh-_tdh_out_type
    // https://learn.microsoft.com/fr-fr/windows/win32/wes/eventmanifestschema-outputtype-complextype
    // https://github.com/fireeye/pywintrace/blob/master/etw/tdh.py
    private static bool TryGetValue(this EventRecord eventRecord, ILogger logger, Property property, [MaybeNullWhen(false)] out string value)
    {
        value = null;
        var inType = property.Type;
        var outType = property.OutType;
        if (BlacklistedPropertyNames.TryGetValue(eventRecord.Id, out var properties) && properties.Contains(property.Name)) return false;
        try
        {
            switch (inType)
            {
                case TDH_INTYPE_UNICODESTRING:
                case TDH_INTYPE_COUNTEDSTRING:
                case TDH_INTYPE_REVERSEDCOUNTEDSTRING:
                case TDH_INTYPE_NONNULLTERMINATEDSTRING:
                    value = ReadUnicodeCountedString(eventRecord, property, inType);
                    break;

                case TDH_INTYPE_ANSISTRING:
                case TDH_INTYPE_COUNTEDANSISTRING:
                case TDH_INTYPE_REVERSEDCOUNTEDANSISTRING:
                case TDH_INTYPE_NONNULLTERMINATEDANSISTRING:
                    value = ReadAnsiCountedString(eventRecord, property, inType);
                    break;

                case TDH_INTYPE_INT8:
                    value = ReadInt8(eventRecord, property);
                    break;

                case TDH_INTYPE_UINT8:
                    value = ReadUInt8(eventRecord, property, outType);
                    break;

                case TDH_INTYPE_INT16:
                    value = ReadInt16(eventRecord, property);
                    break;

                case TDH_INTYPE_UINT16:
                    value = ReadUInt16(eventRecord, property, outType);
                    break;

                case TDH_INTYPE_INT32:
                    value = ReadInt32(eventRecord, property, outType);
                    break;

                case TDH_INTYPE_UINT32:
                case TDH_INTYPE_HEXINT32:
                    value = ReadUInt32(eventRecord, property, outType);
                    break;

                case TDH_INTYPE_INT64:
                    value = ReadInt64(eventRecord, property);
                    break;

                case TDH_INTYPE_UINT64:
                case TDH_INTYPE_HEXINT64:
                    value = ReadUInt64(eventRecord, property, outType);
                    break;

                case TDH_INTYPE_FLOAT:
                    value = ReadFloat(eventRecord, property);
                    break;

                case TDH_INTYPE_DOUBLE:
                    value = ReadDouble(eventRecord, property);
                    break;

                case TDH_INTYPE_BOOLEAN:
                    value = ReadBoolean(eventRecord, property);
                    break;

                case TDH_INTYPE_GUID:
                    value = ReadGuid(eventRecord, property);
                    break;

                case TDH_INTYPE_POINTER:
                    value = ReadPointer(eventRecord, property);
                    break;

                case TDH_INTYPE_FILETIME:
                case TDH_INTYPE_SYSTEMTIME:
                    value = ReadTime(eventRecord, property);
                    break;

                case TDH_INTYPE_SID:
                case TDH_INTYPE_WBEMSID:
                    value = ReadSid(eventRecord, property);
                    break;

                case TDH_INTYPE_BINARY:
                case TDH_INTYPE_HEXDUMP:
                    value = ReadBinary(eventRecord, property, outType);
                    break;

                default:
                    logger.Throttle(property.Name, itself => itself.LogError("An error has occurred while reading property {Name} from event {Id} of {Provider}", property.Name, eventRecord.Id, eventRecord.ProviderName), expiration: TimeSpan.FromMinutes(1));
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Throttle(property.Name, itself => itself.LogError(ex, "An error has occurred while reading property {Name} from event {Id} of {Provider}", property.Name, eventRecord.Id, eventRecord.ProviderName), expiration: TimeSpan.FromMinutes(1));
        }

        return !string.IsNullOrEmpty(value);
    }

    private static string ReadBinary(EventRecord eventRecord, Property property, int outType)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetBinary(property.Name, out var binary))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }
            
        return outType == TDH_OUTTYPE_IPV6 ? GetIPv6(binary) : GetHexBinary(binary);
    }

    private static string ReadSid(EventRecord eventRecord, Property property)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetSecurityIdentifier(property.Name, out var sid))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }
            
        return sid.ToString();
    }

    private static string ReadTime(EventRecord eventRecord, Property property)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetDateTime(property.Name, out var dateTime))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }
            
        return ((DateTime)dateTime).ToString("O");
    }

    private static string ReadPointer(EventRecord eventRecord, Property property)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetPointer(property.Name, out var pointer))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }
            
        return ((IntPtr)pointer).ToInt64().ToString();
    }

    private static string ReadGuid(EventRecord eventRecord, Property property)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetBinary(property.Name, out var binary))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }
            
        return new Guid(binary).ToString();
    }

    private static string ReadBoolean(EventRecord eventRecord, Property property)
    {
        return eventRecord.GetBool(property.Name).ToString();
    }

    private static string ReadDouble(EventRecord eventRecord, Property property)
    {
        return eventRecord.GetDouble(property.Name).ToString(CultureInfo.InvariantCulture);
    }

    private static string ReadFloat(EventRecord eventRecord, Property property)
    {
        return eventRecord.GetFloat(property.Name).ToString(CultureInfo.InvariantCulture);
    }

    private static string ReadUInt64(EventRecord eventRecord, Property property, int outType)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetUInt64(property.Name, out var u64Val))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }
            
        return outType == TDH_OUTTYPE_HEXINT64 ? $"0x{u64Val:x}" : u64Val.ToString();
    }

    private static string ReadInt64(EventRecord eventRecord, Property property)
    {
        if (eventRecord.TryGetInt64(property.Name, out var value))
        {
            return value.ToString();
        }
            
        BlacklistProperty(eventRecord, property);
        return string.Empty;
    }

    private static string ReadUInt32(EventRecord eventRecord, Property property, int outType)
    {
        var value = string.Empty;
        switch (outType)
        {
            case TDH_OUTTYPE_IPV4:
            {
                if (eventRecord.TryGetIPAddress(property.Name, out var ipAddress))
                {
                    value = ipAddress.ToString();
                }
                else
                {
                    BlacklistProperty(eventRecord, property);
                }

                break;
            }
            case TDH_OUTTYPE_ERRORCODE:
            case TDH_OUTTYPE_HRESULT:
            case TDH_OUTTYPE_NTSTATUS:
            case TDH_OUTTYPE_WIN32ERROR:
            case TDH_OUTTYPE_HEXINT32:
            {
                if (eventRecord.TryGetUInt32(property.Name, out var u32Val))
                {
                    value = $"0x{u32Val:x}";
                }
                else
                {
                    BlacklistProperty(eventRecord, property);
                }

                break;
            }
            default:
            {
                if (eventRecord.TryGetUInt32(property.Name, out var u32Val))
                {
                    value = u32Val.ToString();
                }
                else
                {
                    BlacklistProperty(eventRecord, property);
                }

                break;
            }
        }

        return value;
    }

    private static string ReadInt32(EventRecord eventRecord, Property property, int outType)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetInt32(property.Name, out var i32Val))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }

        return outType switch
        {
            TDH_OUTTYPE_HRESULT => $"0x{i32Val:x}",
            _ => i32Val.ToString()
        };
    }

    private static string ReadUInt16(EventRecord eventRecord, Property property, int outType)
    {
        var value = string.Empty;
        if (!eventRecord.TryGetUInt16(property.Name, out var u16Val))
        {
            BlacklistProperty(eventRecord, property);
            return value;
        }

        return outType switch
        {
            TDH_OUTTYPE_PORT => $"{IPAddress.NetworkToHostOrder((short)u16Val) & 0xFFFF}",
            TDH_OUTTYPE_HEXINT16 => $"0x{u16Val:x}",
            _ => u16Val.ToString()
        };
    }

    private static string ReadInt16(EventRecord eventRecord, Property property)
    {
        if (eventRecord.TryGetInt16(property.Name, out var value))
        {
            return value.ToString();
        }
            
        BlacklistProperty(eventRecord, property);
        return string.Empty;
    }

    private static string ReadUInt8(EventRecord eventRecord, Property property, int outType)
    {
        var value = string.Empty;
        switch (outType)
        {
            case TDH_OUTTYPE_HEXINT8:
            {
                if (eventRecord.TryGetUInt8(property.Name, out var integer))
                {
                    value = $"0x{integer:x}";
                }
                else
                {
                    BlacklistProperty(eventRecord, property);
                }

                break;
            }
            default:
            {
                if (eventRecord.TryGetUInt8(property.Name, out var integer))
                {
                    value = integer.ToString();
                }
                else
                {
                    BlacklistProperty(eventRecord, property);
                }

                break;
            }
        }

        return value;
    }

    private static string ReadInt8(EventRecord eventRecord, Property property)
    {
        if (eventRecord.TryGetInt8(property.Name, out var value))
        {
            return value.ToString();
        }
            
        BlacklistProperty(eventRecord, property);
        return string.Empty;
    }

    private static string ReadAnsiCountedString(EventRecord eventRecord, Property property, int inType)
    {
        var value = string.Empty;
        if (inType is TDH_INTYPE_COUNTEDANSISTRING or TDH_INTYPE_REVERSEDCOUNTEDANSISTRING)
        {
            if (eventRecord.TryGetCountedString(property.Name, out var countedString))
            {
                value = countedString;
            }
            else
            {
                BlacklistProperty(eventRecord, property);
            }
        }
        else if (eventRecord.TryGetAnsiString(property.Name, out var ansiString))
        {
            value = ansiString;
        }
        else
        {
            BlacklistProperty(eventRecord, property);
        }

        return value;
    }

    private static string ReadUnicodeCountedString(EventRecord eventRecord, Property property, int inType)
    {
        var value = string.Empty;
        if (inType is TDH_INTYPE_COUNTEDSTRING or TDH_INTYPE_REVERSEDCOUNTEDSTRING)
        {
            if (eventRecord.TryGetCountedString(property.Name, out var countedString))
            {
                value = countedString;
            }
            else
            {
                BlacklistProperty(eventRecord, property);
            }
        }
        else if (eventRecord.TryGetUnicodeString(property.Name, out var unicodeString))
        {
            value = unicodeString;
        }
        else
        {
            BlacklistProperty(eventRecord, property);
        }

        return value;
    }

    private static string GetHexBinary(byte[] data)
    {
        return data.Length == 0 ? string.Empty : $"0x{Convert.ToHexString(data)}";
    }

    private static string GetIPv6(byte[] data)
    {
        return data.Length == 16 ? new IPAddress(data).ToString() : GetHexBinary(data);
    }
        
    private static void BlacklistProperty(EventRecord eventRecord, Property property)
    {
        BlacklistedPropertyNames.AddOrUpdate(eventRecord.Id, addValueFactory: _ => [property.Name], updateValueFactory: (_, current) =>
        {
            current.Add(property.Name);
            return current;
        });
    }
}