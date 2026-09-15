# Veröffentlicht den aktuellen Stand: Quellcode -> main, WebGL-Build -> gh-pages (GitHub Pages).
# Läuft automatisch nach jeder Claude-Antwort (Stop-Hook in .claude/settings.local.json),
# kann aber auch von Hand gestartet werden:  powershell -File tools\publish.ps1 [-Force]
param([switch]$Force)

$root    = Split-Path $PSScriptRoot -Parent
$src     = Join-Path $root 'SoccerFight'
$work    = Join-Path $root '.build'
$mirror  = Join-Path $work 'project'
$out     = Join-Path $work 'webgl'
$site    = Join-Path $work 'site'
$log     = Join-Path $work 'publish.log'
$pending = Join-Path $work 'pending'
$stamp   = Join-Path $work 'deployed.txt'
$built   = Join-Path $work 'built.txt'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
# Pushes melden sich über die GitHub-CLI an (gh auth login), ohne die globale Git-Konfiguration zu ändern
$cred    = @('-c', 'credential.helper=', '-c', "credential.helper=!'C:/Program Files/GitHub CLI/gh.exe' auth git-credential")

New-Item -ItemType Directory -Force $work | Out-Null
function Log($msg) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $msg" | Add-Content $log -Encoding utf8 }

# Nur ein Lauf gleichzeitig. Wer während eines Laufs dazukommt, hinterlässt eine Markierung,
# und der laufende Prozess macht danach noch eine Runde.
$mutex = New-Object System.Threading.Mutex($false, 'SoccerFightPublish')
if (-not $mutex.WaitOne(0)) { New-Item -ItemType File -Force $pending | Out-Null; exit 0 }

try {
    Set-Location $root
    do {
        Remove-Item $pending -ErrorAction SilentlyContinue

        # 1) Quellcode committen und pushen
        git add -A
        $changed = @(git diff --cached --name-only)
        if ($changed.Count -gt 0) {
            $names = ($changed | Select-Object -First 3 | ForEach-Object { Split-Path $_ -Leaf }) -join ', '
            if ($changed.Count -gt 3) { $names += " (+$($changed.Count - 3))" }
            git commit -q -m "Auto-Update: $names"
            Log "commit: $names"
        }
        if ([int](git rev-list --count '@{u}..HEAD') -gt 0) {
            $res = git @cred push -q origin HEAD 2>&1
            if ($LASTEXITCODE -ne 0) { Log "push FAILED: $res"; break }
            Log 'push ok'
        }

        # 2) WebGL nur neu bauen, wenn sich Spielinhalte geändert haben
        $tree = (git rev-parse HEAD:SoccerFight/Assets HEAD:SoccerFight/Packages HEAD:SoccerFight/ProjectSettings) -join ','
        if (-not $Force -and (Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $tree) { continue }
        $sha = git rev-parse --short HEAD

        $isBuilt = (Test-Path $built) -and (Get-Content $built -Raw).Trim() -eq $tree -and (Test-Path (Join-Path $out 'index.html'))
        if ($Force -or -not $isBuilt) {
            # 3) In die Build-Kopie spiegeln (der offene Editor sperrt das Original)
            foreach ($d in 'Assets', 'Packages', 'ProjectSettings') {
                robocopy (Join-Path $src $d) (Join-Path $mirror $d) /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 | Out-Null
            }

            # 4) WebGL bauen
            Log "build $sha ..."
            Remove-Item $out, $built -Recurse -Force -ErrorAction SilentlyContinue
            $unityArgs = "-batchmode -quit -projectPath `"$mirror`" -buildTarget WebGL " +
                         "-executeMethod SoccerFight.EditorTools.WebGLBuilder.Build -sfOut `"$out`" -logFile `"$work\unity-build.log`""
            $p = Start-Process $unity -ArgumentList $unityArgs -Wait -PassThru -WindowStyle Hidden
            if ($p.ExitCode -ne 0 -or -not (Test-Path (Join-Path $out 'index.html'))) {
                Log "build FAILED (exit $($p.ExitCode)), siehe .build\unity-build.log"
                break
            }
            Set-Content $built $tree
            Log "build ok"
        }
        $Force = $false

        # 5) Auf gh-pages veröffentlichen, immer als einzelner Commit, damit das Repo klein bleibt
        $url = git remote get-url origin
        Remove-Item $site -Recurse -Force -ErrorAction SilentlyContinue
        robocopy $out $site /E /NFL /NDL /NJH /NJS /NP | Out-Null
        New-Item -ItemType File -Force (Join-Path $site '.nojekyll') | Out-Null
        git -C $site init -q
        git -C $site add -A
        git -C $site commit -q -m "Deploy $sha"
        $res = git -C $site @cred push -q -f $url HEAD:gh-pages 2>&1
        if ($LASTEXITCODE -ne 0) { Log "deploy FAILED: $res"; break }
        Set-Content $stamp $tree
        Log "deployed $sha"
    } while (Test-Path $pending)
}
finally {
    $mutex.ReleaseMutex()
}
