using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityAndroidRelease.Editor
{
    public static class UnityAndroidBuild
    {
        // Change only when the PowerShell/C# build contract changes incompatibly.
        public const int ProtocolVersion = 1;
        public const int DeviceBuildProtocolVersion = 1;

        [Serializable]
        class BuildReceipt
        {
            public string packageName, versionName, outputPath, format, buildId;
            public int versionCode, protocolVersion;
        }

        public static void BuildFromEnvironment()
        {
            if (Environment.GetEnvironmentVariable("UNITY_RELEASE_PROTOCOL_VERSION") != ProtocolVersion.ToString())
                throw new InvalidOperationException("Incompatible release scripts. Update scripts/ and UnityAndroidBuild.cs together; preserve project configuration and .meta files.");
            BuildAndroid(false);
        }

        public static void BuildForDeviceFromEnvironment()
        {
            if (Environment.GetEnvironmentVariable("UNITY_DEVICE_PROTOCOL_VERSION") != DeviceBuildProtocolVersion.ToString())
                throw new InvalidOperationException("Update the device scripts and Unity helper together.");
            BuildAndroid(true);
        }

        private static void BuildAndroid(bool device)
        {
            var output = RequireEnvironmentVariable(device ? "UNITY_DEVICE_OUTPUT" : "UNITY_RELEASE_BUILD_OUTPUT");
            var result = RequireEnvironmentVariable(device ? "UNITY_DEVICE_RESULT" : "UNITY_RELEASE_BUILD_RESULT");
            var package = RequireEnvironmentVariable(device ? "UNITY_DEVICE_PACKAGE" : "UNITY_RELEASE_PACKAGE_NAME");
            var format = device ? "apk" : RequireEnvironmentVariable("UNITY_RELEASE_BUILD_FORMAT").ToLowerInvariant();
            if (format != "apk" && format != "aab") throw new ArgumentException("Build format must be apk or aab.");
            var signing = device ? RequireEnvironmentVariable("UNITY_DEVICE_SIGNING") : "release";
            if (signing != "debug" && signing != "release") throw new ArgumentException("Invalid signing mode.");
#if UNITY_2021_3_OR_NEWER
            var target = UnityEditor.Build.NamedBuildTarget.Android;
#else
            var target = BuildTargetGroup.Android;
#endif
            if (PlayerSettings.GetApplicationIdentifier(target) != package)
                throw new InvalidOperationException("Android package name does not match release.config.json.");
            var oldBackend = PlayerSettings.GetScriptingBackend(target);
            var oldArchitecture = PlayerSettings.Android.targetArchitectures;
            var oldCode = PlayerSettings.Android.bundleVersionCode;
            var oldCustom = PlayerSettings.Android.useCustomKeystore;
            var oldStore = PlayerSettings.Android.keystoreName;
            var oldStorePassword = PlayerSettings.Android.keystorePass;
            var oldAlias = PlayerSettings.Android.keyaliasName;
            var oldAliasPassword = PlayerSettings.Android.keyaliasPass;
            var oldBundle = EditorUserBuildSettings.buildAppBundle;
            var oldExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                PlayerSettings.Android.useCustomKeystore = signing == "release";
                if (signing == "release")
                {
                    PlayerSettings.Android.keystoreName = RequireEnvironmentVariable("UNITY_RELEASE_KEYSTORE_PATH");
                    PlayerSettings.Android.keystorePass = RequireEnvironmentVariable("UNITY_RELEASE_KEYSTORE_PASSWORD");
                    PlayerSettings.Android.keyaliasName = RequireEnvironmentVariable("UNITY_RELEASE_KEY_ALIAS");
                    PlayerSettings.Android.keyaliasPass = RequireEnvironmentVariable("UNITY_RELEASE_KEY_ALIAS_PASSWORD");
                }
                if (device)
                {
                    PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                }
                var requestedCode = Environment.GetEnvironmentVariable(device ? "UNITY_DEVICE_VERSION_CODE" : "UNITY_RELEASE_VERSION_CODE");
                if (device || !string.IsNullOrWhiteSpace(requestedCode))
                {
                    if (!int.TryParse(requestedCode, out var code) || code < 1)
                        throw new ArgumentException("Android version code must be a positive integer.");
                    PlayerSettings.Android.bundleVersionCode = code;
                }
                EditorUserBuildSettings.buildAppBundle = format == "aab";
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes are configured for the build.");
                Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("Invalid output path."));
                var options = device && Environment.GetEnvironmentVariable("UNITY_DEVICE_DEVELOPMENT") == "1" ? BuildOptions.Development : BuildOptions.None;
                var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.Android, options);
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Android build failed: " + report.summary.result);
                File.WriteAllText(result, JsonUtility.ToJson(new BuildReceipt
                {
                    packageName = package, versionName = PlayerSettings.bundleVersion,
                    versionCode = PlayerSettings.Android.bundleVersionCode,
                    protocolVersion = device ? DeviceBuildProtocolVersion : ProtocolVersion,
                    format = format, outputPath = output, buildId = report.summary.guid.ToString()
                }, true));
                Debug.Log("Android " + format.ToUpperInvariant() + " created: " + output);
            }
            finally
            {
                if (device)
                {
                    PlayerSettings.SetScriptingBackend(target, oldBackend);
                    PlayerSettings.Android.targetArchitectures = oldArchitecture;
                }
                PlayerSettings.Android.bundleVersionCode = oldCode;
                PlayerSettings.Android.useCustomKeystore = oldCustom;
                PlayerSettings.Android.keystoreName = oldStore;
                PlayerSettings.Android.keystorePass = oldStorePassword;
                PlayerSettings.Android.keyaliasName = oldAlias;
                PlayerSettings.Android.keyaliasPass = oldAliasPassword;
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport;
            }
        }

        private static string RequireEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Missing environment variable: " + name);
            return value;
        }
    }
}
