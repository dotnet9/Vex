namespace Vex.Modules.Shell.Services;

public interface IShellLocalizer
{
    string Get(string key);

    string Format(string key, params object?[] args);
}
