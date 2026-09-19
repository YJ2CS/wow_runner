namespace WowRunner.Setup;

/// <summary>
/// WowRunner 安装器入口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 启动可见安装界面。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }
}
