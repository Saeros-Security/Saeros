using System.Reactive.Subjects;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;

public sealed class ProcessLifecycleObserver : IProcessLifecycleObserver
{
    private readonly Subject<ProcessBase> _subject = new();
    
    public void Dispose()
    {
        _subject.Dispose();
    }

    public IDisposable Subscribe(IObserver<ProcessBase> observer)
    {
        return _subject.Subscribe(observer);
    }

    public void OnCompleted()
    {
        
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(ProcessBase value)
    {
        _subject.OnNext(value);
    }
}