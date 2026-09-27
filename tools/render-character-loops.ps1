# Exportiert die sechs Videoloops in der bestehenden Build-Kopie; der offene Editor bleibt frei.
param([Parameter(Mandatory)][string]$Encoder,[string]$Character)
$root = Split-Path $PSScriptRoot -Parent
$mirror = Join-Path $root '.build/project'
$mutex = New-Object System.Threading.Mutex($false, 'SoccerFightPublish')
[void]$mutex.WaitOne()
try {
    foreach ($d in 'Assets','Packages','ProjectSettings') {
        robocopy (Join-Path $root "SoccerFight/$d") (Join-Path $mirror $d) /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 | Out-Null
    }
    $log = Join-Path $root '.build/character-loops.log'
    $out = Join-Path $root 'SoccerFight/Assets/StreamingAssets/CharacterLoops'
    $unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe'
    $sourceRoot = Join-Path $root 'tools/menu-animation/sources'
    $arguments = "-batchmode -projectPath `"$mirror`" -buildTarget WebGL -executeMethod SoccerFight.EditorTools.CharacterLoopExporter.Export -sfLoopOut `"$out`" -sfEncoder `"$Encoder`" -sfSourceRoot `"$sourceRoot`" -logFile `"$log`""
    if ($Character) { $arguments += " -sfCharacter $Character" }
    $process = Start-Process $unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    Select-String $log -Pattern '\[CharacterLoops\]|error CS|Shader error|Exception' | ForEach-Object { $_.Line.Trim() }
    Write-Output "Videoexport: exit $($process.ExitCode)"
} finally {
    $mutex.ReleaseMutex()
}
exit $process.ExitCode
