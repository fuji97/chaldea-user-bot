using System;
using System.Threading.Tasks;
using Rayshift.Models;

namespace Rayshift {
    public interface IRayshiftClient : IDisposable {
        Task<ApiResponse?> GetSupportDeck(Region region, string friendCode);
        Task<ApiResponse?> RequestSupportLookupAsync(Region region, string friendCode);
        Task<bool> RequestSupportLookup(Region region, string friendCode, Func<ApiResponse?, Task>? callback = null);
    }
}