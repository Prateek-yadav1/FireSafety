using UnityEngine;
using UnityEngine.XR;

public class StartupLogger : MonoBehaviour {
    void Awake() {
        Debug.Log($"[StartupLogger] XR device active: {XRSettings.isDeviceActive}");
        var cam = Camera.main;
        if (cam != null) {
            Debug.Log($"[StartupLogger] Camera clear flags: {cam.clearFlags}, background alpha: {cam.backgroundColor.a}");
        } else {
            Debug.LogWarning("[StartupLogger] No Main Camera found.");
        }
        if (GameObject.Find("Kitchen")) {
            Debug.Log("[StartupLogger] Kitchen object present in scene.");
        } else {
            Debug.LogWarning("[StartupLogger] Kitchen object NOT found in scene.");
        }
    }
}
