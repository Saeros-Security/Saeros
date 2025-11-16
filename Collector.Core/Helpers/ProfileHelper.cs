using Shared;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;

namespace Collector.Core.Helpers;

public static class ProfileHelper
{
    public static bool ShouldBeEnabled(string ruleId, DetectionProfile profile, DetectionSeverity severity, DetectionStatus status, AuditPolicyVolume volume)
    {
        if (ruleId.Equals("5b6e58ee-c231-4a54-9eee-af2577802e08", StringComparison.OrdinalIgnoreCase)) return false; // Process Ran With High Privilege
        if (NoisyRules.Contains(ruleId)) return false;
        return profile switch
        {
            DetectionProfile.Core => volume < AuditPolicyVolume.VeryHigh && ShouldBeEnabledForCore(ruleId, severity, status),
            DetectionProfile.CorePlus => volume < AuditPolicyVolume.VeryHigh && status is DetectionStatus.Stable or DetectionStatus.Test && severity >= DetectionSeverity.Medium,
            DetectionProfile.CorePlusPlus => volume < AuditPolicyVolume.VeryHigh && status is DetectionStatus.Stable or DetectionStatus.Test or DetectionStatus.Experimental && severity >= DetectionSeverity.Medium,
            _ => false
        };
    }

    private static readonly HashSet<string> NoisyRules = new(StringComparer.OrdinalIgnoreCase)
    {
        "8c6ec2b2-8dad-4996-9aba-d659afc1b919", // NetShare File Access
        "73f64ce7-a76d-0208-ea75-dd26a09d719b", // User Logoff Event
        "4fbe94b0-577a-4f77-9b13-250e27d440fa", // NTLM Auth
        "7309e070-56b9-408b-a2f4-f1840f8f1ebf", // Logoff
        "c7b22878-e5d8-4c30-b245-e51fd354359e", // Logon (Network)
        "0266af4f-8825-495e-959c-bff801094349", // Logon (Network) (Noisy)
        "84288799-8b61-4d98-bad0-4043c40cf992", // Logoff (Noisy)
        "da6257f3-cf49-464a-96fc-c84a7ce20636", // Kerberos Service Ticket Requested
        "f19849e7-b5ba-404b-a731-9b624d7f6d19", // 'Possible Kerberoasting (RC4 Kerberos Ticket Req)
        "4386b4e0-f268-42a6-b91d-e3bb768976d6", // Kerberoasting
        "d9f336ea-bb16-4a35-8a9c-183216b8d59c", // Kerberos TGT Requested
        "059e7255-411c-1666-a2e5-2e99e294e614", // Pass the Hash Activity 2
        "a85096da-be85-48d7-8ad5-2f957cd74daa", // Logon Failure (Unknown Reason)
        "4af39497-9655-9586-817d-94f0df38913f", // Suspicious Access to Sensitive File Extensions
        "e87bd730-df45-4ae9-85de-6c75369c5d29", // Logon Failure (Wrong Password)
        "9bcf333e-fc4c-5912-eeba-8a0cefe21be4", // Password Policy Enumerated
        "2d20edf4-6141-35c5-e54f-3c578082d1d3", // Suspicious Kerberos RC4 Ticket Encryption
        "ac933178-c222-430d-8dcf-17b4f3a2fed8", // Proc Exec
        "6c34b782-a5b5-4298-80f3-1918caf1f558", // Possible LOLBIN
        "84e5ff02-5f8f-48c4-a7e9-88aa1fb888f7", // Logon (Service) (Noisy)
        "308a3356-4624-7c95-24df-cf5a02e5eb56", // First Time Seen Remote Named Pipe
        "7695295d-281f-23ce-d52e-8336ebd47532", // Protected Storage Service Access
        "53c6b925-8f6a-b834-1463-b4dade337d85", // Non Interactive PowerShell Process Spawned
        "0fd941d7-3dec-afd3-d991-d693f0a6dff8", // Change PowerShell Policies to an Insecure Level
        "d7bb3d76-50b6-1c43-cbaf-4f1600e03c9c", // Potential WMI Lateral Movement WmiPrvSE Spawned PowerShell
        "93c95eee-748a-e1db-18a5-f40035167086", // AD Privileged Users or Groups Reconnaissance
        "b37bf4b0-3cd7-a1dd-ca56-4af874660093", // Suspicious Network Command
        "cd01c787-aad1-bbed-5842-aa8e58410aad", // PetitPotam Suspicious Kerberos TGT Request
        "9d361072-2d35-e275-87b6-4915aa2beab8", // Unusually Long PowerShell CommandLine
        "5ede905b-ba07-4607-d2f1-ae3b552a752f", // Suspicious High IntegrityLevel Conhost Legacy Option
        "5161ecbd-ced9-5f55-3dba-cfb5e38cf9d1", // VMToolsd Suspicious Child Process
        "8f07f78d-22f4-9cc9-b3fb-8d8c7b056395", // Potential PowerShell Command Line Obfuscation
        "24e2ce91-6438-41b5-d23e-48e775ae72bd", // Process Start From Suspicious Folder
        "0d996232-49fa-9bae-0ee6-ad86ec993064", // Suspicious Scan Loop Network
        "42dffab1-87eb-35dd-8aad-81c3744a89ed", // Potential Encoded PowerShell Patterns In CommandLine
        "f2b2d6f5-92ed-d0f5-25fe-38019bd55906", // Import New Module Via PowerShell CommandLine
        "124493b3-4f31-c0bb-dbe9-97f0666635ba", // Visual Basic Script Execution
        "19090407-d63d-5d05-f03e-f254980d972c", // Suspicious WmiPrvSE Child Process
        "5742c4d7-6bb8-d4c7-1abf-eedde7c178df", // WSF/JSE/JS/VBA/VBE File Execution Via Cscript/Wscript
        "70d8280e-179e-392c-fb0d-96528c5d36cc", // Suspicious Execution of Hostname
        "be78b4b9-f54e-84e0-b62f-872d92b15df9", // HackTool - LaZagne Execution
        "dc6be7ef-4455-6b20-2304-ef99f8413cbf", // Suspicious Windows Service Tampering
        "fc5c47f8-9b56-8d98-de6d-cd2b31c648f1", // Suspicious Encoded PowerShell Command Line
        "87226774-feb7-cb9f-bb57-e19cc4fbfb1a", // WMI Persistence - Script Event Consumer
        "a8683f51-05f0-cb77-d513-48b731911be3", // Suspicious Tasklist Discovery Command
        "daad2203-665f-294c-6d2f-f9272c3214f2", // Mimikatz DC Sync => replaced by "Active Directory Replication from Non Machine Account"
        "26773337-b821-6c5b-2c1f-2e6cca581b84", // WmiPrvSE Spawned A Process
        "70824154-ca31-ca8f-0cc1-045e5d217a3a", // Cmd Stream Redirection
        "168763f9-a5fa-29af-e778-ed5054fe3044", // CMD Shell Output Redirect
        "9ea6664e-70c1-5f36-42c2-1fdb75330fb7", // Potentially Suspicious CMD Shell Output Redirect
        "6683ccd7-da7a-b988-1683-7f7a1bf72bf6", // Lateral Movement Indicator ConDrv
        "5e078b34-047a-505f-5c16-344bc38300ff" // System Network Connections Discovery Via Net.EXE
    };

    private static readonly HashSet<string> ActiveDirectoryCoreRules = new(StringComparer.OrdinalIgnoreCase)
    {
        "9658ff48-3ae9-d286-f6ce-0d11b11c74dc", // Enumeration of local administrators
        "02c43736-bee3-eaf1-d0c6-c445893feaf9", // SAMAccountName Impersonation
        "1fb003fd-3505-dd3d-39c9-067a836b7257", // NTDS Extraction
        "c09e33b8-99fc-9b17-c932-0d6d32b75f16", // Golden Ticket
        "c800ccd5-5818-b0f5-1a12-f9c8bc24a433", // DCShadow
        "49d15187-4203-4e11-8acd-8736f25b6608", // Password Spraying
        "23179f25-6fce-4827-bae1-b219deaf563e", // Password Guessing
        "7d4b25c3-0cef-1638-1d47-bb18acda0e6c", // ZeroLogon
        "daad2203-665f-294c-6d2f-f9272c3214f2", // DCSync
        "bcc12e55-1578-5174-2a47-98a6211a1c6c", // PetitPotam
        "c42c534d-16ae-877f-0722-6d6914090855", // DPAPI Domain Backup Key Extraction
        "4386b4e0-f268-42a6-b91d-e3bb768976d6", // Kerberoasting
        "c7f94c63-6fb7-9686-e2c2-2298c9f56ca9" // LSASS Memory Read Access
    };
    
    private static bool ShouldBeEnabledForCore(string ruleId, DetectionSeverity severity, DetectionStatus status)
    {
        if (ActiveDirectoryCoreRules.Contains(ruleId)) return true;
        return status is DetectionStatus.Stable or DetectionStatus.Test && severity >= DetectionSeverity.High;
    }
}