namespace Collector.Detection.Converters;

public static class SysmonEventRegistry
{
    public static readonly IDictionary<int, string> EventMapping = new Dictionary<int, string>
    {
        {
            1, "ProcessCreate"
        },
        {
            2, "FileCreateTime"
        },
        {
            3, "NetworkConnect"
        },
        // 4 cannot be filtered
        {
            5, "ProcessTerminate"
        },
        {
            6, "DriverLoad"
        },
        {
            7, "ImageLoad"
        },
        {
            8, "CreateRemoteThread"
        },
        {
            9, "RawAccessRead"
        },
        {
            10, "ProcessAccess"
        },
        {
            11, "FileCreate"
        },
        {
            12, "RegistryEvent"
        },
        {
            13, "RegistryEvent"
        },
        {
            14, "RegistryEvent"
        },
        {
            15, "FileCreateStreamHash"
        },
        // 16 cannot be filtered
        {
            17, "PipeEvent"
        },
        {
            18, "PipeEvent"
        },
        {
            19, "WmiEvent"
        },
        {
            20, "WmiEvent"
        },
        {
            21, "WmiEvent"
        },
        {
            22, "DNSQuery"
        },
        {
            23, "FileDelete"
        },
        {
            24, "ClipboardChange"
        },
        {
            25, "ProcessTampering"
        },
        {
            26, "FileDeleteDetected"
        },
        {
            27, "FileBlockExecutable"
        },
        {
            28, "FileBlockShredding"
        },
        {
            29, "FileExecutableDetected"
        }
    };
}