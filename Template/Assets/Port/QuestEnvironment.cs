using UnityEngine;
using UnityEngine.Rendering;

public static class QuestEnvironment
{
    static Mesh waterMesh;
    public static void Apply(GameObject track, Vector3[] path, string id, Material water)
    {
        bool city = id == "city";
        Shader.SetGlobalVector("_QuestLightDir", new Vector4(.35f, .8f, .4f, 0));
        Shader.SetGlobalColor("_QuestLightColor", city ? new Color(1.08f, .94f, .79f) : new Color(.93f, 1, .94f));
        Shader.SetGlobalColor("_QuestAmbient", city ? new Color(.3f, .36f, .43f) : new Color(.28f, .3f, .32f));
        RenderSettings.fogColor = city ? new Color(.24f, .34f, .43f) : new Color(.12f, .19f, .24f);
        if (!city || water == null) return;
        Bounds bounds = new Bounds(path[0], Vector3.zero);
        float lowestRoad = path[0].y;
        foreach (var p in path) { bounds.Encapsulate(p); lowestRoad = Mathf.Min(lowestRoad, p.y); }
        foreach (var r in track.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
        if (waterMesh == null)
        {
            waterMesh = new Mesh { name = "Harbor surface" };
            waterMesh.vertices = new[] { new Vector3(-.5f,0,-.5f), new Vector3(.5f,0,-.5f), new Vector3(.5f,0,.5f), new Vector3(-.5f,0,.5f) };
            waterMesh.triangles = new[] { 0,2,1,0,3,2 }; waterMesh.RecalculateBounds();
        }
        var surface = new GameObject("City harbor water"); surface.AddComponent<MeshFilter>().sharedMesh = waterMesh;
        surface.transform.SetParent(track.transform, false);
        surface.transform.position = new Vector3(bounds.center.x, lowestRoad - 2.5f, bounds.center.z);
        surface.transform.localScale = new Vector3(bounds.size.x + 1000, 1, bounds.size.z + 1000);
        var renderer = surface.AddComponent<MeshRenderer>(); renderer.sharedMaterial = water;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        Debug.Log("BFSQUEST_CITY_WATER roadMin=" + lowestRoad + " bounds=" + bounds);
    }
}
