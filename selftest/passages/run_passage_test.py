#!/usr/bin/env python3
"""Run Arabic and Chinese passages through Qalam's local mobile translation API.

The Arabic excerpt is the Akhsar al-Mukhtasarat passage supplied by the user.
The Chinese passage is from Shuo Yuan (說苑), as printed in the local literary-
Chinese primer, not scraped from ctext.org.
"""

import json
import os
from pathlib import Path
import subprocess
import sys
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
EXE = ROOT / "dist" / "Qalam.exe"
PORT = 8796
TOKEN = "passagetest"
URL_FILE = OUT / "mobile-url.txt"
STDOUT = OUT / "server.out.log"
STDERR = OUT / "server.err.log"

EXACT = """لَا فِي عَمْدٍ
وَهُوَ وَاجِبٌ لِمَا تَبْطُلُ بِتَعَمُّدِه ِ
وَسُنَّةٌ لِإِتْيَانٍ بِقَوْلِ مَشْرُوعٍ فِي غَيْرِ مَحَلِّهِ سَهْوًا, وَلَا تَبْطُلُ بِتَعَمُّدِهِ, وَمُبَاحٌ لِتَرْكِ سُنَّةٍ
"""
CLEAN = EXACT.replace("بِتَعَمُّدِه ِ", "بِتَعَمُّدِهِ").replace("سَهْوًا,", "سَهْوًا،")
HEADING = "سُجُودُ السَّهْوِ\n" + EXACT
CHINESE = """知命者不怨天，知己者不怨人。
禍生於欲得，福生於自禁。聖人以心導耳目，小人以耳目導心。
爲善者天報以德。爲不善者天報以禍。"""


def post(url: str, body: dict) -> dict:
    data = json.dumps(body, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=data,
        method="POST",
        headers={"Content-Type": "application/json; charset=utf-8", "X-Qalam-Token": TOKEN},
    )
    with urllib.request.urlopen(request, timeout=90) as response:
        return json.loads(response.read().decode("utf-8"))


def main() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    for path in (URL_FILE, STDOUT, STDERR):
        path.unlink(missing_ok=True)

    with open(STDOUT, "w", encoding="utf-8") as out, open(STDERR, "w", encoding="utf-8") as err:
        process = subprocess.Popen(
            [
                str(EXE),
                "--mobileserve",
                "--port",
                str(PORT),
                "--token",
                TOKEN,
                "--out",
                str(URL_FILE),
            ],
            cwd=str(ROOT),
            stdout=out,
            stderr=err,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        try:
            ready = False
            for _ in range(40):
                if process.poll() is not None:
                    break
                try:
                    with urllib.request.urlopen(
                        f"http://127.0.0.1:{PORT}/api/health?token={TOKEN}",
                        timeout=2,
                    ) as response:
                        response.read()
                    ready = True
                    break
                except Exception:
                    time.sleep(0.5)
            if not ready:
                details = (STDOUT.read_text(encoding="utf-8", errors="replace") + "\n" +
                           STDERR.read_text(encoding="utf-8", errors="replace"))
                raise RuntimeError("mobile server did not become ready\n" + details)

            results = []
            for label, text in (("verbatim", EXACT), ("cleaned", CLEAN), ("with_heading", HEADING)):
                result = post(
                    f"http://127.0.0.1:{PORT}/api/translate?token={TOKEN}",
                    {"text": text, "target": "en"},
                )
                result["label"] = label
                results.append(result)

            (OUT / "arabic-results.json").write_text(
                json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8"
            )
            chinese = post(
                f"http://127.0.0.1:{PORT}/api/translate?token={TOKEN}",
                {"text": CHINESE, "target": "en"},
            )
            chinese["label"] = "shuoyuan"
            (OUT / "chinese-results.json").write_text(
                json.dumps(chinese, ensure_ascii=False, indent=2), encoding="utf-8"
            )
            print(json.dumps({"arabic": results, "chinese": chinese}, ensure_ascii=False, indent=2))
        finally:
            if process.poll() is None:
                process.kill()
            process.wait(timeout=10)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
