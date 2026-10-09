using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace QuestBuilder;

public record GameInstall(string Name, string Folder, string AppId)
{
    public override string ToString() => $"{Name} — {Folder}";
}
public static class GameDiscovery
{
    public static string NormalizeGame(string input)
    {
        var path = Path.GetFullPath(input.Trim().Trim('"'));
        var root = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "GameAssembly.dll")) ||
            !File.Exists(Path.Combine(root, "beatforspeed_Data", "il2cpp_data", "Metadata", "global-metadata.dat")))
            throw new InvalidOperationException("Select beatforspeed.exe or its installation folder. The Unity game data must be installed beside it.");
        return root;
    }
    public static List<GameInstall> DetectSteam()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, key, value) in new[] {
            (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") })
        {
            using var registry = hive.OpenSubKey(key);
            if (registry?.GetValue(value) is string path) roots.Add(Path.GetFullPath(path));
        }
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        if (Directory.Exists(standard)) roots.Add(standard);
        foreach (var root in roots.ToArray())
        {
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                roots.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
        }
        var found = new List<GameInstall>();
        foreach (var root in roots)
        {
            var apps = Path.Combine(root, "steamapps"); if (!Directory.Exists(apps)) continue;
            foreach (var file in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(file); var id = Field(text, "appid"); var name = Field(text, "name");
                if (id is not ("4550260" or "4550310" or "4430260") && !name.Contains("Beat For Speed", StringComparison.OrdinalIgnoreCase)) continue;
                var folder = Path.Combine(apps, "common", Field(text, "installdir"));
                try { folder = NormalizeGame(folder); } catch { continue; }
                if (!found.Any(x => x.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))) found.Add(new(name, folder, id));
            }
        }
        return found.OrderBy(x => x.AppId == "4550260" ? 0 : 1).ToList();
    }
    public static string Field(string text, string name) => Regex.Match(text, "\"" + Regex.Escape(name) + "\"\\s*\"([^\"]*)\"").Groups[1].Value;
    public static string FindUnity()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", "6000.3.3f1", "Editor", "Unity.exe");
        return File.Exists(path) ? path : "";
    }
}
