using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using WowRunner.Models;

namespace WowRunner.Services;

/// <summary>
/// 使用 Windows Credential Manager 保存和读取当前操作者的启动凭据。
/// </summary>
public sealed class CredentialStore
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const string TargetPrefix = "WowRunner/";

    /// <summary>
    /// 判断指定 profile 是否已有保存的凭据。
    /// </summary>
    /// <param name="profileName">profile 名称。</param>
    /// <returns>存在返回 true，否则返回 false。</returns>
    public bool HasCredential(string profileName)
    {
        using var credential = Read(profileName);
        return credential is not null;
    }

    /// <summary>
    /// 读取指定 profile 的凭据。
    /// </summary>
    /// <param name="profileName">profile 名称。</param>
    /// <returns>保存的凭据；不存在时返回 null。</returns>
    public StoredCredential? Read(string profileName)
    {
        var targetName = GetTargetName(profileName);
        if (!CredRead(targetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error, "读取 Windows 凭据失败。");
        }

        try
        {
            var nativeCredential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            var username = Marshal.PtrToStringUni(nativeCredential.UserName) ?? string.Empty;
            var password = Marshal.PtrToStringUni(
                nativeCredential.CredentialBlob,
                checked((int)nativeCredential.CredentialBlobSize / sizeof(char))) ?? string.Empty;

            return new StoredCredential(username, ToSecureString(password));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    /// <summary>
    /// 覆盖保存指定 profile 的凭据。
    /// </summary>
    /// <param name="profile">目标启动 profile。</param>
    /// <param name="password">目标 Windows 用户的密码。</param>
    public void Save(LaunchProfile profile, SecureString password)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(password);

        var targetName = GetTargetName(profile.Name);
        var targetNamePointer = IntPtr.Zero;
        var usernamePointer = IntPtr.Zero;
        var passwordPointer = IntPtr.Zero;
        var passwordBytesPointer = IntPtr.Zero;

        try
        {
            targetNamePointer = Marshal.StringToCoTaskMemUni(targetName);
            usernamePointer = Marshal.StringToCoTaskMemUni(profile.WindowsUser);
            passwordPointer = Marshal.SecureStringToGlobalAllocUnicode(password);

            var passwordBytes = new byte[checked(password.Length * sizeof(char))];
            Marshal.Copy(passwordPointer, passwordBytes, 0, passwordBytes.Length);
            passwordBytesPointer = Marshal.AllocHGlobal(passwordBytes.Length);
            Marshal.Copy(passwordBytes, 0, passwordBytesPointer, passwordBytes.Length);
            CryptographicOperations.ZeroMemory(passwordBytes);

            var nativeCredential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = targetNamePointer,
                CredentialBlobSize = checked((uint)(password.Length * sizeof(char))),
                CredentialBlob = passwordBytesPointer,
                Persist = CredentialPersistLocalMachine,
                UserName = usernamePointer
            };

            if (!CredWrite(ref nativeCredential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "保存 Windows 凭据失败。");
            }
        }
        finally
        {
            if (passwordBytesPointer != IntPtr.Zero)
            {
                ZeroAndFree(passwordBytesPointer, checked(password.Length * sizeof(char)));
            }

            if (passwordPointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(passwordPointer);
            }

            if (targetNamePointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(targetNamePointer);
            }

            if (usernamePointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(usernamePointer);
            }
        }
    }

    /// <summary>
    /// 删除指定 profile 的凭据。
    /// </summary>
    /// <param name="profileName">profile 名称。</param>
    public void Delete(string profileName)
    {
        var targetName = GetTargetName(profileName);
        if (CredDelete(targetName, CredentialTypeGeneric, 0))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
        {
            throw new Win32Exception(error, "删除 Windows 凭据失败。");
        }
    }

    /// <summary>
    /// 生成不会与其他应用冲突的 Credential Manager 目标名。
    /// </summary>
    /// <param name="profileName">profile 名称。</param>
    /// <returns>Credential Manager 目标名。</returns>
    private static string GetTargetName(string profileName) => $"{TargetPrefix}{profileName}";

    /// <summary>
    /// 把普通字符串转换为只读 SecureString，并尽快释放普通字符串引用。
    /// </summary>
    /// <param name="value">密码文本。</param>
    /// <returns>安全字符串。</returns>
    private static SecureString ToSecureString(string value)
    {
        var secureString = new SecureString();
        foreach (var character in value)
        {
            secureString.AppendChar(character);
        }

        secureString.MakeReadOnly();
        return secureString;
    }

    /// <summary>
    /// 清零并释放一段非托管内存。
    /// </summary>
    /// <param name="pointer">内存地址。</param>
    /// <param name="length">需要清零的字节数。</param>
    private static void ZeroAndFree(IntPtr pointer, int length)
    {
        var zeroes = new byte[length];
        Marshal.Copy(zeroes, 0, pointer, length);
        Marshal.FreeHGlobal(pointer);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(
        string targetName,
        uint type,
        uint flags,
        out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(
        ref NativeCredential credential,
        uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(
        string targetName,
        uint type,
        uint flags);

    [DllImport("advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr credential);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}

/// <summary>
/// 表示从 Windows Credential Manager 读取的凭据。
/// </summary>
public sealed class StoredCredential : IDisposable
{
    /// <summary>
    /// 初始化保存的凭据。
    /// </summary>
    /// <param name="userName">Windows 用户名。</param>
    /// <param name="password">Windows 密码。</param>
    public StoredCredential(string userName, SecureString password)
    {
        UserName = userName;
        Password = password;
    }

    /// <summary>
    /// 获取保存的用户名。
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// 获取保存的密码。
    /// </summary>
    public SecureString Password { get; }

    /// <summary>
    /// 释放密码内存。
    /// </summary>
    public void Dispose()
    {
        Password.Dispose();
    }
}
