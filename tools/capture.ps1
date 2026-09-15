# Spielt ein Screenshot-Szenario (CaptureDriver) in der Build-Kopie ab, ohne den offenen Editor zu stören.
#   powershell -File tools\capture.ps1 -Scenario moves -Out C:\pfad\zu\bildern
# Szenarien: all, moves, portrait, quick. Teilt sich die Kopie und den Mutex mit publish.ps1.
param([string]$Scenario = 'portrait', [Parameter(Mandatory)][string]$Out)

$root   = Split-Path $PSScriptRoot -Parent
$mirror = Join-Path $root '.build\project'
$unity  = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'

$mutex = New-Object System.Threading.Mutex($false, 'SoccerFightPublish')
[void]$mutex.WaitOne()
try {
    foreach ($d in 'Assets', 'Packages', 'ProjectSettings') {
        robocopy (Join-Path $root "SoccerFight\$d") (Join-Path $mirror $d) /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 | Out-Null
    }
    New-Item -ItemType Directory -Force $Out | Out-Null
    $log = Join-Path $Out 'unity.log'
    $unityArgs = "-batchmode -projectPath `"$mirror`" -buildTarget WebGL -executeMethod SoccerFight.EditorTools.CaptureRunner.Run " +
                 "-sfCapture $Scenario -sfOut `"$Out`" -logFile `"$log`""
    $p = Start-Process $unity -ArgumentList $unityArgs -Wait -PassThru -WindowStyle Hidden
    Select-String $log -Pattern 'error CS|Shader error|\[Capture\] (finished|timeout)|Exception' | Select-Object -First 20 | ForEach-Object { $_.Line.Trim() }
    "exit $($p.ExitCode), $((Get-ChildItem $Out -Filter *.png).Count) Bilder"
}
finally {
    $mutex.ReleaseMutex()
    # Ein Veröffentlichungslauf, der während der Aufnahme dazukam, hat nur eine Markierung hinterlassen:
    # jetzt nachholen, sonst geht er verloren.
    if (Test-Path (Join-Path $root '.build\pending')) {
        Start-Process powershell.exe -WindowStyle Hidden -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSScriptRoot\publish.ps1`""
    }
}
