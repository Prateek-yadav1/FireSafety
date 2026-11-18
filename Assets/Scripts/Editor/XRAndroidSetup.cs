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
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26; // Android 8.0+ required by Quest
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // 64-bit required
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

        // 2. Configure Oculus Settings (Enable Quest 2, Quest 3, Quest Pro)
        string oculusSettingsPath = "Assets/XR/Settings/OculusSettings.asset";
        OculusSettings oculusSettings = AssetDatabase.LoadAssetAtPath<OculusSettings>(oculusSettingsPath);
        if (oculusSettings == null)
        {
            var guids = AssetDatabase.FindAssets("t:OculusSettings");
            if (guids.Length > 0)
            {
                oculusSettings = AssetDatabase.LoadAssetAtPath<OculusSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
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

        // 3. Get or Create XRGeneralSettingsPerBuildTarget
        XRGeneralSettingsPerBuildTarget perBuildTargetSettings = null;
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perBuildTargetSettings) || perBuildTargetSettings == null)
        {
            perBuildTargetSettings = XRGeneralSettingsPerBuildTarget.GetOrCreate();
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuildTargetSettings, true);
        }

        if (perBuildTargetSettings == null)
        {
            Debug.LogError("[XRAndroidSetup] Could not create or find XRGeneralSettingsPerBuildTarget.");
            return;
        }

        // 4. Configure Android XR Loader
        ConfigureTargetGroup(perBuildTargetSettings, BuildTargetGroup.Android);

        // 5. Configure Standalone XR Loader
        ConfigureTargetGroup(perBuildTargetSettings, BuildTargetGroup.Standalone);

        EditorUtility.SetDirty(perBuildTargetSettings);
        AssetDatabase.SaveAssets();

        Debug.Log("[XRAndroidSetup] ✓ XR Plug-in Management successfully configured for Meta Quest (Android) and PC VR.");
    }

    private static void ConfigureTargetGroup(XRGeneralSettingsPerBuildTarget perBuildTarget, BuildTargetGroup group)
    {
        XRGeneralSettings settings = perBuildTarget.SettingsForBuildTarget(group);
        if (settings == null)
        {
            perBuildTarget.CreateDefaultSettingsForBuildTarget(group);
            settings = perBuildTarget.SettingsForBuildTarget(group);
        }

        if (settings != null)
        {
            settings.InitManagerOnStart = true;

            if (settings.Manager == null)
            {
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            if (settings.Manager != null)
            {
                // Ensure OculusLoader is assigned
                string loaderTypeName = "Unity.XR.Oculus.OculusLoader";
                XRPackageMetadataStore.AssignLoader(settings.Manager, loaderTypeName, group);
                EditorUtility.SetDirty(settings.Manager);
            }

            EditorUtility.SetDirty(settings);
            Debug.Log($"[XRAndroidSetup] ✓ Configured XR settings for {group}");
        }
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
            EditorUtility.DisplayDialog("Build Failed", $"APK Build failed. Check the Unity Console for details.", "OK");
        }
    }
}
#endif
