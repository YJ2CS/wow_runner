using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace WowRunner.Setup;

/// <summary>
/// 提供可见的 WowRunner 安装界面和每用户安装流程。
/// </summary>
public sealed class InstallerForm : Form
{
    private const string PayloadResourceName = "WowRunner.Setup.payload.zip";
    private readonly TextBox _installPathTextBox = new();
    private readonly CheckBox _desktopShortcutCheckBox = new();
    private readonly CheckBox _startMenuShortcutCheckBox = new();
    private readonly CheckBox _launchAfterInstallCheckBox = new();
    private readonly Button _installButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();

    /// <summary>
    /// 初始化安装窗口。
    /// </summary>
    public InstallerForm()
    {
        Text = "WowRunner 安装程序";
        Font = new Font("Microsoft YaHei UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(660, 390);

        BuildLayout();
        Shown += (_, _) =>
        {
            _installPathTextBox.SelectionStart = 0;
            _installPathTextBox.SelectionLength = 0;
            _installPathTextBox.ScrollToCaret();
            ActiveControl = _installButton;
        };
    }

    /// <summary>
    /// 创建安装器控件布局。
    /// </summary>
    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 8
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "安装 WowRunner 配置管理器",
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 16F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 3);

        AddLabel(root, "安装位置", 1);
        _installPathTextBox.Text = GetDefaultInstallPath();
        _installPathTextBox.Dock = DockStyle.Fill;
        root.Controls.Add(_installPathTextBox, 1, 1);
        var browseButton = new Button { Text = "浏览", Dock = DockStyle.Fill };
        browseButton.Click += (_, _) => BrowseInstallPath();
        root.Controls.Add(browseButton, 2, 1);

        AddLabel(root, "安装选项", 2);
        _desktopShortcutCheckBox.Text = "创建桌面快捷方式";
        _desktopShortcutCheckBox.Checked = true;
        _desktopShortcutCheckBox.AutoSize = true;
        root.Controls.Add(_desktopShortcutCheckBox, 1, 2);
        _startMenuShortcutCheckBox.Text = "创建开始菜单快捷方式";
        _startMenuShortcutCheckBox.Checked = true;
        _startMenuShortcutCheckBox.AutoSize = true;
        root.Controls.Add(_startMenuShortcutCheckBox, 1, 3);
        _launchAfterInstallCheckBox.Text = "安装完成后启动配置管理器";
        _launchAfterInstallCheckBox.Checked = true;
        _launchAfterInstallCheckBox.AutoSize = true;
        root.Controls.Add(_launchAfterInstallCheckBox, 1, 4);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Continuous;
        root.Controls.Add(_progressBar, 1, 5);
        _statusLabel.Text = "准备安装。";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_statusLabel, 1, 6);

        _installButton.Text = "开始安装";
        _installButton.Dock = DockStyle.Fill;
        _installButton.Click += InstallButton_Click;
        root.Controls.Add(_installButton, 2, 6);
        root.Controls.Add(new Label { Text = "安装包会保留已有 appsettings.json，不覆盖现有用户配置。", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft }, 0, 7);
        root.SetColumnSpan(root.Controls[^1], 3);
        Controls.Add(root);
    }

    /// <summary>
    /// 添加左侧字段标签。
    /// </summary>
    /// <param name="layout">目标布局。</param>
    /// <param name="text">标签文本。</param>
    /// <param name="row">行号。</param>
    private static void AddLabel(TableLayoutPanel layout, string text, int row)
    {
        layout.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
    }

    /// <summary>
    /// 获取默认的每用户安装目录。
    /// </summary>
    /// <returns>安装目录。</returns>
    private static string GetDefaultInstallPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WowRunner");
    }

    /// <summary>
    /// 打开安装目录选择窗口。
    /// </summary>
    private void BrowseInstallPath()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _installPathTextBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installPathTextBox.Text = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// 执行安装按钮事件。
    /// </summary>
    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_installPathTextBox.Text))
        {
            MessageBox.Show(this, "请选择安装目录。", "安装参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _installButton.Enabled = false;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _statusLabel.Text = "正在解压和复制文件，请稍候……";
        try
        {
            var options = new InstallOptions(
                Path.GetFullPath(_installPathTextBox.Text.Trim()),
                _desktopShortcutCheckBox.Checked,
                _startMenuShortcutCheckBox.Checked,
                _launchAfterInstallCheckBox.Checked);
            var progress = new Progress<string>(message => _statusLabel.Text = message);
            var executablePath = await Task.Run(() => InstallPayload(options, progress));
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            _statusLabel.Text = "安装完成。";

            if (options.LaunchAfterInstall)
            {
                Process.Start(new ProcessStartInfo { FileName = executablePath, UseShellExecute = true });
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;
            _statusLabel.Text = "安装失败。";
            _installButton.Enabled = true;
            MessageBox.Show(this, exception.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 从嵌入 ZIP 解压并安装应用文件。
    /// </summary>
    /// <param name="options">安装选项。</param>
    /// <param name="progress">进度消息回调。</param>
    /// <returns>已安装主程序路径。</returns>
    private static string InstallPayload(InstallOptions options, IProgress<string> progress)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var resource = assembly.GetManifestResourceStream(PayloadResourceName)
            ?? throw new InvalidOperationException("安装包缺少应用文件载荷。");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "WowRunner-Setup-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            using (var payload = new MemoryStream())
            {
                resource.CopyTo(payload);
                payload.Position = 0;
                ZipFile.ExtractToDirectory(payload, temporaryDirectory);
            }

            Directory.CreateDirectory(options.InstallPath);
            foreach (var sourceFile in Directory.GetFiles(temporaryDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(temporaryDirectory, sourceFile);
                var destinationFile = Path.Combine(options.InstallPath, relativePath);
                if (string.Equals(Path.GetFileName(destinationFile), "appsettings.json", StringComparison.OrdinalIgnoreCase) && File.Exists(destinationFile))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                File.Copy(sourceFile, destinationFile, true);
            }

            var executablePath = Path.Combine(options.InstallPath, "WowRunner.exe");
            if (!File.Exists(executablePath))
            {
                throw new InvalidOperationException("安装完成后找不到 WowRunner.exe。");
            }

            progress.Report("正在创建快捷方式……");
            if (options.CreateDesktopShortcut)
            {
                CreateShortcut(executablePath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WowRunner.lnk"), options.InstallPath);
            }

            if (options.CreateStartMenuShortcut)
            {
                var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
                Directory.CreateDirectory(startMenu);
                CreateShortcut(executablePath, Path.Combine(startMenu, "WowRunner.lnk"), options.InstallPath);
            }

            return executablePath;
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, true);
            }
            catch
            {
                // 临时目录清理失败不影响已完成安装。
            }
        }
    }

    /// <summary>
    /// 创建一个指向已安装主程序的 Windows 快捷方式。
    /// </summary>
    /// <param name="targetPath">目标程序路径。</param>
    /// <param name="shortcutPath">快捷方式路径。</param>
    /// <param name="workingDirectory">工作目录。</param>
    private static void CreateShortcut(string targetPath, string shortcutPath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("当前系统不支持创建 Windows 快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Description = "WowRunner 配置管理器";
        shortcut.Save();
    }

    /// <summary>
    /// 表示安装器的用户选项。
    /// </summary>
    private sealed record InstallOptions(
        string InstallPath,
        bool CreateDesktopShortcut,
        bool CreateStartMenuShortcut,
        bool LaunchAfterInstall);
}
