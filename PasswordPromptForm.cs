using System.Security;

namespace WowRunner;

/// <summary>
/// 在管理员身份与原用户不同的 UAC 场景下安全收集一次账号密码。
/// </summary>
public sealed class PasswordPromptForm : Form
{
    private readonly TextBox _passwordTextBox = new();
    private SecureString? _password;

    /// <summary>
    /// 初始化密码输入窗口。
    /// </summary>
    /// <param name="userName">目标 Windows 用户名。</param>
    private PasswordPromptForm(string userName)
    {
        Text = "输入 Windows 用户密码";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(420, 140);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(new Label { Text = "Windows 用户", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(new Label { Text = userName, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
        layout.Controls.Add(new Label { Text = "密码", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        _passwordTextBox.Dock = DockStyle.Fill;
        _passwordTextBox.UseSystemPasswordChar = true;
        layout.Controls.Add(_passwordTextBox, 1, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var okButton = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 80 };
        okButton.Click += (_, e) =>
        {
            if (string.IsNullOrEmpty(_passwordTextBox.Text))
            {
                MessageBox.Show("密码不能为空。", "输入无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            _password = ToSecureString(_passwordTextBox.Text);
        };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 1, 2);
        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    /// <summary>
    /// 显示密码窗口并返回一次性安全密码。
    /// </summary>
    /// <param name="userName">目标 Windows 用户名。</param>
    /// <returns>用户确认时返回密码，取消时返回 null。</returns>
    public static SecureString? Prompt(string userName)
    {
        using var form = new PasswordPromptForm(userName);
        return form.ShowDialog() == DialogResult.OK ? form._password : null;
    }

    /// <summary>
    /// 将普通密码文本转换为只读 SecureString。
    /// </summary>
    /// <param name="value">密码文本。</param>
    /// <returns>安全字符串。</returns>
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
