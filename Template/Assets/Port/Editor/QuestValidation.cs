using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

public static class QuestValidation
{
    public static void Run(QuestContent content)
    {
        var book = JsonUtility.FromJson<QuestThemeBook>(content.chartEffects.text);
        int bindings = 0;
        foreach(var binding in book.bindings)
        {
            var node=(binding.track=="city" ? content.city : content.forest).transform;
            foreach(int index in binding.path)
            {
                if(index<0 || index>=node.childCount) throw new Exception("Effect binding lost: "+binding.id);
                node=node.GetChild(index);
            }
            var renderer=node.GetComponent<Renderer>();
            if(renderer!=null) foreach(var m in renderer.sharedMaterials)
                if(m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); UnityEditor.EditorUtility.SetDirty(m); }
            bindings++;
        }
        Debug.Log("BFSQUEST_EFFECT_VALIDATION themes="+book.themes.Length+" bindings="+bindings);
        int events = 0, hitNotes = 0;
        var unmapped = new System.Collections.Generic.HashSet<string>();
        int visualEvents = 0;
        foreach (var song in content.songs)
        {
            var chart = QuestSongImport.Parse(song.chart.text);
            if (song.audio == null || chart.entities.Length == 0) throw new Exception("Song content validation failed: " + song.title);
            events += chart.entities.Length;
            foreach(var e in chart.entities)
            {
                string suffix=e.datamodel.Substring(e.datamodel.LastIndexOf('/')+1);
                if(suffix=="spawn_cube" || suffix=="spawn_jiucai" || suffix=="spawn_sting") continue;
                if(book.mappings.Any(m=>m.model==suffix) || suffix=="screen_flash" || suffix=="speed_1" || suffix=="speed_2" || suffix=="speed_3") visualEvents++;
                else unmapped.Add(suffix);
            }
            hitNotes += chart.entities.Count(e => e.datamodel.EndsWith("/spawn_cube") || e.datamodel.EndsWith("/spawn_jiucai"));
        }
        Reject("{\"musicData\":{\"bpm\":0},\"entities\":[]}");
        Reject("{\"musicData\":{\"bpm\":120},\"entities\":[{\"beat\":-1,\"key\":7,\"datamodel\":\"cube\"}]}");
        if (content.bike != null)
        {
            var bike = UnityEngine.Object.Instantiate(content.bike);
            var renderers = bike.GetComponentsInChildren<Renderer>(true);
            var front = renderers.FirstOrDefault(r => r.name == "tire_front");
            var rear = renderers.FirstOrDefault(r => r.name == "tire_rear");
            if (front == null || rear == null || Vector3.Dot((front.bounds.center - rear.bounds.center).normalized, Vector3.forward) < .95f)
                throw new Exception("Original bike must face forward along the ride path");
            Debug.Log("BFSQUEST_BIKE_FORWARD " + (front.bounds.center - rear.bounds.center));
            UnityEngine.Object.DestroyImmediate(bike);
        }
        string root = Path.GetFullPath("Validation/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string pack = Path.Combine(root, "roundtrip.bfs");
        using (var zip = ZipFile.Open(pack, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("chart.json").Open())) writer.Write(content.songs[0].chart.text);
            using (var writer = new BinaryWriter(zip.CreateEntry("audio.ogg").Open())) writer.Write(new byte[] { 0x4f, 0x67, 0x67, 0x53 });
            using (var writer = new StreamWriter(zip.CreateEntry("../escaped.txt").Open())) writer.Write("must not extract");
        }
        string dest = Path.Combine(root, "song"); QuestSongImport.Extract(pack, dest);
        var roundtrip = QuestSongImport.Parse(File.ReadAllText(Path.Combine(dest, "chart.json")));
        if (roundtrip.entities.Length != QuestSongImport.Parse(content.songs[0].chart.text).entities.Length || File.Exists(Path.Combine(root, "escaped.txt")))
            throw new Exception("Pack roundtrip/traversal validation failed");
        File.WriteAllText("validation-result.json", "{\"passed\":true,\"songs\":" + content.songs.Length + ",\"events\":" + events + ",\"hitNotes\":" + hitNotes + "}");
        Debug.Log("BFSQUEST_VALIDATION_PASS songs=" + content.songs.Length + " events=" + events + " hitNotes=" + hitNotes);
        File.WriteAllText("effect-audit.txt","Themes="+book.themes.Length+"\nBindings="+bindings+"\nHandled non-note events="+visualEvents+"\nNo original prefab mapping="+string.Join(",",unmapped)+"\nDesktop first/third-person camera markers retain the tracked VR view. HDRP volumetrics, full-screen bloom and motion blur are replaced with mobile lighting/glow feedback.");
    }
    static void Reject(string json)
    {
        try { QuestSongImport.Parse(json); }
        catch (InvalidDataException) { return; }
        throw new Exception("Invalid custom chart was accepted");
    }
}
