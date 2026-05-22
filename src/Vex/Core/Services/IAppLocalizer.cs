namespace Vex.Core.Services;

public interface IAppLocalizer
{
    string Get(string key);

    string Format(string key, params object?[] args);
}
