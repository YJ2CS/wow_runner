using System.Text.Json;
using System.Text.Json.Serialization;
using WowRunner.Models;

namespace WowRunner.Services;

/// <summary>
/// 负责读取和校验启动器配置文件。
/// </summary>
public sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;

    /// <summary>
    /// 初始化配置存储。
    /// </summary>
    /// <param name="path">配置文件路径。</param>
    public ConfigurationStore(string path)
    {
        _path = path;
    }

    /// <summary>
    /// 读取配置；配置文件不存在时创建安全的示例配置。
    /// </summary>
    /// <returns>读取到的配置和是否刚刚创建的标志。</returns>
    public (RunnerConfiguration Configuration, bool Created) Load()
    {
        if (!File.Exists(_path))
        {
            var configuration = CreateDefaultConfiguration();
            Save(configuration);
            return (configuration, true);
        }

        var json = File.ReadAllText(_path);
        var loaded = JsonSerializer.Deserialize<RunnerConfiguration>(json, SerializerOptions)
            ?? throw new InvalidDataException("配置文件为空，无法加载启动配置。");

        Validate(loaded);
        return (loaded, false);
    }

    /// <summary>
    /// 保存配置文件。
    /// </summary>
    /// <param name="configuration">待保存的配置。</param>
    public void Save(RunnerConfiguration configuration)
    {
        Validate(configuration);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(configuration, SerializerOptions);
        File.WriteAllText(_path, json);
    }

    /// <summary>
    /// 查找指定名称的 profile。
    /// </summary>
    /// <param name="configuration">配置对象。</param>
    /// <param name="name">profile 名称。</param>
    /// <returns>匹配的 profile；不存在时返回 null。</returns>
    public static LaunchProfile? FindProfile(RunnerConfiguration configuration, string name)
    {
        return configuration.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取默认 profile。
    /// </summary>
    /// <param name="configuration">配置对象。</param>
    /// <returns>默认 profile；配置没有有效默认值时返回第一个 profile。</returns>
    public static LaunchProfile? GetDefaultProfile(RunnerConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.DefaultProfile))
        {
            var defaultProfile = FindProfile(configuration, configuration.DefaultProfile);
            if (defaultProfile is not null)
            {
                return defaultProfile;
            }
        }

        return configuration.Profiles.FirstOrDefault();
    }

    /// <summary>
    /// 获取配置文件路径。
    /// </summary>
    /// <returns>配置文件完整路径。</returns>
    public string GetPath() => _path;

    /// <summary>
    /// 校验配置的必填项、profile 唯一性和路径格式。
    /// </summary>
    /// <param name="configuration">待校验的配置。</param>
    private static void Validate(RunnerConfiguration configuration)
    {
        if (configuration.Profiles is null || configuration.Profiles.Count == 0)
        {
            throw new InvalidDataException("配置至少需要一个 Profiles 项。");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in configuration.Profiles)
        {
            if (profile is null)
            {
                throw new InvalidDataException("Profiles 中存在空项。");
            }

            profile.Arguments ??= [];
            if (profile.Arguments.Any(argument => argument is null))
            {
                throw new InvalidDataException($"Profile '{profile.Name}' 的 Arguments 中存在空参数。");
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new InvalidDataException("Profiles 中存在空的 Name。");
            }

            if (profile.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException($"Profile '{profile.Name}' 的名称不能用于 Windows 文件名。");
            }

            if (!names.Add(profile.Name))
            {
                throw new InvalidDataException($"Profiles 中存在重复名称：{profile.Name}。");
            }

            if (string.IsNullOrWhiteSpace(profile.WindowsUser))
            {
                throw new InvalidDataException($"Profile '{profile.Name}' 缺少 WindowsUser。");
            }

            profile.WindowsUser = WindowsUserNameRules.Normalize(profile.WindowsUser);

            if (string.IsNullOrWhiteSpace(profile.ExecutablePath))
            {
                throw new InvalidDataException($"Profile '{profile.Name}' 缺少 ExecutablePath。");
            }

            try
            {
                _ = Path.GetFullPath(profile.ExecutablePath);
                if (!string.IsNullOrWhiteSpace(profile.WorkingDirectory))
                {
                    _ = Path.GetFullPath(profile.WorkingDirectory);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new InvalidDataException($"Profile '{profile.Name}' 的路径格式无效。", exception);
            }
        }
    }

    /// <summary>
    /// 创建首次运行使用的默认配置。
    /// </summary>
    /// <returns>默认配置对象。</returns>
    private static RunnerConfiguration CreateDefaultConfiguration()
    {
        return new RunnerConfiguration
        {
            DefaultProfile = "wow",
            Profiles =
            [
                new LaunchProfile
                {
                    Name = "wow",
                    WindowsUser = @".\wow",
                    ExecutablePath = @"C:\Program Files (x86)\Battle.net\Battle.net Launcher.exe",
                    WorkingDirectory = @"C:\Program Files (x86)\Battle.net",
                    Arguments = ["--setregion=US", "--setlanguage=enCN"]
                }
            ]
        };
    }
}
