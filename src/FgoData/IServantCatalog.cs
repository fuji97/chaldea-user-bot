using System.Threading;
using System.Threading.Tasks;

namespace FgoData;

public interface IServantCatalog {
    Task<ServantDetails?> FindServantAsync(string nameQuery, CancellationToken cancellationToken);
}
