import tkinter as tk
import ctypes

# タスクバーのサイズ取得
def get_taskbar_info():
    user32 = ctypes.windll.user32
    screen_w = user32.GetSystemMetrics(0)
    screen_h = user32.GetSystemMetrics(1)
    return screen_w, screen_h

root = tk.Tk()

screen_w, screen_h = get_taskbar_info()

# ウィンドウ設定
root.overrideredirect(True)          # タイトルバー・枠なし
root.wm_attributes("-topmost", True) # 常に最前面
root.wm_attributes("-alpha", 1.0)    # 全体の透明度（0.0=完全透明、1.0=不透明）
root.configure(bg="black")

# タスクバー上に配置（右寄り、高さはタスクバー分）
widget_w = 160
widget_h = 48
x = screen_w - widget_w - 220  # 右から220px（トレイアイコンの左あたり）
y = screen_h - widget_h         # 画面の一番下

root.geometry(f"{widget_w}x{widget_h}+{x}+{y}")

# フレーム（背景色をblackにしてalphaのtransparentcolorと合わせる）
frame = tk.Frame(root, bg="black")
frame.pack(fill="both", expand=True)

# テキスト表示
label = tk.Label(
    frame,
    text="Claude ⬛ 45%",
    fg="#00CFFF",       # 水色テキスト
    bg="black",
    font=("Segoe UI", 11, "bold"),
    padx=8,
)
label.pack(expand=True)

# blackを透明色に指定
root.wm_attributes("-transparentcolor", "black")

# ドラッグ移動（位置調整用）
def start_drag(event):
    root._drag_x = event.x
    root._drag_y = event.y

def do_drag(event):
    dx = event.x - root._drag_x
    dy = event.y - root._drag_y
    nx = root.winfo_x() + dx
    ny = root.winfo_y() + dy
    root.geometry(f"+{nx}+{ny}")

label.bind("<ButtonPress-1>", start_drag)
label.bind("<B1-Motion>", do_drag)

# 右クリックで終了
def quit_app(event=None):
    root.destroy()

label.bind("<Button-3>", quit_app)

# 定期的に最前面を強制（タスクバーに負けないように）
def keep_on_top():
    root.lift()
    root.wm_attributes("-topmost", True)
    root.after(500, keep_on_top)

keep_on_top()
root.mainloop()
