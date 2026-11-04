# Batch experiment runner for AirportMetaAI-Sim.
# Usage (from project root, Unity editor can stay open):
#   1. In Unity: AirportSim > Build Batch Player   (creates Builds/AirportSim/AirportSim.exe)
#   2. powershell -ExecutionPolicy Bypass -File tools/run_batch.ps1 -Tag pilot -Seeds 3 -SpawnRate 30
#   3. python analysis/summarize.py Results/pilot
# Re-running the same Tag skips runs that already have a summary_*.csv (resume after interruption).
param(
    [string]$Tag = "main",
    [int]$Seeds = 30,
    [string[]]$Policies = @("NoRobot", "FixedRule", "MetaAI"),
    [string[]]$Scenarios = @("Normal", "Congested"),
    [double]$SpawnRate = -1,     # passengers per minute; -1 = use SimConfig value
    [int]$Robots = -1,           # -1 = use SimConfig value
    [double]$Duration = 600,
    [double]$TimeScale = 20,
    [int]$Parallel = 2,          # number of simulations run at the same time
    [int]$TimeoutSec = 300       # kill a run that takes longer than this
)

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "Builds\AirportSim\AirportSim.exe"
if (-not (Test-Path $exe)) { Write-Error "Not found: $exe  -> run 'AirportSim > Build Batch Player' in Unity first."; exit 1 }

$outDir = Join-Path $root ("Results\" + $Tag)
$logDir = Join-Path $outDir "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$jobs = @()
foreach ($sc in $Scenarios) { foreach ($pol in $Policies) { for ($s = 0; $s -lt $Seeds; $s++) {
    $jobs += [pscustomobject]@{ Policy = $pol; Scenario = $sc; Seed = $s }
} } }

$total = $jobs.Count; $done = 0; $running = @()
$sw = [Diagnostics.Stopwatch]::StartNew()
foreach ($j in $jobs) {
    $name = "$($j.Policy)_$($j.Scenario)_$($j.Seed)"
    if (Test-Path (Join-Path $outDir "summary_$name.csv")) { $done++; continue }

    $args = @("-batchmode", "-nographics", "-policy", $j.Policy, "-scenario", $j.Scenario, "-seed", $j.Seed,
              "-duration", $Duration, "-timescale", $TimeScale, "-outdir", $outDir,
              "-logFile", (Join-Path $logDir "$name.log"))
    if ($SpawnRate -gt 0) { $args += @("-spawnrate", $SpawnRate) }
    if ($Robots -ge 0) { $args += @("-robots", $Robots) }

    while ($running.Count -ge $Parallel) {
        Start-Sleep -Milliseconds 500
        foreach ($p in $running) { if (-not $p.HasExited -and ((Get-Date) - $p.StartTime).TotalSeconds -gt $TimeoutSec) { Write-Warning "timeout, killed pid $($p.Id)"; $p.Kill() } }
        $finished = @($running | Where-Object { $_.HasExited })
        $done += $finished.Count
        $running = @($running | Where-Object { -not $_.HasExited })
        if ($finished.Count -gt 0) { Write-Host ("[{0}/{1}] {2:N0}s elapsed" -f $done, $total, $sw.Elapsed.TotalSeconds) }
    }
    $running += Start-Process -FilePath $exe -ArgumentList $args -PassThru -WindowStyle Hidden
}
$running | Wait-Process -Timeout $TimeoutSec -ErrorAction SilentlyContinue
$running | Where-Object { -not $_.HasExited } | ForEach-Object { Write-Warning "timeout, killed pid $($_.Id)"; $_.Kill() }
Write-Host ("All done: {0} runs in {1:N0}s -> {2}" -f $total, $sw.Elapsed.TotalSeconds, $outDir)
Write-Host ("summary files: {0} / {1}" -f @(Get-ChildItem $outDir -Filter "summary_*.csv").Count, $total)
