#!/usr/bin/env bash
# Local build (Linux/macOS). Needs Unity 2019.4.40f1 with the Android Build Support module (SDK/NDK/OpenJDK), logged in/licensed.
# usage: UNITY=/path/to/Unity ./tools/build-local.sh
set -euo pipefail
cd "$(dirname "$0")/.."
: "${UNITY:?set UNITY to the Unity 2019.4.40f1 executable}"
[ -d upstream ] || git clone --filter=blob:none "$(grep '^repo:' UPSTREAM.txt | awk '{print $2}')" upstream
git -C upstream checkout "$(grep '^commit:' UPSTREAM.txt | awk '{print $2}')"
python3 tools/apply_port.py --upstream upstream --out Project --model
"$UNITY" -batchmode -nographics -quit -projectPath Project -buildTarget Android \
  -executeMethod HandPlayground.Editor.PortBuild.BuildAndroid -logFile build.log
ls -l Project/Builds/Android/*.apk
