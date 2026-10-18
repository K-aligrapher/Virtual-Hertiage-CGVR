#!/bin/bash
# Renders preview PNGs to Artifacts/shots via Unity play mode (no player build needed).
cd "$(dirname "$0")/.."
rm -rf Artifacts/shots; mkdir -p Artifacts/shots
LOG="C:/Users/Kaligrapher/Documents/Projects/CGVR/Artifacts/shots.log"
timeout ${SHOTS_TIMEOUT:-600} "C:/Program Files/Unity/Hub/Editor/6000.6.1f1/Editor/Unity.exe" -batchmode -projectPath "C:/Users/Kaligrapher/Documents/Projects/CGVR" -executeMethod VirtualHeritage.Editor.HeritageCapture.Run -heritage-shots --heritage-shots $@ -logFile "$LOG"
echo exit $?
grep -n "error CS\|HERITAGE_SHOTS\|NullReference\|Exception" "$LOG" | head -20
ls Artifacts/shots
