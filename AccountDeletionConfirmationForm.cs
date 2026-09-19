namespace WowRunner;

/// <summary>
/// 删除本地 Windows 账号前的不可立即确认对话框。
/// </summary>
public sealed class AccountDeletionConfirmationForm : Form
{
    private readonly Label _countdownLabel = new();
    private readonly Button _confirmButton = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private int _remainingSeconds = 10;

    /// <summary>
    /// 初始化删除确认窗口。
    /// </summary>
    /// <param name="profileName">配置名称。</param>
    /// <param name="windowsUser">将被删除的 Windows 用户。</param>
    private AccountDeletionConfirmationForm(string profileName, string windowsUser)
    {
        Text = "确认删除 Windows 账号";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(480, 190);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(new Label
        {
            Text = $"将删除本地 Windows 用户“{windowsUser}”，并保留配置删除操作。\r\n配置：{profileName}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "删除账号会移除该用户的登录能力；用户目录和文件不会由本工具自动清理。",
            Dock = DockStyle.Fill,
            ForeColor = Color.Firebrick,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        _countdownLabel.Dock = DockStyle.Fill;
        _countdownLabel.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(_countdownLabel, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        _confirmButton.Text = "确认删除账号";
        _confirmButton.DialogResult = DialogResult.OK;
        _confirmButton.Enabled = false;
        _confirmButton.Width = 110;
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
        buttons.Controls.Add(_confirmButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = _confirmButton;
        CancelButton = cancelButton;

        UpdateCountdownText();
        _timer.Interval = 1000;
        _timer.Tick += (_, _) =>
        {
            _remainingSeconds--;
            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                _confirmButton.Enabled = true;
                _countdownLabel.Text = "现在可以确认删除账号。";
                return;
            }

            UpdateCountdownText();
        };
        Shown += (_, _) => _timer.Start();
        FormClosed += (_, _) => _timer.Dispose();
    }

    /// <summary>
    /// 显示十秒倒计时确认窗口。
    /// </summary>
    /// <param name="profileName">配置名称。</param>
    /// <param name="windowsUser">Windows 用户名。</param>
    /// <returns>用户确认删除时返回 true。</returns>
    public static bool Confirm(string profileName, string windowsUser)
    {
        using var form = new AccountDeletionConfirmationForm(profileName, windowsUser);
        return form.ShowDialog() == DialogResult.OK;
    }

    /// <summary>
    /// 更新倒计时显示文本。
    /// </summary>
    private void UpdateCountdownText()
    {
        _countdownLabel.Text = $"请等待 {_remainingSeconds} 秒后再确认删除。";
    }
}
