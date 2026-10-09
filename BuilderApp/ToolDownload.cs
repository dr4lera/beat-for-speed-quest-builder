using System.IO.Compression;
using System.Text.Json;

namespace QuestBuilder;

public static class ToolDownload
{
    public static readonly string DefaultCache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BeatForSpeedQuestBuilder","Tools");
    public static string Extractor => Path.Combine(DefaultCache,"AssetRipper-2.0.0","AssetRipper.GUI.Free.exe");
    public static async Task<string> DownloadExtractor(Action<string> progress,CancellationToken token)
    {
        var folder=Path.Combine(DefaultCache,"AssetRipper-2.0.0"); Directory.CreateDirectory(folder);
        if(File.Exists(Extractor)) return Extractor;
        using var http=new HttpClient {Timeout=TimeSpan.FromMinutes(15)};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BeatForSpeedQuestBuilder/1.0");
        const string url="https://github.com/AssetRipper/AssetRipper/releases/download/2.0.0/AssetRipper_win_x64.zip";
        progress("Downloading AssetRipper 2.0.0 from its official GitHub release.");
        string? digest=null;
        try
        {
            using var metadata=JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/AssetRipper/AssetRipper/releases/tags/2.0.0",token));
            foreach(var asset in metadata.RootElement.GetProperty("assets").EnumerateArray())
                if(asset.GetProperty("name").GetString()=="AssetRipper_win_x64.zip" && asset.TryGetProperty("digest",out var hash)) digest=hash.GetString();
        }
        catch(HttpRequestException) { progress("Release metadata is unavailable; using the fixed official download URL."); }
        var zip=Path.Combine(folder,"download.zip.partial");
        using(var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token))
        {
            response.EnsureSuccessStatusCode(); await using var input=await response.Content.ReadAsStreamAsync(token);
            await using var output=File.Create(zip); await input.CopyToAsync(output,token);
        }
        if(digest?.StartsWith("sha256:")==true && !BuildEngine.Hash(zip).Equals(digest[7..],StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The extractor download failed checksum verification.");
        ZipFile.ExtractToDirectory(zip,folder,true);
        if(!File.Exists(Extractor))
        {
            var exe=Directory.EnumerateFiles(folder,"AssetRipper.GUI.Free.exe",SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidDataException("The official extractor archive is missing its executable.");
            return exe;
        }
        progress("AssetRipper download complete.");return Extractor;
    }
}
