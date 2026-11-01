using System.Threading.Channels;
using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Entities;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Audit;

public sealed class AuditLogChannel : IAuditLogQueue
{
    private readonly Channel<AuditLog> _channel;

    public AuditLogChannel(IOptions<AuditLogOptions> options)
    {
        _channel = Channel.CreateBounded<AuditLog>(new BoundedChannelOptions(
            Math.Max(options.Value.QueueCapacity, 100))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelReader<AuditLog> Reader => _channel.Reader;

    public bool TryEnqueue(AuditLog log) => _channel.Writer.TryWrite(log);
}
