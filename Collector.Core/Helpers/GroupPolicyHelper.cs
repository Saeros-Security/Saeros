using Microsoft.Extensions.Logging;
using Shared.Helpers;

namespace Collector.Core.Helpers;

public static class GroupPolicyHelper
{
    public static async Task UpdateGroupPoliciesAsync(ILogger logger, CancellationToken cancellationToken)
    {
        var success = await ProcessHelper.RunAsync(onError: error => logger.LogError("Could not update Group Policies. Error Code: {ExitCode}", error), processName: "gpupdate.exe", arguments: "/target:computer /force", cancellationToken: cancellationToken);
        if (success)
        {
            logger.LogInformation("Group Policies have been updated");
        }
    }
}