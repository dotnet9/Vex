using System.Net;
using System.Net.Http;
using CodeWF.Tools.UpdateChecking;

namespace Vex.Modules.Shell.Services;

/// <summary>关于窗口只需要版本和发布页；直接读取网页重定向，不请求资产列表或 GitHub API。</summary>
public sealed class ShellUpdateChecker : IUpdateChecker
{
    private readonly HttpClient _http;
    private readonly string _releaseBase;

    public ShellUpdateChecker(HttpClient http, string owner, string repo, string webBase = "https://github.com")
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _releaseBase = $"{webBase.TrimEnd('/')}/{owner}/{repo}/releases";
    }

    public async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Get, _releaseBase + "/latest");
            request.Headers.UserAgent.ParseAdd("Vex-UpdateChecker");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            bool redirect = response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
            if (!redirect && !response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed($"HTTP {(int)response.StatusCode}");
            }

            Uri? target = redirect ? response.Headers.Location : response.RequestMessage?.RequestUri;
            if (target is { IsAbsoluteUri: false })
            {
                target = new Uri(request.RequestUri!, target);
            }

            var releaseBase = new Uri(_releaseBase);
            string tagPrefix = releaseBase.AbsolutePath + "/tag/";
            if (target is null || target.Scheme != releaseBase.Scheme || target.Authority != releaseBase.Authority
                || !target.AbsolutePath.StartsWith(tagPrefix, StringComparison.Ordinal))
            {
                return UpdateCheckResult.Failed("GitHub Releases: invalid version URL");
            }

            string tag = Uri.UnescapeDataString(target.AbsolutePath[tagPrefix.Length..]).TrimEnd('/');
            Version? candidate = VersionUtil.Parse(tag);
            if (candidate is null)
            {
                return UpdateCheckResult.Failed("GitHub Releases: invalid version");
            }

            return VersionUtil.IsNewer(candidate, current)
                ? new UpdateCheckResult(new UpdateInfo(candidate, tag, tag, null,
                    _releaseBase + "/tag/" + Uri.EscapeDataString(tag), null, null, null), true, null)
                : UpdateCheckResult.Latest();
        }
        catch (OperationCanceledException)
        {
            return UpdateCheckResult.Failed(cancellationToken.IsCancellationRequested
                ? "操作已取消"
                : "GitHub 网页检查超时，请稍后重试");
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }
}
