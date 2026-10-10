using System.Net;
using System.Net.Http;
using Vex.Modules.Shell.Services;

int passed = 0;
foreach (bool relative in new[] { false, true })
{
    var result = await Verify(_ => Redirect(relative ? "/dotnet9/Vex/releases/tag/v9.9.9" :
        "https://example.test/dotnet9/Vex/releases/tag/v9.9.9"), new Version(1, 0, 0));
    Check(result.Succeeded && result.Update?.Tag == "v9.9.9"
        && result.Update.PageUrl == "https://example.test/dotnet9/Vex/releases/tag/v9.9.9", "absolute/relative redirect");
}
var followed = await Verify(_ => new HttpResponseMessage(HttpStatusCode.OK)
{
    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.test/dotnet9/Vex/releases/tag/v9.9.9")
}, new Version(1, 0, 0));
Check(followed.Succeeded && followed.Update?.Tag == "v9.9.9", "automatically followed redirect");
foreach (var current in new[] { new Version(9, 9, 9), new Version(10, 0, 0) })
{
    var result = await Verify(_ => Redirect("/dotnet9/Vex/releases/tag/v9.9.9"), current);
    Check(result.Succeeded && result.Update is null, "current or newer version");
}
foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
{
    var result = await Verify(_ => new HttpResponseMessage(status), new Version(1, 0, 0));
    Check(!result.Succeeded && result.Error == $"HTTP {(int)status}", "HTTP failure");
}
foreach (var target in new[] { "/dotnet9/Vex/releases/tag/invalid", "/dotnet9/Vex/releases/latest",
    "https://other.test/dotnet9/Vex/releases/tag/v9.9.9", "/dotnet9/Other/releases/tag/v9.9.9" })
{
    var result = await Verify(_ => Redirect(target), new Version(1, 0, 0));
    Check(!result.Succeeded, "invalid version or redirected repository");
}
var missing = await Verify(_ => new HttpResponseMessage(HttpStatusCode.Found), new Version(1, 0, 0));
Check(!missing.Succeeded, "missing Location header");
var offline = await Verify(_ => throw new HttpRequestException("offline"), new Version(1, 0, 0));
Check(!offline.Succeeded && offline.Error == "offline", "network failure");
var timeout = await Verify(_ => throw new TaskCanceledException("timeout"), new Version(1, 0, 0));
Check(!timeout.Succeeded && timeout.Error!.Contains("超时"), "timeout");
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    using var handler = new ProbeHandler(_ => throw new InvalidOperationException("Unexpected request"));
    using var http = new HttpClient(handler);
    var result = await new ShellUpdateChecker(http, "dotnet9", "Vex", "https://example.test")
        .CheckAsync(new Version(1, 0, 0), cancellation.Token);
    Check(!result.Succeeded && result.Error == "操作已取消" && handler.Requests == 0, "cancel before request");
}
using (var cancellation = new CancellationTokenSource())
{
    var result = await Verify(_ =>
    {
        cancellation.Cancel();
        throw new OperationCanceledException(cancellation.Token);
    }, new Version(1, 0, 0), cancellation.Token);
    Check(!result.Succeeded && result.Error == "操作已取消", "cancel during request");
}
Console.WriteLine($"Passed {passed} update-check scenarios; every check uses at most one web request and no API/assets.");

if (args.Contains("--live"))
{
    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
    var checker = new ShellUpdateChecker(http, "dotnet9", "Vex");
    var result = await checker.CheckAsync(new Version(0, 0, 0));
    Check(result.Succeeded && result.Update is not null, "live GitHub release");
    Console.WriteLine($"Live release: {result.Update!.Tag}; {result.Update.PageUrl}");
    var latest = await checker.CheckAsync(result.Update.Version);
    Check(latest.Succeeded && latest.Update is null, "live current version");
}

void Check(bool success, string scenario)
{
    if (!success) throw new InvalidOperationException("Failed: " + scenario);
    passed++;
}

static HttpResponseMessage Redirect(string target)
{
    var response = new HttpResponseMessage(HttpStatusCode.Found);
    response.Headers.Location = new Uri(target, UriKind.RelativeOrAbsolute);
    return response;
}

static async Task<CodeWF.Toolkit.Core.UpdateChecking.UpdateCheckResult> Verify(
    Func<HttpRequestMessage, HttpResponseMessage> respond, Version current, CancellationToken cancellationToken = default)
{
    using var handler = new ProbeHandler(respond);
    using var http = new HttpClient(handler);
    var result = await new ShellUpdateChecker(http, "dotnet9", "Vex", "https://example.test")
        .CheckAsync(current, cancellationToken);
    if (handler.Requests != 1) throw new InvalidOperationException("Expected exactly one web request");
    return result;
}

sealed class ProbeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        if (request.RequestUri?.AbsoluteUri != "https://example.test/dotnet9/Vex/releases/latest")
            throw new InvalidOperationException("Unexpected endpoint: " + request.RequestUri);
        var response = respond(request);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }
}
