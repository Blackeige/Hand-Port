# Windows local build. Needs Unity 2019.4.40f1 + Android Build Support, Python 3, git.
# usage: .\tools\build-local.ps1 -Unity "C:\Program Files\Unity\Hub\Editor\2019.4.40f1\Editor\Unity.exe"
param([Parameter(Mandatory=$true)][string]$Unity)
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
$repo = (Select-String -Path UPSTREAM.txt -Pattern '^repo:').Line.Split()[1]
$sha  = (Select-String -Path UPSTREAM.txt -Pattern '^commit:').Line.Split()[1]
if (-not (Test-Path upstream)) { git clone --filter=blob:none $repo upstream }
git -C upstream checkout $sha
python tools/apply_port.py --upstream upstream --out Project --model
& $Unity -batchmode -nographics -quit -projectPath Project -buildTarget Android -executeMethod HandPlayground.Editor.PortBuild.BuildAndroid -logFile build.log
Get-ChildItem Project/Builds/Android/*.apk
