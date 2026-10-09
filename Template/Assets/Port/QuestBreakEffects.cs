using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

public sealed class QuestBreakEffects : MonoBehaviour
{
    ParticleSystem particles;
    ParticleSystem sparks;
    AudioSource sound;
    AudioClip clip;
    float nextSound;
    public int BreakCount { get; private set; }
    public void Setup(QuestContent content)
    {
        clip = content.hitSound;
        sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 0; sound.volume = .22f;
        particles = gameObject.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 256;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f);
        main.startSize = new ParticleSystem.MinMaxCurve(.06f, .18f); main.gravityModifier = .7f;
        main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(-3.14f, 3.14f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-3.14f, 3.14f); main.startRotationZ = new ParticleSystem.MinMaxCurve(-3.14f, 3.14f);
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.enabled = false;
        var collision = particles.collision; collision.enabled = false;
        var color = particles.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .35f), new GradientAlphaKey(0, 1) }); color.color = gradient;
        var spin = particles.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-4, 4);
        var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
        if (content.fragmentMeshes != null && content.fragmentMeshes.Length > 0) renderer.SetMeshes(content.fragmentMeshes);
        renderer.sharedMaterial = content.fragmentMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        particles.Play();
        sparks = new GameObject("Hit sparks").AddComponent<ParticleSystem>(); sparks.transform.SetParent(transform);
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var sparkMain = sparks.main; sparkMain.loop = false; sparkMain.playOnAwake = false; sparkMain.maxParticles = 128;
        sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .3f); sparkMain.startSize = .035f;
        var sparkEmission = sparks.emission; sparkEmission.enabled = false;
        var sparkShape = sparks.shape; sparkShape.enabled = false;
        var sparkFade = sparks.colorOverLifetime; sparkFade.enabled = true; sparkFade.color = gradient;
        var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>(); sparkRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        sparkRenderer.SetMeshes(content.fragmentMeshes);
        var sparkMaterial = new Material(content.fragmentMaterial); sparkMaterial.SetFloat("_DstBlend", 1); sparkMaterial.SetFloat("_Brightness", 2.2f);
        sparkRenderer.material = sparkMaterial; sparkRenderer.shadowCastingMode = ShadowCastingMode.Off; sparkRenderer.receiveShadows = false;
        sparks.Play();
    }
    public void Break(Vector3 position, Vector3 forward, bool perfect, float rideSpeed)
    {
        if (particles == null) return;
        BreakCount++;
        position += forward * 2.2f;
        var right = Vector3.Cross(Vector3.up, forward).normalized;
        Color themeColor = Shader.GetGlobalColor("_QuestShardColor");
        if(themeColor.a <= 0) themeColor = new Color(.25f,1,.55f,1);
        Color hitColor = Shader.GetGlobalColor("_QuestHitColor");
        if(hitColor.a <= 0) hitColor = new Color(.65f,1,.9f,1);
        for (int i = 0; i < 12; i++)
        {
            var p = new ParticleSystem.EmitParams { position = position + Vector3.up * .25f + right * Random.Range(-.55f, .55f),
                velocity = Random.insideUnitSphere * 2.3f + forward * (rideSpeed * .85f) + Vector3.up * 1.3f,
                startSize = i < 2 ? .35f : Random.Range(.08f, .2f),
                startColor = perfect ? themeColor : Color.Lerp(themeColor,Color.white,.25f) };
            particles.Emit(p, 1);
        }
        for (int i = 0; i < 8; i++)
            sparks.Emit(new ParticleSystem.EmitParams { position = position + Vector3.up * .3f,
                velocity = Random.insideUnitSphere * 4 + forward * rideSpeed + Vector3.up, startColor = hitColor }, 1);
        InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0, perfect ? .35f : .18f, .045f);
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0, perfect ? .35f : .18f, .045f);
        if (clip != null && Time.unscaledTime >= nextSound) { sound.PlayOneShot(clip); nextSound = Time.unscaledTime + .045f; }
    }
}
