namespace Collector.Tests.Serialization.Rules._962b9ac0_e674_1e9c_b0d9_8a11e5dff4b4;

internal sealed class YamlRule
{
    public const string Yaml = @"title: Account set with reversible encryption (weakness introduction)
id: 962b9ac0-e674-1e9c-b0d9-8a11e5dff4b4
description: Detects scenarios where an attacker set an account with reversible encryption to facilitate brutforce or cracking operations.
references:
- https://github.com/mdecrevoisier/EVTX-to-MITRE-Attack/tree/master/TA0003-Persistence/T1098.xxx-Account%20manipulation
- https://www.blackhillsinfosec.com/how-i-cracked-a-128-bit-password/
tags:
- attack.persistence
- attack.t1098
author: mdecrevoisier
status: experimental
logsource:
  product: windows
  service: security
detection:
  security:
    Channel: Security
  selection:
    EventID: 4738
    UserAccountControl: '%%2091'
  condition: security and selection
falsepositives:
- None
level: high
date: 17/03/2025
ruletype: Sigma";
}