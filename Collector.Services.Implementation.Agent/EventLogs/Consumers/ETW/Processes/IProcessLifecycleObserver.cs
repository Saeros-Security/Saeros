namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;

public interface IProcessLifecycleObserver : IObservable<ProcessBase>, IObserver<ProcessBase>, IDisposable
{
    
}