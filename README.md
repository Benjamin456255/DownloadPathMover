# 📁 DownloadPathMover — Windows 系统文件夹路径迁移工具

一键将 Windows 用户文件夹（下载/桌面/文档/图片/音乐/视频）迁移到指定盘符。修改注册表 + 即时生效 + 可选文件迁移。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/Windows-10%2F11%20x64-0078D6?logo=windows)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 📸 界面预览

```
┌──────────────────────────────────────────────┐
│  📁  系统文件夹路径迁移工具                  │
│  将 Windows 用户文件夹一键迁移到指定盘符     │
├──────────────────────────────────────────────┤
│  🎯 目标盘符:  [D:\________________] [✏️] [🔄]│
├──────────────────────────────────────────────┤
│  📂  选择要迁移的文件夹                      │
│  ☑ 📥 Downloads    当前: C:\Users\...\       │
│  ☑ 🖥️ Desktop      当前: C:\Users\...\       │
│  ☑ 📄 Documents    当前: C:\Users\...\       │
│  ☑ 🖼️ Pictures     当前: C:\Users\...\       │
│  ☑ 🎵 Music        当前: C:\Users\...\       │
│  ☑ 🎬 Videos       当前: C:\Users\...\       │
├──────────────────────────────────────────────┤
│  ⚙️  选项                                    │
│  ☐ 迁移已有文件到新路径（复制，不删除）      │
│  ☐ 仅复制文件，不修改注册表（纯迁移模式）    │
├──────────────────────────────────────────────┤
│  ████████████████░░░░░░░  80%               │
│  Downloads 完成 (5/6)                        │
├──────────────────────────────────────────────┤
│  [🚀 一键修改] [↩️ 恢复默认] [↩ 撤销上次]    │
│  [📥 导入配置] [📤 导出配置] [退出]         │
├──────────────────────────────────────────────┤
│  📋 操作日志 ▾                               │
│  ┌──────────────────────────────────────────┐│
│  │ [12:00:00] 处理: Downloads               ││
│  │ [12:00:01]   文件夹就绪: D:\Downloads    ││
│  │ [12:00:02]   注册表已更新                ││
│  │ [12:00:03] ═══════ 完成 ═══════         ││
│  └──────────────────────────────────────────┘│
│  ⚠️  修改注册表前请确认已备份重要数据        │
└──────────────────────────────────────────────┘
```

---

## ✨ 功能

| 功能 | 说明 |
|------|------|
| 📂 **多文件夹迁移** | 支持 6 个系统文件夹：下载、桌面、文档、图片、音乐、视频 |
| 🔍 **路径预览** | 实时显示每个文件夹在注册表中的当前路径 |
| 🎯 **自定义盘符** | 任意盘符，不限于 D 盘 |
| 📦 **可选文件迁移** | 将原有文件复制到新位置（不删除原文件） |
| 📋 **仅复制模式** | 只迁移文件不修改注册表，适合先复制后切换 |
| ↩️ **恢复默认** | 一键恢复到 Windows 默认路径 |
| ↩ **撤销上次** | 记录上次修改，一键还原（失败可重试） |
| 📥📤 **导入/导出配置** | JSON 配置文件，保存/加载操作方案 |
| 📋 **操作日志** | 彩色日志面板，成功/警告/错误一目了然 |
| 🔄 **即时生效** | SHChangeNotify + WM_SETTINGCHANGE + Explorer 重启，无需重启电脑 |
| 🛡️ **UAC 提权** | app.manifest 声明 requireAdministrator，自动弹出管理员确认 |

---

## 🔧 技术原理

### 注册表修改

修改 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders` 下的 GUID 键值：

| 文件夹 | GUID |
|--------|------|
| Downloads | `{374DE290-123F-4565-9164-39C4925E467B}` |
| Desktop | `{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}` |
| Documents | `{F42EE2D3-909F-4907-8871-4C22FC0BF756}` |
| Pictures | `{0DDD015D-B06C-45D5-8C4B-5CEF4E1F7EF4}` |
| Music | `{A0C69A99-21C8-4671-9703-8B0C2B22D1AF}` |
| Videos | `{35286A68-3C57-41A1-BBB1-0EAE73D76C95}` |

### 系统刷新

1. `SHChangeNotify(SHCNE_ASSOCCHANGED)` — 通知 Shell 文件夹关联变更
2. `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE)` — 广播环境变更
3. `taskkill /fi "USERNAME eq ..." /im explorer.exe` + 重启 — 仅重启当前用户 Explorer

---

## 🚀 快速开始

### 前置条件

- **Windows 10 / 11 (x64)**
- **[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)**（运行时）

  或一键安装：
  ```powershell
  winget install Microsoft.DotNet.DesktopRuntime.8
  ```

### 下载运行

从 [Releases](../../releases) 下载 `DownloadPathMover.exe`，双击运行。首次启动弹出 UAC 对话框，点击「是」。

### 从源码编译

```powershell
git clone https://github.com/Benjamin456255/DownloadPathMover.git
cd DownloadPathMover/csharp-version/DownloadMoverWpf
dotnet publish -c Release -o ../../publish
# 输出: ../../publish/DownloadPathMover.exe
```

如需完全自包含版本（无需 .NET 运行时，约 70 MB）：
```powershell
# 先修改 DownloadMoverWpf.csproj:
#   <SelfContained>true</SelfContained>
dotnet publish -c Release -o ../../publish
```

---

## 📖 使用说明

### 基本操作

1. 在 **目标盘符** 输入框中输入目标盘符（如 `D:\`）
2. 勾选需要迁移的文件夹
3. 可选：勾选「迁移已有文件」将原文件复制到新位置
4. 可选：勾选「仅复制文件」只迁移文件不修改注册表
5. 点击 **🚀 一键修改**

### 恢复默认

1. 勾选需要恢复的文件夹
2. 点击 **↩️ 恢复默认** — 恢复到 `C:\Users\<用户名>\` 下的原始位置

### 撤销上次

修改后如果后悔，点击 **↩ 撤销上次** 即可一键还原。

### 导入/导出配置

- **📤 导出配置** — 把当前盘符、勾选状态、选项保存为 JSON 文件
- **📥 导入配置** — 加载之前保存的配置，自动填充所有选项

---

## ⚠️ 安全注意事项

1. **系统还原点** — 建议运行前创建还原点（`Win + R` → `sysdm.cpl` → 系统保护 → 创建）
2. **注册表备份** — 如需手动备份：
   ```powershell
   reg export "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" "%USERPROFILE%\Desktop\ShellFolders_Backup.reg"
   ```
3. **文件迁移** — 使用复制而非移动，原文件不会丢失
4. **仅当前用户** — 修改仅影响 `HKEY_CURRENT_USER`，不影响其他用户
5. **Explorer 重启** — 执行过程中文件资源管理器会短暂闪烁（1-2 秒），属正常现象
6. **管理员权限** — 需要管理员权限修改注册表，会弹出标准 Windows UAC 对话框

---

## 📁 项目结构

```
DownloadsMover/
├── .gitignore
├── README.md
└── csharp-version/
    └── DownloadMoverWpf/
        ├── DownloadMoverWpf.csproj   # .NET 8 WPF 项目文件
        ├── app.manifest               # UAC requireAdministrator 声明
        ├── app.ico                    # 应用图标
        ├── App.xaml                   # 应用入口 XAML
        ├── App.xaml.cs                # 单实例检查 + 权限验证
        ├── MainWindow.xaml            # 主窗口 UI 布局
        └── MainWindow.xaml.cs         # 核心逻辑（注册表/PInvoke/迁移/撤销/配置）
```

---

## 🛠 技术栈

- **.NET 8** — WPF (Windows Presentation Foundation)
- **P/Invoke** — 直接调用 Win32 API（SHChangeNotify, SendMessageTimeout）
- **Microsoft.Win32.Registry** — 注册表读写
- **System.Text.Json** — 配置文件序列化
- **BackgroundWorker** — 后台异步操作，不阻塞 UI

---

## 📄 许可

MIT License — 可自由使用、修改和分发。
