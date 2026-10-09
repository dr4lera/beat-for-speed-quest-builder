using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

public static class QuestSongImport
{
    public static string ImportRoot
    {
        get
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-quest-imports");
            if (i >= 0 && i + 1 < args.Length) return Path.GetFullPath(args[i + 1]);
#endif
            return Path.Combine(Application.persistentDataPath, "Imports");
        }
    }
    public static string LastError;
    public static List<QuestSong> Scan(string root = null)
    {
        root=root ?? ImportRoot; LastError=null;
        Directory.CreateDirectory(root);
        var result = new List<QuestSong>();
        foreach (string pack in Directory.GetFiles(root, "*.bfs"))
        {
            try
            {
                string dest = Path.Combine(root, Path.GetFileNameWithoutExtension(pack));
                if (!Directory.Exists(dest)) Extract(pack, dest);
            }
            catch (Exception ex) { LastError=Path.GetFileName(pack)+": "+ex.Message; Debug.LogWarning("Song import rejected: " + LastError); }
        }
        var ids=new HashSet<string>();
        foreach (string dir in Directory.GetDirectories(root))
        {
            if (dir.EndsWith(".pending", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                string chartPath = Path.Combine(dir, "chart.json");
                if (!File.Exists(chartPath) || new FileInfo(chartPath).Length > 8 * 1024 * 1024) continue;
                var chart = Parse(File.ReadAllText(chartPath));
                string audio = null;
                foreach (string ext in new[] { ".ogg", ".wav", ".mp3" })
                    if (File.Exists(Path.Combine(dir, "audio" + ext))) { audio = Path.Combine(dir, "audio" + ext); break; }
                if (audio == null) continue;
                if(!string.IsNullOrEmpty(chart.bfsMetadata?.songId) && !ids.Add(chart.bfsMetadata.songId)) continue;
                result.Add(new QuestSong { title = chart.bfsMetadata?.songName ?? Path.GetFileName(dir),
                    customChart = chartPath, customAudio = audio, track = chart.musicData.trackId ?? "forest" });
            }
            catch (Exception ex) { LastError=ex.Message; Debug.LogWarning("Custom chart rejected: " + ex.Message); }
        }
        return result;
    }
    public static QuestChart Parse(string text)
    {
        var chart = JsonUtility.FromJson<QuestChart>(text);
        if (chart?.musicData == null || chart.entities == null || chart.entities.Length > 50000 ||
            !Finite(chart.musicData.bpm) || chart.musicData.bpm < 20 || chart.musicData.bpm > 400 ||
            !Finite(chart.musicData.pathStartOffsetDistance) || !Finite(chart.musicData.offset))
            throw new InvalidDataException("Invalid chart metadata");
        foreach (var e in chart.entities)
            if (e == null || !Finite(e.beat) || e.beat < 0 || e.beat > 50000 || e.key < -100 || e.key > 100 ||
                e.datamodel == null || e.datamodel.Length > 200)
                throw new InvalidDataException("Invalid chart event");
        Array.Sort(chart.entities, (a, b) => a.beat.CompareTo(b.beat));
        return chart;
    }
    static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    public static void Extract(string pack, string dest)
    {
        if(QuestBfs1.IsPacked(pack)) { QuestBfs1.Extract(pack,dest); return; }
        var selected = new List<ZipArchiveEntry>();
        using (var archive = ZipFile.OpenRead(pack))
        {
            if (archive.Entries.Count > 32) throw new InvalidDataException("Too many pack entries");
            long total = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                string n = entry.FullName;
                if (n != "chart.json" && n != "audio.ogg" && n != "audio.wav" && n != "audio.mp3") continue;
                if (!names.Add(n)) throw new InvalidDataException("Duplicate pack entry");
                if (entry.Length > (n == "chart.json" ? 8L : 256L) * 1024 * 1024) throw new InvalidDataException("Pack entry too large");
                total += entry.Length;
                if (total > 272L * 1024 * 1024) throw new InvalidDataException("Pack too large");
                selected.Add(entry);
            }
            if (!names.Contains("chart.json") || !names.Contains("audio.ogg") && !names.Contains("audio.wav") && !names.Contains("audio.mp3"))
                throw new InvalidDataException("Pack needs chart.json and audio.ogg/wav/mp3");
            string staging = dest + ".pending";
            Directory.CreateDirectory(staging);
            foreach (var entry in selected)
            {
                using (var input = entry.Open())
                using (var output = File.Create(Path.Combine(staging, entry.FullName)))
                {
                    byte[] buffer = new byte[65536]; long copied = 0; int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        copied += read;
                        if (copied > entry.Length) throw new InvalidDataException("Entry exceeds declared size");
                        output.Write(buffer, 0, read);
                    }
                }
            }
            Parse(File.ReadAllText(Path.Combine(staging, "chart.json")));
            Directory.Move(staging, dest);
        }
    }
}
