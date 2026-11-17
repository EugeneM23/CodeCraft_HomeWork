using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Saving
{
    /// <summary>
    /// A server, database or cloud that keeps copies of slots, each as one package string (ISaveLoadService.ExportSlot).
    /// Four calls are enough for any backend; SaveSync decides what goes where.
    /// </summary>
    public interface IRemoteSaveStorage
    {
        Task<IReadOnlyList<int>> ListAsync(CancellationToken token = default);
        Task<string> DownloadAsync(int slot, CancellationToken token = default); // null: the server has no such slot
        Task UploadAsync(int slot, string package, CancellationToken token = default);
        Task DeleteAsync(int slot, CancellationToken token = default);
    }
}
