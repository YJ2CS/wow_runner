# WowRunner 领域词汇表

## Profile

一个可被单独选择和启动的用户配置。Profile 包含目标 Windows 用户、目标程序、工作目录和启动参数，并可对应凭据与桌面快捷方式。

## Windows 用户

用于创建 Windows 进程和隔离用户环境的本机用户身份。配置器中输入裸用户名，例如 `wow1`、`wow-alt` 或 `wow_test`；系统身份表示为 `.` 加反斜杠再加用户名。

## 用户凭据

与 Profile 绑定的 Windows 用户密码。凭据属于敏感信息，不属于 Profile 配置内容；它用于登录目标 Windows 用户并启动进程。

## 启动参数

传给目标程序的独立参数集合。参数表达的是目标程序的输入，不是可执行的 shell 命令。

## 桌面快捷方式

绑定到单个 Profile 的桌面入口。用户点击入口时，启动该 Profile，而不是打开未选择配置的通用入口。

## Windows 账号初始化

按 Profile 中的 Windows 用户和密码创建本机 Windows 用户。该操作需要管理员权限；域用户不属于本地账号初始化范围。

## Windows 账号删除

删除 Profile 对应的本机 Windows 登录账号。删除账号默认保留用户目录和文件；删除 Profile 与删除 Windows 账号是两个可分别确认的动作。
