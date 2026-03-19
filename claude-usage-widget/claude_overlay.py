"""Claude usage overlay widget."""

import ctypes
import json
import os
import threading
import tkinter as tk
import winreg
from datetime import datetime, timezone

try:
    ctypes.windll.shcore.SetProcessDpiAwareness(1)
except Exception:
    ctypes.windll.user32.SetProcessDPIAware()

DIR = os.path.dirname(__file__)
DATA_FILE = os.path.join(DIR, "usage_data.json")
STATUS_FILE = os.path.join(DIR, "fetch_status.txt")
CONFIG_FILE = os.path.join(DIR, "overlay_config.json")

REFRESH_MS = 5000
TRANSPARENT = "#010101"
DEFAULT_THEME = "auto"
FALLBACK_THEME = "dark_bg"

THEMES = {
    "dark_bg": {
        "accent": "#55DDFF",
        "muted": "#999999",
        "bar_bg": "#444444",
        "usage_low": "#55DDFF",
        "usage_mid": "#FFCC44",
        "usage_high": "#FF7788",
        "bar_low": "#55DDFF",
        "bar_mid": "#FFCC44",
        "bar_high": "#FF7788",
        "reset_text": "#BBBBBB",
        "reset_shadow": "#222222",
        "menu_bg": "#1E1E1E",
        "menu_fg": "#F0F0F0",
        "menu_active_bg": "#3A3A3A",
        "menu_active_fg": "#F0F0F0",
        "spinner_body": "#55DDFF",
        "spinner_eye": "#FFFFFF",
        "spinner_fast_body": "#FFFFFF",
        "spinner_idle_eye": "#FFFFFF",
        "spinner_stop_body": "#555555",
        "spinner_stop_eye": "#777777",
    },
    "light_bg": {
        "accent": "#A0522D",
        "muted": "#000000",
        "bar_bg": "#D9C4B0",
        "usage_low": "#000000",
        "usage_mid": "#000000",
        "usage_high": "#000000",
        "bar_low": "#A0522D",
        "bar_mid": "#C41E3A",
        "bar_high": "#8B0000",
        "reset_text": "#000000",
        "reset_shadow": "#010101",
        "menu_bg": "#FAF5F0",
        "menu_fg": "#3D2B1F",
        "menu_active_bg": "#EDE0D4",
        "menu_active_fg": "#3D2B1F",
        "spinner_body": "#C2714F",
        "spinner_eye": "#FFFFFF",
        "spinner_fast_body": "#3D2B1F",
        "spinner_idle_eye": "#FFFFFF",
        "spinner_stop_body": "#C4A882",
        "spinner_stop_eye": "#D9C4B0",
    },
}


class MiniInvader(tk.Canvas):
    FRAME_A = [
        [0, 1, 0, 0, 0, 1, 0],
        [1, 1, 1, 1, 1, 1, 1],
        [1, 2, 1, 1, 1, 2, 1],
        [1, 1, 1, 1, 1, 1, 1],
        [1, 1, 1, 1, 1, 1, 1],
        [1, 1, 0, 0, 0, 1, 1],
    ]
    FRAME_B = [
        [0, 1, 0, 0, 0, 1, 0],
        [1, 1, 1, 1, 1, 1, 1],
        [1, 2, 1, 1, 1, 2, 1],
        [1, 1, 1, 1, 1, 1, 1],
        [1, 1, 1, 1, 1, 1, 1],
        [0, 1, 1, 0, 1, 1, 0],
    ]

    def __init__(
        self,
        master,
        px=4,
        color_body="#cc6644",
        color_eye="#ffcc99",
        bg_color=TRANSPARENT,
        fast_body=None,
        idle_eye="#FFFFFF",
        stop_body="#555555",
        stop_eye="#777777",
        interval=300,
        **kwargs,
    ):
        self.px = px
        self.cols = 7
        self.rows = 7
        super().__init__(
            master,
            width=self.cols * px,
            height=self.rows * px,
            bg=bg_color,
            highlightthickness=0,
            **kwargs,
        )
        self.interval = interval
        self.frame_idx = 0
        self.running = False
        self.mode = "stopped"
        self.cells = {}
        self.base_body = color_body
        self.base_eye = color_eye
        self.fast_body = fast_body or color_body
        self.idle_eye = idle_eye
        self.stop_body = stop_body
        self.stop_eye = stop_eye
        self.bg_color = bg_color
        self.color_body = self.stop_body
        self.color_eye = self.stop_eye
        self._build()

    def _build(self):
        for ri, row in enumerate(self.FRAME_A):
            for ci, val in enumerate(row):
                x0 = ci * self.px
                y0 = ri * self.px
                self.cells[(ri, ci)] = self.create_rectangle(
                    x0,
                    y0,
                    x0 + self.px,
                    y0 + self.px,
                    fill=self._cell_color(val),
                    outline="",
                )

    def _cell_color(self, val):
        if val == 1:
            return self.color_body
        if val == 2:
            return self.color_eye
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

    def apply_theme(
        self,
        *,
        color_body,
        color_eye,
        bg_color,
        fast_body,
        idle_eye,
        stop_body,
        stop_eye,
    ):
        self.base_body = color_body
        self.base_eye = color_eye
        self.fast_body = fast_body
        self.idle_eye = idle_eye
        self.stop_body = stop_body
        self.stop_eye = stop_eye
        self.bg_color = bg_color
        self.config(bg=bg_color)
        self._apply_mode_colors()
        self._update_frame()

    def _apply_mode_colors(self):
        if self.mode == "stopped":
            self.color_body = self.stop_body
            self.color_eye = self.stop_eye
            return
        if self.mode == "idle":
            self.color_body = self.base_body
            self.color_eye = self.idle_eye
            return
        if self.mode == "fast":
            self.color_body = self.fast_body
            self.color_eye = self.base_eye
            return
        self.color_body = self.base_body
        self.color_eye = self.base_eye

    def start(self, speed="normal"):
        if speed == "slow":
            self.interval = 500
            self.mode = "slow"
        elif speed == "fast":
            self.interval = 120
            self.mode = "fast"
        else:
            self.interval = 300
            self.mode = "normal"
        self._apply_mode_colors()
        self._update_frame()
        if not self.running:
            self.running = True
            self._tick()

    def stop(self):
        self.running = False
        self.frame_idx = 0
        self.mode = "stopped"
        self._apply_mode_colors()
        self._update_frame()

    def idle(self):
        self.interval = 800
        self.mode = "idle"
        self._apply_mode_colors()
        self._update_frame()
        if not self.running:
            self.running = True
            self._tick()


THEME_AUTO = "auto"


def get_system_theme():
    """Return 'light_bg' or 'dark_bg' based on Windows personalization."""
    try:
        key = winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        )
        val, _ = winreg.QueryValueEx(key, "AppsUseLightTheme")
        winreg.CloseKey(key)
        return "light_bg" if val == 1 else "dark_bg"
    except Exception:
        return FALLBACK_THEME


def normalize_theme_name(theme_name):
    if theme_name == THEME_AUTO:
        return THEME_AUTO
    return theme_name if theme_name in THEMES else FALLBACK_THEME


def load_overlay_config():
    if not os.path.exists(CONFIG_FILE):
        return {"theme": DEFAULT_THEME}
    try:
        with open(CONFIG_FILE, "r", encoding="utf-8") as f:
            config = json.load(f)
        if not isinstance(config, dict):
            return {"theme": DEFAULT_THEME}
    except Exception:
        return {"theme": DEFAULT_THEME}
    config["theme"] = normalize_theme_name(config.get("theme"))
    return config


def save_overlay_config(config):
    payload = dict(config or {})
    payload["theme"] = normalize_theme_name(payload.get("theme"))
    with open(CONFIG_FILE, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)


def get_color(pct, theme):
    if pct is None:
        return theme["muted"]
    if pct >= 80:
        return theme["usage_high"]
    if pct >= 50:
        return theme["usage_mid"]
    return theme["usage_low"]


def get_bar_color(pct, theme):
    if pct is None:
        return theme.get("bar_low", theme["usage_low"])
    if pct >= 80:
        return theme.get("bar_high", theme["usage_high"])
    if pct >= 50:
        return theme.get("bar_mid", theme["usage_mid"])
    return theme.get("bar_low", theme["usage_low"])


def format_reset(resets_at):
    if not resets_at:
        return ""
    try:
        reset_dt = datetime.fromisoformat(resets_at)
        now = datetime.now(timezone.utc)
        sec = int((reset_dt - now).total_seconds())
        if sec <= 0:
            return "reset"
        h, m = sec // 3600, (sec % 3600) // 60
        if h > 24:
            return f"{h // 24}d{h % 24:02d}h"
        if h > 0:
            return f"{h}h{m:02d}m"
        return f"{m}m"
    except Exception:
        return ""


def read_usage():
    try:
        with open(DATA_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None


def read_status():
    try:
        with open(STATUS_FILE, "r", encoding="utf-8") as f:
            return f.read().strip()
    except Exception:
        return ""


class Overlay:
    def __init__(self):
        self.root = tk.Tk()
        self.auto_running = False
        self.auto_timer = None
        self.fetch_proc = None
        self._menu_open = False

        self.config = load_overlay_config()
        self.theme_mode = normalize_theme_name(self.config.get("theme", THEME_AUTO))
        if self.theme_mode == THEME_AUTO:
            self.theme_name = get_system_theme()
        else:
            self.theme_name = self.theme_mode
        self.theme = THEMES[self.theme_name]
        self.theme_var = tk.StringVar(master=self.root, value=self.theme_mode)

        user32 = ctypes.windll.user32
        sw = user32.GetSystemMetrics(0)
        sh = user32.GetSystemMetrics(1)

        w, h = 236, 32
        x = sw - w - 220
        y = sh - h

        self.root.overrideredirect(True)
        self.root.wm_attributes("-topmost", True)
        self.root.configure(bg=TRANSPARENT)
        self.root.wm_attributes("-transparentcolor", TRANSPARENT)
        self.root.geometry(f"{w}x{h}+{x}+{y}")

        font_main = ("Segoe UI Variable", -12)
        font_reset = ("Segoe UI Variable", -11)
        row_h = 12
        y0 = 2
        y1 = y0 + row_h
        grip_x = 0
        grip_w = 18
        lbl_x = 20
        lbl_w = 16
        bar_x = 38
        bar_w = 76
        pct_x = 116
        pct_w = 34
        rst_x = 152
        rst_w = 46
        inv_x = 202

        self.grip = tk.Canvas(
            self.root,
            width=grip_w,
            height=h,
            bg=TRANSPARENT,
            highlightthickness=0,
            cursor="fleur",
        )
        self.grip.place(x=grip_x, y=0)
        self.grip_dots = []
        dot_r = 2.0
        for dx in (5, 13):
            for dy in (7, 14, 21):
                self.grip_dots.append(
                    self.grip.create_oval(
                        dx - dot_r,
                        dy - dot_r,
                        dx + dot_r,
                        dy + dot_r,
                        fill=self.theme["accent"],
                        outline="",
                    )
                )

        self.lbl_5h = tk.Label(
            self.root,
            text="5h",
            font=font_main,
            fg=self.theme["muted"],
            bg=TRANSPARENT,
            anchor="w",
            pady=0,
            padx=0,
        )
        self.lbl_5h.place(x=lbl_x, y=y0, width=lbl_w, height=row_h)

        self.bar_track_5h = tk.Frame(self.root, bg=self.theme["bar_bg"], height=5, width=bar_w)
        self.bar_track_5h.place(x=bar_x, y=y0 + 4, width=bar_w, height=5)
        self.bar_5h = tk.Frame(self.bar_track_5h, bg=self.theme["usage_low"], height=5)
        self.bar_5h.place(x=0, y=0, relwidth=0, relheight=1)

        self.pct_5h = tk.Label(
            self.root,
            text="--%",
            font=font_main,
            fg=self.theme["muted"],
            bg=TRANSPARENT,
            anchor="w",
            pady=0,
            padx=0,
        )
        self.pct_5h.place(x=pct_x, y=y0, width=pct_w, height=row_h)

        self.rst_5h_shadow = tk.Label(
            self.root,
            text="",
            font=font_reset,
            fg=self.theme["reset_shadow"],
            bg=TRANSPARENT,
            anchor="e",
            pady=0,
            padx=0,
        )
        self.rst_5h_shadow.place(x=rst_x + 1, y=y0 + 1, width=rst_w, height=row_h)
        self.rst_5h = tk.Label(
            self.root,
            text="",
            font=font_reset,
            fg=self.theme["reset_text"],
            bg=TRANSPARENT,
            anchor="e",
            pady=0,
            padx=0,
        )
        self.rst_5h.place(x=rst_x, y=y0, width=rst_w, height=row_h)

        self.lbl_7d = tk.Label(
            self.root,
            text="7d",
            font=font_main,
            fg=self.theme["muted"],
            bg=TRANSPARENT,
            anchor="w",
            pady=0,
            padx=0,
        )
        self.lbl_7d.place(x=lbl_x, y=y1, width=lbl_w, height=row_h)

        self.bar_track_7d = tk.Frame(self.root, bg=self.theme["bar_bg"], height=5, width=bar_w)
        self.bar_track_7d.place(x=bar_x, y=y1 + 4, width=bar_w, height=5)
        self.bar_7d = tk.Frame(self.bar_track_7d, bg=self.theme["usage_low"], height=5)
        self.bar_7d.place(x=0, y=0, relwidth=0, relheight=1)

        self.pct_7d = tk.Label(
            self.root,
            text="--%",
            font=font_main,
            fg=self.theme["muted"],
            bg=TRANSPARENT,
            anchor="w",
            pady=0,
            padx=0,
        )
        self.pct_7d.place(x=pct_x, y=y1, width=pct_w, height=row_h)

        self.rst_7d_shadow = tk.Label(
            self.root,
            text="",
            font=font_reset,
            fg=self.theme["reset_shadow"],
            bg=TRANSPARENT,
            anchor="e",
            pady=0,
            padx=0,
        )
        self.rst_7d_shadow.place(x=rst_x + 1, y=y1 + 1, width=rst_w, height=row_h)
        self.rst_7d = tk.Label(
            self.root,
            text="",
            font=font_reset,
            fg=self.theme["reset_text"],
            bg=TRANSPARENT,
            anchor="e",
            pady=0,
            padx=0,
        )
        self.rst_7d.place(x=rst_x, y=y1, width=rst_w, height=row_h)

        self.spinner = MiniInvader(
            self.root,
            px=3,
            color_body=self.theme["spinner_body"],
            color_eye=self.theme["spinner_eye"],
            bg_color=TRANSPARENT,
            fast_body=self.theme["spinner_fast_body"],
            idle_eye=self.theme["spinner_idle_eye"],
            stop_body=self.theme["spinner_stop_body"],
            stop_eye=self.theme["spinner_stop_eye"],
        )
        self.spinner.place(x=inv_x, y=(h - 21) // 2)

        self.grip.bind("<ButtonPress-1>", self._start_drag)
        self.grip.bind("<B1-Motion>", self._do_drag)

        self.menu = tk.Menu(
            self.root,
            tearoff=0,
            font=("Consolas", 10),
            bd=1,
            relief="solid",
        )
        self.menu.add_command(label="Scan Now", command=self._scan_now)
        self.menu.add_command(label="Auto: OFF", command=self._toggle_auto)
        self.menu.add_command(label="Session Key...", command=self._edit_key)
        self.menu.add_separator()
        self.menu.add_radiobutton(
            label="Theme: Auto",
            value="auto",
            variable=self.theme_var,
            command=self._on_theme_selected,
        )
        self.menu.add_radiobutton(
            label="Theme: Dark BG",
            value="dark_bg",
            variable=self.theme_var,
            command=self._on_theme_selected,
        )
        self.menu.add_radiobutton(
            label="Theme: Light BG",
            value="light_bg",
            variable=self.theme_var,
            command=self._on_theme_selected,
        )
        self.menu.add_separator()
        self.menu.add_command(label="Exit", command=self._quit)

        for widget in (
            self.grip,
            self.lbl_5h,
            self.bar_track_5h,
            self.bar_5h,
            self.pct_5h,
            self.rst_5h_shadow,
            self.rst_5h,
            self.lbl_7d,
            self.bar_track_7d,
            self.bar_7d,
            self.pct_7d,
            self.rst_7d_shadow,
            self.rst_7d,
            self.spinner,
        ):
            widget.bind("<Button-3>", self._show_menu)

        self._apply_theme()
        self._watch_system_theme()
        self._keep_on_top()
        self._update_display()
        self._poll_status()

    def _start_drag(self, e):
        self._dx, self._dy = e.x, e.y

    def _do_drag(self, e):
        nx = self.root.winfo_x() + e.x - self._dx
        ny = self.root.winfo_y() + e.y - self._dy
        self.root.geometry(f"+{nx}+{ny}")

    def _apply_theme(self):
        theme = self.theme

        self.root.configure(bg=TRANSPARENT)
        self.root.wm_attributes("-transparentcolor", TRANSPARENT)
        self.grip.config(bg=TRANSPARENT)
        for dot in self.grip_dots:
            self.grip.itemconfig(dot, fill=theme["accent"])

        self.lbl_5h.config(bg=TRANSPARENT)
        self.pct_5h.config(bg=TRANSPARENT)
        self.rst_5h_shadow.config(bg=TRANSPARENT, fg=theme["reset_shadow"])
        self.rst_5h.config(bg=TRANSPARENT, fg=theme["reset_text"])
        self.lbl_7d.config(bg=TRANSPARENT)
        self.pct_7d.config(bg=TRANSPARENT)
        self.rst_7d_shadow.config(bg=TRANSPARENT, fg=theme["reset_shadow"])
        self.rst_7d.config(bg=TRANSPARENT, fg=theme["reset_text"])

        self.bar_track_5h.config(bg=theme["bar_bg"])
        self.bar_track_7d.config(bg=theme["bar_bg"])

        self.menu.config(
            bg=theme["menu_bg"],
            fg=theme["menu_fg"],
            activebackground=theme["menu_active_bg"],
            activeforeground=theme["menu_active_fg"],
            selectcolor=theme["accent"],
        )

        self.spinner.apply_theme(
            color_body=theme["spinner_body"],
            color_eye=theme["spinner_eye"],
            bg_color=TRANSPARENT,
            fast_body=theme["spinner_fast_body"],
            idle_eye=theme["spinner_idle_eye"],
            stop_body=theme["spinner_stop_body"],
            stop_eye=theme["spinner_stop_eye"],
        )

        self._render_usage(read_usage())

    def _on_theme_selected(self):
        self._set_theme(self.theme_var.get())

    def _set_theme(self, mode):
        self.theme_mode = normalize_theme_name(mode)
        if self.theme_mode == THEME_AUTO:
            self.theme_name = get_system_theme()
        else:
            self.theme_name = self.theme_mode
        self.theme = THEMES[self.theme_name]
        self.theme_var.set(self.theme_mode)
        self.config["theme"] = self.theme_mode
        save_overlay_config(self.config)
        self._apply_theme()

    def _on_system_theme_changed(self):
        if self.theme_mode == THEME_AUTO:
            sys_theme = get_system_theme()
            if sys_theme != self.theme_name:
                self.theme_name = sys_theme
                self.theme = THEMES[self.theme_name]
                self._apply_theme()

    def _watch_system_theme(self):
        REG_PATH = r"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize"
        REG_NOTIFY_CHANGE_LAST_SET = 0x00000004
        try:
            key = winreg.OpenKey(
                winreg.HKEY_CURRENT_USER, REG_PATH, 0,
                winreg.KEY_NOTIFY | winreg.KEY_READ,
            )
        except Exception:
            return

        def _watcher():
            while True:
                try:
                    ctypes.windll.advapi32.RegNotifyChangeKeyValue(
                        int(key), False, REG_NOTIFY_CHANGE_LAST_SET, None, False,
                    )
                    self.root.after_idle(self._on_system_theme_changed)
                except Exception:
                    break

        t = threading.Thread(target=_watcher, daemon=True)
        t.start()

    def _show_menu(self, e):
        label = "Auto: ON -> OFF" if self.auto_running else "Auto: OFF -> ON"
        self.menu.entryconfigure(1, label=label)
        self.theme_var.set(self.theme_mode)
        self._menu_open = True
        self.menu.post(e.x_root, e.y_root)
        self._menu_open = False

    def _keep_on_top(self):
        if not self._menu_open:
            self.root.lift()
            self.root.wm_attributes("-topmost", True)
        self.root.after(500, self._keep_on_top)

    def _scan_now(self):
        trigger = os.path.join(DIR, "scan_trigger")
        with open(trigger, "w", encoding="utf-8") as f:
            f.write("scan")
        self.spinner.start(speed="fast")

    def _edit_key(self):
        from setup_dialog import ask_session_key, load_session_key, save_session_key

        key = ask_session_key(title="Session Key Change", current_key=load_session_key())
        if key:
            save_session_key(key)
            self._scan_now()

    def _toggle_auto(self):
        self.auto_running = not self.auto_running
        if self.auto_running:
            self.spinner.idle()
        else:
            self.spinner.stop()

    def _render_usage(self, data):
        theme = self.theme
        muted = theme["muted"]
        reset_text = theme["reset_text"]
        reset_shadow = theme["reset_shadow"]

        fh_data = data.get("five_hour", {}) if data else {}
        sd_data = data.get("seven_day", {}) if data else {}
        fh = fh_data.get("utilization")
        sd = sd_data.get("utilization")
        fr = format_reset(fh_data.get("resets_at"))
        sr = format_reset(sd_data.get("resets_at"))

        if fh is not None:
            color = get_color(fh, theme)
            bar_color = get_bar_color(fh, theme)
            relwidth = max(0.0, min(float(fh), 100.0)) / 100.0
            self.bar_5h.place(x=0, y=0, relwidth=relwidth, relheight=1)
            self.bar_5h.config(bg=bar_color)
            self.pct_5h.config(text=f"{fh:.0f}%", fg=color)
            self.lbl_5h.config(fg=color)
        else:
            self.bar_5h.place(x=0, y=0, relwidth=0, relheight=1)
            self.bar_5h.config(bg=theme.get("bar_low", theme["usage_low"]))
            self.pct_5h.config(text="--%", fg=muted)
            self.lbl_5h.config(fg=muted)
        self.rst_5h.config(text=fr, fg=reset_text)
        self.rst_5h_shadow.config(text=fr, fg=reset_shadow)

        if sd is not None:
            color = get_color(sd, theme)
            bar_color = get_bar_color(sd, theme)
            relwidth = max(0.0, min(float(sd), 100.0)) / 100.0
            self.bar_7d.place(x=0, y=0, relwidth=relwidth, relheight=1)
            self.bar_7d.config(bg=bar_color)
            self.pct_7d.config(text=f"{sd:.0f}%", fg=color)
            self.lbl_7d.config(fg=color)
        else:
            self.bar_7d.place(x=0, y=0, relwidth=0, relheight=1)
            self.bar_7d.config(bg=theme.get("bar_low", theme["usage_low"]))
            self.pct_7d.config(text="--%", fg=muted)
            self.lbl_7d.config(fg=muted)
        self.rst_7d.config(text=sr, fg=reset_text)
        self.rst_7d_shadow.config(text=sr, fg=reset_shadow)

    def _update_display(self):
        self._render_usage(read_usage())
        self.root.after(REFRESH_MS, self._update_display)

    def _poll_status(self):
        status = read_status()
        if status in ("FETCHING", "LOGGING_IN", "STARTING"):
            self.spinner.start(speed="fast")
        elif status.startswith("OK"):
            if self.auto_running:
                self.spinner.idle()
            else:
                self.spinner.stop()
        elif status.startswith("ERROR"):
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
