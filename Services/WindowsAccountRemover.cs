using System.ComponentModel;
using System.Runtime.InteropServices;
using WowRunner.Models;

namespace WowRunner.Services;

/// <summary>
/// 删除 profile 对应的本地 Windows 用户。
/// </summary>
public sealed class WindowsAccountRemover
{
    private const int ErrorUserNotFound = 2221;

    /// <summary>
    /// 删除 profile 对应的本地 Windows 用户。
    /// </summary>
    /// <param name="profile">目标 profile。</param>
    /// <returns>用户已删除或原本不存在时返回 true。</returns>
    public bool DeleteLocalUser(LaunchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalizedUser = WindowsUserNameRules.Normalize(profile.WindowsUser);
        var separator = normalizedUser.IndexOf('\\');
        var userName = normalizedUser[(separator + 1)..];
        var result = NetUserDel(null, userName);
        if (result == 0 || result == ErrorUserNotFound)
        {
            return true;
        }

        throw new Win32Exception(result, "删除本地 Windows 用户失败。");
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserDel(string? serverName, string userName);
}
