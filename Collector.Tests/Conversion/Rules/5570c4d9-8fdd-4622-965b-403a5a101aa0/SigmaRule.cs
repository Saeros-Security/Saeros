namespace Collector.Tests.Conversion.Rules._5570c4d9_8fdd_4622_965b_403a5a101aa0;

public class SigmaRule : IConversionRule
{
    public string Yaml { get; } = @"title: Firewall Rule Modified In The Windows Firewall Exception List
id: 5570c4d9-8fdd-4622-965b-403a5a101aa0
status: test
description: Detects when a rule has been modified in the Windows firewall exception list
references:
    - https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2008-r2-and-2008/dd364427(v=ws.10)
author: frack113
date: 2022-02-19
modified: 2024-01-22
tags:
    - attack.defense-evasion
    - attack.t1562.004
    - detection.threat-hunting
logsource:
    product: windows
    service: firewall-as
detection:
    selection:
        EventID:
            - 2005 # A rule has been modified in the Windows Defender Firewall exception list (Windows 10)
            - 2073 # A rule has been modified in the Windows Defender Firewall exception list. (Windows 11)
    filter_optional_teams:
        ApplicationPath|endswith: '\AppData\local\microsoft\teams\current\teams.exe'
    filter_optional_keybase:
        ApplicationPath|endswith: '\AppData\Local\Keybase\keybase.exe'
    filter_optional_messenger:
        ApplicationPath|endswith: '\AppData\Local\Programs\Messenger\Messenger.exe'
    filter_optional_opera:
        ApplicationPath|contains|all:
            - ':\Users\'
            - '\AppData\Local\Programs\Opera\'
            - '\opera.exe'
    filter_optional_brave:
        ApplicationPath|contains|all:
            - ':\Users\'
            - '\AppData\Local\BraveSoftware\Brave-Browser\Application\brave.exe'
    condition: selection and not 1 of filter_optional_*
level: low";

    public string Conversion { get; } = @"title: Firewall Rule Modified In The Windows Firewall Exception List
id: fa0815f1-5096-8135-abf1-2a3bd7c81d7f
related:
- id: 5570c4d9-8fdd-4622-965b-403a5a101aa0
- type: derived
status: test
description: Detects when a rule has been modified in the Windows firewall exception list
references:
- https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2008-r2-and-2008/dd364427(v=ws.10)
author: frack113
date: 2022-02-19
modified: 2024-01-22
tags:
- attack.defense-evasion
- attack.t1562.004
- detection.threat-hunting
logsource:
  product: windows
  service: firewall-as
detection:
  firewall_as:
    Channel: Microsoft-Windows-Windows Firewall With Advanced Security/Firewall
  selection:
    EventID:
    - 2005
    - 2073
  filter_optional_teams:
    ApplicationPath|endswith: \AppData\local\microsoft\teams\current\teams.exe
  filter_optional_keybase:
    ApplicationPath|endswith: \AppData\Local\Keybase\keybase.exe
  filter_optional_messenger:
    ApplicationPath|endswith: \AppData\Local\Programs\Messenger\Messenger.exe
  filter_optional_opera:
    ApplicationPath|contains|all:
    - :\Users\
    - \AppData\Local\Programs\Opera\
    - \opera.exe
  filter_optional_brave:
    ApplicationPath|contains|all:
    - :\Users\
    - \AppData\Local\BraveSoftware\Brave-Browser\Application\brave.exe
  condition: firewall_as and (selection and not 1 of filter_optional_*)
level: low
ruletype: Sigma
---
";
}