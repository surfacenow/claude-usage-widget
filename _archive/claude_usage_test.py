import webview
import time
import os
import json

STORAGE_PATH = os.path.join(os.path.dirname(__file__), 'webview_data')
DATA_FILE = os.path.join(os.path.dirname(__file__), 'usage_data.json')

started = False

def monitor(window):
    global started
    if started:
        return
    started = True

    print("ログインを待機中...")

    # ログイン完了を待つ
    while True:
        time.sleep(3)
        try:
            url = window.get_current_url() or ""
            if 'claude.ai' in url and '/login' not in url and '/auth' not in url and 'accounts.google' not in url:
                print(f"[OK] ログイン検出！ URL: {url}")
                break
        except:
            pass

    time.sleep(3)

    # ページ遷移せず、JS fetch()でAPIを直接叩く
    print("APIからデータ取得中...")

    # Step1: 組織ID取得
    org_result = window.evaluate_js("""
        (async () => {
            try {
                const r = await fetch('/api/organizations');
                const data = await r.json();
                return JSON.stringify(data);
            } catch(e) {
                return 'ERROR: ' + e.message;
            }
        })()
    """)

    time.sleep(3)
    # evaluate_jsのasync結果を待つ
    org_result = window.evaluate_js("window._lastResult")

    # 別の方法: syncで結果を取得
    print("組織情報を取得中...")
    org_json = window.evaluate_js("""
        var result = null;
        var xhr = new XMLHttpRequest();
        xhr.open('GET', '/api/organizations', false);
        xhr.send();
        xhr.responseText;
    """)

    print(f"組織情報: {str(org_json)[:500]}")

    if not org_json or 'ERROR' in str(org_json):
        print("[error] 組織情報の取得失敗")
        return

    try:
        orgs = json.loads(org_json)
        org_id = orgs[0]['uuid']
        print(f"組織ID: {org_id}")
    except Exception as e:
        print(f"[error] パース失敗: {e}")
        return

    # Step2: 使用量取得（いくつかのエンドポイントを試す）
    endpoints = [
        f'/api/organizations/{org_id}/usage',
        f'/api/organizations/{org_id}/billing/usage',
        f'/api/organizations/{org_id}/rate_limits',
        f'/api/organizations/{org_id}/settings',
    ]

    for ep in endpoints:
        print(f"\n試行: {ep}")
        resp = window.evaluate_js(f"""
            var xhr = new XMLHttpRequest();
            xhr.open('GET', '{ep}', false);
            xhr.send();
            xhr.status + '|' + xhr.responseText;
        """)
        if resp:
            status, _, body = resp.partition('|')
            print(f"  Status: {status}")
            print(f"  Body: {body[:500]}")

            if status == '200' and body:
                with open(DATA_FILE, 'w', encoding='utf-8') as f:
                    json.dump({"endpoint": ep, "data": body, "timestamp": time.time()}, f, ensure_ascii=False)
                print(f"  [saved] {DATA_FILE}")

    print("\n完了！ウィンドウを閉じてOKです。")


window = webview.create_window(
    'Claude Usage - ログインしてください',
    'https://claude.ai/login',
    width=900,
    height=700,
)

webview.start(
    func=monitor,
    args=[window],
    storage_path=STORAGE_PATH,
)
