# Allgemeine Regeln für Coding-Agenten

Diese Regeln gelten projektübergreifend. Projektspezifische Ergänzungen weiter unten bleiben gültig, soweit sie den allgemeinen Regeln nicht widersprechen.

## Arbeitsweise
- Selbstständig, pragmatisch und lösungsorientiert arbeiten; notwendige technische Entscheidungen innerhalb eines erteilten Auftrags selbst treffen.
- Die einfachste robuste und wartbare Lösung bevorzugen. Overengineering, unnötige Abstraktionen und Zukunftsarchitektur vermeiden.
- Änderungen auf die Aufgabe begrenzen; keine großen Refactorings ohne Auftrag.
- Planung, Brainstorming und Vorschläge sind keine Implementierungsfreigabe.
- Neue Funktionen zuerst in einer kleinen, funktionsfähigen Ausführung umsetzen.

## Git und Releases
- Vor Änderungen einen geeigneten Branch `codex/<aufgabe>` anlegen oder den bereits zur Aufgabe gehörenden verwenden. Niemals direkt auf `main` entwickeln.
- Nicht selbstständig nach `origin` pushen oder in `main` mergen; dafür ist ein entsprechender Auftrag erforderlich.
- Kurze, einheitliche und verständliche Commit-Messages verwenden.
- Produktversionen zu definierten Release-Zeitpunkten aktualisieren, nicht automatisch bei jeder Fehlerbehebung.

## Qualität und Abnahme
- Kurze Compilerprüfungen, statische Analysen und gezielte Tests innerhalb der Prozessgrenze eigenständig ausführen.
- Fehlerursachen untersuchen, korrigieren und gezielt erneut prüfen; keine endlosen Wiederholungen.
- Keine unnötigen umfassenden Testreihen ausführen.
- Eine Aufgabe ist erst fertig, wenn ihre vereinbarten Abnahmekriterien erfüllt sind.
- Klar unterscheiden: implementiert, statisch geprüft, kompiliert, automatisiert getestet und praktisch abgenommen.
- Keine Testergebnisse, Quellen, Fähigkeiten oder Erfolge erfinden.

## Prozessdauer und Rückfragen
- Einen einzelnen laufenden Prozess spätestens nach 15 Minuten unterbrechen oder beenden und nachfragen, ob er fortgesetzt werden soll.
- Dabei bisherigen Stand, verbleibenden Aufwand und sinnvolle Fortsetzung kurz nennen.
- Passende Timeouts vorsehen; keine automatische Verlängerung, Neustartschleife oder Umgehung der Rückfrage.
- Normale kurze Arbeit autonom erledigen; bei langen Entwicklungsvorhaben knappe Zwischenstände geben.

## Dokumentation und Aufgaben
- Einen sinnvoll geordneten Dokumentationsordner verwenden. Vorhandene `docs/`, `Documentation/` oder `Assets/Docs/` respektieren; sonst `documentation/` anlegen.
- Dort eine TODO-Liste für Ideen, offene Aufgaben und zukünftige Umsetzungen sowie eine Liste `Umgesetzt` für tatsächlich abgeschlossene Arbeiten führen.
- Bevorzugt `TODO.md` und `Umgesetzt.md` verwenden. Bestehende zentrale Tracker mit beiden Bereichen fortführen, statt doppelte Listen zu pflegen; bei späterer Umstellung Inhalte verlustfrei migrieren.
- Ideen ausdrücklich von beauftragten Aufgaben trennen. Teilumsetzungen aufteilen; offene Anteile bleiben in TODO.
- Erledigte Aufgaben mit Datum und knapper Beschreibung in `Umgesetzt` übernehmen; Implementierung, Tests und Abnahme getrennt kennzeichnen.
- Dokumentation nach Themen gliedern, regelmäßig aktualisieren und Dubletten vermeiden.

## Technik, UI und Sicherheit
- Bei versionsabhängigen APIs nach Möglichkeit aktuelle offizielle Dokumentation nutzen.
- Neue Abhängigkeiten nur bei konkretem Nutzen hinzufügen.
- Wiederverwendbare UI-Komponenten nutzen; in Unity bevorzugt Szenen und Prefabs. Laufzeit-UI ist erlaubt, wenn technisch sinnvoll.
- Keine unnötigen Assets oder Prefabs erstellen.
- Zugangsdaten und Geheimnisse schützen. Keine Benutzerdateien ungefragt löschen und keine kostenpflichtigen Aktionen oder Veröffentlichungen ohne Freigabe.
- Bei riskanten Änderungen Folgen kurz benennen.

## Kontext und Kommunikation
- Nur relevante Dateien, Logs und Informationen lesen; Tokens, Zeit und Kontext sparsam einsetzen.
- Auf Deutsch und in kurzen, klaren Stichpunkten kommunizieren statt langen Fließtexten.
- Abschlussbericht stets knapp: **Geändert**, **Geprüft**, **Offen**.

## Entwicklungsablauf
1. Ziel und Abnahmekriterien klären.
2. Kleinste sinnvolle Lösung implementieren.
3. Gezielt prüfen, Fehler korrigieren und erneut prüfen.
4. TODO/Umgesetzt und notwendige Dokumentation aktualisieren.
5. Ergebnis knapp mit tatsächlichem Prüfstatus melden.

---

# Projektspezifische Ergänzungen

## Unity und Bestandsschutz
- Unity-Version aus `ProjectSettings/ProjectVersion.txt` respektieren; Projekt ist ein 2D-Merge-Puzzle.
- Bestehende Spielmechanik, Punkte-/Combo-Regeln und gespeicherte Spielerpräferenzen bei Änderungen erhalten, soweit der Auftrag nichts anderes verlangt.
- UI bevorzugt in Szene/Prefabs pflegen; vorhandene Instanzen und Referenzen nicht unnötig austauschen.
- Vorhandenen Android-Release-Workflow beibehalten. Builds, Exporte und Store-Uploads sind eigene, ausdrücklich beauftragte Aktionen.
