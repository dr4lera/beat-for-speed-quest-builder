using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

public sealed class QuestMobileImports : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Original/")) return;
        var texture = (TextureImporter)assetImporter;
        texture.maxTextureSize = 1024; texture.mipmapEnabled = true;
        texture.textureCompression = TextureImporterCompression.Compressed;
        var android = texture.GetPlatformTextureSettings("Android");
        android.overridden = true; android.maxTextureSize = 1024;
        android.format = TextureImporterFormat.ASTC_6x6; android.compressionQuality = 50;
        texture.SetPlatformTextureSettings(android);
    }
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Original/")) return;
        var audio = (AudioImporter)assetImporter;
        var settings = audio.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .7f;
        audio.defaultSampleSettings = settings;
    }
}
public static class QuestBuild
{
    [Serializable] class CatalogSource { public SourceSong[] songs; }
    [Serializable] class SourceSong { public string title, chart, audio, track; }
    public static void Prepare()
    {
        PlayerSettings.companyName = "LocalPort";
        PlayerSettings.productName = "Beat For Speed Quest";
        PlayerSettings.bundleVersion = "0.1.4.1";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.zrock.beatforspeed.quest");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.Android.bundleVersionCode = 9;
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
        var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        ps.FindProperty("activeInputHandler").intValue = 1; ps.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline = null;
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i); QualitySettings.renderPipeline = null; }
        QualitySettings.antiAliasing = 2; QualitySettings.shadows = ShadowQuality.Disable;
        Shader mobile = Shader.Find("QuestPort/Mobile");
        if (mobile == null) throw new Exception("Mobile shader missing");
        int materials = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Original" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(m);
            m.enableInstancing = true;
            m.SetFloat("_Cull", FloatProperty(so, "_DoubleSidedEnable") > .5f ? 0 : 2);
            var treeTexture = TextureProperty(so, "_TrunkBaseColorMap") ?? TextureProperty(so, "_BarkBaseColorMap");
            var layerTexture = TextureProperty(so, "_BaseColorMap0");
            var normal = TextureProperty(so, "_NormalMap") ?? TextureProperty(so, "_TrunkNormalMap") ?? TextureProperty(so, "_BarkNormalMap") ?? TextureProperty(so, "_NormalMap0") ?? TextureProperty(so, "_BumpMap");
            var mask = TextureProperty(so, "_MaskMap") ?? TextureProperty(so, "_TrunkMaskMap") ?? TextureProperty(so, "_MaskMap0");
            float metallic = FloatProperty(so, "_Metallic");
            float smoothness = Entry(so, "m_Floats", "_Smoothness")?.floatValue ?? .35f;
            if (normal != null)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
            }
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1);
            m.SetTexture("_MaskMap", mask); m.SetFloat("_UseMask", mask != null ? 1 : 0);
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
            if (treeTexture != null) { m.SetTexture("_MainTex", treeTexture); m.SetColor("_Color", ColorProperty(so, "_TrunkBaseColor", Color.white)); m.SetFloat("_Metallic", 0); m.SetFloat("_Glossiness", .2f); }
            else if (TextureProperty(so, "_MainTex") == null && layerTexture != null) m.SetTexture("_MainTex", layerTexture);
            if (m.shader == mobile)
            {
                if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
                if (mask != null) m.EnableKeyword("_MASKMAP"); else m.DisableKeyword("_MASKMAP");
                if (metallic > .05f && treeTexture == null) m.EnableKeyword("_REFLECTIONS"); else m.DisableKeyword("_REFLECTIONS");
                Color glow = m.GetColor("_EmissionColor");
                if (Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b)) > .001f) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(m); continue;
            }
            Color color = ColorProperty(so, "_BaseColor", ColorProperty(so, "_Color", Color.white));
            Color emission = ColorProperty(so, "_EmissiveColor", ColorProperty(so, "_EmissionColor", Color.black));
            Texture texture = TextureProperty(so, "_BaseColorMap") ?? treeTexture ?? layerTexture ?? TextureProperty(so, "_MainTex");
            Texture emissionMap = TextureProperty(so, "_EmissiveColorMap") ?? TextureProperty(so, "_EmissionMap");
            bool transparent = FloatProperty(so, "_SurfaceType") > .5f;
            bool cutout = FloatProperty(so, "_AlphaCutoffEnable") > .5f;
            m.shader = mobile; m.shaderKeywords = Array.Empty<string>();
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1);
            m.SetTexture("_MaskMap", mask); m.SetFloat("_UseMask", mask != null ? 1 : 0);
            m.SetFloat("_Metallic", treeTexture != null ? 0 : metallic);
            m.SetFloat("_Glossiness", treeTexture != null ? .2f : Mathf.Clamp01(smoothness));
            if (normal != null) m.EnableKeyword("_NORMALMAP");
            if (mask != null) m.EnableKeyword("_MASKMAP");
            if (metallic > .05f && treeTexture == null) m.EnableKeyword("_REFLECTIONS");
            m.SetColor("_Color", color); m.SetColor("_EmissionColor", emission);
            if(treeTexture != null) m.SetColor("_Color", ColorProperty(so,"_TrunkBaseColor",Color.white));
            if (Mathf.Max(emission.r, Mathf.Max(emission.g, emission.b)) > .001f) m.EnableKeyword("_EMISSION");
            if (texture != null) m.SetTexture("_MainTex", texture);
            if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
            m.SetFloat("_Cutoff", cutout ? .4f : 0);
            m.SetFloat("_Cull", FloatProperty(so, "_DoubleSidedEnable") > .5f ? 0 : 2);
            if (transparent) { m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0); m.renderQueue = 3000; }
            else m.renderQueue = cutout ? 2450 : 2000;
            EditorUtility.SetDirty(m); materials++;
        }
        Directory.CreateDirectory("Assets/Port/Generated");
        var catalog = ScriptableObject.CreateInstance<QuestContent>();
        var source = JsonUtility.FromJson<CatalogSource>(File.ReadAllText("catalog-source.json"));
        catalog.songs = source.songs.Select(s => new QuestSong { title = s.title, track = s.track,
            chart = AssetDatabase.LoadAssetAtPath<TextAsset>(s.chart), audio = AssetDatabase.LoadAssetAtPath<AudioClip>(s.audio) }).ToArray();
        if (catalog.songs.Any(s => s.chart == null || s.audio == null)) throw new Exception("Original song links missing");
        catalog.forest = Prefab("ForestScene"); catalog.city = Prefab("CityScene"); catalog.cube = Prefab("MainCube"); catalog.sting = Prefab("Sting");
        catalog.bike = MakeBike();
        catalog.fragmentMaterial = MaterialAsset("Fragments", "QuestPort/Fragments");
        catalog.waterMaterial = MaterialAsset("CityWater", "QuestPort/Water");
        catalog.glowMaterial = MaterialAsset("ThemeGlow", "QuestPort/Glow");
        catalog.chartEffects = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Port/ChartEffects.json");
        foreach (var renderer in catalog.cube.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials) { material.SetFloat("_ThemeNote",1); EditorUtility.SetDirty(material); }
        catalog.fragmentMeshes = MakeFragments();
        catalog.hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Original/AudioClip/sfx_hit_1.wav");
        catalog.uiMaterial = MaterialAsset("UI", "Unlit/Color");
        catalog.canvasMaterial = MaterialAsset("Canvas", "UI/Default");
        catalog.skyMaterial = MaterialAsset("Sky", "Skybox/Cubemap");
        var sky = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Original/Cubemap/AllSky_Overcast4_Low.png");
        if (sky != null) catalog.skyMaterial.SetTexture("_Tex", sky);
        EditorUtility.SetDirty(catalog.skyMaterial);
        if (catalog.forest == null || catalog.city == null || catalog.cube == null) throw new Exception("Original track/note assets missing");
        string path = "Assets/Port/Generated/Content.asset";
        var existing = AssetDatabase.LoadAssetAtPath<QuestContent>(path);
        if (existing == null) AssetDatabase.CreateAsset(catalog, path);
        else { EditorUtility.CopySerialized(catalog, existing); UnityEngine.Object.DestroyImmediate(catalog); catalog = existing; }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var game = new GameObject("BFSQuest"); game.AddComponent<QuestRide>().content = catalog;
        EditorSceneManager.SaveScene(scene, "Assets/Port/Generated/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Port/Generated/Main.unity", true) };
        ConfigureXR(); QuestValidation.Run(catalog); AssetDatabase.SaveAssets();
        File.WriteAllText("prepare-result.json", JsonUtility.ToJson(new PrepareResult { songs = catalog.songs.Length, materials = materials }));
        Debug.Log("BFSQUEST_PREPARED songs=" + catalog.songs.Length + " materials=" + materials);
    }
    [Serializable] class PrepareResult { public int songs, materials; }
    static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Original/GameObject/" + name + ".prefab");
    static Material MaterialAsset(string name, string shader)
    {
        string path = "Assets/Port/Generated/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }
    static Mesh[] MakeFragments()
    {
        const string folder = "Assets/Port/Generated/Fragments";
        Directory.CreateDirectory(folder);
        var source = Prefab("board fragture effect");
        if (source == null) throw new Exception("Original fracture prefab missing");
        var meshes = source.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Where(m => m != null).Distinct().Take(4).ToArray();
        if (meshes.Length == 0) throw new Exception("Original fracture meshes missing");
        return meshes.Select((mesh, i) => {
            string path = folder + "/Chunk" + i + ".asset";
            var result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (result != null) return result;
            result = UnityEngine.Object.Instantiate(mesh); result.name = "Original fracture " + i;
            var bounds = mesh.bounds; float scale = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            result.vertices = mesh.vertices.Select(v => (v - bounds.center) / Mathf.Max(.001f, scale)).ToArray();
            result.RecalculateBounds(); AssetDatabase.CreateAsset(result, path); return result;
        }).ToArray();
    }
    static GameObject MakeBike()
    {
        const string output = "Assets/Port/Generated/Bike.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(output);
        if (existing != null)
        {
            var loaded = PrefabUtility.LoadPrefabContents(output);
            var front = loaded.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "tire_front");
            var back = loaded.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "tire_rear");
            if (front != null && back != null)
            {
                var direction = front.bounds.center - back.bounds.center;
                loaded.transform.GetChild(0).Rotate(0, -Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0, Space.World);
                var all = loaded.GetComponentsInChildren<Renderer>(true);
                var bounds = all[0].bounds; foreach (var renderer in all) bounds.Encapsulate(renderer.bounds);
                loaded.transform.GetChild(0).position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            PrefabUtility.SaveAsPrefabAsset(loaded, output); PrefabUtility.UnloadPrefabContents(loaded); return existing;
        }
        var source = Prefab("PathFollower");
        if (source == null) return null;
        var instance = UnityEngine.Object.Instantiate(source);
        var candidates = instance.GetComponentsInChildren<Transform>(true).Where(t => t.name == "BMW_S_1000_RR_2018").ToArray();
        var model = candidates.OrderByDescending(t => t.GetComponentsInChildren<Renderer>(true).Length).FirstOrDefault();
        if (model == null) { UnityEngine.Object.DestroyImmediate(instance); return null; }
        var bike = new GameObject("Original BMW");
        var copy = UnityEngine.Object.Instantiate(model.gameObject, bike.transform);
        copy.SetActive(true); copy.transform.localPosition = Vector3.zero; copy.transform.localRotation = Quaternion.identity;
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) { r.enabled = true; r.gameObject.SetActive(true); }
        var renderers = copy.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            if (length > .001f) copy.transform.localScale *= 2.2f / length;
            var front = renderers.FirstOrDefault(r => r.name == "tire_front");
            var rear = renderers.FirstOrDefault(r => r.name == "tire_rear");
            if(front != null && rear != null)
            {
                var direction=front.bounds.center-rear.bounds.center;
                copy.transform.Rotate(0,-Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,0,Space.World);
            }
            bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            copy.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }
        var result = PrefabUtility.SaveAsPrefabAsset(bike, output);
        UnityEngine.Object.DestroyImmediate(bike); UnityEngine.Object.DestroyImmediate(instance); return result;
    }
    static SerializedProperty Entry(SerializedObject so, string list, string name)
    {
        var array = so.FindProperty("m_SavedProperties." + list);
        for (int i = 0; i < array.arraySize; i++)
        { var e = array.GetArrayElementAtIndex(i); if (e.FindPropertyRelative("first").stringValue == name) return e.FindPropertyRelative("second"); }
        return null;
    }
    static Color ColorProperty(SerializedObject so, string name, Color fallback) => Entry(so, "m_Colors", name)?.colorValue ?? fallback;
    static Texture TextureProperty(SerializedObject so, string name) => Entry(so, "m_TexEnvs", name)?.FindPropertyRelative("m_Texture")?.objectReferenceValue as Texture;
    static float FloatProperty(SerializedObject so, string name) => Entry(so, "m_Floats", name)?.floatValue ?? 0;
    static void ConfigureXR()
    {
        const string path = "Assets/Port/Generated/XRGeneralSettings.asset";
        var per = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
        if (per == null) { per = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>(); AssetDatabase.CreateAsset(per, path); }
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, per, true);
        if (!per.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) per.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var general = per.SettingsForBuildTarget(BuildTargetGroup.Android); general.InitManagerOnStart = true;
        if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
            throw new Exception("Cannot assign Android OpenXR loader");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        foreach (var feature in xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
        {
            string type = feature.GetType().Name;
            feature.enabled = type == "MetaQuestFeature" || type == "OculusTouchControllerProfile" || type == "MetaQuestTouchProControllerProfile" || type == "MetaQuestTouchPlusControllerProfile" ||
                type == "HandTracking" || type == "MetaHandTrackingAim";
            if (type == "MetaQuestFeature")
            {
                feature.GetType().GetMethod("AddTargetDevice")?.Invoke(feature, new object[] { "quest3", "Quest 3", true });
            }
            EditorUtility.SetDirty(feature);
        }
        EditorUtility.SetDirty(per); EditorUtility.SetDirty(general); EditorUtility.SetDirty(xr);
    }
    public static void BuildDesktop()
    {
        Prepare(); PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        Build("Builds/Desktop/BeatForSpeedQuest.exe", BuildTarget.StandaloneWindows64);
    }
    public static void BuildAndroid()
    {
        Prepare(); EditorUserBuildSettings.buildAppBundle = false;
        Build("Builds/BeatForSpeedQuest-Pro-3.apk", BuildTarget.Android);
    }
    static void Build(string output, BuildTarget target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Port/Generated/Main.unity" },
            locationPathName = output, target = target, options = BuildOptions.CompressWithLz4HC });
        File.WriteAllText(target + "-build.txt", report.summary.result + "\nErrors=" + report.summary.totalErrors + "\nSize=" + report.summary.totalSize);
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Quest port build failed: " + report.summary.totalErrors);
        foreach (string name in new[] { "QuestPort/Mobile", "QuestPort/Fragments", "QuestPort/Water", "QuestPort/Glow" })
            if (ShaderUtil.ShaderHasError(Shader.Find(name))) throw new BuildFailedException("Shader failed: " + name);
        Debug.Log("BFSQUEST_BUILD_SUCCESS " + output);
    }
}
