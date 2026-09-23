@AGENTS.md

## Nur für Claude

- Der Stop-Hook (`.claude/settings.local.json` → `tools/publish.ps1`) committet und veröffentlicht nach jeder
  Antwort. Trotzdem am Ende einer Aufgabe selbst mit einer aussagekräftigen Nachricht committen – der Hook
  ist nur das Sicherheitsnetz.
- Codex arbeitet direkt im Hauptordner auf `main` und veröffentlicht selbst. Vor jeder Aufgabe prüfen, ob
  `.codex-busy` im Hauptordner liegt – dann arbeitet Codex gerade: nichts ändern, dem Nutzer Bescheid geben.
  Neue Codex-Commits bei Gelegenheit kurz gegenlesen.
- Neues dauerhaftes Projektwissen in `AGENTS.md` eintragen, nicht nur ins private Gedächtnis.
