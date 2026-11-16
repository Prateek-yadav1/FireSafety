using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Reporting;

public static class BuildVR {
    // Build the Android APK for Quest
    public static void BuildAndroid() {
        // Ensure the target scene is loaded
        string scenePath = "Assets/Scenes/SampleScene.unity";
        if (!System.IO.File.Exists(scenePath)) {
            Debug.LogError($"Scene not found at {scenePath}");
            return;
        }
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) {
            Debug.LogError("Failed to open SampleScene.");
            return;
        }
        // Load the Kitchen FBX and instantiate it if not already present
        string kitchenPath = "Assets/Kitchen.fbx";
        var kitchenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kitchenPath);
        if (kitchenPrefab != null) {
            // Check if a Kitchen already exists in the scene
            var existing = GameObject.Find(kitchenPrefab.name);
            if (existing == null) {
                PrefabUtility.InstantiatePrefab(kitchenPrefab);
                Debug.Log("Kitchen prefab instantiated into the scene.");
            } else {
                Debug.Log("Kitchen prefab already present in the scene.");
            }
        } else {
            Debug.LogWarning($"Kitchen prefab not found at {kitchenPath}");
        }
        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Build settings
        string[] scenes = new string[] { scenePath };
        string outputPath = "Builds/Android/FireSafety.apk";
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = outputPath;
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;

        // Perform the build
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded) {
            Debug.Log($"Build succeeded: {summary.totalSize} bytes");
        } else if (summary.result == BuildResult.Failed) {
            Debug.LogError("Build failed.");
        }
    }
}
