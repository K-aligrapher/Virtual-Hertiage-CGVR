"""Generates the audio-guide clips from Assets/Heritage/Resources/guide.json.

Each (language, temple, key) becomes Assets/Heritage/Resources/Guide/<lang>/<temple>_<key>.mp3, which the app loads
through Resources, so the guide works offline in the browser, on desktop and on a standalone Quest.

Uses Microsoft Edge neural voices through the `edge-tts` package (internet needed only while generating):
    python -m venv .tts && .tts\\Scripts\\pip install edge-tts
    .tts\\Scripts\\python Tools\\generate_guide_audio.py            # only missing clips
    .tts\\Scripts\\python Tools\\generate_guide_audio.py --force    # regenerate everything
Edit guide.json (and have a native speaker review the Hindi and Kannada), then rerun.
"""
import asyncio
import json
import os
import sys

import edge_tts

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
GUIDE = os.path.join(ROOT, "Assets", "Heritage", "Resources", "guide.json")
OUT = os.path.join(ROOT, "Assets", "Heritage", "Resources", "Guide")


async def main(force: bool) -> None:
    with open(GUIDE, encoding="utf-8") as f:
        data = json.load(f)
    jobs = []
    for lang in data["languages"]:
        os.makedirs(os.path.join(OUT, lang["code"]), exist_ok=True)
        for temple in data["temples"]:
            for entry in temple["entries"]:
                path = os.path.join(OUT, lang["code"], f"{temple['id']}_{entry['key']}.mp3")
                if force or not os.path.exists(path):
                    jobs.append((lang["voice"], entry[lang["code"]], path))
    print(f"{len(jobs)} clip(s) to generate")
    sem = asyncio.Semaphore(4)

    async def one(voice: str, text: str, path: str) -> None:
        async with sem:
            for attempt in range(4):
                try:
                    await edge_tts.Communicate(text, voice, rate="-6%").save(path)
                    print("ok  ", os.path.relpath(path, ROOT))
                    return
                except Exception as exc:  # network hiccup: retry
                    print("retry", os.path.basename(path), exc)
                    await asyncio.sleep(2 + attempt * 2)
            raise SystemExit("failed: " + path)

    await asyncio.gather(*(one(*j) for j in jobs))


if __name__ == "__main__":
    asyncio.run(main("--force" in sys.argv))
