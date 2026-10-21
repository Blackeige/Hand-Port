#!/usr/bin/env python3
"""Assemble the Android/Cardboard port: pristine upstream + overlay (this repo) -> a Unity project.

  python3 tools/apply_port.py --upstream <checkout of upstream@pinned commit> --out Project [--model]

* The upstream checkout is never modified (copied).
* Assets/Oculus (proprietary Oculus Utilities SDK, license forbids partial redistribution) is NOT carried over;
  clean-room replacements with identical GUIDs live in overlay/Assets/OVRCompat.
* --model downloads Google's hand_landmarker.task into StreamingAssets (needs internet).
"""
import argparse, os, re, shutil, sys, urllib.request, hashlib

MODEL_URL = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task"
DROP = ["Assets/Oculus", "Assets/Oculus.meta",
        "Assets/Resources/ONSPSettings.asset", "Assets/Resources/OVRBuildConfig.asset",
        "Assets/Resources/OculusPlatformSettings.asset", "Assets/Resources/OvrAvatarSettings.asset"]

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--upstream", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--model", action="store_true")
    a = ap.parse_args()
    here = os.path.dirname(os.path.abspath(__file__)); overlay = os.path.join(here, "..", "overlay")
    if os.path.exists(a.out): shutil.rmtree(a.out)
    shutil.copytree(a.upstream, a.out, ignore=shutil.ignore_patterns(".git", "Logs", ".idea", "*.sln.DotSettings"))
    for d in DROP:
        for p in (d, d + ".meta"):
            p = os.path.join(a.out, p)
            if os.path.isdir(p): shutil.rmtree(p)
            elif os.path.exists(p): os.remove(p)
    for f in ("Assets/Resources/ONSPSettings.asset.meta", "Assets/Resources/OVRBuildConfig.asset.meta",
              "Assets/Resources/OculusPlatformSettings.asset.meta", "Assets/Resources/OvrAvatarSettings.asset.meta"):
        p = os.path.join(a.out, f)
        if os.path.exists(p): os.remove(p)
    shutil.copytree(overlay, a.out, dirs_exist_ok=True)
    # Unused material that pointed to an Oculus avatar shader -> Standard
    mat = os.path.join(a.out, "Assets/Materials/Red 4.mat")
    if os.path.exists(mat):
        t = open(mat, encoding="utf-8").read()
        t = re.sub(r"m_Shader: \{fileID: \d+, guid: 5e52aa58207bbf24d8eb8ec969e9ae88, type: 3\}",
                   "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}", t)
        open(mat, "w", encoding="utf-8").write(t)
    if a.model:
        d = os.path.join(a.out, "Assets/StreamingAssets"); os.makedirs(d, exist_ok=True)
        p = os.path.join(d, "hand_landmarker.task")
        urllib.request.urlretrieve(MODEL_URL, p)
        print("model:", os.path.getsize(p), "bytes sha256", hashlib.sha256(open(p, "rb").read()).hexdigest())
    return validate(a.out)

def validate(out):
    """Every GUID referenced by game scenes/prefabs/materials must resolve to a file in the project (package GUIDs excepted)."""
    g2p = {}
    for dp, _, fn in os.walk(os.path.join(out, "Assets")):
        for f in fn:
            if f.endswith(".meta"):
                m = re.search(r"^guid: ([0-9a-f]{32})", open(os.path.join(dp, f), encoding="utf-8", errors="ignore").read(), re.M)
                if m: g2p.setdefault(m.group(1), []).append(os.path.join(dp, f)[:-5])
    dup = {g: p for g, p in g2p.items() if len(p) > 1}
    missing = {}
    for dp, _, fn in os.walk(os.path.join(out, "Assets")):
        for f in fn:
            if not f.endswith((".unity", ".prefab", ".mat", ".asset", ".controller", ".anim")): continue
            p = os.path.join(dp, f)
            if p.endswith("Scenes/Slow.unity") or "ConstraintSamples" in p: continue   # not in the build
            t = open(p, encoding="utf-8", errors="ignore").read()
            for g in set(re.findall(r"guid: ([0-9a-f]{32})", t)):
                if g not in g2p and not g.startswith("00000000"): missing.setdefault(g, set()).add(os.path.relpath(p, out))
    print("GUID check: %d assets indexed, %d duplicate GUIDs" % (len(g2p), len(dup)))
    for g, p in dup.items(): print("  DUP", g, p)
    print("Unresolved GUIDs (expected: only Unity/package-provided scripts):", len(missing))
    for g, ps in sorted(missing.items()): print("  ", g, sorted(ps)[:3])
    return 1 if dup else 0

if __name__ == "__main__":
    sys.exit(main())
