using System.Threading.Channels;
using App.Domain.Entities;

namespace App.Application.Abstractions;

/// <summary>
/// Decouples request handling from audit persistence. The middleware only
/// enqueues; a background service batches writes to the database.
/// </summary>
public interface IAuditLogQueue
{
    bool TryEnqueue(AuditLog log);

    ChannelReader<AuditLog> Reader { get; }
}
