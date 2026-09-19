using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using WowRunner.Models;

namespace WowRunner.Services;

/// <summary>
/// 删除 profile 对应的本地 Windows 用户。
/// </summary>
public sealed class WindowsAccountRemover
{
    private const int ErrorUserNotFound = 2221;
    private const int ErrorFileNotFound = 2;

    /// <summary>
    /// 删除 profile 对应的本地 Windows 用户及其用户目录。
    /// </summary>
    /// <param name="profile">目标 profile。</param>
    /// <returns>用户已删除或原本不存在时返回 true。</returns>
    public bool DeleteLocalUser(LaunchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalizedUser = WindowsUserNameRules.Normalize(profile.WindowsUser);
        var separator = normalizedUser.IndexOf('\\');
        var userName = normalizedUser[(separator + 1)..];
        var sid = TryGetLocalUserSid(userName);
        if (sid is not null)
        {
            DeleteUserProfile(sid);
        }

        var result = NetUserDel(null, userName);
        if (result == 0 || result == ErrorUserNotFound)
        {
            return true;
        }

        throw new Win32Exception(result, "删除本地 Windows 用户失败。");
    }

    /// <summary>
    /// 获取本地用户 SID；账号不存在时返回 null。
    /// </summary>
    /// <param name="userName">本地用户名。</param>
    /// <returns>用户 SID。</returns>
    private static SecurityIdentifier? TryGetLocalUserSid(string userName)
    {
        try
        {
            var account = new NTAccount(Environment.MachineName, userName);
            return (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    /// <summary>
    /// 删除用户配置目录和 ProfileList 注册表项。
    /// </summary>
    /// <param name="sid">用户 SID。</param>
    private static void DeleteUserProfile(SecurityIdentifier sid)
    {
        var currentSid = WindowsIdentity.GetCurrent().User;
        if (currentSid is not null && currentSid.Equals(sid))
        {
            throw new InvalidOperationException("不能删除当前登录用户的用户目录。");
        }

        var profilePath = ReadProfilePath(sid);
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return;
        }

        var usersRoot = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))?.FullName;
        if (string.IsNullOrWhiteSpace(usersRoot) || !IsChildPath(profilePath, usersRoot))
        {
            throw new InvalidDataException("目标用户目录不在系统用户目录下，已停止删除以避免误删其他路径。");
        }

        if (!DeleteProfile(sid.Value, profilePath, null))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorFileNotFound)
            {
                throw new Win32Exception(error, "删除 Windows 用户目录失败；请确认该用户未登录。");
            }
        }
    }

    /// <summary>
    /// 从 ProfileList 注册表读取用户目录。
    /// </summary>
    /// <param name="sid">用户 SID。</param>
    /// <returns>用户目录路径。</returns>
    private static string? ReadProfilePath(SecurityIdentifier sid)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid.Value}");
        var value = key?.GetValue("ProfileImagePath") as string;
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Environment.ExpandEnvironmentVariables(value);
    }

    /// <summary>
    /// 判断路径是否位于用户目录根下。
    /// </summary>
    /// <param name="candidate">待删除目录。</param>
    /// <param name="root">用户目录根。</param>
    /// <returns>属于子目录时返回 true。</returns>
    private static bool IsChildPath(string candidate, string root)
    {
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteProfile(
        string sidString,
        string? profilePath,
        string? defaultPath);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserDel(string? serverName, string userName);
}
