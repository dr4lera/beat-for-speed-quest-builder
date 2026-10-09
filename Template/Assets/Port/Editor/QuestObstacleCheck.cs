using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class QuestObstacleCheck
{
    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Original/GameObject/Sting.prefab");
        var obj = UnityEngine.Object.Instantiate(prefab);
        obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        obj.SetActive(true);
        int before = obj.GetComponentsInChildren<Renderer>().Length;
        typeof(QuestRide).GetMethod("ShowObstacle", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {obj});
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new Exception("Obstacle remains invisible");
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled || renderer.sharedMaterial == null) throw new Exception("Obstacle renderer missing");
            bounds.Encapsulate(renderer.bounds);
        }
        Debug.Log($"BFSQUEST_OBSTACLE_CHECK before={before} after={renderers.Length} center={bounds.center} size={bounds.size}");
        var local = (Bounds)typeof(QuestRide).GetMethod("ObstacleBounds", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {obj});
        if (local.size.x < .01f || local.size.x > 2) throw new Exception("Unexpected obstacle collision width");
        var player = new GameObject("Health check").AddComponent<QuestRide>();
        var hp = typeof(QuestRide).GetField("hp", BindingFlags.Instance | BindingFlags.NonPublic);
        var judge = typeof(QuestRide).GetMethod("Judge", BindingFlags.Instance | BindingFlags.NonPublic);
        if ((float)hp.GetValue(player) != 100) throw new Exception("Starting health must be 100");
        for (int i = 0; i < 100; i++) judge.Invoke(player, new object[] {0f});
        if ((float)hp.GetValue(player) != 100) throw new Exception("Missed notes damaged health");
        Debug.Log($"BFSQUEST_HEALTH_CHECK start=100 after100Misses={hp.GetValue(player)} obstacleLocal={local}");
        UnityEngine.Object.DestroyImmediate(player.gameObject);
        UnityEngine.Object.DestroyImmediate(obj);
    }
}
