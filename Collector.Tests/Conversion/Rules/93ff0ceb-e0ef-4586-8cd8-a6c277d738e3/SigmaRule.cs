namespace Collector.Tests.Conversion.Rules._93ff0ceb_e0ef_4586_8cd8_a6c277d738e3;

public class SigmaRule : IConversionRule
{
    public string Yaml { get; } = @"title: Scheduled Task Created - Registry
id: 93ff0ceb-e0ef-4586-8cd8-a6c277d738e3
status: test
description: Detects the creation of a scheduled task via Registry keys.
references:
    - https://center-for-threat-informed-defense.github.io/summiting-the-pyramid/analytics/task_scheduling/
    - https://posts.specterops.io/abstracting-scheduled-tasks-3b6451f6a1c5
author: Center for Threat Informed Defense (CTID) Summiting the Pyramid Team
date: 06/04/2025
tags:
    - attack.execution
    - attack.persistence
    - attack.privilege-escalation
    - attack.s0111
    - attack.t1053.005
    - car.2013-08-001
    - detection.threat-hunting
logsource:
    product: windows
    category: registry_event
detection:
    selection:
        TargetObject|contains:
            - '\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks\'
            - '\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\'
    condition: selection
falsepositives:
    - Likely as this is a normal behaviour on Windows
level: low";

    public string Conversion { get; } = @"title: Scheduled Task Created - Registry
id: 5f72d9b8-ae5d-3c1e-dc9a-5fc3b36d9d6a
related:
- id: 93ff0ceb-e0ef-4586-8cd8-a6c277d738e3
- type: derived
status: test
description: Detects the creation of a scheduled task via Registry keys.
references:
- https://center-for-threat-informed-defense.github.io/summiting-the-pyramid/analytics/task_scheduling/
- https://posts.specterops.io/abstracting-scheduled-tasks-3b6451f6a1c5
author: Center for Threat Informed Defense (CTID) Summiting the Pyramid Team
date: 06/04/2025
tags:
- attack.execution
- attack.persistence
- attack.privilege-escalation
- attack.s0111
- attack.t1053.005
- car.2013-08-001
- detection.threat-hunting
logsource:
  product: windows
  category: registry_event
detection:
  registry_event:
    EventID: 4657
    Channel: Security
  selection:
    ObjectName|contains:
    - \Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks\
    - \Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\
  condition: registry_event and selection
falsepositives:
- Likely as this is a normal behaviour on Windows
level: low
ruletype: Sigma
---
";
}