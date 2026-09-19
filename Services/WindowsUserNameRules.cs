using System.Text.RegularExpressions;

namespace WowRunner.Services;

/// <summary>
/// 规范化仅允许本机 ASCII 用户名的 Windows 身份标识。
/// </summary>
public static partial class WindowsUserNameRules
{
    /// <summary>
    /// 将用户输入转换为 .\user 形式的本机身份标识。
    /// </summary>
    /// <param name="value">裸用户名或已有的 .\user 标识。</param>
    /// <returns>规范化后的 .\user 标识。</returns>
    public static string Normalize(string value)
    {
        var userName = value.Trim();
        if (userName.StartsWith(@".\", StringComparison.Ordinal))
        {
            userName = userName[2..];
        }

        if (!UserNamePattern().IsMatch(userName))
        {
            throw new InvalidDataException("Windows 用户名只能使用英文字母、数字、连字符和下划线，且必须以英文字母开头。");
        }

        return @".\" + userName;
    }

    /// <summary>
    /// 将规范化身份标识转换为配置器显示的裸用户名。
    /// </summary>
    /// <param name="value">.\user 身份标识。</param>
    /// <returns>裸用户名。</returns>
    public static string ToDisplayName(string value)
    {
        return value.StartsWith(@".\", StringComparison.Ordinal) ? value[2..] : value;
    }

    /// <summary>
    /// 判断是否为合法的本机用户名。
    /// </summary>
    /// <param name="value">待判断的用户名。</param>
    /// <returns>合法返回 true。</returns>
    public static bool IsValid(string value)
    {
        try
        {
            _ = Normalize(value);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// 匹配仅允许英文、数字、连字符和下划线的本机用户名。
    /// </summary>
    /// <returns>用户名正则。</returns>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex UserNamePattern();
}
