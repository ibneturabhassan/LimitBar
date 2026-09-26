using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LimitBar.Core.Models;
using LimitBar.Core.Providers;

namespace LimitBar.Core.Services;

public class UsageService
{
    private readonly IEnumerable<IUsageProvider> _providers;
    private readonly Dictionary<string, UsageSnapshot> _cache = new();

    // Simplistic lock object for caching logic
    private readonly object _cacheLock = new object();

    public UsageService(IEnumerable<IUsageProvider> providers)
    {
        _providers = providers;
    }

    public async Task<RefreshResult> RefreshAsync(RefreshReason reason, CancellationToken cancellationToken)
    {
        var tasks = _providers.Select(provider => RefreshProviderAsync(provider, cancellationToken));

        var snapshots = await Task.WhenAll(tasks);

        return new RefreshResult
        {
            Snapshots = snapshots
        };
    }

    private async Task<UsageSnapshot> RefreshProviderAsync(IUsageProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await provider.GetUsageAsync(cancellationToken);

            // Update cache
            lock (_cacheLock)
            {
                _cache[provider.Id] = snapshot;
            }

            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // If it fails, try to return from cache, but append error status
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(provider.Id, out var cachedSnapshot))
                {
                    return cachedSnapshot with
                    {
                        Status = UsageStatus.UnknownError,
                        ErrorMessage = ex.Message
                    };
                }
            }

            return new UsageSnapshot
            {
                ProviderId = provider.Id,
                ProviderName = provider.DisplayName,
                Status = UsageStatus.UnknownError,
                ErrorMessage = ex.Message,
                RetrievedAt = DateTimeOffset.Now
            };
        }
    }

    public IReadOnlyList<UsageSnapshot> GetCachedSnapshots()
    {
        lock (_cacheLock)
        {
            return _cache.Values.ToList();
        }
    }
}
