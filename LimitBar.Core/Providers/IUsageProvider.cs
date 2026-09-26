using System.Threading;
using System.Threading.Tasks;
using LimitBar.Core.Models;

namespace LimitBar.Core.Providers;

public interface IUsageProvider
{
    string Id { get; }

    string DisplayName { get; }

    Task<ProviderAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken);

    Task<UsageSnapshot> GetUsageAsync(
        CancellationToken cancellationToken);
}
