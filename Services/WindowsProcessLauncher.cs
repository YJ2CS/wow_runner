using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using WowRunner.Models;

namespace WowRunner.Services;

/// <summary>
/// 使用 Windows 原生登录 API，以指定用户启动目标进程。
/// </summary>
public sealed class WindowsProcessLauncher
{
    private const uint LogonWithProfile = 1;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private readonly string _baseDirectory;

    /// <summary>
    /// 初始化进程启动器。
    /// </summary>
    /// <param name="baseDirectory">相对路径解析基准目录。</param>
    public WindowsProcessLauncher(string baseDirectory)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
    }

    /// <summary>
    /// 以 profile 对应的 Windows 用户启动目标程序。
    /// </summary>
    /// <param name="profile">启动配置。</param>
    /// <param name="credential">目标用户凭据。</param>
    /// <returns>新进程 ID。</returns>
    public int Launch(LaunchProfile profile, StoredCredential credential)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(credential);

        string executablePath;
        string? workingDirectory;
        try
        {
            executablePath = ResolvePath(profile.ExecutablePath);
            workingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
                ? Path.GetDirectoryName(executablePath)
                : ResolvePath(profile.WorkingDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException("目标路径格式无效。", exception);
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new InvalidDataException("无法确定目标工作目录。");
        }


        var (userName, domain) = SplitWindowsUser(profile.WindowsUser);
        var commandLine = new StringBuilder(BuildCommandLine(executablePath, profile.Arguments));
        var startupInfo = new StartupInfo
        {
            Cb = Marshal.SizeOf<StartupInfo>()
        };
        var processInfo = new ProcessInformation();
        var passwordPointer = IntPtr.Zero;

        try
        {
            passwordPointer = Marshal.SecureStringToGlobalAllocUnicode(credential.Password);
            if (!CreateProcessWithLogonW(
                    userName,
                    domain,
                    passwordPointer,
                    LogonWithProfile,
                    executablePath,
                    commandLine,
                    CreateUnicodeEnvironment,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startupInfo,
                    out processInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "以指定 Windows 用户启动进程失败。");
            }

            return checked((int)processInfo.ProcessId);
        }
        finally
        {
            if (passwordPointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(passwordPointer);
            }

            if (processInfo.ProcessHandle != IntPtr.Zero)
            {
                CloseHandle(processInfo.ProcessHandle);
            }

            if (processInfo.ThreadHandle != IntPtr.Zero)
            {
                CloseHandle(processInfo.ThreadHandle);
            }
        }
    }

    /// <summary>
    /// 按启动器目录解析绝对或相对路径。
    /// </summary>
    /// <param name="path">配置中的路径。</param>
    /// <returns>解析后的完整路径。</returns>
    private string ResolvePath(string path)
    {
        return Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, _baseDirectory);
    }

    /// <summary>
    /// 将可执行文件和参数安全地编码为 Windows 命令行。
    /// </summary>
    /// <param name="executablePath">可执行文件路径。</param>
    /// <param name="arguments">独立参数列表。</param>
    /// <returns>完整命令行。</returns>
    private static string BuildCommandLine(string executablePath, IEnumerable<string> arguments)
    {
        return string.Join(
            ' ',
            new[] { QuoteWindowsArgument(executablePath) }
                .Concat(arguments.Select(QuoteWindowsArgument)));
    }

    /// <summary>
    /// 按 Windows C 运行时规则引用一个命令行参数。
    /// </summary>
    /// <param name="argument">原始参数。</param>
    /// <returns>引用后的参数。</returns>
    private static string QuoteWindowsArgument(string argument)
    {
        if (argument.Length > 0 && argument.All(character => !char.IsWhiteSpace(character) && character != '"'))
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        var backslashCount = 0;

        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', checked(backslashCount * 2 + 1));
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            builder.Append('\\', backslashCount);
            builder.Append(character);
            backslashCount = 0;
        }

        builder.Append('\\', checked(backslashCount * 2));
        builder.Append('"');
        return builder.ToString();
    }

    /// <summary>
    /// 将 .\user、DOMAIN\user 或 UPN 拆分为 Windows API 参数。
    /// </summary>
    /// <param name="windowsUser">配置中的 Windows 用户标识。</param>
    /// <returns>用户名和域名。</returns>
    private static (string UserName, string? Domain) SplitWindowsUser(string windowsUser)
    {
        var separatorIndex = windowsUser.IndexOf('\\');
        if (separatorIndex >= 0)
        {
            var domain = windowsUser[..separatorIndex];
            var userName = windowsUser[(separatorIndex + 1)..];
            if (string.Equals(domain, ".", StringComparison.Ordinal))
            {
                domain = Environment.MachineName;
            }

            return (userName, domain);
        }

        return (windowsUser, windowsUser.Contains('@', StringComparison.Ordinal) ? null : Environment.MachineName);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessWithLogonW(
        string userName,
        string? domain,
        IntPtr password,
        uint logonFlags,
        string? applicationName,
        StringBuilder commandLine,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public uint ProcessId;
        public uint ThreadId;
    }
}
