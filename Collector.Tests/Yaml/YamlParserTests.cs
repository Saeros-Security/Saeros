using System.Text;
using Detection.Yaml;
using FluentAssertions;
using YamlDotNet.Core;

namespace Collector.Tests.Yaml
{
    public class YamlParserTests
    {
        [Fact]
        public void YamlParser_Should_Deserialize()
        {
            // Arrange
            const string yaml = @"
author: Zach Mathis
date: 2022/03/05
modified: 2023/01/13

title: 'File Created (Sysmon Alert)'
description: |
    File create operations are logged when a file is created or overwritten. 
    This event is useful for monitoring autostart locations, like the Startup folder, as well as temporary and download directories, which are common places malware drops during initial infection.
details: 'Rule: %RuleName% ¦ Path: %TargetFilename% ¦ Proc: %Image% ¦ PID: %ProcessId% ¦ PGUID: %ProcessGuid%'

id: c5e6b545-73a4-4650-ae97-67c239005382
level: medium
status: stable
logsource:
    product: windows
    service: sysmon
    definition: 'Sysmon needs to be installed and configured.'
detection:
    selection_basic:
        Channel: Microsoft-Windows-Sysmon/Operational
        EventID: 11
    filter_no_alerts:
        - RuleName: ''
        - RuleName: '-'
    condition: selection_basic and not filter_no_alerts
falsepositives:
tags:
    - sysmon
references:
    - https://docs.microsoft.com/en-us/sysinternals/downloads/sysmon
ruletype: Hayabusa

sample-evtx: |
    <Event xmlns=""http://schemas.microsoft.com/win/2004/08/events/event"">
        <System>
            <Provider Name=""Microsoft-Windows-Sysmon"" Guid=""{5770385F-C22A-43E0-BF4C-06F5698FFBD9}""/>
            <EventID>11</EventID>
            <Version>2</Version>
            <Level>4</Level>
            <Task>11</Task>
            <Opcode>0</Opcode>
            <Keywords>0x8000000000000000</Keywords>
            <TimeCreated SystemTime=""2021-11-18T07:43:04.979196300Z""/>
            <EventRecordID>13279</EventRecordID>
            <Correlation/>
            <Execution ProcessID=""2148"" ThreadID=""3896""/>
            <Channel>Microsoft-Windows-Sysmon/Operational</Channel>
            <Computer>PC-01.cybercat.local</Computer>
            <Security UserID=""S-1-5-18""/>
        </System>
        <EventData>
            <Data Name=""RuleName"">technique_id=T1047,technique_name=File System Permissions Weakness</Data>
            <Data Name=""UtcTime"">2021-11-18 07:43:04.966</Data>
            <Data Name=""ProcessGuid"">{510C1E8A-EF1A-6195-1A00-000000000F00}</Data>
            <Data Name=""ProcessId"">1128</Data>
            <Data Name=""Image"">C:\Windows\System32\svchost.exe</Data>
            <Data Name=""TargetFilename"">C:\Windows\Prefetch\INSTALLUTIL.EXE-9953E407.pf</Data>
            <Data Name=""CreationUtcTime"">2021-11-18 06:18:57.236</Data>
            <Data Name=""User"">NT AUTHORITY\SYSTEM</Data>
        </EventData>
    </Event>";

            // Act
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            var rules = YamlParser.Deserialize<YamlRule>(stream);

            // Assert
            rules.Should().NotBeNull();
        }

        [Fact]
        public void YamlParser_Should_ThrowOnRequiredProperty()
        {
            // Arrange
            const string yaml = @"
#Author section
author: Zach Mathis
date: 2022/03/22
modified: 2022/04/17

#Alert section
title: Possible Timestomping
details: 'Path: %TargetFilename% ¦ Process: %Image% ¦ User: %User% ¦ CreationTime: %CreationUtcTime% ¦ PreviousTime: %PreviousCreationUtcTime% ¦ PID: %PID% ¦ PGUID: %ProcessGuid%'
description: |
    The Change File Creation Time Event is registered when a file creation time is explicitly modified by a process.
    This event helps tracking the real creation time of a file.
    Attackers may change the file creation time of a backdoor to make it look like it was installed with the operating system.
    Note that many processes legitimately change the creation time of a file; it does not necessarily indicate malicious activity.

#Rule section
level: low
status: stable
logsource:
    product: windows
    service: sysmon
    definition: Sysmon needs to be installed and configured.
detection:
    selection_basic:
        Channel: Microsoft-Windows-Sysmon/Operational
        EventID: 2
    condition: selection_basic
falsepositives:
    - unknown
tags:
    - t1070.006
    - attack.defense_evasion
references:
    - https://docs.microsoft.com/en-us/sysinternals/downloads/sysmon
    - https://attack.mitre.org/techniques/T1070/006/
ruletype: Hayabusa

#Sample XML Event
sample-message: |
    File creation time changed:
    RuleName: technique_id=T1099,technique_name=Timestomp
    UtcTime: 2022-04-12 22:52:00.688
    ProcessGuid: {43199d79-0290-6256-3704-000000001400}
    ProcessId: 9752
    Image: C:\TMP\mim.exe
    TargetFilename: C:\Users\IEUser\AppData\Local\Temp\Quest Software\PowerGUI\51f5c69c-5d16-47e1-9864-038c8510d919\mk.ps1
    CreationUtcTime: 2016-05-16 09:13:50.950
    PreviousCreationUtcTime: 2022-04-12 22:52:00.563
    User: ZACH-LOG-TEST\IEUser
sample-evtx: |
    <Event xmlns=""http://schemas.microsoft.com/win/2004/08/events/event"">
        <System>
            <Provider Name=""Microsoft-Windows-Sysmon"" Guid=""{5770385f-c22a-43e0-bf4c-06f5698ffbd9}"" />
            <EventID>2</EventID>
            <Version>5</Version>
            <Level>4</Level>
            <Task>2</Task>
            <Opcode>0</Opcode>
            <Keywords>0x8000000000000000</Keywords>
            <TimeCreated SystemTime=""2022-04-12T22:52:00.689654600Z"" />
            <EventRecordID>8946</EventRecordID>
            <Correlation />
            <Execution ProcessID=""3408"" ThreadID=""4276"" />
            <Channel>Microsoft-Windows-Sysmon/Operational</Channel>
            <Computer>Zach-log-test</Computer>
            <Security UserID=""S-1-5-18"" />
        </System>
        <EventData>
            <Data Name=""RuleName"">technique_id=T1099,technique_name=Timestomp</Data>
            <Data Name=""UtcTime"">2022-04-12 22:52:00.688</Data>
            <Data Name=""ProcessGuid"">{43199d79-0290-6256-3704-000000001400}</Data>
            <Data Name=""ProcessId"">9752</Data>
            <Data Name=""Image"">C:\TMP\mim.exe</Data>
            <Data Name=""TargetFilename"">C:\Users\IEUser\AppData\Local\Temp\Quest Software\PowerGUI\51f5c69c-5d16-47e1-9864-038c8510d919\mk.ps1</Data>
            <Data Name=""CreationUtcTime"">2016-05-16 09:13:50.950</Data>
            <Data Name=""PreviousCreationUtcTime"">2022-04-12 22:52:00.563</Data>
            <Data Name=""User"">ZACH-LOG-TEST\IEUser</Data>
        </EventData>
    </Event>";

            // Act
            var action = () =>
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
                return YamlParser.Deserialize<YamlRule>(stream);
            };

            // Assert
            action.Should().ThrowExactly<YamlException>();
        }
    }
}