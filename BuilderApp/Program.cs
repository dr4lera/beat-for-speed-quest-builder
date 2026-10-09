using System.Text.Json;

namespace QuestBuilder;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string? Arg(string name){int index=Array.IndexOf(args,name);return index>=0 && index+1<args.Length?args[index+1]:null;}
        if(Arg("--ui-smoke") is { } ui)
        {using var form=new MainForm();form.SaveUiSmoke(ui);return 0;}
        if(args.Contains("--detect-steam")) {Console.WriteLine(JsonSerializer.Serialize(GameDiscovery.DetectSteam()));return 0;}
        if(Arg("--game") is { } game)
        {
            var work=Arg("--work") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BeatForSpeedQuestBuilder","Builds");
            try
            {
                var engine=new BuildEngine();engine.Progress+=Console.WriteLine;
                engine.BuildAsync(new(game,Arg("--unity") ?? GameDiscovery.FindUnity(),Arg("--ripper") ?? ToolDownload.Extractor,
                    Arg("--output") ?? Path.Combine(work,"BeatForSpeedQuest-Pro-3.apk"),work,Arg("--exported-project")),CancellationToken.None).GetAwaiter().GetResult();return 0;
            }
            catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        }
        Application.Run(new MainForm());return 0;
    }
}
