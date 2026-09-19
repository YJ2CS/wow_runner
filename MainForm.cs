using System.Diagnostics;
using System.ComponentModel;
using System.Security;
using WowRunner.Models;
using WowRunner.Services;

namespace WowRunner;

/// <summary>
/// 配置、凭据、启动和桌面快捷方式管理窗口。
/// </summary>
public sealed class MainForm : Form
{
    private readonly ConfigurationStore _configurationStore;
    private readonly CredentialStore _credentialStore;
    private readonly RunnerConfiguration _configuration;
    private readonly ListBox _profiles = new();
    private readonly TextBox _nameTextBox = new();
    private readonly TextBox _userTextBox = new();
    private readonly TextBox _pathTextBox = new();
    private readonly TextBox _workingDirectoryTextBox = new();
    private readonly TextBox _argumentsTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly CheckBox _administratorCheckBox = new();
    private readonly ToolTip _toolTip = new();
    private bool _loadingProfile;

    /// <summary>
    /// 初始化配置管理窗口。
    /// </summary>
    /// <param name="configurationStore">配置存储。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <param name="configuration">当前配置。</param>
    /// <param name="created">配置文件是否刚刚创建。</param>
    public MainForm(
        ConfigurationStore configurationStore,
        CredentialStore credentialStore,
        RunnerConfiguration configuration,
        bool created)
    {
        _configurationStore = configurationStore;
        _credentialStore = credentialStore;
        _configuration = configuration;

        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "暴雪战网启动配置管理器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1220, 780);

        BuildLayout();
        ReloadProfiles();
        if (created)
        {
            SetStatus("已创建默认配置，请填写路径并保存。");
        }
    }

    /// <summary>
    /// 创建窗口控件和操作区域。
    /// </summary>
    private void BuildLayout()
    {
        _toolTip.AutoPopDelay = 10000;
        _toolTip.InitialDelay = 300;
        _toolTip.ReshowDelay = 100;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(root);

        var profilePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 474,
            RowCount = 4,
            ColumnCount = 1
        };
        profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 360));
        profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        profilePanel.Controls.Add(new Label { Text = "用户配置列表", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _toolTip.SetToolTip(_profiles, "选择一个配置后，右侧会显示该 Windows 用户、程序路径和启动参数。");
        _profiles.Dock = DockStyle.Fill;
        _profiles.Font = new Font(SystemFonts.DefaultFont.FontFamily, 12F, FontStyle.Regular);
        _profiles.ItemHeight = 34;
        _profiles.IntegralHeight = false;
        _profiles.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        profilePanel.Controls.Add(_profiles, 0, 1);

        var addButton = new Button { Text = "新增用户配置", Dock = DockStyle.Fill };
        _toolTip.SetToolTip(addButton, "新增一个独立的 Windows 用户启动配置，默认带入 US/enCN 参数。");
        addButton.Click += (_, _) => AddProfile();
        profilePanel.Controls.Add(addButton, 0, 2);
        var removeButton = new Button { Text = "删除当前配置", Dock = DockStyle.Fill };
        _toolTip.SetToolTip(removeButton, "删除左侧选中的配置及其已保存凭据；至少保留一个配置。");
        removeButton.Click += (_, _) => RemoveProfile();
        profilePanel.Controls.Add(removeButton, 0, 3);
        root.Controls.Add(profilePanel, 0, 0);

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 10,
            AutoScroll = true
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        for (var row = 0; row < editor.RowCount; row++)
        {
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        }

        editor.RowStyles[7].Height = 82;
        editor.RowStyles[9].Height = 34;

        var launchButton = new Button { Text = "启动当前配置", Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F) };
        _toolTip.SetToolTip(launchButton, "使用当前选中的 profile、保存的 Windows 凭据和参数启动目标程序。");
        launchButton.Click += (_, _) => LaunchSelectedProfile();
        editor.Controls.Add(launchButton, 2, 0);
        var shortcutButton = new Button { Text = "创建桌面快捷方式", Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F) };
        _toolTip.SetToolTip(shortcutButton, "在桌面创建一个只启动当前 profile 的快捷方式；不同 profile 可以创建多个快捷方式。");
        shortcutButton.Click += (_, _) => CreateShortcut();
        editor.Controls.Add(shortcutButton, 1, 0);

        var saveButton = new Button { Text = "保存用户配置", Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F) };
        _toolTip.SetToolTip(saveButton, "保存路径、用户名、工作目录和参数；如果填写了密码，也会同时覆盖保存凭据。");
        saveButton.Click += (_, _) => SaveProfile();
        editor.Controls.Add(saveButton, 1, 1);
        var saveCredentialButton = new Button { Text = "保存用户凭据", Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F) };
        _toolTip.SetToolTip(saveCredentialButton, "只保存密码，不改变路径和启动参数；会覆盖该 profile 的旧凭据。");
        saveCredentialButton.Click += (_, _) => SaveCredential();
        editor.Controls.Add(saveCredentialButton, 2, 1);

        var initializeButton = new Button { Text = "初始化 Windows 账号", Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F) };
        _toolTip.SetToolTip(initializeButton, "请求管理员权限，按当前 Windows 用户名创建本地账号；需要先保存密码。");
        initializeButton.Click += (_, _) => InitializeWindowsAccount();
        editor.Controls.Add(initializeButton, 1, 2);
        var deleteAccountButton = new Button { Text = "删除 Windows 账号", Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F) };
        _toolTip.SetToolTip(deleteAccountButton, "删除当前 Windows 账号及其用户目录，不删除左侧配置；必须等待十秒确认并通过 UAC。");
        deleteAccountButton.Click += (_, _) => DeleteWindowsAccount();
        editor.Controls.Add(deleteAccountButton, 2, 2);

        AddEditorRow(editor, 3, "配置名称", _nameTextBox);
        AddEditorRow(editor, 4, "Windows 用户名", _userTextBox);
        AddEditorRow(editor, 5, "程序路径", _pathTextBox, CreateBrowseButton(_pathTextBox, false));
        AddEditorRow(editor, 6, "工作目录", _workingDirectoryTextBox, CreateBrowseButton(_workingDirectoryTextBox, true));
        AddEditorRow(editor, 7, "参数列表", _argumentsTextBox);
        _toolTip.SetToolTip(_pathTextBox, "要以所选 Windows 用户启动的 EXE 完整路径；选择或修改后会自动更新工作目录。");
        _pathTextBox.Leave += (_, _) => UpdateWorkingDirectoryFromExecutable(_pathTextBox.Text);
        _toolTip.SetToolTip(_workingDirectoryTextBox, "目标程序工作目录；留空时使用 EXE 所在目录。");
        _toolTip.SetToolTip(_argumentsTextBox, "启动参数，默认是 --setregion=US 和 --setlanguage=enCN；可按空格修改。");
        _argumentsTextBox.Multiline = true;
        _argumentsTextBox.Height = 60;
        _argumentsTextBox.Text = "--setregion=US --setlanguage=enCN";
        AddEditorRow(editor, 8, "密码（可选）", _passwordTextBox);
        _passwordTextBox.UseSystemPasswordChar = true;
        _toolTip.SetToolTip(_passwordTextBox, "填写后点击“保存用户配置”即可覆盖保存该 profile 的密码；密码写入 Windows Credential Manager，不写入 JSON。");

        _administratorCheckBox.Text = "启动时要求管理员权限";
        _administratorCheckBox.AutoSize = true;
        _toolTip.SetToolTip(_administratorCheckBox, "勾选后，点击“启动当前配置”或快捷方式时会先请求 UAC 管理员权限。");
        editor.Controls.Add(_administratorCheckBox, 1, 9);
        editor.SetColumnSpan(_administratorCheckBox, 2);
        root.Controls.Add(editor, 1, 0);

        var status = new Label
        {
            Name = "StatusLabel",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
            Text = "先在左侧选择配置；修改右侧内容后点击“保存用户配置”。"
        };
        root.Controls.Add(status, 0, 1);
        root.SetColumnSpan(status, 2);
        _administratorCheckBox.Checked = _configuration.RequireAdministrator;
        _administratorCheckBox.CheckedChanged += (_, _) => SaveGlobalSettings();
    }

    /// <summary>
    /// 添加一个标签、输入框和可选操作按钮行。
    /// </summary>
    /// <param name="panel">目标布局。</param>
    /// <param name="row">行号。</param>
    /// <param name="label">标签文本。</param>
    /// <param name="editor">编辑控件。</param>
    /// <param name="button">可选操作按钮。</param>
    private static void AddEditorRow(TableLayoutPanel panel, int row, string label, Control editor, Control? button = null)
    {
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        editor.Dock = DockStyle.Fill;
        panel.Controls.Add(editor, 1, row);
        if (button is not null)
        {
            panel.Controls.Add(button, 2, row);
        }
    }

    /// <summary>
    /// 创建文件或目录选择按钮。
    /// </summary>
    /// <param name="target">接收路径的输入框。</param>
    /// <param name="directory">是否选择目录。</param>
    /// <returns>选择按钮。</returns>
    private Button CreateBrowseButton(TextBox target, bool directory)
    {
        var button = new Button
        {
            Text = "浏览",
            Width = 88,
            Height = 30,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 5, 3, 3)
        };
        _toolTip.SetToolTip(button, directory ? "选择目标程序的工作目录。" : "选择要启动的 EXE 文件。");
        button.Click += (_, _) =>
        {
            if (directory)
            {
                using var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    target.Text = dialog.SelectedPath;
                }
            }
            else
            {
                using var dialog = new OpenFileDialog { Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*" };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    target.Text = dialog.FileName;
                    UpdateWorkingDirectoryFromExecutable(dialog.FileName);
                }
            }
        };
        return button;
    }

    /// <summary>
    /// 根据可执行文件路径自动更新工作目录为其所在目录。
    /// </summary>
    /// <param name="executablePath">可执行文件路径。</param>
    private void UpdateWorkingDirectoryFromExecutable(string executablePath)
    {
        if (_loadingProfile || string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(executablePath.Trim()));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _workingDirectoryTextBox.Text = directory.Replace('\\', '/');
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // 用户仍在编辑路径时保留当前工作目录，保存时由配置校验报告路径问题。
        }
    }


    /// <summary>
    /// 刷新左侧 profile 列表。
    /// </summary>
    private void ReloadProfiles()
    {
        _profiles.Items.Clear();
        foreach (var profile in _configuration.Profiles)
        {
            _profiles.Items.Add(profile.Name);
        }

        if (_profiles.Items.Count > 0)
        {
            _profiles.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// 将选中的 profile 加载到编辑区。
    /// </summary>
    private void LoadSelectedProfile()
    {
        if (_profiles.SelectedItem is not string name)
        {
            return;
        }

        var profile = ConfigurationStore.FindProfile(_configuration, name);
        if (profile is null)
        {
            return;
        }

        _loadingProfile = true;
        _nameTextBox.Text = profile.Name;
        _userTextBox.Text = WindowsUserNameRules.ToDisplayName(profile.WindowsUser);
        _pathTextBox.Text = profile.ExecutablePath;
        _workingDirectoryTextBox.Text = profile.WorkingDirectory ?? string.Empty;
        _argumentsTextBox.Text = string.Join(" ", profile.Arguments.Select(QuoteForEditor));
        _passwordTextBox.Clear();
        _loadingProfile = false;
    }

    /// <summary>
    /// 新增一个 profile，并填入用户提供的默认启动参数。
    /// </summary>
    private void AddProfile()
    {
        var name = "wow-" + (_configuration.Profiles.Count + 1);
        while (ConfigurationStore.FindProfile(_configuration, name) is not null)
        {
            name += "-new";
        }

        _configuration.Profiles.Add(new LaunchProfile
        {
            Name = name,
            WindowsUser = "wow",
            ExecutablePath = @"C:\Program Files (x86)\Battle.net\Battle.net Launcher.exe",
            WorkingDirectory = @"C:\Program Files (x86)\Battle.net",
            Arguments = ["--setregion=US", "--setlanguage=enCN"]
        });
        ReloadProfiles();
        _profiles.SelectedItem = name;
        SetStatus($"已新增 profile：{name}，请修改并保存。");
    }

    /// <summary>
    /// 删除当前 profile 及其已保存凭据。
    /// </summary>
    private void RemoveProfile()
    {
        if (_profiles.SelectedItem is not string name)
        {
            return;
        }

        if (_configuration.Profiles.Count <= 1)
        {
            MessageBox.Show("至少保留一个用户配置。", "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var profile = ConfigurationStore.FindProfile(_configuration, name);
        if (profile is null)
        {
            return;
        }

        var choice = MessageBox.Show(
            $"删除配置 '{name}'？\r\n\r\n选择“是”同时删除 Windows 用户 {profile.WindowsUser} 及其用户目录；选择“否”只删除配置。",
            "删除配置",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);
        if (choice == DialogResult.Cancel)
        {
            return;
        }

        if (choice == DialogResult.Yes)
        {
            if (!AccountDeletionConfirmationForm.Confirm(name, profile.WindowsUser))
            {
                return;
            }

            var exitCode = Elevation.RelaunchAsAdministrator(["account", "delete", name]);
            if (exitCode != 0)
            {
                MessageBox.Show($"Windows 用户删除失败，退出码：{exitCode}。配置未删除。", "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        _configuration.Profiles.Remove(profile);
        _credentialStore.Delete(name);
        if (string.Equals(_configuration.DefaultProfile, name, StringComparison.OrdinalIgnoreCase))
        {
            _configuration.DefaultProfile = _configuration.Profiles.FirstOrDefault()?.Name ?? string.Empty;
        }

        _configurationStore.Save(_configuration);
        ReloadProfiles();
        SetStatus($"已删除 profile：{name}。");
    }

    /// <summary>
    /// 保存编辑区中的 profile 配置和全局设置。
    /// </summary>
    private void SaveProfile()
    {
        if (_loadingProfile || _profiles.SelectedItem is not string selectedName)
        {
            return;
        }

        var profile = ConfigurationStore.FindProfile(_configuration, selectedName);
        if (profile is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_nameTextBox.Text) || string.IsNullOrWhiteSpace(_userTextBox.Text) || string.IsNullOrWhiteSpace(_pathTextBox.Text))
        {
            MessageBox.Show("配置名称、Windows 用户和程序路径不能为空。", "配置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_nameTextBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show("配置名称包含 Windows 文件名不允许的字符。", "配置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!WindowsUserNameRules.IsValid(_userTextBox.Text))
        {
            MessageBox.Show("Windows 用户名只能使用英文字母、数字、连字符和下划线，且必须以英文字母开头，例如 wow1、wow-alt 或 wow_test。", "配置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.Equals(selectedName, _nameTextBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) &&
            ConfigurationStore.FindProfile(_configuration, _nameTextBox.Text.Trim()) is not null)
        {
            MessageBox.Show("配置名称已存在。", "配置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var oldName = profile.Name;
        profile.Name = _nameTextBox.Text.Trim();
        profile.WindowsUser = WindowsUserNameRules.Normalize(_userTextBox.Text);
        profile.ExecutablePath = _pathTextBox.Text.Trim();
        profile.WorkingDirectory = string.IsNullOrWhiteSpace(_workingDirectoryTextBox.Text) ? null : _workingDirectoryTextBox.Text.Trim();
        profile.Arguments = ParseArguments(_argumentsTextBox.Text);
        if (string.Equals(_configuration.DefaultProfile, oldName, StringComparison.OrdinalIgnoreCase))
        {
            _configuration.DefaultProfile = profile.Name;
        }

        if (!string.Equals(oldName, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            _credentialStore.Delete(oldName);
        }

        _configurationStore.Save(_configuration);
        ReloadProfiles();
        _profiles.SelectedItem = profile.Name;
        if (!string.IsNullOrEmpty(_passwordTextBox.Text))
        {
            SaveCredential();
        }

        SetStatus($"已保存配置：{profile.Name}；如填写密码，凭据也已覆盖保存。");
    }

    /// <summary>
    /// 保存当前 profile 的密码到当前用户 Credential Manager。
    /// </summary>
    private void SaveCredential()
    {
        if (_profiles.SelectedItem is not string name)
        {
            return;
        }

        var profile = ConfigurationStore.FindProfile(_configuration, name);
        if (profile is null || string.IsNullOrEmpty(_passwordTextBox.Text))
        {
            MessageBox.Show("请先选择 profile 并输入密码。", "凭据无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var password = ToSecureString(_passwordTextBox.Text);
        _credentialStore.Save(profile, password);
        _passwordTextBox.Clear();
        SetStatus($"已保存 profile '{profile.Name}' 的凭据。密码未写入配置文件。");
    }

    /// <summary>
    /// 保存全局管理员启动设置。
    /// </summary>
    private void SaveGlobalSettings()
    {
        if (_loadingProfile)
        {
            return;
        }

        _configuration.RequireAdministrator = _administratorCheckBox.Checked;
        _configurationStore.Save(_configuration);
    }

    /// <summary>
    /// 启动当前选中的 profile。
    /// </summary>
    private void LaunchSelectedProfile()
    {
        SaveProfile();
        if (_profiles.SelectedItem is not string name)
        {
            return;
        }

        var profile = ConfigurationStore.FindProfile(_configuration, name);
        if (profile is null)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(_passwordTextBox.Text))
            {
                SaveCredential();
            }

            if (_configuration.RequireAdministrator && !Elevation.IsElevated())
            {
                var exitCode = Elevation.RelaunchAsAdministrator(["run", profile.Name]);
                SetStatus(exitCode == 0 ? $"已请求管理员启动 '{profile.Name}'。" : $"管理员启动失败，退出码：{exitCode}。");
                return;
            }

            using var credential = _credentialStore.Read(profile.Name);
            if (credential is null)
            {
                MessageBox.Show("请先输入密码并点击“保存凭据”。", "缺少凭据", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var processId = new WindowsProcessLauncher(AppContext.BaseDirectory).Launch(profile, credential);
            SetStatus($"已启动 '{profile.Name}'，PID={processId}。");
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(exception.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 为当前 profile 在桌面创建独立快捷方式。
    /// </summary>
    private void CreateShortcut()
    {
        if (_profiles.SelectedItem is not string name)
        {
            return;
        }

        SaveProfile();
        if (_profiles.SelectedItem is not string currentName)
        {
            return;
        }

        name = currentName;
        var profile = ConfigurationStore.FindProfile(_configuration, name)
            ?? throw new InvalidOperationException("找不到当前 profile。");
        var isBattleNet = string.Equals(
            Path.GetFileName(profile.ExecutablePath),
            "Battle.net Launcher.exe",
            StringComparison.OrdinalIgnoreCase);
        var shortcutLabel = isBattleNet
            ? $"暴雪战网 - {name}"
            : $"暴雪战网启动配置管理器 - {name}";
        var shortcutName = $"{shortcutLabel}.lnk";
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktop, shortcutName);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("当前系统不支持创建 Windows 快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = Environment.ProcessPath;
        shortcut.IconLocation = File.Exists(profile.ExecutablePath)
            ? $"{profile.ExecutablePath},0"
            : $"{Environment.ProcessPath},0";
        shortcut.Arguments = $"run {QuoteForCommandLine(name)}";
        shortcut.WorkingDirectory = AppContext.BaseDirectory;
        shortcut.Description = $"以 profile '{name}' 启动配置的程序";
        shortcut.Save();
        SetStatus($"已创建桌面快捷方式：{shortcutName}。");
    }

    /// <summary>
    /// 请求 UAC 后，以管理员身份初始化当前 Windows 用户。
    /// </summary>
    private void InitializeWindowsAccount()
    {
        if (_profiles.SelectedItem is not string name)
        {
            return;
        }

        SaveProfile();
        if (_profiles.SelectedItem is not string currentName)
        {
            return;
        }

        name = currentName;
        if (!string.IsNullOrEmpty(_passwordTextBox.Text))
        {
            SaveCredential();
        }

        if (MessageBox.Show("将请求管理员权限并创建配置中的本地 Windows 用户。继续？", "初始化账号", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位当前程序。"),
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };
            startInfo.ArgumentList.Add("account");
            startInfo.ArgumentList.Add("init");
            startInfo.ArgumentList.Add(name);
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动管理员初始化进程。");
            process.WaitForExit();
            SetStatus(process.ExitCode == 0 ? "Windows 账号初始化完成。" : $"Windows 账号初始化失败，退出码：{process.ExitCode}。");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            SetStatus("用户取消了管理员权限请求。");
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(exception.Message, "账号初始化失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 请求 UAC 后删除当前 profile 对应的本地 Windows 用户，但保留配置。
    /// </summary>
    private void DeleteWindowsAccount()
    {
        if (_profiles.SelectedItem is not string name)
        {
            return;
        }

        var profile = ConfigurationStore.FindProfile(_configuration, name);
        if (profile is null || !AccountDeletionConfirmationForm.Confirm(name, profile.WindowsUser))
        {
            return;
        }

        var exitCode = Elevation.RelaunchAsAdministrator(["account", "delete", name]);
        SetStatus(exitCode == 0
            ? $"Windows 用户 {profile.WindowsUser} 及其用户目录已删除，配置仍保留。"
            : $"Windows 用户删除失败，退出码：{exitCode}。配置仍保留。");
    }


    /// <summary>
    /// 设置窗口底部状态文本。
    /// </summary>
    /// <param name="message">状态消息。</param>
    private void SetStatus(string message)
    {
        if (Controls.Count == 0)
        {
            return;
        }

        var label = Controls.Find("StatusLabel", true).FirstOrDefault();
        if (label is not null)
        {
            label.Text = message;
        }
    }

    /// <summary>
    /// 解析编辑框中的命令行参数，支持双引号包裹含空格参数。
    /// </summary>
    /// <param name="text">参数文本。</param>
    /// <returns>参数列表。</returns>
    private static List<string> ParseArguments(string text)
    {
        var result = new List<string>();
        var builder = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in text)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (builder.Length > 0)
                {
                    result.Add(builder.ToString());
                    builder.Clear();
                }

                continue;
            }

            builder.Append(character);
        }

        if (builder.Length > 0)
        {
            result.Add(builder.ToString());
        }

        return result;
    }

    /// <summary>
    /// 将参数显示为编辑框可读格式。
    /// </summary>
    /// <param name="argument">参数文本。</param>
    /// <returns>必要时加引号的参数。</returns>
    private static string QuoteForEditor(string argument) => argument.Any(char.IsWhiteSpace) ? $"\"{argument}\"" : argument;

    /// <summary>
    /// 将 profile 名称安全地作为快捷方式参数传递。
    /// </summary>
    /// <param name="value">参数文本。</param>
    /// <returns>Windows 命令行参数。</returns>
    private static string QuoteForCommandLine(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    /// <summary>
    /// 将普通字符串转换为 SecureString。
    /// </summary>
    /// <param name="value">普通字符串。</param>
    /// <returns>只读 SecureString。</returns>
    private static SecureString ToSecureString(string value)
    {
        var secure = new SecureString();
        foreach (var character in value)
        {
            secure.AppendChar(character);
        }

        secure.MakeReadOnly();
        return secure;
    }
}
