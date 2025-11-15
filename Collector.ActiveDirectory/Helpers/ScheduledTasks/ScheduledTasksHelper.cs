using Collector.ActiveDirectory.Managers;

namespace Collector.ActiveDirectory.Helpers.ScheduledTasks;

public static class ScheduledTasksHelper
{
    public enum ScheduleTaskType
    {
        ServiceCreation
    }
    
    public static readonly string ServiceCreationTaskName = $"{GroupPolicyManager.CollectorServiceDisplayName} (Service Creation)";
}