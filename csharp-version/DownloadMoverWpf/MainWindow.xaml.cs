using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace DownloadMoverWpf;

public static class FolderPicker
{
    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderIDList(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
        IntPtr hToken, out IntPtr ppidl);

    public static string? Show()
    {
        var dlg = new FileOpenDialog() as IFileOpenDialog;
        if (dlg == null) return null;
        dlg.SetOptions(FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
        // 默认打开"此电脑"
        try
        {
            SHGetKnownFolderIDList(KnownFolders.ComputerFolder, 0, IntPtr.Zero, out var pidl);
            dlg.SetDefaultFolder(pidl);
            Marshal.FreeCoTaskMem(pidl);
        }
        catch { }

        if (dlg.Show(IntPtr.Zero) == 0)
        {
            dlg.GetResult(out var item);
            item.GetDisplayName(SIGDN_FILESYSPATH, out var path);
            return path;
        }
        return null;
    }

    private const int FOS_PICKFOLDERS = 0x20;
    private const int FOS_FORCEFILESYSTEM = 0x40;
    private const int SIGDN_FILESYSPATH = unchecked((int)0x80058000);

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialog { }

    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IntPtr pidl);
        void SetFolder(IntPtr pidl);
        void GetFolder(out IntPtr ppidl);
        void GetCurrentSelection(out IntPtr ppszName);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid([MarshalAs(UnmanagedType.LPStruct)] Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    private static class KnownFolders
    {
        public static readonly Guid ComputerFolder = new("20D04FE0-3AEA-1069-A2D8-08002B30309D");
    }
}

public partial class MainWindow : Window
{
    // ========================================================================
    // 常量
    // ========================================================================
    private const string RegKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";

    private static readonly Dictionary<string, FolderInfo> Folders = new()
    {
        ["Downloads"] = new("{374DE290-123F-4565-9164-39C4925E467B}"),
        ["Desktop"]   = new("{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"),
        ["Documents"] = new("{F42EE2D3-909F-4907-8871-4C22FC0BF756}"),
        ["Pictures"]  = new("{0DDD015D-B06C-45D5-8C4B-5CEF4E1F7EF4}"),
        ["Music"]     = new("{A0C69A99-21C8-4671-9703-8B0C2B22D1AF}"),
        ["Videos"]    = new("{35286A68-3C57-41A1-BBB1-0EAE73D76C95}"),
    };

    private static readonly Dictionary<string, string> DefaultPaths = new()
    {
        ["Downloads"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        ["Desktop"]   = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
        ["Documents"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"),
        ["Pictures"]  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures"),
        ["Music"]     = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music"),
        ["Videos"]    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos"),
    };

    private record FolderInfo(string GuidKey);

    // 撤销记录
    private record UndoEntry(string Folder, string PreviousPath);

    private List<UndoEntry> _lastOperation = [];

    // ========================================================================
    // P/Invoke
    // ========================================================================
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;
    private const int SHCNF_FLUSH = 0x1000;
    private const int HWND_BROADCAST = 0xFFFF;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam,
        string lParam, int fuFlags, int uTimeout, out IntPtr lpdwResult);

    // ========================================================================

    // 路径文本映射
    private Dictionary<string, TextBlock>? _curPathTexts;

    private Dictionary<string, TextBlock> CurPathTexts => _curPathTexts ??= new()
    {
        ["Downloads"] = TxtCurDownloads, ["Desktop"] = TxtCurDesktop,
        ["Documents"] = TxtCurDocuments, ["Pictures"]  = TxtCurPictures,
        ["Music"]     = TxtCurMusic,     ["Videos"]    = TxtCurVideos,
    };

    public MainWindow()
    {
        InitializeComponent();
        SyncFolderPaths();
        RefreshCurrentPaths();
    }

    // ========================================================================
    // 当前路径刷新
    // ========================================================================
    private void RefreshCurrentPaths()
    {
        var current = GetCurrentPathsFromRegistry();
        foreach (var (folder, tb) in CurPathTexts)
        {
            if (current.TryGetValue(folder, out var path) && path != null)
            {
                var sizeStr = "";
                if (Directory.Exists(path))
                {
                    try
                    {
                        var size = GetFolderSize(path);
                        sizeStr = size > 0 ? $"  ({FormatSize(size)})" : "";
                    }
                    catch { }
                }
                tb.Text = $"当前: {TruncatePath(path)}{sizeStr}";
            }
            else
                tb.Text = "当前: (未设置)";
        }
    }

    /// <summary>递归计算文件夹大小（不含子目录深度以避免阻塞 UI）</summary>
    private static long GetFolderSize(string path)
    {
        long total = 0;
        try
        {
            // 仅统计直接子文件 + 一级子文件夹的大小（代表性，不递归过深）
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
            {
                try { total += new FileInfo(f).Length; } catch { }
            }
            foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
            {
                try { total += new DirectoryInfo(d).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => { try { return f.Length; } catch { return 0L; } }); } catch { }
            }
        }
        catch { }
        return total;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1_024.0:F0} KB",
        _ => $"{bytes} B",
    };

    private Dictionary<string, string?> GetCurrentPathsFromRegistry()
    {
        var result = new Dictionary<string, string?>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: false);
            if (key == null) { foreach (var k in Folders.Keys) result[k] = null; return result; }
            foreach (var (name, info) in Folders)
            {
                var v = key.GetValue(info.GuidKey) as string;
                result[name] = v != null ? Environment.ExpandEnvironmentVariables(v) : null;
            }
        }
        catch { foreach (var k in Folders.Keys) result[k] = null; }
        return result;
    }

    private static string TruncatePath(string path, int maxLen = 42)
        => path.Length <= maxLen ? path : "…" + path[^(maxLen - 1)..];

    private void BtnRefreshPaths_Click(object sender, RoutedEventArgs e) => RefreshCurrentPaths();

    // ========================================================================
    // 盘符 + 路径同步
    // ========================================================================
    private void SyncFolderPaths()
    {
        // 盘符变更时仅更新 UI 显示，目标路径由 GetDriveRoot() 动态计算
    }

    private string GetDriveRoot()
    {
        var text = TxtDriveRoot.Text.Trim();
        if (text.Length == 0) return @"D:\";
        if (text.Length == 1 && char.IsLetter(text[0])) return text + @":\";
        if (text.Length == 2 && text[1] == ':') return text + @"\";
        if (text.EndsWith('\\') && text.Length >= 3) return text;
        if (text.Length >= 2 && text[1] == ':' && !text.EndsWith('\\')) return text + @"\";
        return text;
    }

    private static bool IsValidDrive(string path)
        => path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '\\';

    private void TxtDriveRoot_TextChanged(object sender, TextChangedEventArgs e) => SyncFolderPaths();
    private void BtnBrowseDrive_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderPicker.Show();
        if (folder != null)
        {
            // 提取盘符根路径
            try
            {
                var root = Path.GetPathRoot(folder);
                if (!string.IsNullOrEmpty(root))
                    TxtDriveRoot.Text = root;
            }
            catch { TxtDriveRoot.Text = folder; }
        }
    }

    // ========================================================================
    // 日志
    // ========================================================================
    private void Log(string msg, string level = "info")
    {
        Dispatcher.Invoke(() =>
        {
            var color = level switch
            {
                "success" => Color.FromRgb(0x34, 0xD3, 0x99),
                "error" => Color.FromRgb(0xF8, 0x71, 0x71),
                "warn" => Color.FromRgb(0xFB, 0xBF, 0x24),
                _ => Color.FromRgb(0x94, 0xA3, 0xB8),
            };
            var tr = new TextRange(LogBox.Document.ContentEnd, LogBox.Document.ContentEnd)
            {
                Text = $"[{DateTime.Now:HH:mm:ss}] {msg}\n"
            };
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
            LogBox.ScrollToEnd();
        });
    }

    private void TxtLogToggle_Click(object sender, MouseButtonEventArgs e)
    {
        LogBox.Visibility = LogBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        TxtLogToggle.Text = LogBox.Visibility == Visibility.Visible ? "📋 操作日志 ▴" : "📋 操作日志 ▾";
    }

    // ========================================================================
    // 注册表
    // ========================================================================
    private static bool SetFolderPath(string name, string newPath)
    {
        if (!Folders.TryGetValue(name, out var info)) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: true);
            if (key == null) return false;
            key.SetValue(info.GuidKey, newPath, RegistryValueKind.ExpandString);
            return true;
        }
        catch { return false; }
    }

    // ========================================================================
    // 系统刷新
    // ========================================================================
    private static void RefreshSystem()
    {
        try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero); }
        catch { }
        try { SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 5000, out _); }
        catch { }
        try
        {
            var kill = Process.Start(new ProcessStartInfo("taskkill")
            {
                ArgumentList = { "/f", "/fi", $"USERNAME eq {Environment.UserName}", "/im", "explorer.exe" },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            kill?.WaitForExit(3000);
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        }
        catch { }
    }

    // ========================================================================
    // 文件迁移
    // ========================================================================
    private static (int, int) MigrateFolder(string oldPath, string newPath,
        IProgress<(int current, int total, string fileName)>? progress = null)
    {
        int succeeded = 0, failed = 0;
        if (!Directory.Exists(oldPath)) return (0, 0);
        FileSystemInfo[] items;
        try { items = new DirectoryInfo(oldPath).GetFileSystemInfos(); }
        catch { return (0, 1); }
        int total = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            string dst = Path.Combine(newPath, item.Name);
            progress?.Report((i + 1, total, item.Name));
            try
            {
                if (item is DirectoryInfo)
                { if (!Directory.Exists(dst)) succeeded -= CopyDirectory(item.FullName, dst); else succeeded++; }
                else { if (!File.Exists(dst)) { try { File.Copy(item.FullName, dst, overwrite: false); succeeded++; } catch { failed++; } } else succeeded++; }
            }
            catch { failed++; }
        }
        return (succeeded, failed);
    }

    /// <returns>子目录中复制失败的文件数</returns>
    private static int CopyDirectory(string src, string dst)
    {
        int failed = 0;
        Directory.CreateDirectory(dst);
        string[] files;
        try { files = Directory.GetFiles(src); } catch { files = []; }
        foreach (var f in files)
        {
            try { File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: false); }
            catch { failed++; }
        }
        string[] dirs;
        try { dirs = Directory.GetDirectories(src); } catch { dirs = []; }
        foreach (var d in dirs)
        {
            try { failed += CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d))); }
            catch { failed++; }
        }
        return failed;
    }

    // ========================================================================
    // 获取勾选的文件夹
    // ========================================================================
    private List<string> GetCheckedFolders()
    {
        var list = new List<string>();
        if (ChkDownloads.IsChecked == true) list.Add("Downloads");
        if (ChkDesktop.IsChecked == true) list.Add("Desktop");
        if (ChkDocuments.IsChecked == true) list.Add("Documents");
        if (ChkPictures.IsChecked == true) list.Add("Pictures");
        if (ChkMusic.IsChecked == true) list.Add("Music");
        if (ChkVideos.IsChecked == true) list.Add("Videos");
        return list;
    }

    // ========================================================================
    // 一键修改
    // ========================================================================
    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var folders = GetCheckedFolders();
        if (folders.Count == 0)
        {
            MessageBox.Show("请至少选择一个文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var drive = GetDriveRoot();
        if (!IsValidDrive(drive))
        {
            MessageBox.Show($"目标盘符 \"{drive}\" 无效。\n请输入有效的盘符如 D:\\",
                           "无效路径", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDriveRoot.Focus(); TxtDriveRoot.SelectAll();
            return;
        }

        bool copyOnly = ChkCopyOnly.IsChecked == true;

        // ── 操作确认面板 ──
        var currents = GetCurrentPathsFromRegistry();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(copyOnly ? "【仅复制模式 — 不修改注册表】\n" : "");
        sb.AppendLine($"目标盘符: {drive}\n");
        sb.AppendLine("即将执行的操作：\n");
        foreach (var f in folders)
        {
            var cur = currents.GetValueOrDefault(f);
            var tgt = Path.Combine(drive, f);
            sb.AppendLine(copyOnly
                ? $"  📋 {f}:  复制 {cur ?? "?"}  →  {tgt}"
                : $"  🔧 {f}:  {TruncatePath(cur ?? "?")}  →  {tgt}");
        }
        if (ChkMigrate.IsChecked == true)
            sb.AppendLine("\n📦 同时迁移已有文件（复制不删除）");

        // 检查目标盘符剩余空间
        try
        {
            var driveInfo = new DriveInfo(drive[0].ToString());
            if (driveInfo.IsReady)
            {
                sb.AppendLine($"\n💾 目标盘符 {drive[0]}:  可用空间 {FormatSize(driveInfo.AvailableFreeSpace)}");
                if (driveInfo.AvailableFreeSpace < 1_073_741_824) // < 1GB
                    sb.AppendLine("⚠️  可用空间不足 1 GB，请确认！");
            }
        }
        catch { }

        sb.AppendLine("\n⚠️  请确认已备份重要数据！文件以复制方式迁移，原文件不会删除。");
        sb.AppendLine("   注册表修改可通过「撤销上次」按钮恢复。");
        sb.AppendLine("   执行过程中会重启 Windows Explorer（文件资源管理器短暂闪烁）。");
        sb.AppendLine("\n确认执行？");

        if (MessageBox.Show(sb.ToString(), "操作确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        // 保存撤销信息
        _lastOperation = folders
            .Select(f => new UndoEntry(f, currents.GetValueOrDefault(f) ?? DefaultPaths[f]))
            .ToList();

        bool migrate = ChkMigrate.IsChecked == true;

        BtnApply.IsEnabled = false;
        BtnRestore.IsEnabled = false;
        BtnUndo.IsEnabled = false;
        ProgressBar.Value = 0;
        TxtStatus.Text = "正在处理…";
        LogBox.Visibility = Visibility.Visible;
        TxtLogToggle.Text = "📋 操作日志 ▴";

        var worker = new BackgroundWorker();
        worker.DoWork += (_, _) => ApplyChanges(folders, drive, migrate, copyOnly, currents);
        worker.RunWorkerCompleted += (_, _) =>
        {
            BtnApply.IsEnabled = true;
            BtnRestore.IsEnabled = true;
            BtnUndo.IsEnabled = _lastOperation.Count > 0;
            RefreshCurrentPaths();
        };
        worker.RunWorkerAsync();
    }

    private void ApplyChanges(List<string> folders, string drive, bool migrate, bool copyOnly,
        Dictionary<string, string?> currentPaths)
    {
        try
        {
            int total = folders.Count;
            int done = 0;

            foreach (var folder in folders)
            {
                var newPath = Path.Combine(drive, folder);
                Log($"处理: {folder}", "info");
                Directory.CreateDirectory(newPath);
                Log($"  文件夹就绪: {newPath}", "success");

                if (migrate)
                {
                    // 使用注册表实际路径作为源（而非默认路径）
                    var oldPath = currentPaths.GetValueOrDefault(folder) ?? DefaultPaths.GetValueOrDefault(folder);
                    if (oldPath != null && Directory.Exists(oldPath) &&
                        !string.Equals(Path.GetFullPath(oldPath).TrimEnd('\\'),
                                       Path.GetFullPath(newPath).TrimEnd('\\'),
                                       StringComparison.OrdinalIgnoreCase))
                    {
                        var progress = new Progress<(int current, int total, string fileName)>(p =>
                        {
                            if (p.total == 0) return;
                            Report($"📦 迁移 {folder}: {p.fileName}  ({p.current}/{p.total})",
                                   60 + (int)((p.current / (double)p.total) * 15));
                        });
                        var (s, f) = MigrateFolder(oldPath, newPath, progress);
                        Log($"  迁移: 成功 {s}, 失败 {f}", f > 0 ? "warn" : "success");
                    }
                }

                if (!copyOnly)
                {
                    if (!SetFolderPath(folder, newPath))
                        throw new InvalidOperationException($"注册表写入失败: {folder}");
                    Log($"  注册表已更新", "success");
                }
                else
                {
                    Log($"  仅复制 — 跳过注册表", "info");
                }

                done++;
                Report($"{folder} 完成 ({done}/{total})", (int)(done / (double)total * 80) + 10);
            }

            if (!copyOnly)
            {
                Log("刷新系统中…", "info");
                Report("刷新系统中…", 95);
                RefreshSystem();
                Log("系统已刷新 ✓", "success");
            }

            Report("✅ 全部完成！", 100);
            Log("═══════ 完成 ═══════", "success");

            Dispatcher.Invoke(() =>
            {
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                MessageBox.Show(copyOnly
                    ? $"已复制 {folders.Count} 个文件夹到 {drive}（未修改注册表）。"
                    : $"已成功迁移 {folders.Count} 个文件夹到 {drive}。\n更改已即时生效。",
                    "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        catch (Exception ex)
        {
            Log($"错误: {ex.Message}", "error");
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                Report($"❌ 出错: {ex.Message}", 0);
                MessageBox.Show($"操作失败：\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    // ========================================================================
    // 恢复默认
    // ========================================================================
    private void BtnRestore_Click(object sender, RoutedEventArgs e) => RestoreDefaults(GetCheckedFolders());

    private void RestoreDefaults(List<string> folders)
    {
        if (folders.Count == 0)
        {
            MessageBox.Show("请至少选择一个文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("即将恢复以下文件夹到 Windows 默认路径：\n");
        foreach (var f in folders) sb.AppendLine($"  • {f}  →  {DefaultPaths[f]}");
        if (MessageBox.Show(sb.ToString(), "确认恢复", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        var currents = GetCurrentPathsFromRegistry();
        _lastOperation = folders
            .Select(f => new UndoEntry(f, currents.GetValueOrDefault(f) ?? DefaultPaths[f]))
            .ToList();

        BtnApply.IsEnabled = false;
        BtnRestore.IsEnabled = false;
        BtnUndo.IsEnabled = false;
        ProgressBar.Value = 0;
        TxtStatus.Text = "正在恢复…";
        LogBox.Visibility = Visibility.Visible;
        TxtLogToggle.Text = "📋 操作日志 ▴";

        var worker = new BackgroundWorker();
        worker.DoWork += (_, _) =>
        {
            try
            {
                int done = 0;
                foreach (var folder in folders)
                {
                    var dp = DefaultPaths[folder];
                    Log($"恢复: {folder} → {dp}", "info");
                    if (!SetFolderPath(folder, dp))
                        throw new InvalidOperationException($"注册表写入失败: {folder}");
                    Log($"  已恢复", "success");
                    done++;
                    Report($"已恢复 {done}/{folders.Count}", (int)(done / (double)folders.Count * 80) + 10);
                }
                RefreshSystem();
                Report("✅ 已恢复默认路径！", 100);
                Log("═══════ 完成 ═══════", "success");
                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                    MessageBox.Show($"已恢复 {folders.Count} 个文件夹。", "恢复成功", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Log($"错误: {ex.Message}", "error");
                Dispatcher.Invoke(() =>
                {
                    Report($"❌ 出错: {ex.Message}", 0);
                    MessageBox.Show($"恢复失败：\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        };
        worker.RunWorkerCompleted += (_, _) =>
        {
            BtnApply.IsEnabled = true;
            BtnRestore.IsEnabled = true;
            BtnUndo.IsEnabled = _lastOperation.Count > 0;
            RefreshCurrentPaths();
        };
        worker.RunWorkerAsync();
    }

    // ========================================================================
    // 撤销上次
    // ========================================================================
    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOperation.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("即将撤销上次操作：\n");
        foreach (var entry in _lastOperation)
            sb.AppendLine($"  ↩ {entry.Folder}  →  {entry.PreviousPath}");
        if (MessageBox.Show(sb.ToString(), "确认撤销", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        var undoList = new List<UndoEntry>(_lastOperation);
        // 不在此清空 _lastOperation —— 等全部撤销成功后再清空，失败则可重试

        BtnApply.IsEnabled = false;
        BtnRestore.IsEnabled = false;
        BtnUndo.IsEnabled = false;
        ProgressBar.Value = 0;
        TxtStatus.Text = "正在撤销…";

        var worker = new BackgroundWorker();
        worker.DoWork += (_, _) =>
        {
            try
            {
                int done = 0;
                foreach (var entry in undoList)
                {
                    Log($"撤销: {entry.Folder} → {entry.PreviousPath}", "info");
                    if (!SetFolderPath(entry.Folder, entry.PreviousPath))
                        throw new InvalidOperationException($"注册表写入失败: {entry.Folder}");
                    done++;
                    Report($"已撤销 {done}/{undoList.Count}", (int)(done / (double)undoList.Count * 80) + 10);
                }
                RefreshSystem();
                Report("✅ 已撤销！", 100);
                Log("═══════ 撤销完成 ═══════", "success");

                // 全部成功后清空撤销记录
                Dispatcher.Invoke(() => _lastOperation = []);

                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                    MessageBox.Show("已撤销上次操作。", "撤销成功", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Log($"错误: {ex.Message}", "error");
                Dispatcher.Invoke(() =>
                {
                    Report($"❌ 出错: {ex.Message}", 0);
                    MessageBox.Show($"撤销失败：\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        };
        worker.RunWorkerCompleted += (_, _) =>
        {
            BtnApply.IsEnabled = true;
            BtnRestore.IsEnabled = true;
            BtnUndo.IsEnabled = _lastOperation.Count > 0;
            RefreshCurrentPaths();
        };
        worker.RunWorkerAsync();
    }

    // ========================================================================
    // 导出 / 导入配置
    // ========================================================================
    private record Config(string Drive, List<string> Folders, bool Migrate, bool CopyOnly);

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON 配置文件|*.json",
            FileName = "DownloadPathMover_Config.json",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog() != true) return;

        var config = new Config(
            TxtDriveRoot.Text.Trim(),
            GetCheckedFolders(),
            ChkMigrate.IsChecked == true,
            ChkCopyOnly.IsChecked == true);

        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        Log($"配置已导出: {dlg.FileName}", "success");
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON 配置文件|*.json",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var config = JsonSerializer.Deserialize<Config>(json);
            if (config == null || config.Folders == null)
                throw new Exception("配置文件格式无效或缺少必要字段");

            TxtDriveRoot.Text = config.Drive;
            ChkDownloads.IsChecked  = config.Folders.Contains("Downloads");
            ChkDesktop.IsChecked    = config.Folders.Contains("Desktop");
            ChkDocuments.IsChecked  = config.Folders.Contains("Documents");
            ChkPictures.IsChecked   = config.Folders.Contains("Pictures");
            ChkMusic.IsChecked      = config.Folders.Contains("Music");
            ChkVideos.IsChecked     = config.Folders.Contains("Videos");
            ChkMigrate.IsChecked    = config.Migrate;
            ChkCopyOnly.IsChecked   = config.CopyOnly;
            SyncFolderPaths();
            RefreshCurrentPaths();
            Log($"配置已导入: {dlg.FileName}", "success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ========================================================================
    // UI 辅助
    // ========================================================================
    private void Report(string text, int pct)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = text;
            ProgressBar.Value = pct;
        });
    }

    private void BtnQuit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
