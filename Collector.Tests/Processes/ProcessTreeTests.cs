using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Services.Implementation.Agent.Processes;
using FluentAssertions;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Collector.Tests.Processes;

public class ProcessTreeTests
{
    private sealed class ProcessMockService(ILogger<ProcessTreeService> logger)
        : ProcessTreeService(logger, new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance), expiration: TimeSpan.FromMilliseconds(500))
    {
        protected override void SendTree(ProcessKey key, ProcessTree processTree)
        {
            ProcessTreeByKey.TryAdd(key, processTree);
        }

        public IDictionary<ProcessKey, ProcessTree> ProcessTreeByKey { get; } = new Dictionary<ProcessKey, ProcessTree>();
    }
    
    private static readonly Guid MicrosoftWindowsSecurityAuditingProviderGuid = new("54849625-5478-4994-A5BA-3E3B0328C30D");
    private const string WorkstationName = "Computer";
    private const string Domain = "WORKGROUP";
    private const long SubjectLogonId = 0x926c;
    private const long TargetLogonId = 0x3e7;
    private const string SubjectUserSid = "S-1-5-21-1004336348-1177238915-682003330-512";
    private const string TargetUserSid = "S-1-5-18";
    
    [Fact]
    public void ProcessTree_Should_Index()
    {
        var key = new ProcessKey(WorkstationName, Domain, processId: 9001, processName: "C:\\Windows\\System32\\app2.exe", SubjectLogonId);

        var processTreeService = new ProcessMockService(NullLogger<ProcessMockService>.Instance);

        processTreeService.Add(WorkstationName, Domain, MicrosoftWindowsSecurityAuditingProviderGuid, eventId: 4688, eventData: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"NewProcessId", "6137"},
            {"NewProcessName", "C:\\Windows\\System32\\svchost.exe"},
            {"ProcessId", "1267"},
            {"ParentProcessName", "C:\\Windows\\System32\\svchost.exe"},
            {"CommandLine", "-k 512"},
            {"SubjectLogonId", $"{SubjectLogonId}"},
            {"TargetLogonId", $"{TargetLogonId}"},
            {"SubjectUserSid", SubjectUserSid},
            {"TargetUserSid", TargetUserSid}
        });
                
        processTreeService.Add(WorkstationName, Domain, MicrosoftWindowsSecurityAuditingProviderGuid, eventId: 4688, eventData: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"NewProcessId", "729"},
            {"NewProcessName", "C:\\Windows\\System32\\explorer.exe"},
            {"ProcessId", "6137"},
            {"ParentProcessName", "C:\\Windows\\System32\\svchost.exe"},
            {"CommandLine", ""},
            {"SubjectLogonId", $"{SubjectLogonId}"},
            {"TargetLogonId", $"{TargetLogonId}"},
            {"SubjectUserSid", SubjectUserSid},
            {"TargetUserSid", TargetUserSid}
        });
        
        processTreeService.Add(WorkstationName, Domain, MicrosoftWindowsSecurityAuditingProviderGuid, eventId: 4688, eventData: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"NewProcessId", "9000"},
            {"NewProcessName", "C:\\Windows\\System32\\app1.exe"},
            {"ProcessId", "729"},
            {"ParentProcessName", "C:\\Windows\\System32\\explorer.exe"},
            {"CommandLine", ""},
            {"SubjectLogonId", $"{SubjectLogonId}"},
            {"TargetLogonId", $"{TargetLogonId}"},
            {"SubjectUserSid", SubjectUserSid},
            {"TargetUserSid", TargetUserSid}
        });
        
        processTreeService.Add(WorkstationName, Domain, MicrosoftWindowsSecurityAuditingProviderGuid, eventId: 4688, eventData: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"NewProcessId", "9001"},
            {"NewProcessName", "C:\\Windows\\System32\\app2.exe"},
            {"ProcessId", "729"},
            {"ParentProcessName", "C:\\Windows\\System32\\explorer.exe"},
            {"CommandLine", "google.com"},
            {"SubjectLogonId", $"{SubjectLogonId}"},
            {"TargetLogonId", $"{TargetLogonId}"},
            {"SubjectUserSid", SubjectUserSid},
            {"TargetUserSid", TargetUserSid}
        });

        var action = () =>
        {
            processTreeService.ProcessTreeByKey.TryGetValue(key, out var processTree).Should().BeTrue();
            processTree.Value.Should().Be("[1267]\tC:\\Windows\\System32\\svchost.exe\r\n[6137]\t C:\\Windows\\System32\\svchost.exe\r\n[729]\t  C:\\Windows\\System32\\explorer.exe\r\n[9001]\t   C:\\Windows\\System32\\app2.exe");
        };

        action.Should().NotThrowAfter(TimeSpan.FromSeconds(10), pollInterval: TimeSpan.FromSeconds(1));
    }
}