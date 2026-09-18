using System.Threading;
using System.Threading.Tasks;
using Rayshift.Models;

namespace Rayshift;

public interface IRayshiftClient {
    Task<ApiResponse> GetSupportDeckAsync(Region region, string friendCode, CancellationToken cancellationToken);
    Task<ApiResponse> RequestSupportLookupAsync(Region region, string friendCode, CancellationToken cancellationToken);
}
