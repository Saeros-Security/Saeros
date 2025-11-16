using Detection.Helpers;

namespace Collector.Detection.Helpers;

internal static class SigmaRuleMetadataHelper
{
    private static readonly IDictionary<string, string?> Descriptions = DescriptionHelper.GetDescriptions();

    public static string OverrideDescription(string ruleId, string description)
    {
        if (Descriptions.TryGetValue(ruleId, out var overriden))
        {
            return string.IsNullOrEmpty(overriden) ? description : overriden;
        }

        return description;
    }
    
    public static string OverrideTitle(string ruleId, string title)
    {
        if (ruleId.Equals("c800ccd5-5818-b0f5-1a12-f9c8bc24a433"))
        {
            return "DCShadow";
        }
        
        if (ruleId.Equals("49d15187-4203-4e11-8acd-8736f25b6608"))
        {
            return "Password Spraying";
        }
        
        if (ruleId.Equals("23179f25-6fce-4827-bae1-b219deaf563e"))
        {
            return "Password Guessing";
        }
        
        if (ruleId.Equals("7d4b25c3-0cef-1638-1d47-bb18acda0e6c"))
        {
            return "ZeroLogon";
        }
        
        if (ruleId.Equals("daad2203-665f-294c-6d2f-f9272c3214f2"))
        {
            return "DCSync";
        }
        
        if (ruleId.Equals("bcc12e55-1578-5174-2a47-98a6211a1c6c"))
        {
            return "PetitPotam";
        }

        if (ruleId.Equals("4386b4e0-f268-42a6-b91d-e3bb768976d6"))
        {
            return "Kerberoasting";
        }
        
        if (ruleId.Equals("9658ff48-3ae9-d286-f6ce-0d11b11c74dc"))
        {
            return "Enumeration of Local Administrators";
        }

        if (ruleId.Equals("02c43736-bee3-eaf1-d0c6-c445893feaf9"))
        {
            return "SAMAccountName Impersonation";
        }
        
        if (ruleId.Equals("1fb003fd-3505-dd3d-39c9-067a836b7257"))
        {
            return "NTDS Extraction";
        }
        
        if (ruleId.Equals("c09e33b8-99fc-9b17-c932-0d6d32b75f16"))
        {
            return "Golden Ticket";
        }
        
        if (ruleId.Equals("c7f94c63-6fb7-9686-e2c2-2298c9f56ca9"))
        {
            return "LSASS Memory Read Access";
        }

        return title;
    }
}