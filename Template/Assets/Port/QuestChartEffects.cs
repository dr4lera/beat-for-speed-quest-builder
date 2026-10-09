using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable] public class QuestThemeBook { public QuestTheme[] themes; public QuestThemeMap[] mappings; public QuestEffectBinding[] bindings; }
[Serializable] public class QuestThemeMap { public string model, theme; public int eventType; }
[Serializable] public class QuestTheme { public string name; public Color primary; public QuestThemeEffect[] effects; }
[Serializable] public class QuestThemeEffect {
    public string id, colorPropertyName; public int effectType, receiverMode;
    public Color baseColor, emissiveColor, customColor, lightColor, particleStartColor, shardColorA, shardEmissiveColor;
    public float emissiveIntensity, lightIntensity, lightRange, hdrpNormalScale, hdrpSmoothnessRemapMax;
    public bool gameObjectActive, meshRendererEnabled; public Vector3 transformScale;
}
[Serializable] public class QuestEffectBinding { public string track, id; public int[] path; public bool forceEmission; public float emissionLimit; }

public sealed class QuestChartEffects : MonoBehaviour
{
    class Target { public QuestEffectBinding binding; public Transform node; public Renderer renderer; public ParticleSystem particle; public Material[] materials; public Color glow; public bool glowOn; }
    readonly List<Target> targets = new List<Target>();
    static QuestThemeBook book;
    Transform head;
    string trackId;
    Material glowMaterial;
    readonly List<Renderer> halos = new List<Renderer>();
    readonly List<Material> ownedMaterials = new List<Material>();
    readonly List<Target> glowing = new List<Target>();
    readonly List<Target> nearest = new List<Target>();
    MaterialPropertyBlock block = new MaterialPropertyBlock();
    float nextGlow, flash;
    LineRenderer lightning;
    float lightningUntil;
    public int ThemeChanges { get; private set; }
    public void Setup(QuestContent content, Transform track, string id, Transform eye)
    {
        if (book == null) book = JsonUtility.FromJson<QuestThemeBook>(content.chartEffects.text);
        head = eye; trackId = id; glowMaterial = content.glowMaterial;
        lightning = new GameObject("Chart lightning").AddComponent<LineRenderer>(); lightning.transform.SetParent(transform);
        lightning.useWorldSpace = true; lightning.positionCount = 12; lightning.startWidth = lightning.endWidth = .08f;
        var boltMaterial = new Material(content.uiMaterial); boltMaterial.color = new Color(.55f,.8f,1); ownedMaterials.Add(boltMaterial);
        lightning.sharedMaterial = boltMaterial; lightning.enabled = false; lightning.shadowCastingMode = ShadowCastingMode.Off;
        foreach (var b in book.bindings)
        {
            if (b.track != id) continue;
            var node = track; bool found = true;
            foreach (int index in b.path) { if (index >= node.childCount) { found = false; break; } node = node.GetChild(index); }
            if (!found) throw new InvalidOperationException("Original effect binding missing: " + b.id);
            var t = new Target { binding = b, node = node, renderer = node.GetComponent<Renderer>(), particle = node.GetComponent<ParticleSystem>() };
            if (t.renderer != null)
            {
                t.materials = t.renderer.sharedMaterials;
                foreach (var m in t.materials) if (m.HasProperty("_EmissionColor")) m.EnableKeyword("_EMISSION");
            }
            targets.Add(t);
        }
        for (int i = 0; i < 12; i++)
        {
            var halo = new GameObject("Theme light glow"); halo.transform.SetParent(transform);
            halo.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = halo.AddComponent<MeshRenderer>(); r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.enabled = false; halos.Add(r);
        }
        ApplyTheme(id == "city" ? "CityBlue" : "Cloud");
        Debug.Log("BFSQUEST_EFFECT_BINDINGS track=" + id + " targets=" + targets.Count);
    }
    static Mesh quad;
    static Mesh Quad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "Glow quad" };
        quad.vertices = new[] { new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0) };
        quad.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up }; quad.triangles = new[] {0,1,2,0,2,3}; quad.RecalculateBounds(); return quad;
    }
    public static bool Supported(string model)
    {
        string suffix = model.Substring(model.LastIndexOf('/') + 1);
        if (suffix.StartsWith("speed_") || suffix == "screen_flash") return true;
        if (book != null) foreach (var m in book.mappings) if (m.model == suffix) return true;
        return false;
    }
    public void ResetTheme() { ThemeChanges = 0; ApplyTheme(trackId == "city" ? "CityBlue" : "Cloud"); }
    public void Event(string model, Color tint)
    {
        string suffix = model.Substring(model.LastIndexOf('/') + 1);
        if(suffix=="color_rgb") { ApplyColor(tint); return; }
        if (suffix == "screen_flash" || suffix == "spawn_thunder")
        {
            flash = suffix == "spawn_thunder" ? .35f : .2f;
            if(suffix == "spawn_thunder")
            {
                Vector3 end = head.position + Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized*25 + head.right*UnityEngine.Random.Range(-12,12);
                end.y -= 3;
                for(int i=0;i<12;i++) lightning.SetPosition(i,end+Vector3.up*(30-i*30f/11)+head.right*UnityEngine.Random.Range(-1f,1f));
                lightningUntil=Time.time+.2f; lightning.enabled=true;
            }
            Debug.Log("BFSQUEST_EFFECT " + suffix); return;
        }
        foreach (var m in book.mappings)
            if (m.model == suffix)
            {
                if(m.eventType == 4) ApplyTheme(m.theme);
                else if(m.eventType == 2 || m.eventType == 3) Debug.Log("BFSQUEST_VR_VIEW_MARKER " + suffix + " keeping tracked view");
                return;
            }
    }
    void ApplyColor(Color color)
    {
        if(color.a<=0) return;
        Shader.SetGlobalColor("_QuestNoteColor",color); Shader.SetGlobalColor("_QuestNoteEmission",color*1.5f);
        Shader.SetGlobalColor("_QuestShardColor",color); Shader.SetGlobalColor("_QuestHitColor",color);
        Shader.SetGlobalColor("_QuestAmbient",Color.Lerp(new Color(.16f,.2f,.26f),color*.3f,.5f));
        Shader.SetGlobalColor("_QuestLightColor",Color.Lerp(Color.white,color,.5f));
        RenderSettings.fogColor=Color.Lerp(new Color(.08f,.12f,.18f),color*.25f,.5f);
        foreach(var t in targets)
        {
            if(t.renderer==null || !t.binding.id.EndsWith("_material")) continue;
            t.renderer.GetPropertyBlock(block); block.SetColor("_EmissionColor",color*1.5f); t.renderer.SetPropertyBlock(block); block.Clear();
        }
        foreach(var t in glowing) t.glow=color*.8f;
        ThemeChanges++; Debug.Log("BFSQUEST_COLOR_RGB "+color);
    }
    void ApplyTheme(string name)
    {
        var theme = Array.Find(book.themes, t => t.name == name); if (theme == null) return;
        var configs = new Dictionary<string, QuestThemeEffect>(); foreach (var e in theme.effects) configs[e.id] = e;
        glowing.Clear(); ThemeChanges++;
        bool night = name.Contains("Night") || name.StartsWith("Dark") || name == "Ghost";
        var accent = theme.primary; accent.a = 1;
        Shader.SetGlobalColor("_QuestAmbient", night ? Color.Lerp(new Color(.12f,.15f,.2f),accent*.3f,.35f) : new Color(.3f,.34f,.4f));
        Shader.SetGlobalColor("_QuestLightColor", night ? Color.Lerp(Color.white,accent,.6f)*.6f : new Color(1.05f,.97f,.88f));
        RenderSettings.fogColor = night ? Color.Lerp(new Color(.04f,.06f,.09f),accent*.22f,.5f) : Color.Lerp(new Color(.24f,.34f,.43f),accent*.45f,.15f);
        if(RenderSettings.skybox != null) RenderSettings.skybox.SetColor("_Tint", night ? Color.Lerp(new Color(.13f,.16f,.2f),accent*.4f,.3f) : new Color(.5f,.5f,.5f));
        if (configs.TryGetValue("cube_material", out var cube))
        { Shader.SetGlobalColor("_QuestNoteColor",cube.baseColor); Shader.SetGlobalColor("_QuestNoteEmission",Limit(cube.emissiveColor*Mathf.Max(1,cube.emissiveIntensity),2)); }
        if(configs.TryGetValue("shard",out var shard)) Shader.SetGlobalColor("_QuestShardColor",shard.shardColorA);
        if(configs.TryGetValue("HitExplode_material",out var hit)) Shader.SetGlobalColor("_QuestHitColor",Limit(hit.customColor,1));
        foreach (var t in targets)
        {
            if (!configs.TryGetValue(t.binding.id, out var e)) continue;
            if (e.effectType == 6) t.node.gameObject.SetActive(e.gameObjectActive);
            else if (e.effectType == 7 && t.renderer != null) t.renderer.enabled = e.meshRendererEnabled;
            else if (e.effectType == 8) t.node.localScale = e.transformScale;
            else if (e.effectType == 3 && t.particle != null) { var main=t.particle.main; main.startColor=e.particleStartColor; main.maxParticles=Mathf.Min(main.maxParticles,128); }
            else if ((e.effectType == 0 || e.effectType == 4 || e.effectType == 5) && t.renderer != null)
            {
                var emission=Limit(e.emissiveColor*Mathf.Max(1,e.emissiveIntensity),t.binding.forceEmission ? t.binding.emissionLimit : 2);
                t.renderer.GetPropertyBlock(block);
                if (e.effectType == 0) { block.SetColor("_Color",e.baseColor); block.SetColor("_EmissionColor",emission); }
                else if (e.effectType == 4) block.SetColor(e.colorPropertyName == "_BaseColor" ? "_Color" : "_EmissionColor",Limit(e.customColor,2));
                else { block.SetFloat("_BumpScale",e.hdrpNormalScale); block.SetFloat("_Glossiness",e.hdrpSmoothnessRemapMax); }
                t.renderer.SetPropertyBlock(block); block.Clear();
            }
            else if (e.effectType == 1)
            {
                t.glow=Limit(e.lightColor,.8f); t.glowOn=e.lightIntensity>0;
                if (t.glowOn) glowing.Add(t);
            }
        }
        Debug.Log("BFSQUEST_THEME " + name + " glows=" + glowing.Count);
    }
    static Color Limit(Color c,float max)
    { float peak=Mathf.Max(c.r,Mathf.Max(c.g,c.b)); if (peak>max) c*=max/peak; c.a=1; return c; }
    void LateUpdate()
    {
        if(lightning != null && Time.time>lightningUntil) lightning.enabled=false;
        Shader.SetGlobalFloat("_QuestFlash",flash); flash=Mathf.MoveTowards(flash,0,Time.deltaTime*2.5f);
        if (head == null || Time.unscaledTime<nextGlow) return; nextGlow=Time.unscaledTime+.1f;
        nearest.Clear();
        foreach (var t in glowing)
        {
            if (!t.node.gameObject.activeInHierarchy || (t.node.position-head.position).sqrMagnitude>6400) continue;
            int i=0; float d=(t.node.position-head.position).sqrMagnitude;
            while(i<nearest.Count && (nearest[i].node.position-head.position).sqrMagnitude<d) i++;
            if(i<12) { nearest.Insert(i,t); if(nearest.Count>12) nearest.RemoveAt(12); }
        }
        for(int i=0;i<halos.Count;i++)
        {
            var r=halos[i]; r.enabled=i<nearest.Count; if(!r.enabled) continue;
            var t=nearest[i]; r.transform.position=t.node.position; r.transform.rotation=head.rotation; r.transform.localScale=Vector3.one*1.6f;
            block.Clear(); block.SetColor("_Color",t.glow); r.SetPropertyBlock(block);
        }
    }
    void OnDestroy() { foreach(var m in ownedMaterials) Destroy(m); Shader.SetGlobalFloat("_QuestFlash",0); }
}
