using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace WowRunner.Services;

/// <summary>
/// 负责检测管理员权限并按需通过 UAC 重启当前启动器。
/// </summary>
public static class Elevation
{
    private const int UserCancelledError = 1223;

    /// <summary>
    /// 判断当前进程是否已经以管理员身份运行。
    /// </summary>
    /// <returns>当前进程属于管理员组且令牌已提升时返回 true。</returns>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 请求 UAC 并使用原始参数重新启动当前 EXE。
    /// </summary>
    /// <param name="arguments">原始命令行参数。</param>
    /// <returns>子进程退出码；UAC 被取消时返回 Windows 错误码。</returns>
    public static int RelaunchAsAdministrator(IEnumerable<string> arguments)
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法定位当前启动器路径。");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add("--elevated");
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动管理员进程。");
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == UserCancelledError)
        {
            Console.Error.WriteLine("用户取消了管理员权限请求。");
            return UserCancelledError;
        }
    }
}
