"""初回セットアップダイアログ — セッションキー入力・保存"""
import tkinter as tk
import os

DIR = os.path.dirname(__file__)
ENV_FILE = os.path.join(DIR, '.env')


def load_session_key():
    """.envからセッションキーを読み込む。なければNone"""
    if not os.path.exists(ENV_FILE):
        return None
    with open(ENV_FILE, 'r') as f:
        for line in f:
            line = line.strip()
            if line.startswith('SESSION_KEY=') and not line.startswith('#'):
                val = line.split('=', 1)[1].strip()
                if val:
                    return val
    return None


def save_session_key(key):
    """.envにセッションキーを保存"""
    with open(ENV_FILE, 'w') as f:
        f.write(f"SESSION_KEY={key}\n")


def ask_session_key(title="Claude Usage Widget — Setup", current_key=None):
    """tkinterダイアログでセッションキーを入力させる。OKならキーを返す、キャンセルならNone"""
    result = [None]

    root = tk.Tk()
    root.title(title)
    root.configure(bg="#1E1E1E")
    root.resizable(False, False)

    w, h = 520, 260
    sx = root.winfo_screenwidth() // 2 - w // 2
    sy = root.winfo_screenheight() // 2 - h // 2
    root.geometry(f"{w}x{h}+{sx}+{sy}")

    font = ("Consolas", 10)
    sfont = ("Consolas", 9)

    # 説明
    tk.Label(root, text="Claude.ai Session Key", font=("Consolas", 14, "bold"),
             fg="#55DDFF", bg="#1E1E1E").pack(pady=(18, 4))

    tk.Label(root, text="ブラウザのCookieから sessionKey をコピーしてください",
             font=sfont, fg="#AAAAAA", bg="#1E1E1E").pack()

    tk.Label(root, text="(F12 → Application → Cookies → claude.ai → sessionKey)",
             font=sfont, fg="#777777", bg="#1E1E1E").pack(pady=(0, 12))

    # 入力欄
    frame = tk.Frame(root, bg="#1E1E1E")
    frame.pack(padx=20, fill="x")

    entry = tk.Entry(frame, font=font, bg="#2D2D2D", fg="#F0F0F0",
                     insertbackground="#F0F0F0", relief="flat", bd=6)
    entry.pack(fill="x", ipady=4)

    if current_key:
        entry.insert(0, current_key)
        entry.select_range(0, tk.END)

    # ステータス
    status_lbl = tk.Label(root, text="", font=sfont, fg="#FF7788", bg="#1E1E1E")
    status_lbl.pack(pady=(4, 0))

    # ボタン
    btn_frame = tk.Frame(root, bg="#1E1E1E")
    btn_frame.pack(pady=(12, 0))

    def on_ok(event=None):
        key = entry.get().strip()
        if not key:
            status_lbl.config(text="キーを入力してください")
            return
        if not key.startswith("sk-ant-"):
            status_lbl.config(text="sk-ant- で始まるキーを入力してください")
            return
        result[0] = key
        root.destroy()

    def on_cancel():
        root.destroy()

    ok_btn = tk.Button(btn_frame, text="  Save  ", font=font, bg="#55DDFF",
                       fg="#1E1E1E", activebackground="#88EEFF", relief="flat",
                       cursor="hand2", command=on_ok)
    ok_btn.pack(side="left", padx=8)

    cancel_btn = tk.Button(btn_frame, text=" Cancel ", font=font, bg="#444444",
                           fg="#AAAAAA", activebackground="#555555", relief="flat",
                           cursor="hand2", command=on_cancel)
    cancel_btn.pack(side="left", padx=8)

    entry.bind("<Return>", on_ok)
    entry.focus_set()

    root.mainloop()
    return result[0]


def ensure_session_key():
    """キーがあればそのまま返す。なければダイアログを出す。"""
    key = load_session_key()
    if key:
        return key
    key = ask_session_key()
    if key:
        save_session_key(key)
    return key


if __name__ == "__main__":
    key = ask_session_key(current_key=load_session_key())
    if key:
        save_session_key(key)
        print(f"Saved: {key[:20]}...")
    else:
        print("Cancelled")
