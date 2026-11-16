using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vex.Git;

/// <summary>凭据存取抽象：Windows 凭据管理器 / macOS·Linux 加密文件。
/// PAT 之外提供通用键值（key 形如 platform/name）。</summary>
public interface ICredentialStore
{
    string? GetPat(string host);
    void SavePat(string host, string pat);
    void DeletePat(string host);

    /// <summary>通用凭据（key 形如 platform/name）。</summary>
    string? Get(string key);
    void Save(string key, string value);
    void Delete(string key);
}

/// <summary>按平台选择实现：Windows 用系统凭据管理器；其余平台回退到本机加密文件（DPAPI / 明文兜底）。</summary>
public static class CredentialStoreFactory
{
    public static ICredentialStore Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsCredentialStore();
        }
        return new ProtectedFileCredentialStore();
    }
}

/// <summary>Windows 凭据管理器（advapi32 CredRead/CredWrite，Generic 凭据；PAT 目标 vex/pat/&lt;host&gt;，通用键 vex/secret/&lt;key&gt;）。</summary>
public sealed class WindowsCredentialStore : ICredentialStore
{
    public string? GetPat(string host) => ReadCredential("vex/pat/" + host);
    public void SavePat(string host, string pat) => WriteCredential("vex/pat/" + host, pat);
    public void DeletePat(string host) => CredDelete("vex/pat/" + host, 1, 0);

    public string? Get(string key) => ReadCredential("vex/secret/" + key);
    public void Save(string key, string value) => WriteCredential("vex/secret/" + key, value);
    public void Delete(string key) => CredDelete("vex/secret/" + key, 1, 0);

    private static string? ReadCredential(string target)
    {
        if (!CredRead(target, 1, 0, out var credPtr, out _)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred is null || cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0) return null;
            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    private static void WriteCredential(string target, string value)
    {
        CredDelete(target, 1, 0);
        var blob = Encoding.UTF8.GetBytes(value);
        // 持久化策略降级重试：LocalMachine 在 Entra/AAD 域加入机器上会被
        // 策略拒绝（CredWrite 返回 false），Enterprise 为合规降级路径——两档都失败才抛错并带错误码。
        foreach (var persist in new[] { 2, 3 }) // 2=LOCAL_MACHINE, 3=ENTERPRISE
        {
            var cred = new CREDENTIAL
            {
                Type = 1,
                TargetName = target,
                CredentialBlobSize = blob.Length,
                CredentialBlob = Marshal.AllocHGlobal(blob.Length),
                Persist = persist,
                UserName = Environment.UserName,
            };
            try
            {
                Marshal.Copy(blob, 0, cred.CredentialBlob, blob.Length);
                if (CredWrite(cred, 0)) return;
            }
            finally
            {
                Marshal.FreeHGlobal(cred.CredentialBlob);
            }
        }
        var error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException(
            $"写入 Windows 凭据管理器失败（Win32 错误码 {error}；域策略机器请确认允许凭据 Enterprise 持久化）。");
    }

    // CREDENTIAL 为含 string 的非 blittable 类：必须按值传（存根产出 CREDENTIAL*）。
    // 此前写 `ref` 在现代 .NET 下按二级指针封送，CredWrite 读到的 Flags/Type/BlobSize/
    // Persist 全是堆指针字节 → ERROR_INVALID_PARAMETER(87)。
    // CredWrite 对结构体只读，无需 ref 回写语义。
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(CREDENTIAL credential, int flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName = string.Empty;
        public string Comment = string.Empty;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias = string.Empty;
        public string UserName = string.Empty;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credentialPtr, out int size);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}

/// <summary>非 Windows 的兜底：文件存于用户目录，DPAPI（CurrentUser）加密；DPAPI 不可用时降级 Base64 并留提示。</summary>
public sealed class ProtectedFileCredentialStore : ICredentialStore
{
    private static string StorePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vex");
        return Path.Combine(dir, "credentials.bin");
    }

    public string? GetPat(string host) => Get("pat/" + host);
    public void SavePat(string host, string pat) => Save("pat/" + host, pat);
    public void DeletePat(string host) => Delete("pat/" + host);

    public string? Get(string key)
    {
        var path = StorePath();
        if (!File.Exists(path)) return null;
        var entries = Load(path);
        return entries.TryGetValue(key, out var value) ? value : null;
    }

    public void Save(string key, string value)
    {
        var entries = Load(StorePath());
        entries[key] = value;
        Persist(entries);
    }

    public void Delete(string key)
    {
        var entries = Load(StorePath());
        if (entries.Remove(key)) Persist(entries);
    }

    private static Dictionary<string, string> Load(string path)
    {
        try
        {
            var raw = File.ReadAllBytes(path);
            var plain = Protect(raw, decrypt: true);
            // AOT 源生成上下文：NativeAOT 下反射序列化被禁用（PAT 凭据读写崩溃路径）。
            return JsonSerializer.Deserialize(plain, CredentialJsonContext.Default.DictionaryStringString) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void Persist(Dictionary<string, string> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath())!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(entries, CredentialJsonContext.Default.DictionaryStringString);
        File.WriteAllBytes(StorePath(), Protect(plain, decrypt: false));
    }

    private static byte[] Protect(byte[] data, bool decrypt)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return decrypt
                ? ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser)
                : ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        // 非 Windows 兜底：Base64（文件权限即隐私边界）；Secret Service 集成留待后续。
        return decrypt ? Convert.FromBase64String(Encoding.UTF8.GetString(data)) : Encoding.UTF8.GetBytes(Convert.ToBase64String(data));
    }
}

/// <summary>凭据键值存储的 AOT 源生成上下文（NativeAOT 下反射序列化被禁用）。</summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = false)]
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CredentialJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
