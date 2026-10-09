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

        [Serializable]
        class BuildReceipt
        {
            public string packageName, versionName, outputPath, format;
            public int versionCode, protocolVersion;
        }
        public static void BuildFromEnvironment()
        {
            if (Environment.GetEnvironmentVariable("UNITY_RELEASE_PROTOCOL_VERSION") != ProtocolVersion.ToString())
                throw new InvalidOperationException("Incompatible release scripts. Update scripts/ and UnityAndroidBuild.cs together; preserve project configuration and .meta files.");

            var outputPath = RequireEnvironmentVariable("UNITY_RELEASE_BUILD_OUTPUT");
            var format = RequireEnvironmentVariable("UNITY_RELEASE_BUILD_FORMAT");
            var isBundle = string.Equals(format, "aab", StringComparison.OrdinalIgnoreCase);

            if (!isBundle && !string.Equals(format, "apk", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("UNITY_RELEASE_BUILD_FORMAT must be 'apk' or 'aab'.");
            }

            var originalKeystoreName = PlayerSettings.Android.keystoreName;
            var originalKeystorePass = PlayerSettings.Android.keystorePass;
            var originalKeyaliasName = PlayerSettings.Android.keyaliasName;
            var originalKeyaliasPass = PlayerSettings.Android.keyaliasPass;
            var originalUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            var originalBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            var originalExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var originalVersionCode = PlayerSettings.Android.bundleVersionCode;

            try
            {
                string packageName = RequireEnvironmentVariable("UNITY_RELEASE_PACKAGE_NAME");
#if UNITY_2021_3_OR_NEWER
                var actualPackageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
#else
                var actualPackageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
#endif
                if (actualPackageName != packageName)
                    throw new InvalidOperationException("Android package name does not match release.config.json.");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = RequireEnvironmentVariable("UNITY_RELEASE_KEYSTORE_PATH");
                PlayerSettings.Android.keystorePass = RequireEnvironmentVariable("UNITY_RELEASE_KEYSTORE_PASSWORD");
                PlayerSettings.Android.keyaliasName = RequireEnvironmentVariable("UNITY_RELEASE_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = RequireEnvironmentVariable("UNITY_RELEASE_KEY_ALIAS_PASSWORD");
                EditorUserBuildSettings.buildAppBundle = isBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                var requestedVersionCode = Environment.GetEnvironmentVariable("UNITY_RELEASE_VERSION_CODE");
                if (!string.IsNullOrWhiteSpace(requestedVersionCode))
                {
                    if (!int.TryParse(requestedVersionCode, out var versionCode) || versionCode < 1)
                    {
                        throw new ArgumentException("UNITY_RELEASE_VERSION_CODE must be a positive integer.");
                    }

                    PlayerSettings.Android.bundleVersionCode = versionCode;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Invalid output path."));
                var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                if (scenes.Length == 0)
                {
                    throw new InvalidOperationException("No enabled scenes are configured for the build.");
                }

                var report = BuildPipeline.BuildPlayer(scenes, outputPath, BuildTarget.Android, BuildOptions.None);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Android build failed: {report.summary.result}");
                }

                Debug.Log($"Android {format.ToUpperInvariant()} created: {outputPath}");
                File.WriteAllText(RequireEnvironmentVariable("UNITY_RELEASE_BUILD_RESULT"), JsonUtility.ToJson(new BuildReceipt
                {
                    packageName = packageName,
                    versionName = PlayerSettings.bundleVersion,
                    versionCode = PlayerSettings.Android.bundleVersionCode,
                    protocolVersion = ProtocolVersion,
                    format = isBundle ? "aab" : "apk",
                    outputPath = outputPath
                }, true));
            }
            finally
            {
                PlayerSettings.Android.keystoreName = originalKeystoreName;
                PlayerSettings.Android.keystorePass = originalKeystorePass;
                PlayerSettings.Android.keyaliasName = originalKeyaliasName;
                PlayerSettings.Android.keyaliasPass = originalKeyaliasPass;
                PlayerSettings.Android.useCustomKeystore = originalUseCustomKeystore;
                EditorUserBuildSettings.buildAppBundle = originalBuildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = originalExportProject;
                PlayerSettings.Android.bundleVersionCode = originalVersionCode;
            }
        }

        private static string RequireEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name)
                   ?? throw new InvalidOperationException($"Missing environment variable: {name}");
        }
    }
}
