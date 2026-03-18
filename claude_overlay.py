"""Claude使用量オーバーレイ"""
import tkinter as tk
import ctypes
import json
import os
import subprocess
import math
from datetime import datetime, timezone

DIR = os.path.dirname(__file__)
DATA_FILE = os.path.join(DIR, 'usage_data.json')
STATUS_FILE = os.path.join(DIR, 'fetch_status.txt')
FETCHER = os.path.join(DIR, 'claude_fetcher.py')

REFRESH_MS = 5000
AUTO_INTERVAL = 300

T = "#010101"


class MiniInvader(tk.Canvas):
    """Clawdインベーダー — 7x7、足2本歩行アニメ"""
    FRAME_A = [
        [0,1,0,0,0,1,0],  # アンテナ
        [1,1,1,1,1,1,1],  # 頭
        [1,2,1,1,1,2,1],  # 目
        [1,1,1,1,1,1,1],  # ボディ
        [1,1,1,1,1,1,1],  # ボディ
        [1,1,0,0,0,1,1],  # 足（まっすぐ）
        [1,1,0,0,0,1,1],
    ]
    FRAME_B = [
        [0,1,0,0,0,1,0],  # アンテナ
        [1,1,1,1,1,1,1],  # 頭
        [1,2,1,1,1,2,1],  # 目
        [1,1,1,1,1,1,1],  # ボディ
        [1,1,1,1,1,1,1],  # ボディ
        [0,1,1,0,1,1,0],  # 足（開く）
        [0,1,1,0,1,1,0],
    ]

    def __init__(self, master, px=4, color_body="#cc6644", color_eye="#ffcc99",
                 bg_color="#010101", interval=300, **kwargs):
        self.px = px
        self.cols = 7
        self.rows = 7
        size_w = self.cols * px
        size_h = self.rows * px
        super().__init__(master, width=size_w, height=size_h,
                         bg=bg_color, highlightthickness=0, **kwargs)
        self.color_body = color_body
        self.base_body = color_body
        self.color_eye = color_eye
        self.bg_color = bg_color
        self.interval = interval
        self.frame_idx = 0
        self.running = False
        self.cells = {}
        self._build()

    def _build(self):
        grid = self.FRAME_A
        for ri, row in enumerate(grid):
            for ci, val in enumerate(row):
                x0 = ci * self.px
                y0 = ri * self.px
                color = self._cell_color(val)
                oid = self.create_rectangle(
                    x0, y0, x0 + self.px, y0 + self.px,
                    fill=color, outline=""
                )
                self.cells[(ri, ci)] = oid

    def _cell_color(self, val):
        if val == 1: return self.color_body
        if val == 2: return self.color_eye
        return self.bg_color

    def _update_frame(self):
        grid = self.FRAME_A if self.frame_idx == 0 else self.FRAME_B
        for ri, row in enumerate(grid):
            for ci, val in enumerate(row):
                self.itemconfig(self.cells[(ri, ci)], fill=self._cell_color(val))

    def _tick(self):
        if not self.running:
            return
        self.frame_idx ^= 1
        self._update_frame()
        self.after(self.interval, self._tick)

    def start(self, speed="normal"):
        if speed == "slow":
            self.interval = 500
            self.color_body = self.base_body
        elif speed == "fast":
            self.interval = 120
            self.color_body = "#FFFFFF"
        else:
            self.interval = 300
            self.color_body = self.base_body
        self._update_frame()
        if not self.running:
            self.running = True
            self._tick()

    def stop(self):
        """停止＋グレーアウト"""
        self.running = False
        self.frame_idx = 0
        self.color_body = "#555555"
        self.color_eye = "#777777"
        self._update_frame()

    def idle(self):
        """アイドル = ゆっくりパタパタ（常にalive感）"""
        self.interval = 800
        self.color_body = self.base_body
        self.color_eye = "#FFFFFF"
        self._update_frame()
        if not self.running:
            self.running = True
            self._tick()


# 明るくて映える色
WHITE   = "#F0F0F0"
CYAN    = "#55DDFF"
AMBER   = "#FFCC44"
CORAL   = "#FF7788"
MINT    = "#55FFAA"
MUTED   = "#999999"
BAR_BG  = "#444444"


def get_color(pct):
    if pct is None: return MUTED
    if pct >= 80:   return CORAL
    if pct >= 50:   return AMBER
    return CYAN


def format_reset(resets_at):
    if not resets_at: return ""
    try:
        reset_dt = datetime.fromisoformat(resets_at)
        now = datetime.now(timezone.utc)
        sec = int((reset_dt - now).total_seconds())
        if sec <= 0: return "reset"
        h, m = sec // 3600, (sec % 3600) // 60
        if h > 24: return f"{h//24}d{h%24:02d}h"
        if h > 0:  return f"{h}h{m:02d}m"
        return f"{m}m"
    except:
        return ""


def read_usage():
    try:
        with open(DATA_FILE, 'r', encoding='utf-8') as f:
            return json.load(f)
    except:
        return None


def read_status():
    try:
        with open(STATUS_FILE, 'r') as f:
            return f.read().strip()
    except:
        return ""


class Overlay:
    def __init__(self):
        self.root = tk.Tk()
        self.auto_running = False
        self.auto_timer = None
        self.fetch_proc = None

        user32 = ctypes.windll.user32
        sw = user32.GetSystemMetrics(0)
        sh = user32.GetSystemMetrics(1)

        w, h = 200, 26
        x = sw - w - 220
        y = sh - h

        self.root.overrideredirect(True)
        self.root.wm_attributes("-topmost", True)
        self.root.configure(bg=T)
        self.root.wm_attributes("-transparentcolor", T)
        self.root.geometry(f"{w}x{h}+{x}+{y}")

        font = ("Consolas", 8, "bold")

        # 行のY座標（2行を詰めて配置）
        row_h = 13
        y0 = 0      # 5h行
        y1 = row_h   # 7d行

        # レイアウトX座標
        grip_x = 0
        lbl_x = 12
        bar_x = 28
        bar_w = 65
        pct_x = 96
        rst_x = 132
        inv_x = 175

        # グリップ（Canvasドットパターン）
        self.grip = tk.Canvas(self.root, width=10, height=h,
            bg=T, highlightthickness=0, cursor="fleur")
        self.grip.place(x=grip_x, y=0)
        dot_r = 1.5
        for dx in [3, 7]:
            for dy in range(4, h - 2, 5):
                self.grip.create_oval(dx-dot_r, dy-dot_r, dx+dot_r, dy+dot_r,
                    fill=CYAN, outline="")

        # ── 5h行 ──
        self.lbl_5h = tk.Label(self.root, text="5h", font=font, fg=MUTED, bg=T, anchor="w", pady=0, padx=0)
        self.lbl_5h.place(x=lbl_x, y=y0, height=row_h)

        bar1 = tk.Frame(self.root, bg=BAR_BG, height=5, width=bar_w)
        bar1.place(x=bar_x, y=y0+4, width=bar_w, height=5)
        self.bar_5h = tk.Frame(bar1, bg=CYAN, height=5)
        self.bar_5h.place(x=0, y=0, relwidth=0, relheight=1)

        self.pct_5h = tk.Label(self.root, text="--%", font=font, fg=MUTED, bg=T, anchor="e", pady=0, padx=0)
        self.pct_5h.place(x=pct_x, y=y0, width=35, height=row_h)

        self.rst_5h_shadow = tk.Label(self.root, text="", font=font, fg="#222222", bg=T, anchor="e", pady=0, padx=0)
        self.rst_5h_shadow.place(x=rst_x+1, y=y0+1, height=row_h)
        self.rst_5h = tk.Label(self.root, text="", font=font, fg="#BBBBBB", bg=T, anchor="e", pady=0, padx=0)
        self.rst_5h.place(x=rst_x, y=y0, height=row_h)

        # ── 7d行 ──
        self.lbl_7d = tk.Label(self.root, text="7d", font=font, fg=MUTED, bg=T, anchor="w", pady=0, padx=0)
        self.lbl_7d.place(x=lbl_x, y=y1, height=row_h)

        bar2 = tk.Frame(self.root, bg=BAR_BG, height=5, width=bar_w)
        bar2.place(x=bar_x, y=y1+4, width=bar_w, height=5)
        self.bar_7d = tk.Frame(bar2, bg=CYAN, height=5)
        self.bar_7d.place(x=0, y=0, relwidth=0, relheight=1)

        self.pct_7d = tk.Label(self.root, text="--%", font=font, fg=MUTED, bg=T, anchor="e", pady=0, padx=0)
        self.pct_7d.place(x=pct_x, y=y1, width=35, height=row_h)

        self.rst_7d_shadow = tk.Label(self.root, text="", font=font, fg="#222222", bg=T, anchor="e", pady=0, padx=0)
        self.rst_7d_shadow.place(x=rst_x+1, y=y1+1, height=row_h)
        self.rst_7d = tk.Label(self.root, text="", font=font, fg="#BBBBBB", bg=T, anchor="e", pady=0, padx=0)
        self.rst_7d.place(x=rst_x, y=y1, height=row_h)

        # インベーダー
        self.spinner = MiniInvader(self.root, px=3,
            color_body=CYAN, color_eye="#FFFFFF", bg_color=T)
        self.spinner.place(x=inv_x, y=(h-21)//2)

        # ── イベント ──
        self.grip.bind("<ButtonPress-1>", self._start_drag)
        self.grip.bind("<B1-Motion>", self._do_drag)

        # メニュー用の非透過トップレベル
        self.menu = tk.Menu(self.root, tearoff=0, bg="#1E1E1E", fg=WHITE,
            activebackground="#3A3A3A", activeforeground=WHITE,
            font=("Consolas", 10), bd=1, relief="solid",
            selectcolor=WHITE)
        self.menu.add_command(label="Scan Now", command=self._scan_now)
        self.menu.add_command(label="Auto: OFF", command=self._toggle_auto)
        self.menu.add_command(label="Session Key...", command=self._edit_key)
        self.menu.add_separator()
        self.menu.add_command(label="Exit", command=self._quit)

        for widget in [self.grip, self.lbl_5h, self.pct_5h, self.rst_5h,
                       self.lbl_7d, self.pct_7d, self.rst_7d, self.spinner]:
            widget.bind("<Button-3>", self._show_menu)

        self._keep_on_top()
        self._update_display()
        self._poll_status()

    def _start_drag(self, e):
        self._dx, self._dy = e.x, e.y
    def _do_drag(self, e):
        nx = self.root.winfo_x() + e.x - self._dx
        ny = self.root.winfo_y() + e.y - self._dy
        self.root.geometry(f"+{nx}+{ny}")

    def _show_menu(self, e):
        lbl = "Auto: ON → OFF" if self.auto_running else "Auto: OFF → ON"
        self.menu.entryconfigure(1, label=lbl)
        self._menu_open = True
        self.menu.post(e.x_root, e.y_root)
        self._menu_open = False

    def _keep_on_top(self):
        if not getattr(self, '_menu_open', False):
            self.root.lift()
            self.root.wm_attributes("-topmost", True)
        self.root.after(500, self._keep_on_top)

    def _scan_now(self):
        """トリガーファイルを作成 → 常駐フェッチャーが検知して取得"""
        trigger = os.path.join(DIR, 'scan_trigger')
        with open(trigger, 'w') as f:
            f.write('scan')
        self.spinner.start(speed="fast")

    def _edit_key(self):
        """セッションキー再設定ダイアログ"""
        from setup_dialog import ask_session_key, load_session_key, save_session_key
        key = ask_session_key(title="Session Key 変更", current_key=load_session_key())
        if key:
            save_session_key(key)
            self._scan_now()

    def _toggle_auto(self):
        """オーバーレイ側のAuto表示切替"""
        self.auto_running = not self.auto_running
        if self.auto_running:
            self.spinner.idle()
        else:
            self.spinner.stop()

    def _update_display(self):
        data = read_usage()
        if data:
            fh_d = data.get("five_hour", {})
            sd_d = data.get("seven_day", {})
            fh = fh_d.get("utilization")
            sd = sd_d.get("utilization")
            fr = format_reset(fh_d.get("resets_at"))
            sr = format_reset(sd_d.get("resets_at"))

            if fh is not None:
                c = get_color(fh)
                self.bar_5h.place(x=0, y=0, relwidth=fh/100, relheight=1)
                self.bar_5h.config(bg=c)
                self.pct_5h.config(text=f"{fh:.0f}%", fg=c)
                self.rst_5h.config(text=fr)
                self.rst_5h_shadow.config(text=fr)
                self.lbl_5h.config(fg=c)

            if sd is not None:
                c = get_color(sd)
                self.bar_7d.place(x=0, y=0, relwidth=sd/100, relheight=1)
                self.bar_7d.config(bg=c)
                self.pct_7d.config(text=f"{sd:.0f}%", fg=c)
                self.rst_7d.config(text=sr)
                self.rst_7d_shadow.config(text=sr)
                self.lbl_7d.config(fg=c)

        self.root.after(REFRESH_MS, self._update_display)

    def _poll_status(self):
        s = read_status()
        if s in ("FETCHING", "LOGGING_IN", "STARTING"):
            self.spinner.start(speed="fast")
        elif s.startswith("OK"):
            if self.auto_running:
                self.spinner.idle()
            else:
                self.spinner.stop()
        elif s.startswith("ERROR"):
            if self.auto_running:
                self.spinner.idle()
            else:
                self.spinner.stop()

        self.root.after(2000, self._poll_status)

    def _quit(self):
        self.root.destroy()

    def run(self):
        self.root.mainloop()


if __name__ == "__main__":
    Overlay().run()
