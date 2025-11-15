using System.Runtime.Versioning;
using Collector.ActiveDirectory.AuditPolicies;
using FluentAssertions;
using Vanara.PInvoke;

namespace Collector.Tests.AuditPolicies;

[SupportedOSPlatform("windows")]
public class AuditPoliciesTests
{
    [Fact]
    public void GetAuditPolicies_Should_RetrieveAuditPolicies()
    {
        var policies = AuditPolicyAdvanced.GetAuditOptionBySubcategory(new HashSet<int> { 4662 });
        policies.Count.Should().Be(1);
        policies.Should().ContainKey(new Guid("0CCE923B-69AE-11D9-BED3-505054503030"));
        policies.Single().Value.Should().Be(AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS | AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE);
    }
}