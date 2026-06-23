using System.Windows;

namespace DownloadMoverWpf;

public partial class App : Application
{
    /// <summary>单实例互斥锁 — 存为静态字段防止被 GC 回收。</summary>
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例检查 — Mutex 必须在整个应用生命周期内保持存活
        _singleInstanceMutex = new Mutex(true, @"Global\DownloadPathMover_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            MessageBox.Show("DownloadPathMover 已经在运行中。",
                            "程序已在运行", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 管理员权限检查（兜底 — app.manifest 已声明 requireAdministrator）
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
        {
            MessageBox.Show("此工具需要管理员权限。\n请右键 →「以管理员身份运行」。",
                            "需要管理员权限", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }
}
