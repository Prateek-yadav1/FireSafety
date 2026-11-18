#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using Unity.XR.Oculus;

[InitializeOnLoad]
public static class XRAndroidSetup
{
    static XRAndroidSetup()
    {
        EditorApplication.delayCall += EnsureQuestConfiguration;
    }

    [MenuItem("FireSafety/⚡ 1-Click Fix Meta Quest VR Settings", priority = -20)]
    public static void EnsureQuestConfiguration()
    {
        Debug.Log("[XRAndroidSetup] === Configuring XR Plug-in Management for Meta Quest ===");

        // 1. Ensure PlayerSettings for Android (Quest requirements)
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

        // 2. Configure Oculus Settings
        string oculusSettingsPath = "Assets/XR/Settings/OculusSettings.asset";
        OculusSettings oculusSettings = AssetDatabase.LoadAssetAtPath<OculusSettings>(oculusSettingsPath);
        if (oculusSettings == null)
        {
            var guids = AssetDatabase.FindAssets("t:OculusSettings");
            if (guids.Length > 0)
                oculusSettings = AssetDatabase.LoadAssetAtPath<OculusSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (oculusSettings != null)
        {
            oculusSettings.TargetQuest2 = true;
            oculusSettings.TargetQuest3 = true;
            oculusSettings.TargetQuestPro = true;
            oculusSettings.m_StereoRenderingModeAndroid = OculusSettings.StereoRenderingModeAndroid.Multiview;
            EditorUtility.SetDirty(oculusSettings);
            Debug.Log("[XRAndroidSetup] ✓ Enabled Quest 2, Quest 3, and Quest Pro in OculusSettings.");
        }

        // 3. Get XRGeneralSettingsPerBuildTarget from EditorBuildSettings
        XRGeneralSettingsPerBuildTarget perBuildTargetSettings = null;
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perBuildTargetSettings);

        if (perBuildTargetSettings == null)
        {
            // Try to load from asset database
            string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            perBuildTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);

            if (perBuildTargetSettings == null)
            {
                var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (guids.Length > 0)
                    perBuildTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (perBuildTargetSettings == null)
            {
                Debug.Log("[XRAndroidSetup] XRGeneralSettingsPerBuildTarget not found — please configure XR Plug-in Management via Edit > Project Settings > XR Plug-in Management.");
                return;
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuildTargetSettings, true);
        }

        // 4. Configure Android and Standalone build target groups
        ConfigureTargetGroup(perBuildTargetSettings, BuildTargetGroup.Android);
        ConfigureTargetGroup(perBuildTargetSettings, BuildTargetGroup.Standalone);

        EditorUtility.SetDirty(perBuildTargetSettings);
        AssetDatabase.SaveAssets();

        Debug.Log("[XRAndroidSetup] ✓ XR Plug-in Management successfully configured for Meta Quest (Android) and PC VR.");
    }

    private static void ConfigureTargetGroup(XRGeneralSettingsPerBuildTarget perBuildTarget, BuildTargetGroup group)
    {
        // Get existing XR general settings for this platform group
        XRGeneralSettings settings = perBuildTarget.SettingsForBuildTarget(group);

        if (settings == null)
        {
            // Settings not configured — log and skip
            Debug.LogWarning($"[XRAndroidSetup] No XR settings for {group} — configure manually via Edit > Project Settings > XR Plug-in Management.");
            return;
        }

        settings.InitManagerOnStart = true;

        if (settings.Manager != null)
        {
            // Assign the OculusLoader if not already assigned
            string loaderTypeName = "Unity.XR.Oculus.OculusLoader";
            XRPackageMetadataStore.AssignLoader(settings.Manager, loaderTypeName, group);
            EditorUtility.SetDirty(settings.Manager);
        }

        EditorUtility.SetDirty(settings);
        Debug.Log($"[XRAndroidSetup] ✓ Configured XR settings for {group}");
    }

    [MenuItem("FireSafety/🚀 1-Click Build Quest APK", priority = 10)]
    public static void BuildQuestAPK()
    {
        EnsureQuestConfiguration();

        Debug.Log("[XRAndroidSetup] ═══ Building Quest APK to FireSafety.apk ═══");
        string[] scenes = new string[] { "Assets/Scenes/SampleScene.unity" };
        string outputPath = "FireSafety.apk";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[XRAndroidSetup] ★ APK Build Succeeded! ({report.summary.totalSize / (1024 * 1024)} MB) at {outputPath}");
            EditorUtility.DisplayDialog("Build Succeeded", $"FireSafety.apk was successfully built for Meta Quest!\nSize: {report.summary.totalSize / (1024 * 1024)} MB\nPath: {outputPath}", "OK");
        }
        else
        {
            Debug.LogError($"[XRAndroidSetup] ❌ APK Build Failed with {report.summary.totalErrors} errors.");
            EditorUtility.DisplayDialog("Build Failed", "APK Build failed. Check the Unity Console for details.", "OK");
        }
    }
}
#endif
