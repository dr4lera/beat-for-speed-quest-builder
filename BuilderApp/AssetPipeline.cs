using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace QuestBuilder;

public class AssetPipeline(Action<string> log)
{
    static readonly JsonSerializerOptions Json = new() {WriteIndented=true};
    static readonly Regex Headers = new(@"^--- !u!(\d+) &(-?\d+)\r?\n(.*?)(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline);
    record Chunk(int Kind,long Id,string Text);
    static List<Chunk> Chunks(string file) => Headers.Matches(File.ReadAllText(file)).Select(m=>new Chunk(int.Parse(m.Groups[1].Value),long.Parse(m.Groups[2].Value),m.Groups[3].Value)).ToList();
    static Dictionary<string,object?> Mapping(YamlNode node)
    {
        var map=(YamlMappingNode)node;
        return map.Children.ToDictionary(p=>((YamlScalarNode)p.Key).Value!,p=>Value(p.Value));
    }
    static object? Value(YamlNode node)
    {
        if(node is YamlMappingNode) return Mapping(node);
        if(node is YamlSequenceNode sequence) return sequence.Children.Select(Value).ToList();
        var scalar=(YamlScalarNode)node; var value=scalar.Value;
        if(value is null or "" or "null" or "~") return null;
        if(scalar.Style is YamlDotNet.Core.ScalarStyle.DoubleQuoted or YamlDotNet.Core.ScalarStyle.SingleQuoted) return value;
        if(long.TryParse(value,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var integer)) return integer;
        if(double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var number)) return number;
        if(bool.TryParse(value,out var boolean)) return boolean;
        return value;
    }
    static Dictionary<string,object?> Document(string body)
    {
        var yaml=new YamlStream(); yaml.Load(new StringReader(body)); return Mapping(yaml.Documents[0].RootNode);
    }
    static Dictionary<string,object?> Object(string file) => (Dictionary<string,object?>)Document(Chunks(file)[0].Text)["MonoBehaviour"]!;
    static Dictionary<string,object?> Dict(object? value) => value as Dictionary<string,object?> ?? new();
    static List<object?> List(object? value) => value as List<object?> ?? new();
    static string Text(object? value) => value?.ToString() ?? "";
    static long Id(string text,string key) { var m=Regex.Match(text,@"\b"+Regex.Escape(key)+@": \{fileID: (-?\d+)");return m.Success?long.Parse(m.Groups[1].Value):0; }
    static long Ref(object? value) => Convert.ToInt64(Dict(value).GetValueOrDefault("fileID") ?? 0);
    public void Prepare(string exported,string project,CancellationToken token)
    {
        var assets=Path.Combine(exported,"Assets");
        if(!Directory.Exists(assets)) throw new InvalidOperationException("Invalid local Unity asset export.");
        var guidPaths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var meta in Directory.EnumerateFiles(assets,"*.meta",SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            var guid=Regex.Match(File.ReadAllText(meta),@"^guid: (\w+)",RegexOptions.Multiline).Groups[1].Value;
            if(guid.Length>0) guidPaths[guid]=meta[..^5];
        }
        var pointMeta=Directory.EnumerateFiles(assets,"PathPointTool.cs.meta",SearchOption.AllDirectories).FirstOrDefault(p=>p.Replace('\\','/').Contains("/Assembly-CSharp/"))
            ?? throw new InvalidOperationException("The game export lacks the supported path-point schema.");
        var pointGuid=Regex.Match(File.ReadAllText(pointMeta),@"^guid: (\w+)",RegexOptions.Multiline).Groups[1].Value;
        File.WriteAllText(Path.Combine(project,"Assets","Port","PathPointTool.cs.meta"),"fileFormatVersion: 2\nguid: "+pointGuid+"\nMonoImporter:\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n");
        var destination=Path.Combine(project,"Assets","Original");
        if(Directory.Exists(destination)) throw new InvalidOperationException("Staging folder already exists; start a new build job.");
        Directory.CreateDirectory(destination);
        var folders=new[] {"AnimationClip","AnimatorController","AnimatorOverrideController","AudioClip","AudioMixerController","Avatar","Cubemap","D3D12DeviceFilterLists","Font","GameObject","Material","Mesh","OcclusionCullingData","Scenes","Sprite","TextAsset","Texture2D","Texture3D"};
        foreach(var folder in folders)
        {
            var from=Path.Combine(assets,folder); var to=Path.Combine(destination,folder);
            if(Directory.Exists(from)) CopyTree(from,to,token);
            if(File.Exists(from+".meta")) File.Copy(from+".meta",to+".meta");
        }
        foreach(var required in new[]{"ForestScene","CityScene","MainCube","Sting","PathFollower","board fragture effect"})
            if(!File.Exists(Path.Combine(destination,"GameObject",required+".prefab"))) throw new InvalidOperationException("Unsupported asset layout: missing "+required);
        var songs=new List<object>();
        foreach(var data in Directory.EnumerateFiles(Path.Combine(assets,"MonoBehaviour"),"MusicDataAsset*.asset"))
        {
            var meta=Object(data);
            var chartGuid=Text(Dict(meta.GetValueOrDefault("jsonFile")).GetValueOrDefault("guid"));
            var audioGuid=Text(Dict(meta.GetValueOrDefault("musicClip")).GetValueOrDefault("guid"));
            if(!guidPaths.TryGetValue(chartGuid,out var chartFile) || !guidPaths.TryGetValue(audioGuid,out var audioFile)) continue;
            using var chart=JsonDocument.Parse(File.ReadAllText(chartFile));
            var title=chart.RootElement.TryGetProperty("bfsMetadata",out var info) && info.TryGetProperty("songName",out var name) ? name.GetString() : Path.GetFileNameWithoutExtension(data);
            var track=chart.RootElement.GetProperty("musicData").TryGetProperty("trackId",out var id)?id.GetString():"forest";
            songs.Add(new {title,track,chart="Assets/Original/"+Path.GetRelativePath(assets,chartFile).Replace('\\','/'),audio="Assets/Original/"+Path.GetRelativePath(assets,audioFile).Replace('\\','/')});
        }
        if(songs.Count==0) throw new InvalidOperationException("No compatible bundled songs/charts found in this installation.");
        File.WriteAllText(Path.Combine(project,"catalog-source.json"),JsonSerializer.Serialize(new{songs},Json));
        var effects=Effects(assets,guidPaths);
        File.WriteAllText(Path.Combine(project,"Assets","Port","ChartEffects.json"),JsonSerializer.Serialize(effects,Json));
        int removed=0,prefabs=0;
        var strip=new HashSet<int>{114,95,82,20,120,198,199,328,329};
        foreach(var prefab in Directory.EnumerateFiles(destination,"*.prefab",SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            var text=File.ReadAllText(prefab); var removedIds=new HashSet<long>();
            text=Headers.Replace(text,match=> {
                int kind=int.Parse(match.Groups[1].Value);
                bool crashParticle=Path.GetFileName(prefab)=="HitExplode.prefab" && (kind==198 || kind==199);
                if(strip.Contains(kind) && !crashParticle && !(kind==114 && match.Value.Contains(pointGuid))) {removedIds.Add(long.Parse(match.Groups[2].Value));return "";}
                return match.Value;
            });
            text=Regex.Replace(text,@"^  - component: \{fileID: (-?\d+)\}[^\S\r\n]*\r?\n",m=>removedIds.Contains(long.Parse(m.Groups[1].Value))?"":m.Value,RegexOptions.Multiline);
            File.WriteAllText(prefab,text); removed+=removedIds.Count;prefabs++;
        }
        log($"Prepared {songs.Count} bundled songs, {prefabs} prefabs; removed {removed} desktop components.");
    }
    object Effects(string assets,Dictionary<string,string> guids)
    {
        var themes=new List<object>();
        foreach(var file in Directory.EnumerateFiles(Path.Combine(assets,"MonoBehaviour"),"LevelTheme_*.asset"))
        {
            var data=Object(file); if(!data.ContainsKey("themeName")) continue;
            var configs=new Dictionary<string,Dictionary<string,object?>>();
            var templateGuid=Text(Dict(data.GetValueOrDefault("configTemplate")).GetValueOrDefault("guid"));
            if(guids.TryGetValue(templateGuid,out var template))
                foreach(var effect in List(Object(template).GetValueOrDefault("effectConfigs"))) {var e=Dict(effect);configs[Text(e["id"])]=e;}
            foreach(var effect in List(data.GetValueOrDefault("effectConfigs"))) {var e=Dict(effect);configs[Text(e["id"])]=e;}
            foreach(var effect in configs.Values)
                foreach(var key in new[]{"gameObjectActive","meshRendererEnabled"}) if(effect.ContainsKey(key)) effect[key]=Convert.ToInt64(effect[key])!=0;
            themes.Add(new{name=data["themeName"],primary=data["uiPrimaryColor"],effects=configs.Values.ToList()});
        }
        var mappings=new List<object>();
        var config=Object(Path.Combine(assets,"MonoBehaviour","MusicPathPrefabConfig.asset"));
        foreach(var entry in List(config["datamodelPrefabs"]))
        {
            var data=Dict(entry);
            foreach(var prefab in List(data.GetValueOrDefault("prefabs")))
            {
                if(!guids.TryGetValue(Text(Dict(prefab).GetValueOrDefault("guid")),out var file)) continue;
                foreach(var chunk in Chunks(file).Where(c=>c.Kind==114 && c.Text.Contains("stringParameter:")))
                {
                    var e=Dict(Document(chunk.Text)["MonoBehaviour"]);
                    mappings.Add(new{model=Text(data["datamodel"]).Split('/')[^1],theme=e.GetValueOrDefault("stringParameter"),eventType=e.GetValueOrDefault("eventType")});
                }
            }
        }
        var bindings=new List<object>();
        foreach(var scene in new[]{"CityScene","ForestScene"})
        {
            var chunks=Chunks(Path.Combine(assets,"GameObject",scene+".prefab")).ToDictionary(c=>c.Id);
            var transforms=new Dictionary<long,(long Parent,List<long> Children)>(); var byGame=new Dictionary<long,long>();
            foreach(var chunk in chunks.Values.Where(c=>c.Kind==4))
            {
                var child=Regex.Match(chunk.Text,@"m_Children:(.*?)m_Father:",RegexOptions.Singleline).Groups[1].Value;
                transforms[chunk.Id]=(Id(chunk.Text,"m_Father"),Regex.Matches(child,@"fileID: (-?\d+)").Select(m=>long.Parse(m.Groups[1].Value)).ToList());
                byGame[Id(chunk.Text,"m_GameObject")]=chunk.Id;
            }
            long Game(long component) => chunks.TryGetValue(component,out var c) ? (c.Kind==1?component:Id(c.Text,"m_GameObject")) : 0;
            int[] PathTo(long game)
            {
                var current=byGame[game]; var path=new List<int>();
                while(transforms[current].Parent!=0)
                {
                    var parent=transforms[current].Parent; int index=transforms[parent].Children.IndexOf(current);
                    if(index<0) throw new InvalidDataException("Invalid original transform hierarchy"); path.Add(index);current=parent;
                }
                path.Reverse(); return path.ToArray();
            }
            foreach(var chunk in chunks.Values.Where(c=>c.Kind==114 && c.Text.Contains("effectId:")))
            {
                var data=Dict(Document(chunk.Text)["MonoBehaviour"]); long target=0;
                foreach(var field in new[]{"targetRenderer","targetLight","targetParticle","targetGameObject"}) {target=Ref(data.GetValueOrDefault(field));if(target!=0)break;}
                var targetGame=Game(target); if(targetGame==0) targetGame=Ref(data["m_GameObject"]);
                bindings.Add(new{track=scene=="CityScene"?"city":"forest",id=data["effectId"],path=PathTo(targetGame),
                    forceEmission=Convert.ToInt64(data.GetValueOrDefault("overrideEmissiveColorIntensity")??0)!=0,
                    emissionLimit=data.GetValueOrDefault("forcedEmissiveColorIntensity")??1,modes=data.GetValueOrDefault("localModeOverrides")});
            }
        }
        if(themes.Count==0 || bindings.Count==0) throw new InvalidOperationException("The original theme schema was not found.");
        log($"Recovered {themes.Count} theme presets, {mappings.Count} event mappings and {bindings.Count} effect bindings.");
        return new{themes,mappings,bindings};
    }
    static void CopyTree(string from,string to,CancellationToken token)
    {
        Directory.CreateDirectory(to);
        foreach(var file in Directory.EnumerateFiles(from,"*",SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested(); var target=Path.Combine(to,Path.GetRelativePath(from,file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
        }
    }
}
