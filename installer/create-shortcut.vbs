Option Explicit

Dim shell, shortcut, targetPath, shortcutPath, arguments
Set shell = CreateObject("WScript.Shell")

targetPath = WScript.Arguments(0)
shortcutPath = WScript.Arguments(1)
arguments = WScript.Arguments(2)

Set shortcut = shell.CreateShortcut(shortcutPath)
shortcut.TargetPath = targetPath
shortcut.Arguments = arguments
shortcut.WorkingDirectory = shell.CurrentDirectory
shortcut.Description = "WowRunner 配置管理器"
shortcut.Save
