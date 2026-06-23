# 📁 一键修改 Windows 下载路径到 D 盘

一个 Windows 桌面工具，将系统默认下载文件夹从 C 盘迁移到 `D:\Downloads`，一键完成，即时生效。

---

## 📑 目录

- [功能概述](#功能概述)
- [两种技术方案](#两种技术方案)
- [方案 A：Python 版](#方案-apython-版)
  - [源代码结构](#python-源代码结构)
  - [编译/打包指南](#python-编译打包指南)
  - [运行要求](#python-运行要求)
- [方案 B：C# 版](#方案-bc-版)
  - [源代码结构](#c-源代码结构)
  - [编译/打包指南](#c-编译打包指南)
  - [运行要求](#c-运行要求)
- [技术细节](#技术细节)
- [安全注意事项](#安全注意事项)
- [常见问题](#常见问题)
- [许可](#许可)

---

## 功能概述

| 功能 | 说明 |
|------|------|
| 🔧 **注册表修改** | 自动修改 `HKCU\...\User Shell Folders` 下两个键值 |
| 📂 **文件夹创建** | 自动在 D 盘创建 `Downloads` 目录 |
| 📋 **文件迁移（可选）** | 将原 C 盘下载的文件复制到新位置（不删除原文件） |
| 🔄 **即时生效** | 通过 `SHChangeNotify` + `WM_SETTINGCHANGE` + 重启 Explorer 实现 |
| 🛡️ **UAC 提权** | 自动请求管理员权限 |
| 🖥️ **GUI 界面** | 简洁友好的图形界面 |

---

## 两种技术方案

| 维度 | 方案 A：Python + Tkinter | 方案 B：C# (.NET 8) WinForms |
|------|--------------------------|------------------------------|
| **开发语言** | Python 3.10+ | C# 12 (.NET 8) |
| **GUI 框架** | Tkinter（标准库） | WinForms |
| **注册表操作** | `winreg`（标准库） | `Microsoft.Win32.Registry` |
| **系统调用** | `ctypes` 调用 Win32 API | P/Invoke 直接调用 Win32 API |
| **打包方式** | PyInstaller | `dotnet publish` |
| **单文件 .exe** | ✅ 约 10-15 MB | ✅ 框架依赖约 200 KB / 自包含约 65 MB |
| **运行时依赖** | 无（自带 Python） | 框架依赖模式需 .NET 8 运行时 |
| **UAC 提权方式** | `ShellExecuteW` + `--uac-admin` | `app.manifest` 声明 `requireAdministrator` |
| **建议场景** | 快速原型、非 .NET 环境 | 企业部署、已有 .NET 环境的用户 |

---

## 方案 A：Python 版

### Python 源代码结构

```
python-version/
├── download_mover.py    ← 主程序（含 GUI + 全部逻辑）
└── build.bat             ← 一键打包脚本
```

### Python 编译/打包指南

#### 前置条件
1. 安装 **Python 3.10+**：[python.org](https://www.python.org/downloads/)
2. 安装时勾选 **"Add Python to PATH"**

#### 打包步骤（一键）

```batch
# 进入 python-version 目录
cd python-version

# 运行打包脚本
build.bat
```

脚本会自动：
1. 检测 Python 是否已安装
2. 安装/升级 PyInstaller
3. 清理旧构建产物
4. 打包为单个 `dist\DownloadMover.exe`

#### 手动打包

```powershell
# 安装 PyInstaller
pip install pyinstaller

# 打包（在 python-version 目录下执行）
pyinstaller --onefile --windowed --name "DownloadMover" --uac-admin --clean download_mover.py

# 输出: dist\DownloadMover.exe
```

#### 参数说明

| 参数 | 说明 |
|------|------|
| `--onefile` | 打包成单个 .exe 文件 |
| `--windowed` | 不显示控制台窗口（纯 GUI） |
| `--name` | 输出文件名 |
| `--uac-admin` | 嵌入 UAC 管理员权限清单 |
| `--clean` | 清理临时文件 |

### Python 运行要求
- 打包后的 .exe 无需 Python 环境即可运行
- 仅支持 **Windows 10 / 11**（x64）
- 首次运行需要点击 UAC 对话框的「是」

---

## 方案 B：C# 版

### C# 源代码结构

```
csharp-version/
├── DownloadMover/
│   ├── DownloadMover.csproj       ← 项目文件（.NET 8 WinForms）
│   ├── app.manifest                ← UAC 权限清单
│   ├── Program.cs                  ← 入口（单实例检查 + 权限验证）
│   ├── MainForm.cs                 ← 主窗口逻辑（注册表/PInvoke/迁移）
│   └── MainForm.Designer.cs        ← UI 布局代码
└── build.bat                       ← 一键编译/发布脚本
```

### C# 编译/打包指南

#### 前置条件
1. 安装 **.NET 8.0 SDK**：[dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)
   - 选择 **".NET 8.0 SDK"**（不是 Runtime）
2. 安装后重新打开命令行

#### 编译与发布步骤（一键）

```batch
# 进入 csharp-version/DownloadMover 目录
cd csharp-version\DownloadMover

# 运行构建脚本
..\build.bat
```

脚本会自动：
1. 检测 .NET SDK
2. 还原 NuGet 依赖
3. 编译 Release 版本
4. 发布到 `..\publish\` 目录

#### 手动编译

```powershell
# 进入项目目录
cd csharp-version\DownloadMover

# 还原依赖
dotnet restore

# 编译（检查是否有错误）
dotnet build -c Release

# 发布为单个文件（框架依赖模式）
dotnet publish -c Release -o ..\publish
```

#### 自包含模式（无需 .NET 运行时）

如需生成完全自包含的 .exe（约 65 MB，无需安装 .NET 运行时），修改 `DownloadMover.csproj`：

```xml
<SelfContained>true</SelfContained>
```

然后重新发布：

```powershell
dotnet publish -c Release -o ..\publish-self-contained
```

### C# 运行要求
- **框架依赖模式**：需要安装 [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **自包含模式**：无需任何运行时，但 .exe 较大
- 仅支持 **Windows 10 / 11**（x64）
- 首次运行需要点击 UAC 对话框的「是」

---

## 技术细节

### 修改的注册表项

```
路径: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders

修改的键值:
  ① {374DE290-123F-4565-9164-39C4925E467B}  → D:\Downloads  (GUID 键，Windows Vista+)
  ② Downloads                                → D:\Downloads  (字符串键，兼容)
```

### 系统刷新方式

为了让修改**立即生效**（无需重启电脑），程序依次执行：

| 序号 | 方式 | 作用 |
|------|------|------|
| ① | `SHChangeNotify(SHCNE_ASSOCCHANGED, ...)` | 通知 Shell 文件夹关联已变更 |
| ② | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, ...)` | 广播环境变量变更到所有窗口 |
| ③ | `taskkill /f /im explorer.exe` + 重启 | 重启文件资源管理器，确保 100% 生效 |

### 文件迁移策略

- **复制而非移动**：从原路径**复制**文件到新路径，**不删除**原文件
- **跳过已存在**：如果目标文件已存在则跳过（不会覆盖）
- **默认为关闭**：文件迁移复选框默认不勾选（更安全保守的设计）
- 如有大量文件，迁移过程可能需要较长时间

---

## 安全注意事项

### ⚠️ 使用前必读

1. **系统还原点**
   - 建议在运行工具前手动创建一个系统还原点：
     - 按 `Win + R` → 输入 `sysdm.cpl` →「系统保护」→「创建」
   - 如果修改后出现问题，可以通过还原点回滚

2. **注册表备份**
   - 本工具仅修改 `HKEY_CURRENT_USER` 下的路径（当前用户），风险可控
   - 但建议提前备份：
     ```powershell
     # 导出相关注册表分支
     reg export "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" "%USERPROFILE%\Desktop\ShellFolders_Backup.reg"
     ```

3. **文件安全**
   - 文件迁移使用**复制**（不删除原文件），因此原有文件不会丢失
   - 尽管如此，建议在操作前确保重要数据已备份

4. **管理员权限**
   - 程序需要管理员权限才能修改注册表和 `C:\Users` 下的文件
   - 会弹出标准的 Windows UAC 对话框
   - 如果你不确定此工具的来源，可以右键查看数字签名或源代码

5. **Explorer 重启**
   - 执行过程中文件资源管理器（Explorer.exe）会短暂闪烁/重启
   - 打开的文件管理器窗口会关闭
   - 任务栏和桌面图标会短暂消失并恢复（正常现象，约 1-2 秒）

6. **杀毒软件误报**
   - PyInstaller 打包的程序偶尔会被某些杀毒软件误报为可疑文件
   - 这是因为 PyInstaller 将 Python 解释器打包进 .exe 的行为与某些恶意软件相似
   - 如果你收到警告，可以将此目录加入白名单，或改用 C# 版本
   - C# 版本通常不会被误报

7. **仅限当前用户**
   - 修改仅影响当前登录用户（`HKEY_CURRENT_USER`），不影响其他用户账户

8. **目标盘符**
   - 确保 D 盘存在且有足够的可用空间
   - 如果 D 盘不存在，程序会报错
   - 你可以修改源代码中的 `NEW_DOWNLOAD_PATH` 常量为其他路径

---

## 常见问题

### Q: 为什么提示「需要管理员权限」？
A: 修改注册表和系统文件夹需要管理员权限。右键点击 .exe →「以管理员身份运行」，或在弹出的 UAC 对话框中点击「是」。

### Q: 为什么文件迁移了好久？
A: 如果原下载文件夹中有大量文件（如几十 GB），迁移会需要较长时间。你可以不勾选「迁移文件」选项，事后手动复制。

### Q: 修改后下载文件夹图标没有变化？
A: 尝试注销并重新登录，或重启电脑。重启 Explorer 通常已足够，但极少数情况下需要完全重新登录。

### Q: 如何改回 C 盘？
A: 再次运行程序，将 `NEW_DOWNLOAD_PATH` 修改为原路径（通常是 `C:\Users\<你的用户名>\Downloads`），或在注册表中手动改回。

### Q: Python 版和 C# 版哪个更好？
A:
- **Python 版**：打包后单文件约 10-15 MB，无需运行时，适合快速使用
- **C# 版**：原生 Windows 体验更好，不会被杀毒软件误报，但框架依赖模式需要 .NET 运行时

### Q: D 盘没有怎么办？
A: 修改源代码中的目标路径（`NEW_DOWNLOAD_PATH`）为其他有空间的分区路径。

---

## 许可

MIT License — 可自由使用、修改和分发。

---

*最后更新: 2025 年 7 月*
