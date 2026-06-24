#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
================================================================================
 一键修改 Windows 默认下载路径到 D:\Downloads
 One-Click Windows Download Folder Mover to D:\Downloads
================================================================================

 技术栈: Python + Tkinter (ttk 主题) + winreg + ctypes
 Tkinter 是 Python 标准库，无需安装任何第三方包。

 许可: MIT License
================================================================================
"""

import os
import sys
import shutil
import ctypes
import threading
import winreg
import tkinter as tk
from tkinter import ttk, messagebox

# ============================================================================
# 常量
# ============================================================================

NEW_DOWNLOAD_PATH = r"D:\Downloads"
REG_KEY_PATH = r"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"
REG_VALUE_NAMES = [
    "{374DE290-123F-4565-9164-39C4925E467B}",
    "Downloads",
]
DEFAULT_ENABLE_MIGRATION = False

SHCNE_ASSOCCHANGED = 0x08000000
SHCNF_IDLIST = 0x0000
SHCNF_FLUSH = 0x1000
HWND_BROADCAST = 0xFFFF
WM_SETTINGCHANGE = 0x001A


# ============================================================================
# 工具函数
# ============================================================================

def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False


def elevate_to_admin():
    if is_admin():
        return
    ret = ctypes.windll.shell32.ShellExecuteW(
        None, "runas", sys.executable,
        f'"{sys.argv[0]}"', None, 1)
    if ret <= 32:
        ctypes.windll.user32.MessageBoxW(
            0,
            "此工具需要管理员权限才能修改注册表。\n请在 UAC 对话框中点击「是」。",
            "需要管理员权限", 0x00000030)
    sys.exit(0 if ret > 32 else 1)


def get_current_download_path():
    try:
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, REG_KEY_PATH, 0, winreg.KEY_READ)
        for name in REG_VALUE_NAMES:
            try:
                value, _ = winreg.QueryValueEx(key, name)
                if value and os.path.isabs(value):
                    winreg.CloseKey(key)
                    return os.path.expandvars(value)
            except FileNotFoundError:
                continue
        winreg.CloseKey(key)
    except Exception:
        pass
    return None


def set_download_path(new_path):
    try:
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, REG_KEY_PATH, 0,
                             winreg.KEY_WRITE | winreg.KEY_READ)
        for name in REG_VALUE_NAMES:
            winreg.SetValueEx(key, name, 0, winreg.REG_EXPAND_SZ, new_path)
        winreg.CloseKey(key)
        return True
    except Exception:
        return False


def refresh_system():
    try:
        ctypes.windll.shell32.SHChangeNotify(
            SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, None, None)
    except Exception:
        pass
    try:
        ctypes.windll.user32.SendMessageTimeoutW(
            HWND_BROADCAST, WM_SETTINGCHANGE, 0, "Environment",
            0x0002, 5000, ctypes.byref(ctypes.c_ulong()))
    except Exception:
        pass
    try:
        import subprocess
        subprocess.run(["taskkill", "/f", "/im", "explorer.exe"],
                       capture_output=True)
        subprocess.Popen(["explorer.exe"],
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception:
        pass


def migrate_files(old_path, new_path, progress_callback=None):
    succeeded, failed = 0, 0
    if not os.path.exists(old_path):
        return 0, 0
    items = os.listdir(old_path)
    total = len(items)
    for i, item in enumerate(items):
        src = os.path.join(old_path, item)
        dst = os.path.join(new_path, item)
        if progress_callback:
            progress_callback(i + 1, total)
        try:
            if os.path.isdir(src):
                if not os.path.exists(dst):
                    shutil.copytree(src, dst)
            else:
                if not os.path.exists(dst):
                    shutil.copy2(src, dst)
            succeeded += 1
        except Exception:
            failed += 1
    return succeeded, failed


# ============================================================================
# 主应用
# ============================================================================

class App:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("下载路径一键修改工具 — Download Folder Mover")
        self.root.geometry("560x480")
        self.root.resizable(False, False)
        self.root.configure(bg="#f0f2f5")

        # 居中
        self.root.update_idletasks()
        sw, sh = self.root.winfo_screenwidth(), self.root.winfo_screenheight()
        w, h = self.root.winfo_width(), self.root.winfo_height()
        self.root.geometry(f"+{(sw - w) // 2}+{(sh - h) // 2}")

        self.old_path = None
        self.worker_thread = None
        self._setup_style()
        self._build_ui()
        self._load_current()

    # ── ttk 主题 ──────────────────────────────────────────────────────────

    def _setup_style(self):
        style = ttk.Style(self.root)
        # 尝试使用现代主题
        available = style.theme_names()
        if "vista" in available:
            style.theme_use("vista")
        elif "winnative" in available:
            style.theme_use("winnative")

        # 自定义样式
        style.configure("Title.TLabel",
                        font=("Microsoft YaHei UI", 15, "bold"),
                        foreground="#1a1a2e", background="#f0f2f5")
        style.configure("Subtitle.TLabel",
                        font=("Microsoft YaHei UI", 9),
                        foreground="#6b7280", background="#f0f2f5")
        style.configure("Card.TLabelframe",
                        background="white", relief="solid",
                        borderwidth=1, padding=14)
        style.configure("Card.TLabelframe.Label",
                        font=("Microsoft YaHei UI", 10, "bold"),
                        foreground="#374151", background="white")
        style.configure("Path.TLabel",
                        font=("Consolas", 10),
                        foreground="#374151", background="#f9fafb",
                        padding=10, anchor="w")
        style.configure("TargetPath.TLabel",
                        font=("Consolas", 10, "bold"),
                        foreground="#2563eb", background="#eff6ff",
                        padding=10, anchor="w")
        style.configure("Status.TLabel",
                        font=("Microsoft YaHei UI", 9),
                        foreground="#9ca3af", background="#f0f2f5")
        style.configure("SuccessStatus.TLabel",
                        font=("Microsoft YaHei UI", 9),
                        foreground="#059669", background="#f0f2f5")
        style.configure("ErrorStatus.TLabel",
                        font=("Microsoft YaHei UI", 9),
                        foreground="#dc2626", background="#f0f2f5")
        style.configure("Footer.TLabel",
                        font=("Microsoft YaHei UI", 8),
                        foreground="#ef4444", background="#fef2f2",
                        padding=6)
        style.configure("Apply.TButton",
                        font=("Microsoft YaHei UI", 11, "bold"),
                        padding=12)
        style.configure("Success.TButton",
                        font=("Microsoft YaHei UI", 11, "bold"),
                        padding=12)
        style.configure("Quit.TButton",
                        font=("Microsoft YaHei UI", 10),
                        padding=10, foreground="#6b7280")
        style.configure("Migrate.TCheckbutton",
                        font=("Microsoft YaHei UI", 9),
                        foreground="#374151", background="white")

    # ── UI 布局 ───────────────────────────────────────────────────────────

    def _build_ui(self):
        pad = {"padx": 26, "pady": 0}
        container = tk.Frame(self.root, bg="#f0f2f5")
        container.pack(fill="both", expand=True)

        # 标题
        ttk.Label(container, text="📁  一键修改下载路径到 D 盘",
                  style="Title.TLabel").pack(pady=(22, 0), **pad)

        ttk.Label(container, text="将 Windows 默认下载文件夹从 C 盘迁移到 D:\\Downloads，即时生效",
                  style="Subtitle.TLabel").pack(pady=(4, 18), **pad)

        # ---- 当前状态卡片 ----
        card1 = ttk.Labelframe(container, text="  📋  当前状态  ",
                               style="Card.TLabelframe")
        card1.pack(fill="x", pady=(0, 10), **pad)

        self.lbl_current = ttk.Label(card1, text="正在读取…",
                                     style="Path.TLabel")
        self.lbl_current.pack(fill="x")

        # ---- 目标路径卡片 ----
        card2 = ttk.Labelframe(container, text="  🎯  目标路径  ",
                               style="Card.TLabelframe")
        card2.pack(fill="x", pady=(0, 10), **pad)

        ttk.Label(card2, text=NEW_DOWNLOAD_PATH,
                  style="TargetPath.TLabel").pack(fill="x")

        # ---- 选项卡片 ----
        card3 = ttk.Labelframe(container, text="  ⚙️  选项  ",
                               style="Card.TLabelframe")
        card3.pack(fill="x", pady=(0, 14), **pad)

        self.chk_migrate = tk.BooleanVar(value=DEFAULT_ENABLE_MIGRATION)
        ttk.Checkbutton(card3, text="将原有下载文件迁移到新路径（复制，不删除原文件）",
                        variable=self.chk_migrate,
                        style="Migrate.TCheckbutton").pack(anchor="w")

        # ---- 进度条 ----
        self.progress = ttk.Progressbar(container, mode="determinate")
        self.progress.pack(fill="x", pady=(0, 2), **pad)

        # ---- 状态 ----
        self.lbl_status = ttk.Label(container, text="",
                                    style="Status.TLabel")
        self.lbl_status.pack(pady=(0, 14), **pad)

        # ---- 按钮 ----
        btn_frame = tk.Frame(container, bg="#f0f2f5")
        btn_frame.pack(pady=(0, 10))

        self.btn_apply = tk.Button(
            btn_frame, text="🚀   一 键 修 改",
            font=("Microsoft YaHei UI", 11, "bold"),
            bg="#3b82f6", fg="white", activebackground="#2563eb",
            activeforeground="white", relief="flat",
            padx=28, pady=10, cursor="hand2", bd=0,
            command=self._on_apply)
        self.btn_apply.pack(side="left", padx=6)

        self.btn_quit = tk.Button(
            btn_frame, text="退出",
            font=("Microsoft YaHei UI", 10),
            bg="#e5e7eb", fg="#374151", activebackground="#d1d5db",
            relief="flat", padx=24, pady=10, cursor="hand2", bd=0,
            command=self.root.destroy)
        self.btn_quit.pack(side="left", padx=6)

        # ---- 页脚 ----
        footer_frame = tk.Frame(container, bg="#fef2f2")
        footer_frame.pack(fill="x", side="bottom", pady=(0, 12), **pad)
        ttk.Label(footer_frame,
                  text="⚠️  修改注册表前请确认已备份重要数据。本工具仅修改当前用户设置。",
                  style="Footer.TLabel").pack(fill="x")

        # 让 container 撑满
        container.pack_propagate(False)

    # ── 加载当前设置 ──────────────────────────────────────────────────────

    def _load_current(self):
        try:
            self.old_path = get_current_download_path()
            if self.old_path:
                self.lbl_current.config(text=self.old_path, style="Path.TLabel")
                # 检查是否已经是目标路径
                try:
                    if os.path.normcase(os.path.normpath(self.old_path)) == \
                       os.path.normcase(os.path.normpath(NEW_DOWNLOAD_PATH)):
                        self.lbl_current.config(
                            text=self.old_path, style="TargetPath.TLabel")
                        self.lbl_status.config(
                            text="✅ 当前下载路径已是 D:\\Downloads，无需修改。",
                            style="SuccessStatus.TLabel")
                        self.btn_apply.config(
                            text="✅  已完成", bg="#10b981",
                            activebackground="#059669", state="disabled")
                except Exception:
                    pass
            else:
                self.lbl_current.config(text="（无法读取注册表）")
        except Exception as e:
            self.lbl_current.config(text=f"（读取失败: {e}）")

    # ── 一键修改 ──────────────────────────────────────────────────────────

    def _on_apply(self):
        if not messagebox.askokcancel(
            "确认修改",
            f"即将把下载路径修改为：\n\n    {NEW_DOWNLOAD_PATH}\n\n"
            f"当前路径：{self.old_path or '(未知)'}\n\n是否继续？",
            icon="warning"):
            return

        self.btn_apply.config(state="disabled", text="⏳  正在执行…",
                              bg="#9ca3af", activebackground="#9ca3af")
        self.lbl_status.config(text="正在处理，请稍候…", style="Status.TLabel")
        self.progress["value"] = 0

        def task():
            try:
                self._report("正在创建目标文件夹…", 10)
                os.makedirs(NEW_DOWNLOAD_PATH, exist_ok=True)

                if self.chk_migrate.get() and self.old_path:
                    try:
                        same = os.path.exists(self.old_path) and \
                               os.path.exists(NEW_DOWNLOAD_PATH) and \
                               os.path.samefile(self.old_path, NEW_DOWNLOAD_PATH)
                    except (OSError, Exception):
                        same = False

                    if os.path.exists(self.old_path) and not same:
                        def cb(cur, tot):
                            pct = 20 + int((cur / tot) * 40)
                            self._report(f"正在迁移文件… {cur}/{tot}", pct)
                        s, f = migrate_files(self.old_path, NEW_DOWNLOAD_PATH, cb)
                        self._report(f"迁移完成：成功 {s} 项，失败 {f} 项", 60)
                    else:
                        self._report("跳过迁移（路径相同或不存在）", 60)
                else:
                    self._report("已跳过文件迁移", 60)

                self._report("正在修改注册表…", 75)
                if not set_download_path(NEW_DOWNLOAD_PATH):
                    raise RuntimeError("注册表写入失败，请确认以管理员权限运行。")

                self._report("正在刷新系统…", 90)
                refresh_system()

                self._report("✅ 修改成功！下载路径已变更至 D:\\Downloads", 100,
                             is_success=True)
                self.root.after(0, self._on_done_success)

            except Exception as e:
                self.root.after(0, self._on_done_error, str(e))

        self.worker_thread = threading.Thread(target=task, daemon=True)
        self.worker_thread.start()

    def _report(self, text, pct, is_success=False):
        style = "SuccessStatus.TLabel" if is_success else "Status.TLabel"
        self.root.after(0, lambda: self.lbl_status.config(text=text, style=style))
        self.root.after(0, lambda: self.progress.configure(value=pct))

    def _on_done_success(self):
        self.lbl_current.config(text=NEW_DOWNLOAD_PATH, style="TargetPath.TLabel")
        self.btn_apply.config(text="✅  已完成", bg="#10b981",
                              activebackground="#059669", state="disabled")
        messagebox.showinfo("修改成功",
                            f"下载文件夹路径已成功修改为：\n\n    {NEW_DOWNLOAD_PATH}\n\n"
                            "更改已即时生效。你可以打开「此电脑 → 下载」验证。")

    def _on_done_error(self, msg):
        self.lbl_status.config(text=f"❌ 出错: {msg}", style="ErrorStatus.TLabel")
        self.btn_apply.config(state="normal", text="🚀   一 键 修 改",
                              bg="#3b82f6", activebackground="#2563eb")
        messagebox.showerror("操作失败", f"修改过程中发生错误：\n\n{msg}")

    def run(self):
        self.root.mainloop()


# ============================================================================
# 入口
# ============================================================================

def main():
    # 全局异常钩子
    def _hook(exc_type, exc_value, exc_tb):
        import traceback
        err = "".join(traceback.format_exception(exc_type, exc_value, exc_tb))
        try:
            ctypes.windll.user32.MessageBoxW(
                0, f"程序发生未处理的异常：\n\n{err}",
                f"错误 - {exc_type.__name__}", 0x00000010)
        except Exception:
            pass
        sys.__excepthook__(exc_type, exc_value, exc_tb)
    sys.excepthook = _hook

    if not is_admin():
        elevate_to_admin()

    app = App()
    app.run()


if __name__ == "__main__":
    main()
