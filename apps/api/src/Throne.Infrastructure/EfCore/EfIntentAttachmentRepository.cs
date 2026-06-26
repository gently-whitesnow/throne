using Microsoft.EntityFrameworkCore;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// SQLite/EF Core port for intent attachments. The payload lives in the
/// <c>content_bytes</c> BLOB column. Per-intent limits and content-type validation live
/// in the use case; the repository assumes a pre-validated payload and only persists
/// what it is given. Upload/delete writes still demand the ambient context via
/// <c>IUnitOfWork.ExecuteOutsideTransactionAsync</c>. Whole-intent cleanup and
/// background compression also tolerate a transient context because their application
/// callers are intentionally sessionless.
/// </summary>
internal sealed class EfIntentAttachmentRepository(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IIntentAttachmentRepository
{
    public Task<int> CountByIntentAsync(IntentId intentId, CancellationToken ct) =>
        ReadAsync((ctx, c) =>
        {
            var wire = intentId.Value;
            return ctx.Set<IntentAttachmentRow>().AsNoTracking()
                .CountAsync(r => r.IntentId == wire, c);
        }, ct);

    public Task<IReadOnlyList<IntentAttachment>> ListByIntentAsync(IntentId intentId, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentAttachment>>(async (ctx, c) =>
        {
            var wire = intentId.Value;
            var rows = await ctx.Set<IntentAttachmentRow>().AsNoTracking()
                .Where(r => r.IntentId == wire)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(c);
            return rows.Select(IntentAttachmentRowMapper.ToDomain).ToArray();
        }, ct);

    public async Task<UploadIntentAttachmentOutcome> AddAsync(
        IntentId intentId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        var ctx = RequireWriteContext(nameof(AddAsync));

        var safeName = string.IsNullOrWhiteSpace(fileName) ? "upload" : Path.GetFileName(fileName);
        if (safeName.Length == 0)
        {
            safeName = "upload";
        }
        var declaredType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();

        var bytes = await ReadAllAsync(content, ct);
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N");

        var row = new IntentAttachmentRow
        {
            Id = id,
            IntentId = intentId.Value,
            FileName = safeName,
            ContentType = declaredType,
            SizeBytes = bytes.LongLength,
            CreatedAt = now,
            // Mark images as pending so the compression worker picks them up; non-images
            // stay NULL and are skipped by ListPendingCompressionAsync.
            CompressionState = declaredType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? IntentAttachmentRowMapper.CompressionStatePending
                : null,
            ContentBytes = bytes,
        };

        ctx.Set<IntentAttachmentRow>().Add(row);
        await ctx.SaveChangesAsync(ct);

        return new UploadIntentAttachmentOutcome(IntentAttachmentRowMapper.ToDomain(row));
    }

    public Task<IntentAttachmentContent?> OpenContentAsync(IntentId intentId, string attachmentId, CancellationToken ct) =>
        ReadAsync<IntentAttachmentContent?>(async (ctx, c) =>
        {
            var intentWire = intentId.Value;
            var row = await ctx.Set<IntentAttachmentRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == attachmentId && r.IntentId == intentWire, c);
            if (row is null)
            {
                return null;
            }
            // MemoryStream over the byte[] — safe because SQLite already materialized the
            // BLOB into memory by the time the row arrived from the provider.
            var stream = new MemoryStream(row.ContentBytes, writable: false);
            return new IntentAttachmentContent(IntentAttachmentRowMapper.ToDomain(row), stream);
        }, ct);

    public async Task<DeleteIntentAttachmentOutcome> DeleteAsync(
        IntentId intentId, string attachmentId, CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(DeleteAsync));

        var intentWire = intentId.Value;
        var affected = await ctx.Set<IntentAttachmentRow>()
            .Where(r => r.Id == attachmentId && r.IntentId == intentWire)
            .ExecuteDeleteAsync(ct);
        return affected == 0
            ? new DeleteIntentAttachmentOutcome.NotFound()
            : new DeleteIntentAttachmentOutcome.Deleted(intentId.Value, attachmentId);
    }

    public async Task DeleteAllForIntentAsync(IntentId intentId, CancellationToken ct)
    {
        await WithWriteContextAsync(
            async (ctx, c) =>
            {
                var wire = intentId.Value;
                await ctx.Set<IntentAttachmentRow>()
                    .Where(r => r.IntentId == wire)
                    .ExecuteDeleteAsync(c);
            },
            ct);
    }

    public Task<IReadOnlyList<PendingCompressionItem>> ListPendingCompressionAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize < 1)
        {
            return Task.FromResult<IReadOnlyList<PendingCompressionItem>>([]);
        }

        return ReadAsync<IReadOnlyList<PendingCompressionItem>>(async (ctx, c) =>
        {
            // Backfill-friendly: pick up new pending rows and any legacy image attachment
            // that was uploaded before the flag existed. Single BLOB model means the
            // storage key handed back is just the attachment id.
            var ready = IntentAttachmentRowMapper.CompressionStateReady;
            var rows = await ctx.Set<IntentAttachmentRow>().AsNoTracking()
                .Where(r => r.ContentType.StartsWith("image/")
                    && (r.CompressionState == null || r.CompressionState != ready))
                .OrderBy(r => r.CreatedAt)
                .Take(batchSize)
                .Select(r => new { r.Id, r.ContentType })
                .ToListAsync(c);
            return rows
                .Select(r => new PendingCompressionItem(r.Id, r.Id, r.ContentType))
                .ToArray();
        }, ct);
    }

    public Task<Stream?> OpenRawContentAsync(string contentId, CancellationToken ct) =>
        ReadAsync<Stream?>(async (ctx, c) =>
        {
            // In the EF backend the «storage key» the worker holds is just the attachment
            // id (see ListPendingCompressionAsync); the original BLOB is whatever lives in
            // content_bytes until ApplyCompressionAsync overwrites it.
            var bytes = await ctx.Set<IntentAttachmentRow>().AsNoTracking()
                .Where(r => r.Id == contentId)
                .Select(r => r.ContentBytes)
                .FirstOrDefaultAsync(c);
            return bytes is null ? null : new MemoryStream(bytes, writable: false);
        }, ct);

    public async Task ApplyCompressionAsync(
        string attachmentId,
        string previousContentId,
        DownscaledImage compressed,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(compressed);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousContentId);

        await WithWriteContextAsync(
            async (ctx, c) =>
            {
                var ready = IntentAttachmentRowMapper.CompressionStateReady;
                var newBytes = compressed.Data;
                var newSize = compressed.Data.LongLength;
                var newType = compressed.MimeType;
                var width = compressed.Width;
                var height = compressed.Height;

                // Single-statement CAS: only flip rows that have not been claimed by another
                // worker. Lost races leave the existing (already-compressed) bytes untouched.
                await ctx.Set<IntentAttachmentRow>()
                    .Where(r => r.Id == attachmentId
                        && (r.CompressionState == null || r.CompressionState != ready))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.ContentBytes, newBytes)
                        .SetProperty(r => r.ContentType, newType)
                        .SetProperty(r => r.SizeBytes, newSize)
                        .SetProperty(r => r.DerivedWidth, width)
                        .SetProperty(r => r.DerivedHeight, height)
                        .SetProperty(r => r.CompressionState, ready), c);
            },
            ct);
    }

    private static async Task<byte[]> ReadAllAsync(Stream content, CancellationToken ct)
    {
        if (content is MemoryStream { Length: > 0 } already && already.TryGetBuffer(out var seg))
        {
            // Avoid the extra copy when the upload was already buffered in memory.
            var copy = new byte[seg.Count];
            Buffer.BlockCopy(seg.Array!, seg.Offset, copy, 0, seg.Count);
            return copy;
        }
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentAttachmentRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private async Task WithWriteContextAsync(
        Func<ThroneDbContext, CancellationToken, Task> write,
        CancellationToken ct)
    {
        var ambient = Sessions.Current;
        if (ambient is not null)
        {
            await write(ambient, ct);
            return;
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        await write(context, ct);
    }
}
