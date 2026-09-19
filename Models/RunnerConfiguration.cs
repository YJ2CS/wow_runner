namespace WowRunner.Models;

/// <summary>
/// 启动器的根配置。
/// </summary>
public sealed class RunnerConfiguration
{
    /// <summary>
    /// 获取或设置运行目标前是否要求启动器以管理员权限运行。
    /// </summary>
    public bool RequireAdministrator { get; set; }

    /// <summary>
    /// 获取或设置未指定 profile 时使用的 profile 名称。
    /// </summary>
    public string DefaultProfile { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置可用的启动 profile 列表。
    /// </summary>
    public List<LaunchProfile> Profiles { get; set; } = [];
}

/// <summary>
/// 描述一个 Windows 用户下的应用启动配置。
/// </summary>
public sealed class LaunchProfile
{
    /// <summary>
    /// 获取或设置 profile 的唯一名称，用于命令行切换和凭据映射。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 Windows 登录身份，例如 .\wow 或 DOMAIN\wow。
    /// </summary>
    public string WindowsUser { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置待启动的可执行文件完整路径。
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置目标进程的工作目录；为空时使用可执行文件所在目录。
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 获取或设置按独立参数传递给目标程序的参数列表。
    /// </summary>
    public List<string> Arguments { get; set; } = [];
}
