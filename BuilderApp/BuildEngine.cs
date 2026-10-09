using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace QuestBuilder;

public record BuildOptions(string Game, string Unity, string Ripper, string Output, string WorkRoot, string? ExportedProject = null);
public class BuildEngine
{
    public event Action<string>? Progress;
    public string? JobFolder { get; private set; }
    void Log(string text)
    {
        Progress?.Invoke(text);
        if (JobFolder != null) File.AppendAllText(Path.Combine(JobFolder,"builder.log"), $"{DateTimeOffset.Now:O} {text}\n");
    }
    public async Task<string> BuildAsync(BuildOptions options, CancellationToken token)
    {
        var game = GameDiscovery.NormalizeGame(options.Game);
        var workRoot = Path.GetFullPath(options.WorkRoot); var output = Path.GetFullPath(options.Output);
        if (Inside(workRoot, game) || Inside(output, game)) throw new InvalidOperationException("Choose build/output folders outside the game installation.");
        if (!File.Exists(options.Unity)) throw new FileNotFoundException("Select the installed Unity 6000.3.3f1 editor.");
        if (!(FileVersionInfo.GetVersionInfo(options.Unity).ProductVersion ?? "").StartsWith("6000.3.3f1",StringComparison.Ordinal))
            throw new InvalidOperationException("Use Unity 6000.3.3f1 to preserve this port's build configuration. Open Get Unity + setup for the exact editor download.");
        var android = Path.Combine(Path.GetDirectoryName(options.Unity)!, "Data", "PlaybackEngines", "AndroidPlayer");
        foreach (var required in new[] { "SDK/platforms/android-34/android.jar", "NDK/source.properties", "OpenJDK/bin/java.exe" })
            if (!File.Exists(Path.Combine(android, required.Replace('/',Path.DirectorySeparatorChar))))
                throw new InvalidOperationException("Unity Android Build Support, SDK platform 34, NDK and OpenJDK are required. Missing: " + required);
        if (options.ExportedProject == null && !File.Exists(options.Ripper)) throw new FileNotFoundException("Select or download AssetRipper 2.0.0.");
        JobFolder = Path.Combine(workRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(JobFolder);
        var initialAssembly = Hash(Path.Combine(game,"GameAssembly.dll"));
        var metadataFile = Path.Combine(game,"beatforspeed_Data","il2cpp_data","Metadata","global-metadata.dat");
        var initialMetadata = Hash(metadataFile);
        Log("Checking local game files. " + game);
        var source = new { gameAssemblySha256=initialAssembly, metadataSha256=initialMetadata,
            demoBuildTested="25225340", portVersion="0.1.4.1" };
        File.WriteAllText(Path.Combine(JobFolder,"source-provenance.json"),JsonSerializer.Serialize(source,new JsonSerializerOptions{WriteIndented=true}));
        var project = Path.Combine(JobFolder,"QuestProject");
        Directory.CreateDirectory(project);
        using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("QuestBuilder.Payload.zip")!)
        using (var archive = new ZipArchive(payload))
        {
            foreach (var entry in archive.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(project,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
                if (!Inside(target,project)) throw new InvalidDataException("Unsafe template archive entry");
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target);
            }
        }
        string exported;
        if (options.ExportedProject is { } reuse)
        {
            exported=Path.GetFullPath(reuse); Log("Using supplied local asset export: " + exported);
        }
        else
        {
            exported=await ExportAsync(options.Ripper,game,token);
        }
        Log("Preparing original tracks, music, theme data and mobile materials.");
        await Task.Run(() => new AssetPipeline(Log).Prepare(exported,project,token),token);
        Log("Building Android ARM64 APK in Unity. First builds can take several minutes.");
        var unityLog=Path.Combine(JobFolder,"unity-build.log");
        var args=new[] {"-batchmode","-nographics","-quit","-buildTarget","Android","-projectPath",project,"-executeMethod","QuestBuild.BuildAndroid","-logFile",unityLog};
        await RunProcess(options.Unity,args,JobFolder,token,unityLog);
        var report=Path.Combine(project,"Android-build.txt"); var apk=Path.Combine(project,"Builds","BeatForSpeedQuest-Pro-3.apk");
        if (!File.Exists(report) || !File.ReadAllText(report).StartsWith("Succeeded") || !File.Exists(apk) ||
            !File.ReadAllText(unityLog).Contains("BFSQUEST_BUILD_SUCCESS")) throw new InvalidOperationException("Unity did not produce a validated APK. See " + unityLog);
        if (Hash(Path.Combine(game,"GameAssembly.dll"))!=initialAssembly || Hash(metadataFile)!=initialMetadata)
            throw new InvalidOperationException("The source installation changed during the build. Retry after Steam finishes updating.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.Copy(apk,output,true);
        File.WriteAllText(output+".sha256",Hash(output)+"  "+Path.GetFileName(output)+"\n");
        File.WriteAllText(Path.Combine(JobFolder,"result.json"),JsonSerializer.Serialize(new { success=true, apk=output,sha256=Hash(output), source },new JsonSerializerOptions {WriteIndented=true}));
        Log("APK complete: " + output); return output;
    }
    async Task<string> ExportAsync(string exe,string game,CancellationToken token)
    {
        var socket=new TcpListener(IPAddress.Loopback,0); socket.Start(); int port=((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        var ripperLog=Path.Combine(JobFolder!,"assetripper.log");
        using var process=Start(exe,["--headless","--port",port.ToString(),"--log-path",ripperLog],JobFolder!);
        using var cancel=token.Register(()=>Kill(process));
        using var http=new HttpClient {BaseAddress=new Uri($"http://127.0.0.1:{port}"),Timeout=Timeout.InfiniteTimeSpan};
        bool ready=false;
        for (int i=0;i<120;i++)
        {
            token.ThrowIfCancellationRequested();
            if(process.HasExited) throw new InvalidOperationException("AssetRipper stopped. See " + ripperLog);
            try { using var response=await http.GetAsync("/",token); ready=response.IsSuccessStatusCode; if(ready) break; }
            catch(HttpRequestException) { }
            await Task.Delay(1000,token);
        }
        if(!ready) { Kill(process); throw new InvalidOperationException("AssetRipper did not start its local interface."); }
        try
        {
            Log("Reading game assets with AssetRipper.");
            using(var response=await http.PostAsync("/LoadFolder",new FormUrlEncodedContent(new Dictionary<string,string>{{"path",game}}),token)) response.EnsureSuccessStatusCode();
            var export=Path.Combine(JobFolder!,"FreshExport");
            Log("Exporting the local Unity assets.");
            using(var response=await http.PostAsync("/Export/UnityProject",new FormUrlEncodedContent(new Dictionary<string,string>{{"path",export}}),token)) response.EnsureSuccessStatusCode();
            var project=Path.Combine(export,"ExportedProject");
            if(!Directory.Exists(Path.Combine(project,"Assets"))) throw new InvalidOperationException("AssetRipper did not export a Unity project.");
            return project;
        }
        finally { Kill(process); }
    }
    async Task RunProcess(string exe,IEnumerable<string> args,string cwd,CancellationToken token,string log)
    {
        using var process=Start(exe,args,cwd); using var cancel=token.Register(()=>Kill(process));
        long oldLength=-1; var next=DateTime.UtcNow;
        while(!process.HasExited)
        {
            token.ThrowIfCancellationRequested();
            if(DateTime.UtcNow>=next)
            {
                next=DateTime.UtcNow.AddSeconds(20);
                if(File.Exists(log)) { var length=new FileInfo(log).Length; Log(length!=oldLength ? "Unity is compiling/importing; log updated." : "Unity is still processing the build."); oldLength=length; }
            }
            await Task.Delay(1000,token); process.Refresh();
        }
        if(process.ExitCode!=0) throw new InvalidOperationException("Unity build failed (exit "+process.ExitCode+"). See "+log);
    }
    static Process Start(string exe,IEnumerable<string> args,string cwd)
    {
        var info=new ProcessStartInfo(exe) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=cwd,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args) info.ArgumentList.Add(arg);
        var process=Process.Start(info) ?? throw new InvalidOperationException("Could not start "+exe);
        process.OutputDataReceived+=(_,_)=>{}; process.ErrorDataReceived+=(_,_)=>{};
        process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }
    static void Kill(Process process) { try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) { } }
    public static string Hash(string path) { using var stream=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    static bool Inside(string path,string root) => path.Equals(root,StringComparison.OrdinalIgnoreCase) || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
}
