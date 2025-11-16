using System.IO.Pipes;
using System.Security.Principal;
using Collector.Core;
using Collector.Core.SystemAudits;
using Microsoft.Extensions.Hosting;
using Shared.Helpers;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Factories;

internal sealed class NamedPipeConnectionFactory(IHostApplicationLifetime applicationLifetime, string serverName, Action<SystemAuditKey, string, AuditStatus> onAudit)
{
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _, CancellationToken cancellationToken = default)
    {
        var clientStream = new NamedPipeClientStream(
            serverName: serverName,
            pipeName: Constants.Application.NamedPipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.WriteThrough | PipeOptions.Asynchronous,
            impersonationLevel: TokenImpersonationLevel.None);

        var key = DomainHelper.DomainJoined ? new SystemAuditKey(SystemAuditType.DomainController, serverName) : new SystemAuditKey(SystemAuditType.Collector);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, applicationLifetime.ApplicationStopping);
            if (cts.IsCancellationRequested) return clientStream;
            await clientStream.ConnectAsync(cts.Token);
            onAudit(key, serverName, AuditStatus.Success);
            return clientStream;
        }
        catch
        {
            onAudit(key, serverName, AuditStatus.Failure);
            await clientStream.DisposeAsync();
            throw;
        }
    }
}