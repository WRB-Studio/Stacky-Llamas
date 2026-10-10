using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace UnityAndroidRelease.Editor
{
    public static class UnityAndroidReleaseMenu
    {
        private const string Root = "Tools/Unity Android Release Tools/";
        [Serializable] private sealed class Configuration { public string SecretsKey = ""; }
        [Serializable] private sealed class Settings { public string UnityPath = "", AdbPath = "", BuildRoot = ""; }
        [Serializable] private sealed class Device { public string Serial = "", State = ""; }
        [Serializable] private sealed class DeviceResponse { public Device[] Devices = new Device[0]; }
        private static Process process;
        private static Task<string> stdout, stderr;
        private static readonly ConcurrentQueue<string> stages = new ConcurrentQueue<string>();
        private static OperationWindow window;
        private static DateTime started;
        private static string stage, logPath;
        private static float progress;
        private static Action<string> completed;
        private static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem(Root + "Build/APK bauen", false, 10)]
        private static void BuildApk() { Build("Build-Apk.ps1", "Signiertes APK bauen"); }
        [MenuItem(Root + "Build/AAB bauen", false, 11)]
        private static void BuildAab() { Build("Build-Aab.ps1", "Signiertes AAB bauen"); }
        [MenuItem(Root + "Handy/Bauen, installieren und starten", false, 30)]
        private static void Install() { SelectDevice(false); }
        [MenuItem(Root + "Handy/Mit Release-Signierung installieren und starten", false, 31)]
        private static void InstallRelease() { SelectDevice(true); }
        [MenuItem(Root + "Handy/Verbundene Geräte anzeigen", false, 32)]
        private static void ShowDevices() { SelectDevice(false, true); }
        [MenuItem(Root + "Google Drive/APK bauen und lokal exportieren", false, 50)]
        private static void DriveApk() { Build("Build-AndroidAndExportToDrive.ps1", "APK lokal nach Drive exportieren", ",\"Format\":\"apk\""); }
        [MenuItem(Root + "Google Drive/AAB bauen und lokal exportieren", false, 51)]
        private static void DriveAab() { Build("Build-AndroidAndExportToDrive.ps1", "AAB lokal nach Drive exportieren", ",\"Format\":\"aab\""); }
        [MenuItem(Root + "Google Drive/Ziel prüfen", false, 52)]
        private static void CheckDrive() { Run("Build-AndroidAndExportToDrive.ps1", "Lokales Drive-Ziel prüfen", "\"CheckOnly\":true"); }
        [MenuItem(Root + "Google Play/Versioncode prüfen (online)", false, 70)]
        private static void CheckPlay() { Run("Build-AabAndSubmitToPlay.ps1", "Play-Versioncode prüfen", "\"CheckOnly\":true"); }
        [MenuItem(Root + "Google Play/AAB bauen und intern hochladen", false, 71)]
        private static void InternalPlay() { Build("Build-AabAndSubmitToPlay.ps1", "AAB zum internen Play-Test hochladen", ",\"Track\":\"internal\""); }
        [MenuItem(Root + "Google Play/AAB bauen und Production übermitteln...", false, 72)]
        private static void ProductionPlay()
        {
            if (EditorUtility.DisplayDialog("Google Play Production", "Ein signiertes AAB wird gebaut und an Production übermittelt. Googles Prüfung und Veröffentlichungseinstellungen gelten weiterhin. Fortfahren?", "Übermitteln", "Abbrechen"))
                Build("Build-AabAndSubmitToPlay.ps1", "AAB an Play Production übermitteln", ",\"ConfirmProduction\":true");
        }
        [MenuItem(Root + "Einrichtung/Projektkonfiguration öffnen", false, 90)]
        private static void OpenConfig() { OpenPath(Path.Combine(Project, "scripts", "release.config.json")); }
        [MenuItem(Root + "Einrichtung/Signierung einrichten...", false, 91)]
        private static void ConfigureSigning()
        {
            try
            {
                Process.Start(new ProcessStartInfo {
                    FileName = Shell(), WorkingDirectory = Project, UseShellExecute = true,
                    Arguments = "-NoProfile -NoExit -ExecutionPolicy Bypass -File " + Quote(Path.Combine(Project, "scripts", "Set-ReleaseSecrets.ps1"))
                });
            } catch (Exception error) { Debug.LogError(error.Message); }
        }
        [MenuItem(Root + "Einrichtung/Lokalen Drive-Ordner auswählen...", false, 92)]
        private static void ConfigureDrive()
        {
            string folder = EditorUtility.OpenFolderPanel("Synchronisierten Drive-Ordner auswählen", "", "");
            if (!string.IsNullOrWhiteSpace(folder)) Run("Set-DriveExportDirectory.ps1", "Drive-Ziel speichern", "\"DriveDirectory\":" + Json(folder));
        }
        [MenuItem(Root + "Einrichtung/Buildcache und ADB einstellen...", false, 93)]
        private static void ConfigureDevice() { EditorWindow.GetWindow<SettingsWindow>(true, "Buildcache und ADB").Load(); }
        [MenuItem(Root + "Prüfungen/Lokale Tests ausführen", false, 110)]
        private static void Tests() { Run("Test-ReleaseWorkflow.ps1", "Lokale Workflowtests", ""); }
        [MenuItem(Root + "Dateien/Builds und Logs öffnen", false, 130)]
        private static void OpenBuilds() { var folder = Path.Combine(Project, "Builds", "Android"); Directory.CreateDirectory(folder); OpenPath(folder); }
        [MenuItem(Root + "Hilfe/Dokumentation öffnen", false, 150)]
        private static void Help() { Application.OpenURL("https://github.com/WRB-Studio/unity-android-release-tools#readme"); }

        [MenuItem(Root + "Build/APK bauen", true)]
        [MenuItem(Root + "Build/AAB bauen", true)]
        [MenuItem(Root + "Handy/Bauen, installieren und starten", true)]
        [MenuItem(Root + "Handy/Mit Release-Signierung installieren und starten", true)]
        [MenuItem(Root + "Handy/Verbundene Geräte anzeigen", true)]
        [MenuItem(Root + "Google Drive/APK bauen und lokal exportieren", true)]
        [MenuItem(Root + "Google Drive/AAB bauen und lokal exportieren", true)]
        [MenuItem(Root + "Google Drive/Ziel prüfen", true)]
        [MenuItem(Root + "Google Play/Versioncode prüfen (online)", true)]
        [MenuItem(Root + "Google Play/AAB bauen und intern hochladen", true)]
        [MenuItem(Root + "Google Play/AAB bauen und Production übermitteln...", true)]
        [MenuItem(Root + "Einrichtung/Signierung einrichten...", true)]
        [MenuItem(Root + "Einrichtung/Lokalen Drive-Ordner auswählen...", true)]
        [MenuItem(Root + "Einrichtung/Buildcache und ADB einstellen...", true)]
        [MenuItem(Root + "Prüfungen/Lokale Tests ausführen", true)]
        private static bool CanRun() => Application.platform == RuntimePlatform.WindowsEditor && !Application.isBatchMode &&
            !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !BuildPipeline.isBuildingPlayer && process == null;

        private static void Build(string script, string title, string extra = "")
        {
            if (!CanRun() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();
            try { Run(script, title, "\"UnityPath\":" + Json(EditorApplication.applicationPath) + ",\"BuildRoot\":" + Json(Cache()) + extra); }
            catch (Exception error) { Debug.LogError(error.Message); }
        }

        private static void SelectDevice(bool releaseSigning, bool displayOnly = false)
        {
            Run("Get-AndroidDevices.ps1", "Android-Geräte suchen", "\"UnityPath\":" + Json(EditorApplication.applicationPath), output => {
                try
                {
                    var devices = JsonUtility.FromJson<DeviceResponse>(output.Trim()).Devices;
                    if (devices.Length == 0) { EditorUtility.DisplayDialog("Android-Gerät", "Kein Gerät gefunden. Handy verbinden und USB-Debugging aktivieren.", "OK"); return; }
                    if (!displayOnly && devices.Length == 1 && devices[0].State == "device") { InstallDevice(devices[0].Serial, releaseSigning); return; }
                    var picker = ScriptableObject.CreateInstance<DeviceWindow>();
                    picker.devices = devices; picker.releaseSigning = releaseSigning; picker.displayOnly = displayOnly;
                    picker.titleContent = new GUIContent("Android-Gerät auswählen"); picker.minSize = new Vector2(460, 130); picker.ShowUtility();
                } catch (Exception error) { Debug.LogError("Geräteliste konnte nicht gelesen werden: " + error.Message); }
            });
        }

        private static void InstallDevice(string serial, bool releaseSigning)
        {
            Build("Build-ApkAndInstall.ps1", "APK bauen, installieren und Spiel starten",
                ",\"Development\":true,\"DeviceSerial\":" + Json(serial) + (releaseSigning ? ",\"ReleaseSigning\":true" : ""));
        }

        private static string LocalSettingsFolder()
        {
            var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(Path.Combine(Project, "scripts", "release.config.json")));
            if (config == null || !Regex.IsMatch(config.SecretsKey ?? "", "^[A-Za-z0-9][A-Za-z0-9_-]*$")) throw new InvalidOperationException("Ungültiger SecretsKey in release.config.json.");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnityAndroidRelease", config.SecretsKey);
        }
        private static Settings ReadSettings()
        {
            var path = Path.Combine(Project, "Builds", "Android", "device-build.json");
            if (!File.Exists(path)) path = Path.Combine(LocalSettingsFolder(), "device-build.json");
            return File.Exists(path) ? JsonUtility.FromJson<Settings>(File.ReadAllText(path)) ?? new Settings() : new Settings();
        }
        private static string Cache()
        {
            var configured = ReadSettings().BuildRoot;
            return string.IsNullOrWhiteSpace(configured) ? Path.Combine(LocalSettingsFolder(), "BuildCache") : Path.GetFullPath(configured);
        }
        private static string Shell()
        {
            var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
            if (!File.Exists(shell)) shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(shell)) throw new InvalidOperationException("PowerShell wurde nicht gefunden.");
            return shell;
        }
        private static string Json(string value) { return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\""; }
        private static string Quote(string value) { return "\"" + Regex.Replace(Regex.Replace(value, "(\\\\*)\"", "$1$1\\\""), "(\\\\+)$", "$1$1") + "\""; }
        private static void OpenPath(string path) { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }

        private static void Run(string script, string title, string parameters, Action<string> callback = null)
        {
            if (!CanRun()) return;
            try
            {
                var directory = Path.Combine(Project, "Builds", "Android"); Directory.CreateDirectory(directory);
                var id = Guid.NewGuid().ToString("N");
                var request = Path.Combine(directory, "menu-" + id + ".json");
                File.WriteAllText(request, "{\"ScriptName\":" + Json(script) + ",\"Parameters\":{" + parameters + "}}", new UTF8Encoding(false));
                logPath = Path.Combine(directory, "menu-" + id + ".log");
                process = Process.Start(new ProcessStartInfo {
                    FileName = Shell(), WorkingDirectory = Project,
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(Path.Combine(Project, "scripts", "Invoke-UnityMenuCommand.ps1")) + " -RequestFile " + Quote(request),
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                });
                while (stages.TryDequeue(out _)) { }
                completed = callback; started = DateTime.UtcNow; stage = title; progress = 0;
                stdout = ReadOutput(process.StandardOutput); stderr = process.StandardError.ReadToEndAsync();
                EditorApplication.update += Poll;
                EditorApplication.quitting += Cancel;
                AssemblyReloadEvents.beforeAssemblyReload += Cancel;
                window = ScriptableObject.CreateInstance<OperationWindow>();
                window.titleContent = new GUIContent("Unity Android Release Tools"); window.minSize = new Vector2(520, 170); window.ShowUtility();
            } catch (Exception error) { Cancel(); Debug.LogError("Aufruf fehlgeschlagen: " + error.Message); }
        }
        private static async Task<string> ReadOutput(StreamReader reader)
        {
            var output = new StringBuilder(); string line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                output.AppendLine(line);
                if (line.StartsWith("DEVICE_STAGE|", StringComparison.Ordinal)) stages.Enqueue(line);
            }
            return output.ToString();
        }
        private static void Poll()
        {
            if (process == null) return;
            while (stages.TryDequeue(out var next))
            {
                var parts = next.Split('|');
                if (parts.Length == 3 && int.TryParse(parts[1], out var step))
                {
                    progress = Mathf.Clamp01(step / 7f);
                    stage = parts[2];
                }
            }
            if (!process.HasExited || !stdout.IsCompleted || !stderr.IsCompleted) return;
            var exit = process.ExitCode; var callback = completed;
            string output = "", errors = "";
            try { output = stdout.GetAwaiter().GetResult(); errors = stderr.GetAwaiter().GetResult(); File.WriteAllText(logPath, output + errors); }
            catch (Exception error) { exit = 1; errors = error.Message; }
            var resultLog = logPath; Cleanup();
            if (exit == 0)
            {
                string summary = output.Contains("App launch: started") ? "APK installiert, Version geprüft und Spiel auf dem Handy gestartet."
                    : output.Contains("Cloud upload NOT confirmed") ? "Lokale Drive-Kopie bestätigt. Cloud-Upload nicht bestätigt."
                    : "Vorgang erfolgreich.";
                Debug.Log(summary + " Lokales Protokoll: " + resultLog);
                callback?.Invoke(output);
            }
            else { Debug.LogError("Vorgang fehlgeschlagen: " + (errors.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Details im Protokoll") + "\n" + resultLog); }
        }
        private static void Cancel()
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    using (var stop = Process.Start(new ProcessStartInfo {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe"),
                        Arguments = "/PID " + process.Id + " /T /F", UseShellExecute = false, CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    })) { stop?.WaitForExit(2000); }
                }
            }
            catch (Exception error) { Debug.LogWarning("Abbruch konnte nicht bestätigt werden: " + error.Message); }
            finally { Cleanup(); }
        }
        private static void Cleanup()
        {
            EditorApplication.update -= Poll; EditorApplication.quitting -= Cancel; AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            process?.Dispose(); process = null; stdout = stderr = null; completed = null;
            var current = window; window = null; if (current != null) current.Close();
        }
        private sealed class OperationWindow : EditorWindow
        {
            private void OnInspectorUpdate() { Poll(); Repaint(); }
            private void OnGUI()
            {
                EditorGUILayout.LabelField(stage ?? "Vorbereitung", EditorStyles.wordWrappedLabel);
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true)), progress, "Abgeschlossene Arbeitsschritte");
                EditorGUILayout.LabelField("Laufzeit: " + (DateTime.UtcNow - started).ToString(@"hh\:mm\:ss"));
                EditorGUILayout.LabelField("Die Leiste zeigt Arbeitsschritte, keine verbleibende Zeit. Details stehen im lokalen Protokoll.", EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Abbrechen")) Cancel();
            }
            private void OnDestroy() { if (process != null) Cancel(); }
        }
        private sealed class DeviceWindow : EditorWindow
        {
            internal Device[] devices; internal bool releaseSigning, displayOnly;
            private void OnGUI()
            {
                foreach (var device in devices)
                {
                    EditorGUILayout.LabelField(device.Serial + " · " + device.State);
                    if (!displayOnly && device.State == "device" && GUILayout.Button("Dieses Gerät verwenden")) { var serial = device.Serial; Close(); InstallDevice(serial, releaseSigning); }
                }
                EditorGUILayout.LabelField("Bei unauthorized: Handy entsperren und USB-Debugging bestätigen.", EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Geräteliste aktualisieren")) { Close(); SelectDevice(releaseSigning, displayOnly); }
            }
        }
        private sealed class SettingsWindow : EditorWindow
        {
            private string cache = "", adb = "";
            internal void Load() { var settings = ReadSettings(); cache = settings.BuildRoot; adb = settings.AdbPath; }
            private void OnGUI()
            {
                EditorGUILayout.LabelField("Nur lokale Pfade; keine Speicherung in Git.", EditorStyles.wordWrappedLabel);
                cache = EditorGUILayout.TextField("Dedizierter Buildcache", cache);
                if (GUILayout.Button("Leeren Cacheordner auswählen")) { var selected = EditorUtility.OpenFolderPanel("Dedizierten Cache auswählen", "", ""); if (selected.Length > 0) cache = selected; }
                adb = EditorGUILayout.TextField("ADB (optional)", adb);
                if (GUILayout.Button("ADB auswählen")) { var selected = EditorUtility.OpenFilePanel("adb.exe auswählen", "", "exe"); if (selected.Length > 0) adb = selected; }
                if (GUILayout.Button("Speichern"))
                {
                    var parameters = "\"UnityPath\":" + Json(EditorApplication.applicationPath);
                    if (!string.IsNullOrWhiteSpace(cache)) parameters += ",\"BuildRoot\":" + Json(cache);
                    if (!string.IsNullOrWhiteSpace(adb)) parameters += ",\"AdbPath\":" + Json(adb);
                    Run("Set-AndroidDeviceBuildSettings.ps1", "Lokale Buildpfade speichern", parameters); Close();
                }
            }
        }
    }
}
