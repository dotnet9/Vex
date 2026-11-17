using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IRecentDocumentStore
{
    IReadOnlyList<RecentDocument> Load(int maxCount);

    void Save(IEnumerable<RecentDocument> documents);
}
