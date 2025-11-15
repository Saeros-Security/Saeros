using Collector.Detection.Rules;

namespace Collector.Detection.Extensions;

public static class RuleMatchExtensions
{
    private const string CommandLine = nameof(CommandLine);
    private const string NewProcessName = nameof(NewProcessName);
    private const string MemberName = nameof(MemberName);
    private const string RelativeTargetName = nameof(RelativeTargetName);
    private const string ObjectDN = nameof(ObjectDN);
    private const string ServiceName = "Saeros_Collector";
    private const string Member = "CN=Saeros";
    private const string ConsolePath = @"C:\PROGRA~1\SAEROS~1\Console\Console.exe";
    private const string ConsoleProgramPath = @"C:\ProgramData\Saeros\Console";
    private const string GpUpdate = "CMD.exe /c GPUpdate.exe /target:computer /force";
    private const string ScheduledTask = @"Policies\{3560FF19-45A3-4F9A-956B-937A04D2AABF}\Machine\Preferences\ScheduledTasks\ScheduledTasks.xml";
    private const string ProductPath = @"C:\Program Files\Saeros";
    private const string ObjectDNGPO = "CN={3560FF19-45A3-4F9A-956B-937A04D2AABF},CN=POLICIES,CN=SYSTEM";
    private const string ForwardedEvents = "wevtutil  sl forwardedevents /ms:1000000000";
    
    public static bool Filter(this RuleMatch ruleMatch)
    {
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Use Short Name Path in Image") && ruleMatch.WinEvent.EventData.TryGetValue(NewProcessName, out var newProcessName))
        {
            return newProcessName.Equals(ConsolePath);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Use Short Name Path in Command Line") && ruleMatch.WinEvent.EventData.TryGetValue(NewProcessName, out newProcessName))
        {
            return newProcessName.Equals(ConsolePath);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Suspicious SYSVOL Domain Group Policy Access") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out var commandLine))
        {
            return commandLine.Contains(ServiceName);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Possible LOLBIN") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Contains(ServiceName);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("New Service Creation") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Contains(ServiceName);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("New Service Creation Using Sc.EXE") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Contains(ServiceName);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.StartsWith("User Added To", StringComparison.OrdinalIgnoreCase) && ruleMatch.WinEvent.EventData.TryGetValue(MemberName, out var memberName))
        {
            return memberName.StartsWith(Member);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Suspicious Recursive Takeown") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Contains(ConsoleProgramPath);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("File or Folder Permissions Modifications") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Contains(ConsoleProgramPath);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Suspicious Process Created Via Wmic.EXE") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Contains(GpUpdate);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("First Time Seen Remote Named Pipe") && ruleMatch.WinEvent.EventData.TryGetValue(RelativeTargetName, out var relativeTargetName))
        {
            return relativeTargetName.Equals(ServiceName);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Persistence and Execution at Scale via GPO Scheduled Task") && ruleMatch.WinEvent.EventData.TryGetValue(RelativeTargetName, out relativeTargetName))
        {
            return relativeTargetName.Contains(ScheduledTask);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Persistence and Execution at Scale via GPO Scheduled Task") && ruleMatch.WinEvent.EventData.TryGetValue(ObjectDN, out var objectDN))
        {
            return objectDN.StartsWith(ObjectDNGPO);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Powershell Defender Exclusion") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Contains(ProductPath);
        }
        
        if (ruleMatch.DetectionDetails.RuleMetadata.Title.Equals("Suspicious Eventlog Clearing or Configuration Change Activity") && ruleMatch.WinEvent.EventData.TryGetValue(CommandLine, out commandLine))
        {
            return commandLine.Equals(ForwardedEvents);
        }

        return false;
    }
}