using Collector.ActiveDirectory.AuditPolicies.Models;
using AuditPolicyVolume = Shared.AuditPolicyVolume;

namespace Collector.ActiveDirectory.AuditPolicies;

public static class AuditPolicyMapping
{
    public static readonly IDictionary<Guid, AuditPolicyVolume> VolumeBySubcategoryGuid = new Dictionary<Guid, AuditPolicyVolume>
    {
        {
            new Guid("{0CCE923F-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9242-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9240-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.VeryHigh
        },
        {
            new Guid("{0CCE9241-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9239-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9236-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9238-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE923A-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9237-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9235-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE922D-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0cce9248-69ae-11d9-bed3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE922B-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE922C-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE922E-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE924A-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE923E-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.VeryHigh
        },
        {
            new Guid("{0CCE923B-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE923C-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE923D-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9217-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0cce9247-69ae-11d9-bed3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE921A-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0cce9249-69ae-11d9-bed3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9218-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9219-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9216-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9215-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9243-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE921C-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE921B-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9222-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9221-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9244-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9224-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE921D-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9226-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9225-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9223-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE921F-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9227-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE921E-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9245-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9220-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9246-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE922F-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9230-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9231-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE9233-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9232-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9234-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9229-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.VeryHigh
        },
        {
            new Guid("{0CCE9228-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.High
        },
        {
            new Guid("{0CCE922A-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9213-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Medium
        },
        {
            new Guid("{0CCE9214-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9210-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9211-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        },
        {
            new Guid("{0CCE9212-69AE-11D9-BED3-505054503030}"), AuditPolicyVolume.Low
        }
    };

    public static readonly IDictionary<Guid, string> SubcategoryNameByGuid = new Dictionary<Guid, string>
    {
        {
            new Guid("{0CCE923F-69AE-11D9-BED3-505054503030}"), "Audit Credential Validation"
        },
        {
            new Guid("{0CCE9242-69AE-11D9-BED3-505054503030}"), "Audit Kerberos Authentication Service"
        },
        {
            new Guid("{0CCE9240-69AE-11D9-BED3-505054503030}"), "Audit Kerberos Service Ticket Operations"
        },
        {
            new Guid("{0CCE9241-69AE-11D9-BED3-505054503030}"), "Audit Other Account Logon Events"
        },
        {
            new Guid("{0CCE9239-69AE-11D9-BED3-505054503030}"), "Audit Application Group Management"
        },
        {
            new Guid("{0CCE9236-69AE-11D9-BED3-505054503030}"), "Audit Computer Account Management"
        },
        {
            new Guid("{0CCE9238-69AE-11D9-BED3-505054503030}"), "Audit Distribution Group Management"
        },
        {
            new Guid("{0CCE923A-69AE-11D9-BED3-505054503030}"), "Audit Other Account Management Events"
        },
        {
            new Guid("{0CCE9237-69AE-11D9-BED3-505054503030}"), "Audit Security Group Management"
        },
        {
            new Guid("{0CCE9235-69AE-11D9-BED3-505054503030}"), "Audit User Account Management"
        },
        {
            new Guid("{0CCE922D-69AE-11D9-BED3-505054503030}"), "Audit DPAPI Activity"
        },
        {
            new Guid("{0cce9248-69ae-11d9-bed3-505054503030}"), "Audit PNP Activity"
        },
        {
            new Guid("{0CCE922B-69AE-11D9-BED3-505054503030}"), "Audit Process Creation"
        },
        {
            new Guid("{0CCE922C-69AE-11D9-BED3-505054503030}"), "Audit Process Termination"
        },
        {
            new Guid("{0CCE922E-69AE-11D9-BED3-505054503030}"), "Audit RPC Events"
        },
        {
            new Guid("{0CCE924A-69AE-11D9-BED3-505054503030}"), "Audit Token Right Adjusted"
        },
        {
            new Guid("{0CCE923E-69AE-11D9-BED3-505054503030}"), "Audit Detailed Directory Service Replication"
        },
        {
            new Guid("{0CCE923B-69AE-11D9-BED3-505054503030}"), "Audit Directory Service Access"
        },
        {
            new Guid("{0CCE923C-69AE-11D9-BED3-505054503030}"), "Audit Directory Service Changes"
        },
        {
            new Guid("{0CCE923D-69AE-11D9-BED3-505054503030}"), "Audit Directory Service Replication"
        },
        {
            new Guid("{0CCE9217-69AE-11D9-BED3-505054503030}"), "Audit Account Lockout"
        },
        {
            new Guid("{0cce9247-69ae-11d9-bed3-505054503030}"), "Audit User/Device Claims"
        },
        {
            new Guid("{0CCE921A-69AE-11D9-BED3-505054503030}"), "Audit IPsec Extended Mode"
        },
        {
            new Guid("{0cce9249-69ae-11d9-bed3-505054503030}"), "Audit Group Membership"
        },
        {
            new Guid("{0CCE9218-69AE-11D9-BED3-505054503030}"), "Audit IPsec Main Mode"
        },
        {
            new Guid("{0CCE9219-69AE-11D9-BED3-505054503030}"), "Audit IPsec Quick Mode"
        },
        {
            new Guid("{0CCE9216-69AE-11D9-BED3-505054503030}"), "Audit Logoff"
        },
        {
            new Guid("{0CCE9215-69AE-11D9-BED3-505054503030}"), "Audit Logon"
        },
        {
            new Guid("{0CCE9243-69AE-11D9-BED3-505054503030}"), "Audit Network Policy Server"
        },
        {
            new Guid("{0CCE921C-69AE-11D9-BED3-505054503030}"), "Audit Other Logon/Logoff Events"
        },
        {
            new Guid("{0CCE921B-69AE-11D9-BED3-505054503030}"), "Audit Special Logon"
        },
        {
            new Guid("{0CCE9222-69AE-11D9-BED3-505054503030}"), "Audit Application Generated"
        },
        {
            new Guid("{0CCE9221-69AE-11D9-BED3-505054503030}"), "Audit Certification Services"
        },
        {
            new Guid("{0CCE9244-69AE-11D9-BED3-505054503030}"), "Audit Detailed File Share"
        },
        {
            new Guid("{0CCE9224-69AE-11D9-BED3-505054503030}"), "Audit File Share"
        },
        {
            new Guid("{0CCE921D-69AE-11D9-BED3-505054503030}"), "Audit File System"
        },
        {
            new Guid("{0CCE9226-69AE-11D9-BED3-505054503030}"), "Audit Filtering Platform Connection"
        },
        {
            new Guid("{0CCE9225-69AE-11D9-BED3-505054503030}"), "Audit Filtering Platform Packet Drop"
        },
        {
            new Guid("{0CCE9223-69AE-11D9-BED3-505054503030}"), "Audit Handle Manipulation"
        },
        {
            new Guid("{0CCE921F-69AE-11D9-BED3-505054503030}"), "Audit Kernel Object"
        },
        {
            new Guid("{0CCE9227-69AE-11D9-BED3-505054503030}"), "Audit Other Object Access Events"
        },
        {
            new Guid("{0CCE921E-69AE-11D9-BED3-505054503030}"), "Audit Registry"
        },
        {
            new Guid("{0CCE9245-69AE-11D9-BED3-505054503030}"), "Audit Removable Storage"
        },
        {
            new Guid("{0CCE9220-69AE-11D9-BED3-505054503030}"), "Audit SAM"
        },
        {
            new Guid("{0CCE9246-69AE-11D9-BED3-505054503030}"), "Audit Central Access Policy Staging"
        },
        {
            new Guid("{0CCE922F-69AE-11D9-BED3-505054503030}"), "Audit Audit Policy Change"
        },
        {
            new Guid("{0CCE9230-69AE-11D9-BED3-505054503030}"), "Audit Authentication Policy Change"
        },
        {
            new Guid("{0CCE9233-69AE-11D9-BED3-505054503030}"), "Audit Filtering Platform Policy Change"
        },
        {
            new Guid("{0CCE9232-69AE-11D9-BED3-505054503030}"), "Audit MPSSVC Rule-Level Policy Change"
        },
        {
            new Guid("{0CCE9234-69AE-11D9-BED3-505054503030}"), "Audit Other Policy Change Events"
        },
        {
            new Guid("{0CCE9229-69AE-11D9-BED3-505054503030}"), "Audit Non-Sensitive Privilege Use"
        },
        {
            new Guid("{0CCE9228-69AE-11D9-BED3-505054503030}"), "Audit Sensitive Privilege Use"
        },
        {
            new Guid("{0CCE922A-69AE-11D9-BED3-505054503030}"), "Audit Other Privilege Use Events"
        },
        {
            new Guid("{0CCE9213-69AE-11D9-BED3-505054503030}"), "Audit IPsec Driver"
        },
        {
            new Guid("{0CCE9214-69AE-11D9-BED3-505054503030}"), "Audit Other System Events"
        },
        {
            new Guid("{0CCE9210-69AE-11D9-BED3-505054503030}"), "Audit Security State Change"
        },
        {
            new Guid("{0CCE9211-69AE-11D9-BED3-505054503030}"), "Audit Security System Extension"
        },
        {
            new Guid("{0CCE9212-69AE-11D9-BED3-505054503030}"), "Audit System Integrity"
        },
        {
            new Guid("{0CCE9231-69AE-11D9-BED3-505054503030}"), "Audit Authorization Policy Change"
        },
    };

    public static readonly IDictionary<Guid, ISet<AuditPolicyEventId>> EventIdBySubcategoryGuid = new Dictionary<Guid, ISet<AuditPolicyEventId>>
    {
        {
            // Audit Credential Validation
            new Guid("{0CCE923F-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4774, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4775, AuditPolicyStatus.Failure),
                new(4776, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4777, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Kerberos Authentication Service
            new Guid("{0CCE9242-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4768, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4771, AuditPolicyStatus.Failure),
                new(4772, AuditPolicyStatus.Failure),
                new(4820, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Kerberos Service Ticket Operations
            new Guid("{0CCE9240-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4769, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4770),
                new(4773, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Other Account Logon Events
            new Guid("{0CCE9241-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {

            }
        },
        {
            // Audit Application Group Management
            new Guid("{0CCE9239-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4783),
                new(4784),
                new(4785),
                new(4786),
                new(4787),
                new(4788),
                new(4789),
                new(4790),
                new(4791),
                new(4792)
            }
        },
        {
            // Audit Computer Account Management
            new Guid("{0CCE9236-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4741),
                new(4742),
                new(4743)
            }
        },
        {
            // Audit Distribution Group Management
            new Guid("{0CCE9238-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4749),
                new(4750),
                new(4751),
                new(4752),
                new(4753),
                new(4759),
                new(4760),
                new(4761),
                new(4762),
                new(4763),
                new(4744),
                new(4745),
                new(4746),
                new(4747),
                new(4748)
            }
        },
        {
            // Audit Other Account Management Events
            new Guid("{0CCE923A-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4782),
                new(4793)
            }
        },
        {
            // Audit Security Group Management
            new Guid("{0CCE9237-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4731),
                new(4732),
                new(4733),
                new(4734),
                new(4735),
                new(4764),
                new(4799),
                new(4727),
                new(4737),
                new(4728),
                new(4729),
                new(4730),
                new(4754),
                new(4755),
                new(4756),
                new(4757),
                new(4758)
            }
        },
        {
            // Audit User Account Management
            new Guid("{0CCE9235-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4720),
                new(4722),
                new(4723, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4724, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4725),
                new(4726),
                new(4738),
                new(4740),
                new(4765),
                new(4766, AuditPolicyStatus.Failure),
                new(4767),
                new(4780),
                new(4781),
                new(4794, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4797),
                new(4798),
                new(5376),
                new(5377)
            }
        },
        {
            // Audit DPAPI Activity
            new Guid("{0CCE922D-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4692, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4693, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4694, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4695, AuditPolicyStatus.Success | AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit PNP Activity
            new Guid("{0cce9248-69ae-11d9-bed3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(6416),
                new(6419),
                new(6420),
                new(6421),
                new(6422),
                new(6423),
                new(6424)
            }
        },
        {
            // Audit Process Creation
            new Guid("{0CCE922B-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4688),
                new(4696)
            }
        },
        {
            // Audit Process Termination
            new Guid("{0CCE922C-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4689)
            }
        },
        {
            // Audit RPC Events
            new Guid("{0CCE922E-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(5712)
            }
        },
        {
            // Audit Token Right Adjusted
            new Guid("{0CCE924A-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4703)
            }
        },
        {
            // Audit Detailed Directory Service Replication
            new Guid("{0CCE923E-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4928, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4929, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4930, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4931, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4934),
                new(4935, AuditPolicyStatus.Failure),
                new(4936),
                new(4937)
            }
        },
        {
            // Audit Directory Service Access
            new Guid("{0CCE923B-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4661, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4662, AuditPolicyStatus.Success | AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Directory Service Changes
            new Guid("{0CCE923C-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(5136),
                new(5137),
                new(5138),
                new(5139),
                new(5141),
                new(5169),
                new(5170)
            }
        },
        {
            // Audit Directory Service Replication
            new Guid("{0CCE923D-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4932),
                new(4933, AuditPolicyStatus.Success | AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Account Lockout
            new Guid("{0CCE9217-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4625, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit User/Device Claims
            new Guid("{0cce9247-69ae-11d9-bed3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4626)
            }
        },
        {
            // Audit IPsec Extended Mode
            new Guid("{0CCE921A-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4978),
                new(4979),
                new(4980),
                new(4981),
                new(4982),
                new(4983),
                new(4984)
            }
        },
        {
            // Audit Group Membership
            new Guid("{0cce9249-69ae-11d9-bed3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4627)
            }
        },
        {
            // Audit IPsec Main Mode
            new Guid("{0CCE9218-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4646),
                new(4650),
                new(4651),
                new(4652, AuditPolicyStatus.Failure),
                new(4653, AuditPolicyStatus.Failure),
                new(4655),
                new(4976),
                new(5049),
                new(5453)
            }
        },
        {
            // Audit IPsec Quick Mode
            new Guid("{0CCE9219-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4654, AuditPolicyStatus.Failure),
                new(4977),
                new(5451),
                new(5452)
            }
        },
        {
            // Audit Logoff
            new Guid("{0CCE9216-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4634),
                new(4647)
            }
        },
        {
            // Audit Logon
            new Guid("{0CCE9215-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4624),
                new(4625, AuditPolicyStatus.Failure),
                new(4648),
                new(4675)
            }
        },
        {
            // Audit Network Policy Server
            new Guid("{0CCE9243-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(6272),
                new(6273),
                new(6274),
                new(6275),
                new(6276),
                new(6277),
                new(6278),
                new(6279),
                new(6280)
            }
        },
        {
            // Audit Other Logon/Logoff Events
            new Guid("{0CCE921C-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4649),
                new(4778),
                new(4779),
                new(4800),
                new(4801),
                new(4802),
                new(4803),
                new(5378, AuditPolicyStatus.Failure),
                new(5632),
                new(5633)
            }
        },
        {
            // Audit Special Logon
            new Guid("{0CCE921B-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4964),
                new(4672)
            }
        },
        {
            // Audit Application Generated
            new Guid("{0CCE9222-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4665),
                new(4666),
                new(4667),
                new(4668)
            }
        },
        {
            // Audit Certification Services
            new Guid("{0CCE9221-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4868),
                new(4869),
                new(4870),
                new(4871),
                new(4872),
                new(4873),
                new(4874),
                new(4875),
                new(4876),
                new(4877),
                new(4878),
                new(4879),
                new(4880),
                new(4881),
                new(4882),
                new(4883),
                new(4884),
                new(4885),
                new(4886),
                new(4887),
                new(4888, AuditPolicyStatus.Failure),
                new(4889),
                new(4890),
                new(4891),
                new(4892),
                new(4893),
                new(4894),
                new(4895),
                new(4896),
                new(4897),
                new(4898),
                new(4899),
                new(4900),
                new(5120)
            }
        },
        {
            // Audit Detailed File Share
            new Guid("{0CCE9244-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(5145, AuditPolicyStatus.Success | AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit File Share
            new Guid("{0CCE9224-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(5140, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5142),
                new(5143),
                new(5144),
                new(5168, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit File System
            new Guid("{0CCE921D-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4656, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4658),
                new(4659),
                new(4660),
                new(4663),
                new(4664),
                new(4985),
                new(5051),
                new(4670)
            }
        },
        {
            // Audit Filtering Platform Connection
            new Guid("{0CCE9226-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(5031, AuditPolicyStatus.Failure),
                new(5150),
                new(5151, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5154),
                new(5155, AuditPolicyStatus.Failure),
                new(5156),
                new(5157, AuditPolicyStatus.Failure),
                new(5158),
                new(5159, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Filtering Platform Packet Drop
            new Guid("{0CCE9225-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(5152, AuditPolicyStatus.Failure),
                new(5153)
            }
        },
        {
            // Audit Handle Manipulation
            new Guid("{0CCE9223-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4658),
                new(4690),
                new(4658)
            }
        },
        {
            // Audit Kernel Object
            new Guid("{0CCE921F-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4656, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4658),
                new(4660),
                new(4663)
            }
        },
        {
            // Audit Other Object Access Events
            new Guid("{0CCE9227-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4659),
                new(4671),
                new(4691),
                new(5148, AuditPolicyStatus.Failure),
                new(5149, AuditPolicyStatus.Failure),
                new(4698),
                new(4699),
                new(4700),
                new(4701),
                new(4702),
                new(5888),
                new(5889),
                new(5890)
            }
        },
        {
            // Audit Registry
            new Guid("{0CCE921E-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4663),
                new(4656, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4658),
                new(4659),
                new(4660),
                new(4657),
                new(5039),
                new(4670)
            }
        },
        {
            // Audit Removable Storage
            new Guid("{0CCE9245-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4656, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4658),
                new(4663)
            }
        },
        {
            // Audit SAM
            new Guid("{0CCE9220-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4661, AuditPolicyStatus.Success | AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Central Access Policy Staging
            new Guid("{0CCE9246-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4818)
            }
        },
        {
            // Audit Audit Policy Change
            new Guid("{0CCE922F-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4715),
                new(4719),
                new(4817),
                new(4902),
                new(4906),
                new(4907),
                new(4908),
                new(4912),
                new(4904),
                new(4905)
            }
        },
        {
            // Audit Authentication Policy Change
            new Guid("{0CCE9230-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4670),
                new(4706),
                new(4707),
                new(4716),
                new(4713),
                new(4717),
                new(4718),
                new(4739),
                new(4864),
                new(4865),
                new(4866),
                new(4867)
            }
        },
        {
            // Audit Filtering Platform Policy Change
            new Guid("{0CCE9233-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4709),
                new(4710),
                new(4711),
                new(4712, AuditPolicyStatus.Failure),
                new(5040),
                new(5041),
                new(5042),
                new(5043),
                new(5044),
                new(5045),
                new(5046),
                new(5047),
                new(5048),
                new(5440),
                new(5441),
                new(5442),
                new(5443),
                new(5444),
                new(5446),
                new(5448),
                new(5449),
                new(5450),
                new(5456),
                new(5457, AuditPolicyStatus.Failure),
                new(5458),
                new(5459, AuditPolicyStatus.Failure),
                new(5460),
                new(5461, AuditPolicyStatus.Failure),
                new(5462, AuditPolicyStatus.Failure),
                new(5463),
                new(5464),
                new(5465),
                new(5466, AuditPolicyStatus.Failure),
                new(5467, AuditPolicyStatus.Failure),
                new(5468),
                new(5471),
                new(5472, AuditPolicyStatus.Failure),
                new(5473),
                new(5474, AuditPolicyStatus.Failure),
                new(5477, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit MPSSVC Rule-Level Policy Change
            new Guid("{0CCE9232-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4944),
                new(4945),
                new(4946),
                new(4947),
                new(4948),
                new(4949),
                new(4950),
                new(4951, AuditPolicyStatus.Failure),
                new(4952, AuditPolicyStatus.Failure),
                new(4953, AuditPolicyStatus.Failure),
                new(4954),
                new(4956),
                new(4957, AuditPolicyStatus.Failure),
                new(4958, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Other Policy Change Events
            new Guid("{0CCE9234-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4714),
                new(4819),
                new(4826),
                new(4909),
                new(4910),
                new(5063, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5064, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5065, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5066, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5067, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5068, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5069, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5070, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5447),
                new(6144),
                new(6145, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Non-Sensitive Privilege Use
            new Guid("{0CCE9229-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4673, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4674, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4985)
            }
        },
        {
            // Audit Sensitive Privilege Use
            new Guid("{0CCE9228-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4673, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4674, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(4985)
            }
        },
        {
            // Audit Other Privilege Use Events
            new Guid("{0CCE922A-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4985)
            }
        },
        {
            // Audit IPsec Driver
            new Guid("{0CCE9213-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4960),
                new(4961),
                new(4962),
                new(4963),
                new(4965),
                new(5478),
                new(5479),
                new(5480, AuditPolicyStatus.Failure),
                new(5483, AuditPolicyStatus.Failure),
                new(5484, AuditPolicyStatus.Failure),
                new(5485, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Other System Events
            new Guid("{0CCE9214-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4821, AuditPolicyStatus.Failure),
                new(4822, AuditPolicyStatus.Failure),
                new(4823, AuditPolicyStatus.Failure),
                new(4824, AuditPolicyStatus.Failure),
                new(4825),
                new(4830),
                new(5024),
                new(5025),
                new(5027, AuditPolicyStatus.Failure),
                new(5028, AuditPolicyStatus.Failure),
                new(5029, AuditPolicyStatus.Failure),
                new(5030, AuditPolicyStatus.Failure),
                new(5032, AuditPolicyStatus.Failure),
                new(5033),
                new(5034),
                new(5035, AuditPolicyStatus.Failure),
                new(5037, AuditPolicyStatus.Failure),
                new(5058, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5059, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(5071, AuditPolicyStatus.Failure),
                new(5146, AuditPolicyStatus.Failure),
                new(5147, AuditPolicyStatus.Failure),
                new(5379),
                new(5380),
                new(5381),
                new(5382),
                new(6400, AuditPolicyStatus.Failure),
                new(6401, AuditPolicyStatus.Failure),
                new(6402, AuditPolicyStatus.Failure),
                new(6403, AuditPolicyStatus.Failure),
                new(6404, AuditPolicyStatus.Failure),
                new(6405),
                new(6406),
                new(6407),
                new(6408, AuditPolicyStatus.Failure),
                new(6409, AuditPolicyStatus.Failure),
                new(6417),
                new(6418),
                new(8191)
            }
        },
        {
            // Audit Security State Change
            new Guid("{0CCE9210-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4608),
                new(4609),
                new(4616),
                new(4621)
            }
        },
        {
            // Audit Security System Extension
            new Guid("{0CCE9211-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4610),
                new(4611),
                new(4614),
                new(4622),
                new(4697)
            }
        },
        {
            // Audit System Integrity
            new Guid("{0CCE9212-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4612),
                new(4615),
                new(4618),
                new(4816),
                new(5038, AuditPolicyStatus.Failure),
                new(5056),
                new(5062),
                new(5057, AuditPolicyStatus.Failure),
                new(5060, AuditPolicyStatus.Failure),
                new(5061, AuditPolicyStatus.Success | AuditPolicyStatus.Failure),
                new(6281, AuditPolicyStatus.Failure),
                new(6410, AuditPolicyStatus.Failure)
            }
        },
        {
            // Audit Authorization Policy Change
            new Guid("{0CCE9231-69AE-11D9-BED3-505054503030}"), new HashSet<AuditPolicyEventId>
            {
                new(4703),
                new(4704),
                new(4705),
                new(4670),
                new(4911),
                new(4913)
            }
        }
    };
}