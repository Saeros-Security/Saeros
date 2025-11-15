namespace Collector.Tests.Serialization.Rules.SpecialKeywords;

internal sealed class YamlRule
{
    public const string Yaml = @"title: Special Keywords
id: 6a9d841b-71d8-43e3-a105-940e69066cd1
related:
    - id: b6188d2f-b3c4-4d2c-a17d-9706e0851af0
      type: derived
status: test
description: Test Special Keywords
author: Tests
date: 2023-05-15
modified: 2023-05-20
logsource:
    category: image_load
    product: windows
detection:
    selection:
        Channel: System
        EventID: 7045
        ServiceName:
            - value: malicious-service
        ImagePath:
            min_length: 10
    condition: selection
level: medium
ruletype: Sigma";
}