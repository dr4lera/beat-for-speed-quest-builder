using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.UI;
using System.Threading.Tasks;

public sealed class QuestRide : MonoBehaviour
{
    public QuestContent content;
    QuestTracking tracking;
    Transform origin, head, panel;
    Camera cameraRig;
    QuestGui gui;
    QuestBreakEffects effects;
    QuestChartEffects chartEffects;
    readonly List<QuestSong> library = new List<QuestSong>();
    readonly List<LiveNote> notes = new List<LiveNote>();
    readonly Queue<GameObject> cubes = new Queue<GameObject>(), stings = new Queue<GameObject>();
    GameObject track, bike;
    string trackId, message = "Lean or use a thumbstick to steer. Point to choose a song.";
    Vector3[] positions;
    float[] distances;
    float pathLength, speed, songStartOffset, songOffset, riderOffset, nextImmune, accuracySum;
    float riderVelocity, nextHudUpdate;
    float[] speedTimes, speedDistances, speedMultipliers;
    int effectCursor;
    double startDsp;
    AudioSource music;
    AudioClip customClip;
    QuestChart chart;
    int cursor, totalNotes, judged, perfect, good, misses, combo, maxCombo, score, page;
    float hp = 100;
    bool playing, paused, loading, smoke;
    bool importing;
    bool capturedBreak;
    float smokeStart, smokeBestOffset;
    QuestMenuItem dwell;
    float dwellTime;
    bool dwellFired;
    Material uiMaterial;
    GameObject reticle;
    LineRenderer pointerLine;
    class LiveNote { public QuestEntity e; public GameObject obj; public float time; public bool obstacle; }
    float SongTime => (float)(AudioSettings.dspTime - startDsp);

    void Awake()
    {
        gameObject.name = "BFSQuest";
        Application.targetFrameRate = 72;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.Disable;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.65f, .68f, .72f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.12f, .19f, .24f); RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 145;
        origin = new GameObject("Ride Origin").transform;
        head = new GameObject("Head").transform; head.SetParent(origin, false); head.localPosition = new Vector3(0, 1.5f, 0);
        cameraRig = head.gameObject.AddComponent<Camera>(); cameraRig.nearClipPlane = .08f; cameraRig.farClipPlane = 160;
        RenderSettings.skybox = content.skyMaterial;
        if (content.skyMaterial != null) Shader.SetGlobalTexture("_QuestReflection", content.skyMaterial.GetTexture("_Tex"));
        cameraRig.clearFlags = CameraClearFlags.Skybox; cameraRig.backgroundColor = RenderSettings.fogColor;
        head.gameObject.AddComponent<AudioListener>();
        tracking = origin.gameObject.AddComponent<QuestTracking>(); tracking.head = head;
        music = gameObject.AddComponent<AudioSource>(); music.spatialBlend = 0;
        uiMaterial = new Material(content.uiMaterial); uiMaterial.color = new Color(.02f, .07f, .11f);
        panel = new GameObject("Quest Menu").transform;
        gui = new QuestGui(panel, origin, content.canvasMaterial, TogglePause);
        effects = new GameObject("Note breaks").AddComponent<QuestBreakEffects>(); effects.Setup(content);
        reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(reticle.GetComponent<Collider>());
        reticle.transform.localScale = Vector3.one * .025f;
        reticle.GetComponent<Renderer>().material = new Material(content.uiMaterial) { color = Color.cyan };
        pointerLine = new GameObject("Pointer").AddComponent<LineRenderer>(); pointerLine.positionCount = 2;
        pointerLine.startWidth = pointerLine.endWidth = .004f;
        pointerLine.material = reticle.GetComponent<Renderer>().material;
        smoke = Array.IndexOf(Environment.GetCommandLineArgs(), "-quest-smoke") >= 0;
        gameObject.AddComponent<QuestPerformance>().view = cameraRig;
    }
    IEnumerator Start()
    {
        library.AddRange(content.songs); yield return LoadTrack("forest"); ShowLibrary();
        yield return RefreshImports(false);
        for (int i = 0; i < 40; i++) { WarmNote(content.cube, cubes); WarmNote(content.sting, stings); }
        if (smoke)
        {
            smokeStart = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-quest-song");
            string selected = i >= 0 && i + 1 < args.Length ? args[i + 1] : "WarmUp";
            var song = selected == "custom" ? library.Find(s => s.customAudio != null) : library.Find(s => s.title == selected);
            StartCoroutine(Play(song ?? library[0]));
        }
    }
    IEnumerator RefreshImports(bool lastPage)
    {
        if(importing) yield break;
        importing=true; message="Reading local songs..."; gui.status.text=message;
        string root=QuestSongImport.ImportRoot;
        var task=Task.Run(()=>QuestSongImport.Scan(root));
        while(!task.IsCompleted) yield return null;
        if(task.IsFaulted) message="Import failed: "+task.Exception.GetBaseException().Message;
        else
        {
            library.Clear(); library.AddRange(content.songs); library.AddRange(task.Result);
            message=QuestSongImport.LastError!=null ? "Import failed: "+QuestSongImport.LastError : "Found "+task.Result.Count+" imported songs";
            if(lastPage) page=Mathf.Max(0,(library.Count-1)/4);
        }
        importing=false; if(!playing) ShowLibrary();
    }
    IEnumerator LoadTrack(string id)
    {
        if (trackId == id && track != null) yield break;
        if (track != null) Destroy(track);
        if (bike != null) Destroy(bike);
        yield return null;
        trackId = id; speed = id == "city" ? 45 : 30;
        track = Instantiate(id == "city" ? content.city : content.forest);
        track.transform.position = Vector3.zero; track.SetActive(true);
        PathPointTool best = null;
        foreach (var p in track.GetComponentsInChildren<PathPointTool>(true))
            if (best == null || p.Points.Count > best.Points.Count) best = p;
        if (best == null || best.Points.Count < 2) throw new InvalidOperationException("Original track path is missing");
        var smooth = new List<Vector3>();
        for (int i = 0; i < best.Points.Count - 1; i++)
        {
            Vector3 a = best.Points[Mathf.Max(0, i - 1)].position, b = best.Points[i].position;
            Vector3 c = best.Points[i + 1].position, d = best.Points[Mathf.Min(best.Points.Count - 1, i + 2)].position;
            for (int j = 0; j < 12; j++)
            {
                float t = j / 12f;
                smooth.Add(.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t));
            }
        }
        smooth.Add(best.Points[best.Points.Count - 1].position);
        positions = smooth.ToArray(); distances = new float[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(positions[i - 1], positions[i]);
        }
        pathLength = distances[distances.Length - 1];
        QuestEnvironment.Apply(track, positions, id, content.waterMaterial);
        chartEffects = track.AddComponent<QuestChartEffects>(); chartEffects.Setup(content, track.transform, id, head);
        if (pathLength < 10) throw new InvalidOperationException("Original track path has invalid length");
        foreach (var r in track.GetComponentsInChildren<Renderer>(true))
        { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        foreach (var l in track.GetComponentsInChildren<Light>(true)) l.enabled = false;
        if (content.bike != null) { bike = Instantiate(content.bike); bike.SetActive(false); }
        SetRidePose(0); Debug.Log("BFSQUEST_TRACK " + id + " points=" + positions.Length + " length=" + pathLength);
    }
    void Sample(float distance, out Vector3 p, out Vector3 direction)
    {
        float d = Mathf.Repeat(distance, pathLength);
        int i = Array.BinarySearch(distances, d); if (i < 0) i = ~i;
        i = Mathf.Clamp(i, 1, distances.Length - 1);
        float length = Mathf.Max(.0001f, distances[i] - distances[i - 1]);
        p = Vector3.Lerp(positions[i - 1], positions[i], (d - distances[i - 1]) / length);
        direction = (positions[i] - positions[i - 1]).normalized;
    }
    void SetRidePose(float distance)
    {
        Sample(distance, out var p, out var direction);
        var planar = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (planar.sqrMagnitude < .01f) planar = Vector3.forward;
        var rotation = Quaternion.LookRotation(planar, Vector3.up);
        var right = rotation * Vector3.right;
        origin.position = p + right * riderOffset + Vector3.up * .4f;
        origin.rotation = playing ? Quaternion.Slerp(origin.rotation, rotation, 1 - Mathf.Exp(-12 * Time.deltaTime)) : rotation;
        if (bike != null) { bike.transform.position = p + right * riderOffset - Vector3.up * .5f; bike.transform.rotation = rotation; }
    }
    IEnumerator Play(QuestSong song)
    {
        if (loading || importing) yield break;
        loading = true; message = "Loading " + song.title; gui.status.text = message;
        try { chart = QuestSongImport.Parse(song.chart != null ? song.chart.text : File.ReadAllText(song.customChart)); }
        catch (Exception ex) { message = ex.Message; loading = false; ShowLibrary(); yield break; }
        AudioClip clip = song.audio;
        if (song.customAudio != null)
        {
            AudioType type = song.customAudio.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS :
                song.customAudio.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG : AudioType.WAV;
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(song.customAudio).AbsoluteUri, type))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { message = request.error; loading = false; ShowLibrary(); yield break; }
                if (customClip != null) Destroy(customClip);
                customClip = DownloadHandlerAudioClip.GetContent(request); clip = customClip;
            }
        }
        if (clip == null) { message = "Song audio is missing"; loading = false; ShowLibrary(); yield break; }
        yield return LoadTrack((chart.musicData.trackId ?? song.track ?? "forest").ToLowerInvariant() == "city" ? "city" : "forest");
        effectCursor = 0; chartEffects.ResetTheme();
        ClearNotes(); cursor = totalNotes = judged = perfect = good = misses = combo = maxCombo = score = 0;
        accuracySum = 0; hp = 100; nextImmune = 0; riderOffset = 0; riderVelocity = 0;
        foreach (var e in chart.entities) if (Collectible(e)) totalNotes++;
        songOffset = chart.musicData.offset; songStartOffset = chart.musicData.pathStartOffsetDistance;
        BuildSpeedTimeline();
        tracking.Recenter(); SetRidePose(songStartOffset); music.clip = clip;
        startDsp = AudioSettings.dspTime + 2; music.PlayScheduled(startDsp);
        playing = true; paused = false; loading = false; panel.gameObject.SetActive(false);
        reticle.SetActive(false); pointerLine.enabled = false;
        if (bike != null) bike.SetActive(true);
        Debug.Log("BFSQUEST_PLAY title=" + song.title + " notes=" + totalNotes + " clip=" + clip.length);
    }
    static bool Collectible(QuestEntity e) => e.datamodel.EndsWith("/spawn_cube") || e.datamodel.EndsWith("/spawn_jiucai");
    static bool Obstacle(QuestEntity e) => e.datamodel.EndsWith("/spawn_sting");
    float EventTime(QuestEntity e) => e.beat * 60 / chart.musicData.bpm + songOffset;
    void Update()
    {
        if (tracking == null || positions == null) return;
        bool back = tracking.left.back || tracking.right.back || Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (back && playing) TogglePause();
        if (playing && !paused)
        {
            float time = SongTime;
            float steer = tracking.Steering;
            if (smoke)
            {
                foreach (var e in chart.entities)
                    if (Collectible(e) && EventTime(e) >= time) { smokeBestOffset = (e.key - 7) * 1.2f; break; }
                steer = Mathf.Clamp(smokeBestOffset / 2.5f, -1, 1);
            }
            riderOffset = Mathf.SmoothDamp(riderOffset, steer * 2.5f, ref riderVelocity, .085f, 15, Time.deltaTime);
            while(effectCursor < chart.entities.Length && EventTime(chart.entities[effectCursor]) <= time)
            {
                var e = chart.entities[effectCursor++];
                if (!Collectible(e) && !Obstacle(e)) chartEffects.Event(e.datamodel,e.tint);
            }
            float distance = songStartOffset + DistanceAt(time);
            SetRidePose(distance);
            while (cursor < chart.entities.Length && EventTime(chart.entities[cursor]) <= time + 5)
            {
                var e = chart.entities[cursor++];
                if (!Collectible(e) && !Obstacle(e)) continue;
                bool obstacle = Obstacle(e);
                var prefab = obstacle ? content.sting : content.cube;
                var pool = obstacle ? stings : cubes;
                GameObject obj = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab);
                Sample(songStartOffset + DistanceAt(EventTime(e)), out var p, out var direction);
                var rotation = Quaternion.LookRotation(direction);
                obj.transform.position = p + rotation * Vector3.right * ((e.key - 7) * (obstacle ? 1 : 1.2f)) + Vector3.up * .5f;
                obj.transform.rotation = rotation; obj.SetActive(true);
                foreach (var c in obj.GetComponentsInChildren<Collider>()) c.enabled = false;
                notes.Add(new LiveNote { e = e, obj = obj, time = EventTime(e), obstacle = obstacle });
            }
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var n = notes[i]; float dt = time - n.time;
                float delta = Mathf.Abs(riderOffset - (n.e.key - 7) * (n.obstacle ? 1 : 1.2f));
                if (dt >= -.04f && dt <= .08f && delta <= (n.obstacle ? .65f : 1.2f))
                {
                    if (n.obstacle) { combo = 0; if (time >= nextImmune) { hp -= 30; nextImmune = time + 2; } }
                    else {
                        effects.Break(n.obj.transform.position, origin.forward, delta <= .6f, SpeedAt(time)); Judge(delta <= .6f ? 1 : .3f);
                        if (smoke && !capturedBreak) { capturedBreak = true; StartCoroutine(CaptureBreak()); }
                    }
                    ReturnNote(n); notes.RemoveAt(i);
                }
                else if (dt > .08f)
                { if (!n.obstacle) Judge(0); ReturnNote(n); notes.RemoveAt(i); }
            }
            if (Time.unscaledTime >= nextHudUpdate)
            {
                nextHudUpdate = Time.unscaledTime + .1f;
                gui.hud.text = time < 0 ? "Get ready " + Mathf.CeilToInt(-time) :
                    $"Score {score:N0}   Combo {combo}   HP {hp:0}\nPerfect {perfect}   Good {good}   Miss {misses}";
            }
            gui.hudRoot.SetActive(true); UpdateMenu();
            if (hp <= 0 || time > music.clip.length + .1f) Finish(hp <= 0 ? "Ride ended" : "Song complete");
        }
        else { gui.hudRoot.SetActive(false); UpdateMenu(); }
        if (smoke && Time.realtimeSinceStartup - smokeStart > 25)
        {
            smoke = false;
            string outDir = Path.Combine(Application.persistentDataPath, "Smoke");
            string[] args = Environment.GetCommandLineArgs(); int evidence = Array.IndexOf(args, "-quest-evidence");
            if (evidence >= 0 && evidence + 1 < args.Length) outDir = args[evidence + 1];
            Directory.CreateDirectory(outDir);
            CaptureFrame(Path.Combine(outDir, "ride.png"));
            File.WriteAllText(Path.Combine(outDir, "result.json"), JsonUtility.ToJson(new SmokeResult {
                points = positions.Length, judged = judged, perfect = perfect, misses = misses, breaks = effects.BreakCount, themes = chartEffects.ThemeChanges, time = SongTime, renderers = track.GetComponentsInChildren<Renderer>().Length }));
            Debug.Log("BFSQUEST_SMOKE " + outDir); StartCoroutine(QuitAfterCapture());
        }
    }
    IEnumerator CaptureBreak()
    {
        yield return new WaitForSeconds(.06f);
        string outDir = Path.Combine(Application.persistentDataPath, "Smoke");
        string[] args = Environment.GetCommandLineArgs(); int evidence = Array.IndexOf(args, "-quest-evidence");
        if (evidence >= 0 && evidence + 1 < args.Length) outDir = args[evidence + 1];
        Directory.CreateDirectory(outDir); CaptureFrame(Path.Combine(outDir, "break.png"));
    }
    void BuildSpeedTimeline()
    {
        var times = new List<float> {0}; var lengths = new List<float> {0}; var multipliers = new List<float> {1};
        foreach(var e in chart.entities)
        {
            string type=e.datamodel.Substring(e.datamodel.LastIndexOf('/')+1);
            float value=type=="speed_1" ? 1 : type=="speed_2" ? 1.5f : type=="speed_3" ? 2 : 0;
            if(value==0) continue;
            float time=Mathf.Max(0,EventTime(e)); int last=times.Count-1;
            if(time==times[last]) { multipliers[last]=value; continue; }
            lengths.Add(lengths[last]+(time-times[last])*speed*multipliers[last]); times.Add(time); multipliers.Add(value);
        }
        speedTimes=times.ToArray(); speedDistances=lengths.ToArray(); speedMultipliers=multipliers.ToArray();
    }
    int SpeedSegment(float time)
    { int i=Array.BinarySearch(speedTimes,Mathf.Max(0,time)); return i>=0 ? i : Mathf.Max(0,~i-1); }
    float DistanceAt(float time)
    { time=Mathf.Max(0,time); int i=SpeedSegment(time); return speedDistances[i]+(time-speedTimes[i])*speed*speedMultipliers[i]; }
    float SpeedAt(float time) => speed*speedMultipliers[SpeedSegment(time)];
    [Serializable] class SmokeResult { public int points, judged, perfect, misses, renderers, breaks, themes; public float time; }
    void CaptureFrame(string path)
    {
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        var previousTarget = cameraRig.targetTexture;
        cameraRig.targetTexture = target; cameraRig.Render(); RenderTexture.active = target;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        cameraRig.targetTexture = previousTarget; RenderTexture.active = previous;
        Destroy(texture); target.Release(); Destroy(target);
    }
    IEnumerator QuitAfterCapture() { yield return new WaitForSecondsRealtime(2); Application.Quit(); }
    void Judge(float value)
    {
        judged++; accuracySum += value;
        if (value == 0) { misses++; combo = 0; }
        else { if (value == 1) { perfect++; hp = Mathf.Min(100, hp + 1); } else good++; combo++; maxCombo = Mathf.Max(combo, maxCombo); }
        score = totalNotes == 0 ? 0 : Mathf.RoundToInt(accuracySum / totalNotes * 1000000);
    }
    void Finish(string caption)
    {
        playing = paused = false; music.Stop(); ClearNotes();
        message = $"{caption}\nScore {score:N0}  |  Accuracy {(judged == 0 ? 0 : accuracySum / judged * 100):0.0}%  |  Best combo {maxCombo}";
        ShowLibrary();
    }
    void TogglePause()
    {
        paused = !paused;
        if (paused) { music.Pause(); pauseAt = AudioSettings.dspTime; ShowPause(); }
        else { startDsp += AudioSettings.dspTime - pauseAt; music.UnPause(); panel.gameObject.SetActive(false); tracking.Recenter(); }
    }
    double pauseAt;
    void WarmNote(GameObject prefab, Queue<GameObject> pool)
    {
        if (prefab == null) return;
        var obj = Instantiate(prefab); obj.SetActive(false);
        foreach (var c in obj.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        pool.Enqueue(obj);
    }
    void ReturnNote(LiveNote n) { n.obj.SetActive(false); (n.obstacle ? stings : cubes).Enqueue(n.obj); }
    void ClearNotes() { foreach (var n in notes) if (n.obj != null) ReturnNote(n); notes.Clear(); }
    void MenuStart()
    {
        panel.gameObject.SetActive(true); panel.position = origin.position + Vector3.up * head.localPosition.y;
        panel.rotation = Quaternion.Euler(0, head.eulerAngles.y, 0); panel.position += panel.forward * 2.5f;
        gui.hudRoot.SetActive(false); dwell = null; dwellTime = 0; dwellFired = false;
    }
    void ShowLibrary()
    {
        MenuStart(); gui.Clear("BEAT FOR SPEED", message);
        int first = page * 4;
        if (first >= library.Count) first = page = 0;
        for (int i = first; i < Mathf.Min(first + 4, library.Count); i++)
        {
            QuestSong song = library[i];
            string subtitle = song.track + (song.customAudio != null ? "   ·   Your song" : "   ·   Built-in");
            gui.Button(song.title + "\n" + subtitle, 200 - (i - first) * 80, () => StartCoroutine(Play(song)));
        }
        gui.Button("Import songs / local files", -150, ShowImports, true);
        gui.Button("Next page   ·   " + (page + 1) + "/" + Mathf.Max(1, (library.Count + 3) / 4), -230, () => { page = library.Count <= 4 ? 0 : (page + 1) % ((library.Count + 3) / 4); ShowLibrary(); });
        gui.Button("Steering: " + tracking.mode + "   ·   Change", -310, () => { tracking.CycleMode(); ShowLibrary(); });
        gui.Button("Recenter lean / controllers", -390, () => { tracking.Recenter(); message = "Steering center calibrated"; ShowLibrary(); });
    }
    void ShowPause()
    {
        MenuStart(); gui.Clear("PAUSED", "Resume recenters your neutral lean.");
        gui.Button("Resume ride", 150, TogglePause, true);
        gui.Button("Steering: " + tracking.mode + "   ·   Change", -60, () => { tracking.CycleMode(); ShowPause(); });
        gui.Button("Song library", 50, () => { playing = paused = false; music.Stop(); ClearNotes(); message = "Choose a song"; ShowLibrary(); });
    }
    void ShowImports()
    {
        MenuStart(); gui.Clear("IMPORT YOUR SONGS", "Select a .bfs song pack saved on your headset.");
        gui.Button("Choose local .bfs file", 180, OpenLocalFile, true);
        gui.Button("Refresh imported songs", 90, () => StartCoroutine(RefreshImports(true)));
        gui.Note("A song pack includes its music and beat chart.\nYou can also copy packs with the USB import tool.", -40);
        gui.Button("Back to song library", -230, ShowLibrary);
    }
    QuestMenuItem Pick(Ray ray, out Vector3 point)
    {
        point = ray.GetPoint(2.5f);
        if (Physics.Raycast(ray, out var hit, 8, 1 << 30)) { point = hit.point; return hit.collider.GetComponent<QuestMenuItem>(); }
        return null;
    }
    void UpdateMenu()
    {
        QuestMenuItem target = null; Ray ray = new Ray(head.position, head.forward); bool pressed = false, pointer = false;
        Vector3 point = ray.GetPoint(2.5f);
        for (int index = 0; index < 2; index++)
        {
            var p = index == 0 ? tracking.right : tracking.left;
            if (!p.valid) continue; var item = Pick(p.ray, out var hit);
            if (item == null) continue; target = item; point = hit; ray = p.ray; pressed = p.pressed; pointer = true; break;
        }
        if (!tracking.Tracked && Mouse.current != null)
        { ray = cameraRig.ScreenPointToRay(Mouse.current.position.ReadValue()); target = Pick(ray, out point); pressed = Mouse.current.leftButton.wasPressedThisFrame; pointer = true; }
        if (!pointer) target = Pick(ray, out point);
        reticle.SetActive(panel.gameObject.activeSelf || target != null); reticle.transform.position = point - ray.direction * .015f;
        pointerLine.enabled = pointer && tracking.Tracked && target != null; pointerLine.SetPosition(0, ray.origin); pointerLine.SetPosition(1, point);
        if (target != dwell)
        {
            if (dwell != null && dwell.graphic != null) dwell.graphic.color = new Color(.075f, .12f, .19f);
            if (dwell != null && dwell.progressBar != null) dwell.progressBar.sizeDelta = new Vector2(0, 4);
            dwell = target; dwellTime = 0; dwellFired = false;
            if (dwell != null && dwell.graphic != null) dwell.graphic.color = new Color(.035f, .38f, .42f);
        }
        if (target != null && !pointer && !dwellFired) { dwellTime += Time.unscaledDeltaTime; if (dwellTime > 1.4f) { pressed = true; dwellFired = true; } }
        if (target != null && target.progressBar != null) target.progressBar.sizeDelta = new Vector2(target.buttonWidth * (pointer ? 0 : Mathf.Clamp01(dwellTime / 1.4f)), 4);
        if (pressed && target != null) target.action?.Invoke();
    }
    public void OpenLocalFile()
    {
        if(importing) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var picker = new AndroidJavaClass("com.zrock.bfsquest.SongPicker")) picker.CallStatic("launch", gameObject.name);
#else
        message = "Put .bfs packs in " + QuestSongImport.ImportRoot; ShowLibrary();
#endif
    }
    public void OnSongPicked(string result)
    {
        if(result.StartsWith("OK:")) { StartCoroutine(RefreshImports(true)); return; }
        message=result=="CANCELLED" ? "Selection cancelled" : result;
        ShowLibrary();
    }
}
public sealed class QuestMenuItem : MonoBehaviour { public Action action; public Image graphic; public RectTransform progressBar; public float buttonWidth; }
