# Android Release Workflow

Integration aus dem aktuellen `main`-Stand von [Unity Android Release Tools](https://github.com/WRB-Studio/unity-android-release-tools). [Update-Anleitung](https://github.com/WRB-Studio/unity-android-release-tools/blob/main/UPDATING.md) und [Integrationsanleitung](https://github.com/WRB-Studio/unity-android-release-tools/blob/main/INTEGRATION.md) wurden berücksichtigt.

Installation und beauftragte Updates verwenden standardmäßig den frisch abgerufenen Stand von `origin/main` des Tool-Repositories. Eine bestimmte Version wird nur auf ausdrücklichen Wunsch ausgewählt. Builds laden keine Toolupdates nach. Zuletzt abgeglichener Quellstand am 2026-10-10: `8d9fce859fa9a606a621acd524665e109e7b8e28`; diese ID dokumentiert ausschließlich den geprüften Stand und legt zukünftige Updates nicht fest.

## Projektkonfiguration

- Unity: `6000.3.25f1`, Android-Paket: `com.WRBStudio.StackyLamas`.
- Artefaktname: `Stacky-Llamas`; lokale Konfigurationszuordnung: `com-WRBStudio-StackyLamas`.
- Aktivierte Szene: `Assets/Scenes/Ingame.unity`.
- App-Version und Versioncode werden aus Unity gelesen und nicht bei der Installation geändert.
- Signierung und vorhandener gemeinsamer Play-Schlüssel werden außerhalb von Git unter `%LOCALAPPDATA%/UnityAndroidRelease/com-WRBStudio-StackyLamas/release-secrets.xml` verwendet. Passwörter sind mit Windows-DPAPI verschlüsselt.
- Persönlicher Keystore-Pfad und Alias wurden aus `ProjectSettings.asset` entfernt. Der Release-Build setzt die Signierung vorübergehend aus dem lokalen Secret-Speicher. Bereits vorhandene Angaben in älteren Git-Commits bleiben davon unberührt.
- Ein Drive-Ziel wird ausschließlich lokal eingerichtet. Ohne konkreten Exportauftrag wird nicht in synchronisierte Ordner kopiert.
- Der bestehende gemeinsame Uploader hat für diese App die bestätigten Lese-, Test-Release-, Production- und Store-Verwaltungsrechte. Production und Store-Verwaltung wurden nach ausdrücklicher Bestätigung für den beauftragten Release ergänzt; damit verbunden sind die von Google gebündelten Richtlinien- und Deeplink-Rechte. Administrator- und kontoweite Rechte wurden nicht ergänzt.

## Gemeinsamer Service-Account

Der bereits in dieser Session bestätigte gemeinsame Uploader und dessen Cloud-Projekt werden bei Updates beibehalten. Die lokale Secret-Konfiguration verweist auf die vorhandene gemeinsame Schlüsseldatei; dieses Spiel behält seine eigene Signierung und seinen eigenen `SecretsKey`.

Eine funktionierende Schlüsseldatei oder ein passender Accountname allein ersetzt keine bestätigte Auswahl. Fehlt die Auswahl oder widerspricht die lokale Konfiguration ihr, vor Identitäts- oder Rechteänderungen das gewünschte vorhandene Cloud-Projekt und den gemeinsamen Account klären. Bei fehlender Schlüsseldatei zuerst nach einer vorhandenen Datei suchen beziehungsweise deren lokalen Pfad erfragen; niemals den Schlüsselinhalt im Chat anfordern.

Ein allgemeiner Integrations-, Update- oder Uploadauftrag autorisiert keine neuen Cloud-Projekte, Service-Accounts oder zusätzlichen Schlüssel. Diese Ressourcen nur nach einem konkreten ausdrücklichen Auftrag anlegen. Zugriffsfehler rechtfertigen keinen Ersatzaccount. Rechte nur für die gewünschte App und den benötigten Track ergänzen; keine pauschale Freigabe für alle Apps. Bestehende Accounts und Schlüssel ohne gesonderten Bereinigungsauftrag erhalten.

## Lokale Prüfungen und Builds

Unity vor einem Batch-Build speichern. Direkte Release-CLI-Builds ohne `-BuildRoot` erfordern einen geschlossenen Projekt-Editor. Das Unity-Menü und CLI-Aufrufe mit `-BuildRoot` verwenden eine isolierte Projektkopie und erlauben einen geöffneten Quell-Editor. Als Cache ausschließlich einen neuen leeren Ordner außerhalb des Projekts oder einen bereits vom Tool für dieses Projekt markierten Cache verwenden. Lokale Cache-/ADB-Einstellungen lassen sich im Menü oder mit `Set-AndroidDeviceBuildSettings.ps1` speichern und bleiben außerhalb von Git.

Den installierten Editor über `-UnityPath` angeben, falls Unity Hubs Installationspfad nicht aktuell ist. Die Kompilierungs- und Menüprüfung für dieses Update verwendete den zur Projektversion passenden installierten Editor.

```powershell
./scripts/Test-ReleaseWorkflow.ps1
./scripts/Test-UnityHelperCompilation.ps1 -UnityPath '<Editor>/Unity.exe'

# Ausschließlich lokales signiertes APK:
./scripts/Build-Apk.ps1

# Ausschließlich lokales signiertes AAB:
./scripts/Build-Aab.ps1

# Ausschließlich lokales signiertes AAB:
. ./scripts/ReleaseCommon.ps1
Invoke-UnityAndroidBuild -Format aab

# Play-Zugriff und nächsten freien Versioncode prüfen; kein Upload:
./scripts/Build-AabAndSubmitToPlay.ps1 -Track internal -CheckOnly
```

Builds, Nachweise und Logs liegen unter `Builds/Android` und bleiben außerhalb von Git. Der Buildhelfer stellt Signierung, Versioncode und Bundle-/Gradle-Export-Einstellungen wieder her. Für einen späteren Play-Release den online ermittelten freien Versioncode verwenden; lokale Buildtests verwenden standardmäßig den Projektcode.

Play- und Drive-Befehle ohne `-CheckOnly` übertragen Artefakte und sind nur bei einem ausdrücklichen Upload-/Exportauftrag auszuführen. Production verlangt zusätzlich `-ConfirmProduction`, Store-Texte `-ConfirmMetadata`. Geräteinstallation und automatischer App-Start mit `Build-ApkAndInstall.ps1` brauchen ebenfalls einen entsprechenden Auftrag; `-ReplaceExistingApp` ist ein gesondertes Opt-in für Datenlöschung bei Signaturwechsel. Die mitgelieferte GitHub-CI prüft ausschließlich offline unter PowerShell 5.1 und 7; sie baut und veröffentlicht nichts.

## Toolupdate am 2026-10-10

Alle PowerShell-Skripte, der Ruby-Helfer und beide Unity-Editor-Dateien gemeinsam aus dem frisch abgerufenen `origin/main` übernommen. Projektkonfiguration, Signierung, gemeinsame Play-Identität, Store-Texte und vorhandene Buildhelfer-GUID erhalten. Neue Menüdatei mit eigener stabiler `.meta` ergänzt. Die lokale LF-/CRLF-Korrektur samt Regressionstest bleibt erforderlich und wurde zusammengeführt.

Neu ist das Menü **Tools → Unity Android Release Tools** mit 18 Aktionen für APK/AAB, Handy, Drive, Play, Einrichtung, Tests, Dateien und Hilfe. Menü-Builds verwenden eine gesperrte Buildkopie; Fortschritt und Abbruch werden im Editor angezeigt. Gerätebuilds ändern den Versioncode im Quellprojekt nicht und können nach bestätigter Installation das Spiel starten.

Prüfungen für diesen übernommenen Stand:

- Offline-Suite unter Windows PowerShell 5.1 und PowerShell 7 bestanden, einschließlich simuliertem Gerätebuild/Installation/App-Start und Menü-Dispatcher.
- Beide Editor-Dateien gegen Unity `6000.3.25f1` ohne Warnungen kompiliert.
- Alle 18 Menüaktionen in einem temporären Unity-Testprojekt registriert; keine Menüaktion ausgeführt. Ein erster Versuch scheiterte im Unity-internen MovedFromExtractor; die separate Wiederholung bestand.
- Bestehende lokale Signierung entschlüsselbar, Keystore und Play-Schlüsseldatei vorhanden. Die zunächst im Sandboxprofil fehlende Konfiguration ist im normalen Windows-Benutzerprofil vorhanden; keine Neukonfiguration erforderlich.
- Play-Zugriff mit `-CheckOnly` bestanden: Maximum `8`, nächster freier Code `9`. Keine Releaseänderung übertragen.
- Kein echter Android-Build, keine Geräteinstallation, kein Drive-Export und keine Veröffentlichung im Rahmen dieses Updates. Klickbedienung und Gameplay auf dem Handy bleiben praktische Prüfungen für einen gesonderten Build-/Installationsauftrag.

## Lokale Anpassung an die Vorlage

`Get-ProjectVersion` akzeptiert zusätzlich CRLF am Ende der Versioncode-Zeile. Ohne diese Korrektur scheitert die Vorlage an der vorhandenen Windows-Datei. `Test-UnityAndroidBuild.ps1` prüft LF und CRLF. Bei Updates diese Korrektur beibehalten, bis sie in der Quelle enthalten ist.

Skripte und C#-Helfer bei einem Update gemeinsam aus dem frisch abgerufenen `origin/main`-Stand übernehmen und die tatsächlich verwendete Quell-ID anschließend dokumentieren. Projektkonfiguration, bestätigte Service-Account-Auswahl, Secret-Zuordnung und bestehende Unity-Meta-GUID erhalten. Bei Fehlern nur Tooländerungen zurücknehmen; unabhängige Spieländerungen und lokale Zugangsdaten erhalten.

## Verifiziert am 2026-10-04

Erneuter Abgleich nach der Aktualisierung der Quellanleitungen: Skripte, C#-Helfer und CI sind im aktuellen `main`-Stand gegenüber der ersten Integration unverändert. Die lokale CRLF-Korrektur samt Regressionstest bleibt erhalten. Daher war kein erneuter APK-/AAB-Build erforderlich. Die aktualisierten Update- und Service-Account-Regeln wurden übernommen; die vorhandene gemeinsame Account-, Cloud-Projekt- und Schlüsseldateizuordnung stimmt weiterhin überein. Die Play-Prüfung mit `-CheckOnly` wurde erneut erfolgreich ausgeführt (Maximum `7`, nächster Code `8`). Keine Identität, Schlüsseldatei oder Berechtigung wurde bei diesem Abgleich geändert.

- Offline-Suite unter Windows PowerShell 5.1 und PowerShell 7 bestanden, einschließlich LF-/CRLF-Regression.
- Buildhelfer gegen die installierte Unity-Version ohne Warnungen kompiliert.
- Vorhandene Zugangsdaten entschlüsselbar; Keystore und privater Schlüssel des vorhandenen Spiel-Alias geprüft.
- Echter lokaler APK- und AAB-Build bestanden: Version `1.7`, Versioncode `7`, Paket `com.WRBStudio.StackyLamas`, Target SDK `36`, ARM64 und ARMv7.
- APK mit `apksigner` verifiziert, AAB mit `jarsigner` verifiziert und mit `bundletool` validiert. Beide Signaturzertifikate entsprechen dem bestehenden konfigurierten Release-Schlüssel. Die JAR-Prüfung meldet die bei diesem vorhandenen selbstsignierten Android-Zertifikat erwarteten Vertrauenskette-/Zeitstempelhinweise.
- Play-API-Prüfung nach bestätigter App-Freigabe bestanden: höchster belegter Versioncode `7`, nächster freier Code `8`. Leere API-Edits werden verworfen; keine Builds oder Metadaten wurden übertragen.
- Buildgenerierte Performance-Testdateien und die Änderung an `preloadedAssets` wurden zurückgesetzt.
- Kein Upload, kein Drive-Export und keine Veröffentlichung. GitHub-CI wurde nicht online ausgelöst. Gerätetest und tatsächliche Release-Schreibrechte wurden nicht durch einen Upload geprüft.

Lokale Ergebnisse: `Builds/Android/Stacky-Llamas.apk`, `Builds/Android/Stacky-Llamas.aab`; Buildnachweise: `build-apk.json`, `build-aab.json`. Diese Testartefakte verwenden den unveränderten Projektcode `7` und können daher nicht als neuer Play-Release hochgeladen werden; für einen beauftragten Release ermittelt der Play-Befehl den freien Code online.

## Beauftragter Production-Release am 2026-10-04

- Die tatsächlichen bisherigen DE-/EN-Store-Texte aus Google Play gelesen. Falsche Aussagen über Bestenlisten entfernt; lokale Bestwerte, Combo-Feedback und Steuerung anhand des Spielcodes beschrieben. Store-Titel bleibt `Stacky Llama`.
- Neue Kurz-/Langbeschreibungen und Versionshinweise in `release/play-metadata.json` lokal validiert und vom Nutzer ausdrücklich freigegeben.
- Neu gebautes signiertes AAB: sichtbare Version `1.7`, automatisch ermittelter Versioncode `8`, Target SDK `36`. Bundle-Struktur, Signatur, Paketname und Manifest-Versioncode geprüft. Das aktuelle `Stacky-Llamas.aab` und `build-aab.json` ersetzen die vorherigen lokalen Testdateien mit Code `7`; das APK bleibt der vorherige lokale Testbuild.
- AAB und freigegebene DE-/EN-Texte gemeinsam mit Fastlane in `production` übernommen, Status `completed`, vollständiger Roll-out. Bilder und Screenshots nicht geändert.
- Production-Track und sämtliche übertragenen Beschreibungen und Versionshinweise nach dem Upload per API erneut gelesen und exakt verglichen: bestanden.
- Play Console bestätigt die laufenden Vorabprüfungen und die eingereichte Änderung „Vollständigen Roll-out starten“. Verwaltete Veröffentlichung ist ausgeschaltet; die öffentliche Bereitstellung erfolgt automatisch nach Googles Freigabe. Der erfolgreiche API-Commit allein belegt noch keine öffentliche Verfügbarkeit.
- Buildgenerierte Änderungen an Performance-Testdateien und `preloadedAssets` zurückgesetzt. Lokale Projekt-Version und Signierungseinstellungen wiederhergestellt.

## Freigegebene deutsche Beschreibung aktualisiert

Am 2026-10-04 die anschließend vom Nutzer freigegebene lockerere deutsche Kurz- und Langbeschreibung in release/play-metadata.json übernommen und per Play-API gespeichert. Beide Texte nach dem Commit erneut gelesen und exakt verglichen. Englische Beschreibung unverändert verifiziert. Kein neuer Build oder Bundle-Upload; öffentliche Sichtbarkeit hängt von Googles Prüfung ab.

Die freigegebene deutsche Fassung anschließend auf Nutzerauftrag ins Englische übersetzt und am 2026-10-04 ebenfalls lokal übernommen sowie bei Google Play gespeichert. Englische Kurz- und Langbeschreibung nach dem Commit exakt verglichen; deutsche Beschreibung unverändert verifiziert. Kein neuer Bundle-Upload.
