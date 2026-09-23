@AGENTS.md

## Nur für Claude

- Der Stop-Hook (`.claude/settings.local.json` → `tools/publish.ps1`) committet und veröffentlicht nach jeder
  Antwort. Trotzdem am Ende einer Aufgabe selbst mit einer aussagekräftigen Nachricht committen – der Hook
  ist nur das Sicherheitsnetz.
- Codex-Arbeit kommt über den Branch `codex` (Ordner `C:\Users\Master\SoccerFight-codex`). Vor dem Mergen
  nach `main` den Diff lesen und kompilieren (capture.ps1), dann `git merge codex`.
- Neues dauerhaftes Projektwissen in `AGENTS.md` eintragen, nicht nur ins private Gedächtnis.
