using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IAppSettingsStore
{
    AppSettings Current { get; }

    AppSettings Update(Func<AppSettings, AppSettings> update);
}
