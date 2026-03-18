"""Claude使用量フェッチャー（常駐プロセス） — requests版"""
import sys
import time
import os
import json
import logging
from curl_cffi import requests
from setup_dialog import load_session_key, ask_session_key, save_session_key

DIR = os.path.dirname(__file__)
DATA_FILE = os.path.join(DIR, 'usage_data.json')
STATUS_FILE = os.path.join(DIR, 'fetch_status.txt')
TRIGGER_FILE = os.path.join(DIR, 'scan_trigger')
LOG_FILE = os.path.join(DIR, 'fetcher.log')

logging.basicConfig(
    filename=LOG_FILE,
    level=logging.INFO,
    format='[%(asctime)s] %(message)s',
    datefmt='%H:%M:%S',
)
log = logging.getLogger(__name__)

AUTO_INTERVAL = 300  # 5分


def write_status(s):
    with open(STATUS_FILE, 'w') as f:
        f.write(s)


def make_headers(session_key):
    return {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36',
        'Accept': 'application/json, text/plain, */*',
        'Accept-Language': 'ja,en-US;q=0.9,en;q=0.8',
        'Accept-Encoding': 'gzip, deflate, br',
        'Referer': 'https://claude.ai/settings/usage',
        'Origin': 'https://claude.ai',
        'sec-ch-ua': '"Chromium";v="122", "Not(A:Brand";v="24", "Google Chrome";v="122"',
        'sec-ch-ua-mobile': '?0',
        'sec-ch-ua-platform': '"Windows"',
        'sec-fetch-dest': 'empty',
        'sec-fetch-mode': 'cors',
        'sec-fetch-site': 'same-origin',
        'cookie': f'sessionKey={session_key}',
    }


def fetch_usage(session_key):
    """APIからデータ取得。成功=True, 認証エラー='auth_error', その他エラー=False"""
    write_status("FETCHING")
    headers = make_headers(session_key)
    try:
        r = requests.get('https://claude.ai/api/organizations',
                         headers=headers, impersonate="chrome120", timeout=15)
        if r.status_code == 403 or r.status_code == 401:
            write_status("ERROR auth")
            return 'auth_error'
        if r.status_code != 200:
            write_status(f"ERROR org {r.status_code}")
            return False

        orgs = r.json()
        org_id = orgs[0]['uuid']

        r2 = requests.get(f'https://claude.ai/api/organizations/{org_id}/usage',
                          headers=headers, impersonate="chrome120", timeout=15)
        if r2.status_code != 200:
            write_status(f"ERROR usage {r2.status_code}")
            return False

        usage = r2.json()
        data = {
            "five_hour": usage.get("five_hour", {}),
            "seven_day": usage.get("seven_day", {}),
            "timestamp": time.time(),
        }
        with open(DATA_FILE, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False)

        fh = data["five_hour"].get("utilization", "?")
        sd = data["seven_day"].get("utilization", "?")
        log.info(f"5h: {fh}% | 7d: {sd}%")
        write_status(f"OK {fh}% / {sd}%")
        return True
    except Exception as e:
        log.error(f"{e}")
        write_status(f"ERROR {e}")
        return False


def check_trigger():
    if os.path.exists(TRIGGER_FILE):
        os.remove(TRIGGER_FILE)
        return True
    return False


def get_valid_key():
    """有効なセッションキーを取得。なければダイアログを出す。"""
    key = load_session_key()
    if key:
        return key
    key = ask_session_key()
    if key:
        save_session_key(key)
    return key


def main():
    log.info("Claude Usage Fetcher 起動")

    write_status("STARTING")

    session_key = get_valid_key()
    if not session_key:
        log.info("セッションキー未設定。終了。")
        write_status("ERROR no key")
        return

    # 初回取得
    result = fetch_usage(session_key)
    if result == 'auth_error':
        log.info("セッションキー無効。再入力ダイアログ表示。")
        session_key = ask_session_key(title="セッションキーが無効です", current_key=session_key)
        if not session_key:
            write_status("ERROR no key")
            return
        save_session_key(session_key)
        result = fetch_usage(session_key)
        if result == 'auth_error':
            log.error("認証失敗。終了。")
            write_status("ERROR auth")
            return

    if result is True:
        log.info("初回取得成功")

    # 常駐ループ
    last_fetch = time.time()
    while True:
        time.sleep(3)

        now = time.time()
        triggered = check_trigger()

        if triggered or (now - last_fetch >= AUTO_INTERVAL):
            if triggered:
                log.info("トリガー検出 → スキャン")
            else:
                log.info("定期スキャン")

            result = fetch_usage(session_key)
            if result == 'auth_error':
                log.info("セッション切れ。再入力ダイアログ表示。")
                session_key = ask_session_key(title="セッション切れ — 再入力", current_key=session_key)
                if session_key:
                    save_session_key(session_key)
                else:
                    write_status("ERROR no key")
                    return
            last_fetch = time.time()


if __name__ == "__main__":
    main()
