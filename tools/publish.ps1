# Veröffentlicht den aktuellen Stand: Quellcode -> main, WebGL-Build -> gh-pages (GitHub Pages).
# Läuft automatisch nach jeder Claude-Antwort (Stop-Hook in .claude/settings.local.json),
# kann aber auch von Hand gestartet werden:  powershell -File tools\publish.ps1 [-Force]
# Nur im Hauptordner; siehe AGENTS.md, Abschnitt Zusammenarbeit.
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

# Nur der Hauptordner veröffentlicht. Zweite Arbeitsordner (git worktree, z. B. der von Codex)
# committen selbst und werden von Hand nach main gemergt.
$common = [IO.Path]::GetFullPath((git -C $root rev-parse --path-format=absolute --git-common-dir))
if ((Split-Path $common -Parent) -ne $root) { exit 0 }

New-Item -ItemType Directory -Force $work | Out-Null
function Log($msg) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $msg" | Add-Content $log -Encoding utf8 }

# Arbeitet gerade eine andere KI im Hauptordner, liegt dort .codex-busy: dann nichts committen oder
# bauen, sonst landen halbfertige Änderungen auf main und der Webseite. Die nächste Runde holt es nach.
if (Test-Path (Join-Path $root '.codex-busy')) { Log 'pausiert: .codex-busy liegt im Hauptordner'; exit 0 }

# Nur ein Lauf gleichzeitig. Wer während eines Laufs dazukommt, hinterlässt eine Markierung,
# und der laufende Prozess macht danach noch eine Runde.
$mutex = New-Object System.Threading.Mutex($false, 'SoccerFightPublish')
if (-not $mutex.WaitOne(0)) {
    # Die Build-Kopie ist belegt (anderer Lauf oder capture.ps1): Markierung setzen, der Belegende holt nach
    New-Item -ItemType File -Force $pending | Out-Null
    Log 'wartet: Build-Kopie belegt, wird danach nachgeholt'
    exit 0
}

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
        # Neue Branches haben noch kein Upstream: dann einmal mit -u pushen
        $upstream = git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null
        if (-not $upstream -or [int](git rev-list --count '@{u}..HEAD') -gt 0) {
            $res = git @cred push -q -u origin HEAD 2>&1
            if ($LASTEXITCODE -ne 0) { Log "push FAILED: $res"; break }
            Log 'push ok'
        }

        # Die Webseite zeigt nur main. Andere Branches (z. B. redesign) werden nur gesichert.
        $branch = git rev-parse --abbrev-ref HEAD
        if ($branch -ne 'main') { Log "branch ${branch}: nur gepusht, Webseite bleibt auf main"; continue }

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
            # WaitForExit statt -Wait: -Wait wartet auch auf den Compiler-Server, den Unity zurücklässt (~10 min)
            $p = Start-Process $unity -ArgumentList $unityArgs -PassThru -WindowStyle Hidden
            $p.WaitForExit()
            if ($p.ExitCode -ne 0 -or -not (Test-Path (Join-Path $out 'index.html'))) {
                Log "build FAILED (exit $($p.ExitCode)), siehe .build\unity-build.log"
                break
            }
            Set-Content $built $tree
            Log "build ok"
        }
        $Force = $false

        # 5) Auf gh-pages veröffentlichen, immer als einzelner Commit, damit das Repo klein bleibt.
        #    Die Dateien des vorigen Builds bleiben eine Runde erhalten: Ein Browser, der noch die alte
        #    index.html im Cache hat (Pages: 10 Minuten), lädt so weiter einen vollständigen alten Stand
        #    statt einer Mischung aus alt und neu.
        $url = git remote get-url origin
        $keep = Join-Path $work 'prev-build'
        Remove-Item $keep -Recurse -Force -ErrorAction SilentlyContinue
        $prevIndex = Join-Path $site 'index.html'
        if (Test-Path $prevIndex) {
            $html = Get-Content $prevIndex -Raw
            New-Item -ItemType Directory -Force $keep | Out-Null
            Get-ChildItem (Join-Path $site 'Build') -File -ErrorAction SilentlyContinue |
                Where-Object { $html.Contains($_.Name) } | Copy-Item -Destination $keep
        }
        Remove-Item $site -Recurse -Force -ErrorAction SilentlyContinue
        robocopy $out $site /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if (Test-Path $keep) {
            Get-ChildItem $keep -File | Where-Object { -not (Test-Path (Join-Path $site "Build\$($_.Name)")) } |
                Copy-Item -Destination (Join-Path $site 'Build')
        }
        New-Item -ItemType File -Force (Join-Path $site '.nojekyll') | Out-Null
        git -C $site init -q
        git -C $site config core.autocrlf false
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
