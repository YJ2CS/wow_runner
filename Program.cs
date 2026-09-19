using System.ComponentModel;
using System.Security;
using System.Text.Json;
using WowRunner.Models;
using WowRunner.Services;

namespace WowRunner;

/// <summary>
/// 启动器命令行入口。
/// </summary>
internal static class Program
{
    private const string ElevatedArgument = "--elevated";

    /// <summary>
    /// 执行启动器命令。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>进程退出码。</returns>
    private static int Main(string[] args)
    {
        var userArguments = args
            .Where(argument => !string.Equals(argument, ElevatedArgument, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var configurationPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var configurationStore = new ConfigurationStore(configurationPath);
        var credentialStore = new CredentialStore();

        try
        {
            var loaded = configurationStore.Load();
            if (userArguments.Length == 0)
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm(configurationStore, credentialStore, loaded.Configuration, loaded.Created));
                return 0;
            }

            if (loaded.Created)
            {
                Console.WriteLine($"已创建配置文件：{configurationPath}");
                Console.WriteLine("请先编辑配置文件中的路径和 WindowsUser，然后重新运行。");
                return 0;
            }

            return ExecuteCommand(userArguments, loaded.Configuration, configurationStore, credentialStore);
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"配置文件 JSON 格式错误：{configurationPath}");
            return 2;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"配置文件无效：{exception.Message}");
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"操作失败：{exception.Message}");
            return 2;
        }
        catch (Win32Exception exception)
        {
            Console.Error.WriteLine($"Windows 操作失败，错误码：{exception.NativeErrorCode}。");
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"文件操作失败：{exception.Message}");
            return 4;
        }

    }

    /// <summary>
    /// 根据命令行分发实际操作。
    /// </summary>
    /// <param name="args">去除内部提权参数后的命令行参数。</param>
    /// <param name="configuration">启动器配置。</param>
    /// <param name="configurationStore">配置存储。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>命令退出码。</returns>
    private static int ExecuteCommand(
        string[] args,
        RunnerConfiguration configuration,
        ConfigurationStore configurationStore,
        CredentialStore credentialStore)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "run";
        return command switch
        {
            "run" => RunProfile(args.Skip(1).FirstOrDefault(), configuration, credentialStore),
            "list" => ListProfiles(configuration, credentialStore),
            "credential" => ExecuteCredentialCommand(args.Skip(1).ToArray(), configuration, credentialStore),
            "account" => ExecuteAccountCommand(args.Skip(1).ToArray(), configuration, credentialStore),
            "config" => PrintConfigurationPath(configurationStore),
            "help" or "--help" or "-h" => PrintHelp(),
            _ => UnknownCommand(command)
        };
    }


    /// <summary>
    /// 执行本地 Windows 账号初始化命令。
    /// </summary>
    /// <param name="args">account 子命令参数。</param>
    /// <param name="configuration">启动器配置。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>命令退出码。</returns>
    private static int ExecuteAccountCommand(
        string[] args,
        RunnerConfiguration configuration,
        CredentialStore credentialStore)
    {
        if (!string.Equals(args.FirstOrDefault(), "init", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(args.FirstOrDefault(), "delete", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("account 仅支持 init <profile> 或 delete <profile>。");
            return 2;
        }

        var profileName = args.Skip(1).FirstOrDefault();
        var profile = string.IsNullOrWhiteSpace(profileName)
            ? null
            : ConfigurationStore.FindProfile(configuration, profileName);
        if (profile is null)
        {
            Console.Error.WriteLine("请指定有效 profile，例如：account init wow");
            return 2;
        }

        if (!Elevation.IsElevated())
        {
            return Elevation.RelaunchAsAdministrator(Environment.GetCommandLineArgs().Skip(1));
        }

        if (string.Equals(args.FirstOrDefault(), "delete", StringComparison.OrdinalIgnoreCase))
        {
            new WindowsAccountRemover().DeleteLocalUser(profile);
            Console.WriteLine($"Windows 用户 '{profile.WindowsUser}' 已删除或原本不存在。");
            return 0;
        }

        using var storedCredential = credentialStore.Read(profile.Name);
        SecureString? passwordOverride = null;
        if (storedCredential is null || !string.Equals(storedCredential.UserName, profile.WindowsUser, StringComparison.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
            passwordOverride = PasswordPromptForm.Prompt(profile.WindowsUser);
            if (passwordOverride is null)
            {
                return 1;
            }
        }

        try
        {
            new WindowsAccountInitializer().EnsureLocalUser(profile, credentialStore, passwordOverride);
            Console.WriteLine($"Windows 用户 '{profile.WindowsUser}' 已初始化或已经存在。");
            return 0;
        }
        finally
        {
            passwordOverride?.Dispose();
        }
    }


    /// <summary>
    /// 启动指定 profile 的目标程序。
    /// </summary>
    /// <param name="profileName">profile 名称；为空时使用默认 profile。</param>
    /// <param name="configuration">启动器配置。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>命令退出码。</returns>
    private static int RunProfile(
        string? profileName,
        RunnerConfiguration configuration,
        CredentialStore credentialStore)
    {
        var profile = string.IsNullOrWhiteSpace(profileName)
            ? ConfigurationStore.GetDefaultProfile(configuration)
            : ConfigurationStore.FindProfile(configuration, profileName);
        if (profile is null)
        {
            Console.Error.WriteLine("找不到要运行的 profile，请执行 list 查看可用名称。");
            return 2;
        }

        if (configuration.RequireAdministrator && !Elevation.IsElevated())
        {
            return Elevation.RelaunchAsAdministrator(Environment.GetCommandLineArgs().Skip(1));
        }

        using var credential = GetOrCreateCredential(profile, credentialStore);
        if (credential is null)
        {
            return 1;
        }

        var launcher = new WindowsProcessLauncher(AppContext.BaseDirectory);
        var processId = launcher.Launch(profile, credential);
        Console.WriteLine($"已使用 {profile.WindowsUser} 启动 profile '{profile.Name}'，PID={processId}。");
        return 0;
    }

    /// <summary>
    /// 列出 profile 及其凭据是否已保存，但不输出密码。
    /// </summary>
    /// <param name="configuration">启动器配置。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>命令退出码。</returns>
    private static int ListProfiles(RunnerConfiguration configuration, CredentialStore credentialStore)
    {
        Console.WriteLine($"默认 profile：{configuration.DefaultProfile}");
        foreach (var profile in configuration.Profiles)
        {
            var saved = credentialStore.HasCredential(profile.Name) ? "已保存" : "未设置";
            Console.WriteLine($"{profile.Name,-16} 用户={profile.WindowsUser,-24} 凭据={saved}");
        }

        return 0;
    }

    /// <summary>
    /// 执行凭据设置或删除命令。
    /// </summary>
    /// <param name="args">credential 子命令参数。</param>
    /// <param name="configuration">启动器配置。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>命令退出码。</returns>
    private static int ExecuteCredentialCommand(
        string[] args,
        RunnerConfiguration configuration,
        CredentialStore credentialStore)
    {
        var action = args.FirstOrDefault()?.ToLowerInvariant();
        var profileName = args.Skip(1).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            Console.Error.WriteLine("请指定 profile 名称，例如：credential set wow");
            return 2;
        }

        if (action is "clear" or "delete")
        {
            return ClearCredential(profileName, credentialStore);
        }

        var profile = ConfigurationStore.FindProfile(configuration, profileName);
        if (profile is null)
        {
            Console.Error.WriteLine("找不到指定 profile，请检查配置文件。");
            return 2;
        }

        return action switch
        {
            "set" => SetCredential(profile, credentialStore),
            _ => UnknownCredentialCommand()
        };

    }

    /// <summary>
    /// 交互式设置一个 profile 的 Windows 凭据。
    /// </summary>
    /// <param name="profile">目标 profile。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>命令退出码。</returns>
    private static int SetCredential(LaunchProfile profile, CredentialStore credentialStore)
    {
        Console.WriteLine($"为 profile '{profile.Name}' 设置凭据。");
        Console.WriteLine($"Windows 用户：{profile.WindowsUser}");
        using var password = ReadPassword("请输入密码（不会显示，也不会写入配置文件）：");
        credentialStore.Save(profile, password);
        Console.WriteLine("凭据已保存到当前 Windows 用户的 Credential Manager。");
        return 0;
    }

    /// <summary>
    /// 删除一个 profile 的已保存凭据。
    /// </summary>
    /// <param name="profileName">profile 名称，可以是已从配置中删除的旧名称。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>命令退出码。</returns>
    private static int ClearCredential(string profileName, CredentialStore credentialStore)
    {
        credentialStore.Delete(profileName);
        Console.WriteLine($"已删除 profile '{profileName}' 的已保存凭据。");
        return 0;
    }

    /// <summary>
    /// 获取已有凭据；首次运行或用户名变化时询问是否保存新密码。
    /// </summary>
    /// <param name="profile">目标 profile。</param>
    /// <param name="credentialStore">凭据存储。</param>
    /// <returns>可用于启动的凭据；用户取消时返回 null。</returns>
    private static StoredCredential? GetOrCreateCredential(
        LaunchProfile profile,
        CredentialStore credentialStore)
    {
        var existing = credentialStore.Read(profile.Name);
        if (existing is not null && string.Equals(existing.UserName, profile.WindowsUser, StringComparison.OrdinalIgnoreCase))
        {
            return existing;
        }

        existing?.Dispose();
        ApplicationConfiguration.Initialize();
        using var password = PasswordPromptForm.Prompt(profile.WindowsUser);
        if (password is null)
        {
            return null;
        }

        credentialStore.Save(profile, password);
        return credentialStore.Read(profile.Name);
    }

    /// <summary>
    /// 安全地从控制台读取密码。
    /// </summary>
    /// <param name="prompt">提示文本。</param>
    /// <returns>只读 SecureString。</returns>
    private static SecureString ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var password = new SecureString();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key is ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.RemoveAt(password.Length - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.AppendChar(key.KeyChar);
            }
        }

        if (password.Length == 0)
        {
            password.Dispose();
            throw new InvalidOperationException("密码不能为空。");
        }

        password.MakeReadOnly();
        return password;
    }

    /// <summary>
    /// 输出配置文件位置。
    /// </summary>
    /// <param name="configurationStore">配置存储。</param>
    /// <returns>命令退出码。</returns>
    private static int PrintConfigurationPath(ConfigurationStore configurationStore)
    {
        Console.WriteLine(configurationStore.GetPath());
        return 0;
    }

    /// <summary>
    /// 输出帮助信息。
    /// </summary>
    /// <returns>命令退出码。</returns>
    private static int PrintHelp()
    {
        Console.WriteLine("WowRunner - 使用独立 Windows 用户启动配置中的程序");
        Console.WriteLine();
        Console.WriteLine("用法：");
        Console.WriteLine("  WowRunner.exe                         运行默认 profile");
        Console.WriteLine("  WowRunner.exe run <profile>           运行指定 profile");
        Console.WriteLine("  WowRunner.exe list                    列出 profile 和凭据状态");
        Console.WriteLine("  WowRunner.exe credential set <name>   首次设置或更新凭据");
        Console.WriteLine("  WowRunner.exe credential clear <name> 删除已保存凭据");
        Console.WriteLine("  WowRunner.exe account init <name>    初始化本地 Windows 用户");
        Console.WriteLine("  WowRunner.exe account delete <name> 删除本地 Windows 用户");
        Console.WriteLine("  WowRunner.exe config                  输出配置文件路径");
        return 0;
    }

    /// <summary>
    /// 处理未知主命令。
    /// </summary>
    /// <param name="command">未知命令。</param>
    /// <returns>错误退出码。</returns>
    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"未知命令：{command}。执行 help 查看用法。");
        return 2;
    }

    /// <summary>
    /// 处理未知凭据子命令。
    /// </summary>
    /// <returns>错误退出码。</returns>
    private static int UnknownCredentialCommand()
    {
        Console.Error.WriteLine("credential 仅支持 set 和 clear。");
        return 2;
    }
}
