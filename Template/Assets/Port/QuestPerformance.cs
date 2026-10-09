using System;
using UnityEngine;
using UnityEngine.XR;

public sealed class QuestPerformance : MonoBehaviour
{
    public Camera view;
    float average, stable, nextAdjust;
    float maxScale = .85f, minScale = .65f;
    float maxDistance = 220;
    void Start()
    {
        string model = SystemInfo.deviceModel.ToLowerInvariant();
        bool quest3 = model.Contains("quest 3") || model.Contains("eureka");
        if (quest3) { maxScale = 1.05f; minScale = .8f; maxDistance = 280; }
        QualitySettings.lodBias = quest3 ? 1.25f : .8f;
        XRSettings.renderViewportScale = Mathf.Min(1, maxScale);
        view.farClipPlane = maxDistance;
        average = 1f / 72; nextAdjust = Time.realtimeSinceStartup + 12;
        RenderSettings.fogEndDistance = maxDistance - 15;
        RenderSettings.fogStartDistance = maxDistance * .5f;
        Debug.Log("BFSQUEST_QUALITY model=" + SystemInfo.deviceModel + " scale=" + maxScale + " distance=" + maxDistance);
    }
    void OnApplicationFocus(bool focus)
    {
        if (!focus) return;
        average = 1f / 72; nextAdjust = Time.realtimeSinceStartup + 8; stable = 0;
        XRSettings.renderViewportScale = Mathf.Min(1, maxScale);
    }
    void Update()
    {
        if (!XRSettings.isDeviceActive) return;
        average = Mathf.Lerp(average, Time.unscaledDeltaTime, .025f);
        if (Time.realtimeSinceStartup < nextAdjust) return;
        nextAdjust = Time.realtimeSinceStartup + 2;
        float scale = XRSettings.renderViewportScale;
        if (average > .0155f)
        {
            XRSettings.renderViewportScale = Mathf.Max(minScale, scale - .05f);
            view.farClipPlane = Mathf.Max(110, view.farClipPlane - 10); stable = 0;
        }
        else if (average > 0 && average < .0145f)
        {
            stable += 2;
            if (stable >= 10) { XRSettings.renderViewportScale = Mathf.Min(Mathf.Min(1, maxScale), scale + .025f); view.farClipPlane = Mathf.Min(maxDistance, view.farClipPlane + 5); stable = 0; }
        }
        else stable = 0;
        RenderSettings.fogStartDistance = view.farClipPlane * .5f; RenderSettings.fogEndDistance = view.farClipPlane - 15;
    }
}
