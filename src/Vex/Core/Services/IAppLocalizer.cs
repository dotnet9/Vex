using System.Globalization;

namespace Vex.Core.Services;

public interface IAppLocalizer
{
    event EventHandler<EventArgs>? CultureChanged;

    CultureInfo Culture { get; }

    void SetCulture(string cultureName);

    string Get(string key);

    string Format(string key, params object?[] args);
}
