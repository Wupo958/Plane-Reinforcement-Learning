# Starts the ML-Agents trainer and waits for the Unity Editor to connect.
#
#   .\train.ps1                  # start a fresh run (run id: airplane)
#   .\train.ps1 -Resume          # continue the last 'airplane' run
#   .\train.ps1 -RunId night2    # start a differently-named run
#   .\train.ps1 -TimeScale 5     # slow the sim down if physics gets unstable
#
# Once it prints "Start training by pressing the Play button in the Unity Editor",
# open Assets/2. Prototype Agent/ProtoypeScene.unity and press Play.

param(
    [string]$RunId = "airplane",
    [switch]$Resume,
    [switch]$Force,
    [int]$TimeScale = 10
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$learn = Join-Path $PSScriptRoot "venv\Scripts\mlagents-learn.exe"
if (-not (Test-Path $learn)) {
    Write-Error "venv not found. Run:  uv venv --python 3.10 venv ; uv pip install --python venv -r requirements.txt"
}

$mlArgs = @(
    "config/airplaneProtoytpe.yaml",
    "--run-id=$RunId",
    "--time-scale=$TimeScale",
    "--quality-level=0"
)
if ($Resume) { $mlArgs += "--resume" }
if ($Force)  { $mlArgs += "--force" }

Write-Host "Run id      : $RunId"
Write-Host "Time scale  : $TimeScale"
Write-Host "Results dir : results\$RunId"
Write-Host ""

& $learn @mlArgs
