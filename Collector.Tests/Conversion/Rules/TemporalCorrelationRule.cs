namespace Collector.Tests.Conversion.Rules;

public class TemporalCorrelationRule : IConversionRule
{
    public string Yaml { get; } = @"title: Successful password spray
id: 23179f25-6fce-4827-bae1-b219deaf563a
author: yamatosecurity
correlation:
  type: temporal
  rules:
    - many_failed_logins
    - successful_login
  group-by:
    - Computer
  timespan: 1m
  generate: true
level: critical
ruletype: Hayabusa
status: stable
---
id: 5dbf63ae-07a2-4b47-b75d-e8430e686a29
title: Many Failed Logons!
author: author1
name: many_failed_logins
level: high
status: stable
logsource:
  product: windows
  service: security
correlation:
  #generate: true
  type: value_count
  rules:
    - failed_logins
  group-by:
    - IpAddress
    - Computer
  timespan: 5m
  condition:
    gte: 5
    field: TargetUserName
---
id: 23179f25-6fce-4827-bae1-b219deaf563c
title: Failed Logon
author: author1
name: failed_logins
level: medium
status: stable
logsource:
  product: windows
  service: security
detection:
  selection:
    Channel: Security
    EventID: 4625
  condition: selection
---
id: 23179f25-6fce-4827-bae1-b219deaf563x
title: Successful Login
author: author2
name: successful_login
level: informational
status: stable
logsource:
  product: windows
  service: security
detection:
  selection:
    Channel: Security
    EventID: 4624
  condition: selection
";

    public string Conversion { get; } = @"title: Successful password spray
id: 138d1cf4-3afb-6e13-df6c-e1ca97f95db5
related:
- id: 23179f25-6fce-4827-bae1-b219deaf563a
- type: derived
author: yamatosecurity
correlation:
  type: temporal
  rules:
  - many_failed_logins
  - successful_login
  group-by:
  - Computer
  timespan: 1m
  generate: true
level: critical
ruletype: Hayabusa
status: stable
date: 06/04/2025
---
title: Many Failed Logons!
id: c173a783-4a33-d698-4456-81bbf0422130
related:
- id: 5dbf63ae-07a2-4b47-b75d-e8430e686a29
- type: derived
author: author1
name: many_failed_logins
level: high
status: stable
logsource:
  product: windows
  service: security
correlation:
  type: value_count
  rules:
  - failed_logins
  group-by:
  - IpAddress
  - Computer
  timespan: 5m
  condition:
    gte: 5
    field: TargetUserName
date: 06/04/2025
ruletype: Sigma
---
title: Failed Logon
id: a5d73d7e-dec0-3a02-16b2-969ac5b87a42
related:
- id: 23179f25-6fce-4827-bae1-b219deaf563c
- type: derived
author: author1
name: failed_logins
level: medium
status: stable
logsource:
  product: windows
  service: security
detection:
  security:
    Channel: Security
  selection:
    Channel: Security
    EventID: 4625
  condition: security and selection
date: 06/04/2025
ruletype: Sigma
---
title: Successful Login
id: fcba83a4-9c87-e5a4-816a-dca30fcd3dc6
related:
- id: 23179f25-6fce-4827-bae1-b219deaf563x
- type: derived
author: author2
name: successful_login
level: informational
status: stable
logsource:
  product: windows
  service: security
detection:
  security:
    Channel: Security
  selection:
    Channel: Security
    EventID: 4624
  condition: security and selection
date: 06/04/2025
ruletype: Sigma
---
";
}