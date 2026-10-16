# Starts the ML-Agents trainer and waits for the Unity Editor to connect.
#
#   .\train.ps1                  # start a fresh run (run id: airplane)
#   .\train.ps1 -Resume          # continue the last 'airplane' run
#   .\train.ps1 -RunId night2    # start a differently-named run
#   .\train.ps1 -TimeScale 5     # slow the sim down if physics gets unstable
#   .\train.ps1 -Config config/airplaneProtoytpe.yaml   # train the old prototype instead
#
# The terrain run lives under run id 'terrain-06', so continuing it is:
#   .\train.ps1 -RunId terrain-06 -Resume
#
# Once it prints "Start training by pressing the Play button in the Unity Editor",
# open Assets/3. Terrain Agent/TerrainWorldScene.unity and press Play. TerrainTrainingScene is
# the older farm; terrain-06 was trained in TerrainWorldScene.

param(
    [string]$RunId = "airplane",
    [string]$Config = "config/airplaneTerrain.yaml",
    [switch]$Resume,
    [string]$InitializeFrom = "",
    [switch]$Force,
    [int]$TimeScale = 10,
    [int]$TimeoutWait = 7200
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$learn = Join-Path $PSScriptRoot "venv\Scripts\mlagents-learn.exe"
if (-not (Test-Path $learn)) {
    Write-Error "venv not found. Run:  uv venv --python 3.10 venv ; uv pip install --python venv -r requirements.txt"
}

$mlArgs = @(
    $Config,
    "--run-id=$RunId",
    "--time-scale=$TimeScale",
    "--quality-level=0",
    # Default is 60s, which expires before you can alt-tab over and hit Play.
    "--timeout-wait=$TimeoutWait"
)
if ($Resume) { $mlArgs += "--resume" }
if ($InitializeFrom) { $mlArgs += "--initialize-from=$InitializeFrom" }
if ($Force)  { $mlArgs += "--force" }

Write-Host "Config      : $Config"
Write-Host "Run id      : $RunId"
Write-Host "Time scale  : $TimeScale"
Write-Host "Results dir : results\$RunId"
Write-Host ""

& $learn @mlArgs
