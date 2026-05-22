namespace Vex.Modules.Shell.Services;

public interface IShellStatusPublisher
{
    void Publish(string message);

    void PublishResource(string key);

    void PublishResourceFormat(string key, params object?[] args);
}
