using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using WowRunner.Models;

namespace WowRunner.Services;

/// <summary>
/// 创建本地 Windows 用户，并复用 profile 中已保存的凭据。
/// </summary>
public sealed class WindowsAccountInitializer
{
    private const int ErrorUserExists = 2224;
    private const int UserPrivilege = 1;
    private const uint UserFlagScript = 0x0001;

    /// <summary>
    /// 以管理员权限确保 profile 对应的本地 Windows 用户存在。
    /// </summary>
    /// <param name="profile">目标 profile。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <param name="passwordOverride">可选的临时密码；用于 UAC 切换到其他管理员账号时避免跨用户读取凭据。</param>
    /// <returns>用户已创建或已经存在时返回 true。</returns>
    public bool EnsureLocalUser(LaunchProfile profile, CredentialStore credentialStore, SecureString? passwordOverride = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(credentialStore);

        var (userName, domain) = SplitLocalUser(profile.WindowsUser);
        if (!string.Equals(domain, ".", StringComparison.Ordinal) &&
            !string.Equals(domain, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("账号初始化仅支持本机 Windows 用户，不支持域用户或 UPN。");
        }

        StoredCredential? credential = null;
        var passwordSource = passwordOverride;
        if (passwordSource is null)
        {
            credential = credentialStore.Read(profile.Name)
                ?? throw new InvalidDataException("请先为该 profile 保存密码，再初始化 Windows 账号。");
            if (!string.Equals(credential.UserName, profile.WindowsUser, StringComparison.OrdinalIgnoreCase))
            {
                credential.Dispose();
                throw new InvalidDataException("已保存凭据的用户名与 profile 的 WindowsUser 不一致。");
            }

            passwordSource = credential.Password;
        }

        var password = ToPlainString(passwordSource);
        try
        {
            var userInfo = new UserInfo1
            {
                Name = userName,
                Password = password,
                Privilege = UserPrivilege,
                Flags = UserFlagScript
            };
            var result = NetUserAdd(null, 1, ref userInfo, out _);
            if (result == 0 || result == ErrorUserExists)
            {
                return true;
            }

            throw new Win32Exception(result, "初始化本地 Windows 用户失败。");
        }
        finally
        {
            password = string.Empty;
            credential?.Dispose();
        }
    }

    /// <summary>
    /// 拆分仅支持本机账号的用户标识。
    /// </summary>
    /// <param name="windowsUser">.\user 或 MACHINE\user。</param>
    /// <returns>用户名和域标识。</returns>
    private static (string UserName, string Domain) SplitLocalUser(string windowsUser)
    {
        var separator = windowsUser.IndexOf('\\');
        if (separator < 0)
        {
            return (windowsUser, ".");
        }

        var domain = windowsUser[..separator];
        var userName = windowsUser[(separator + 1)..];
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new InvalidDataException("WindowsUser 缺少用户名。");
        }

        return (userName, domain);
    }

    /// <summary>
    /// 将 SecureString 转为仅供 Win32 API 调用的临时密码文本。
    /// </summary>
    /// <param name="password">安全密码。</param>
    /// <returns>临时密码文本。</returns>
    private static string ToPlainString(SecureString password)
    {
        var pointer = Marshal.SecureStringToBSTR(password);
        try
        {
            return Marshal.PtrToStringBSTR(pointer) ?? string.Empty;
        }
        finally
        {
            Marshal.ZeroFreeBSTR(pointer);
        }
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserAdd(
        string? serverName,
        int level,
        ref UserInfo1 userInfo,
        out uint parameterError);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UserInfo1
    {
        public string? Name;
        public string? Password;
        public uint PasswordAge;
        public uint Privilege;
        public string? HomeDirectory;
        public string? Comment;
        public uint Flags;
        public string? ScriptPath;
    }
}
