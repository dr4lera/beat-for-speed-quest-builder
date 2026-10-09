using System;
using UnityEngine;

[Serializable] public sealed class QuestSong
{
    public string title;
    public TextAsset chart;
    public AudioClip audio;
    public string track;
    [NonSerialized] public string customAudio;
    [NonSerialized] public string customChart;
}
public sealed class QuestContent : ScriptableObject
{
    public QuestSong[] songs;
    public GameObject forest, city, cube, sting, bike;
    public Material noteMaterial, uiMaterial, skyMaterial, canvasMaterial;
    public Material fragmentMaterial, waterMaterial, glowMaterial;
    public TextAsset chartEffects;
    public Mesh[] fragmentMeshes;
    public AudioClip hitSound;
    public GameObject crashPrefab;
    public AudioClip crashSound;
}
[Serializable] public sealed class QuestChart
{
    public QuestMusic musicData;
    public QuestEntity[] entities;
    public QuestMetadata bfsMetadata;
}
[Serializable] public sealed class QuestMusic
{
    public float bpm, offset, pathStartOffsetDistance;
    public string trackId;
}
[Serializable] public sealed class QuestMetadata { public string songName, songId; }
[Serializable] public sealed class QuestEntity
{
    public float beat, width;
    public int key;
    public string datamodel;
    public Color tint;
}
