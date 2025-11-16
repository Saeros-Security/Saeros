using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Helpers;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;

namespace Collector.Databases.Implementation.Caching.Series;

public sealed class LogonSeries : ISeries
{
    private readonly ConcurrentLogonDictionary _successLogons = new(capacity: 10);
    private readonly ConcurrentLogonDictionary _failureLogons = new(capacity: 10);
    
    public void Insert(SuccessLogonTracer tracer)
    {
        _successLogons.AddOrUpdate(new AccountLogon(tracer.TargetAccount, tracer.TargetComputer, tracer.LogonType, tracer.SourceComputer, tracer.SourceIpAddress, tracer.Count), cumulative: true);
    }
    
    public void Insert(FailureLogonTracer tracer)
    {
        _failureLogons.AddOrUpdate(new AccountLogon(tracer.TargetAccount, tracer.TargetComputer, tracer.LogonType, tracer.SourceComputer, tracer.SourceIpAddress, tracer.Count), cumulative: true);
    }

    public IEnumerable<AccountLogon> EnumerateSuccessLogon()
    {
        return _successLogons.Enumerate();
    }
    
    public IEnumerable<AccountLogon> EnumerateFailureLogon()
    {
        return _failureLogons.Enumerate();
    }
    
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}