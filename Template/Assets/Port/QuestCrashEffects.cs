using UnityEngine;
using UnityEngine.XR;

public sealed class QuestCrashEffects : MonoBehaviour
{
    GameObject effect;
    ParticleSystem[] particles;
    AudioSource sound;
    AudioClip clip;
    public void Setup(QuestContent content)
    {
        clip = content.crashSound;
        sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 0; sound.volume = .55f;
        if (content.crashPrefab == null) return;
        effect = Instantiate(content.crashPrefab, transform);
        effect.transform.localPosition = new Vector3(0, .6f, 2.1f);
        effect.transform.localRotation = Quaternion.identity;
        effect.SetActive(false);
        particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particle in particles)
        {
            var main = particle.main; main.loop = false; main.playOnAwake = false;
            main.maxParticles = Mathf.Min(main.maxParticles, 256); main.stopAction = ParticleSystemStopAction.None;
            var collision = particle.collision; collision.enabled = false;
        }
    }
    public void Impact()
    {
        if (effect != null)
        {
            effect.SetActive(true);
            foreach (var particle in particles) { particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Play(true); }
        }
        if (clip != null) sound.PlayOneShot(clip);
        InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0, .65f, .12f);
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0, .65f, .12f);
        Debug.Log("BFSQUEST_CRASH_FX");
    }
}
