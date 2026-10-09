using System.Diagnostics;
using System.Text.Json;

namespace QuestBuilder;

public sealed class MainForm : Form
{
    readonly TextBox game=new(),unity=new(),ripper=new(),output=new(),logs=new();
    readonly ComboBox installs=new();
    readonly Button build=new(),cancel=new();
    readonly Label status=new();
    readonly ProgressBar progress=new();
    CancellationTokenSource? cancellation;
    readonly string work=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BeatForSpeedQuestBuilder","Builds");
    readonly Color background=Color.FromArgb(18,24,32),panel=Color.FromArgb(30,39,50),foreground=Color.FromArgb(232,239,245),accent=Color.FromArgb(42,190,175);
    public MainForm()
    {
        Text="Beat For Speed · Quest APK Builder"; Width=1100;Height=840;MinimumSize=new Size(1000,780);
        StartPosition=FormStartPosition.CenterScreen; BackColor=background;ForeColor=foreground;Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=1,RowCount=9};Controls.Add(layout);
        foreach(var size in new[]{78,96,80,80,80,56,48}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,size));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
        var header=new Panel {Dock=DockStyle.Fill};
        header.Controls.Add(new Label {Text="Build your Quest APK",Font=new Font("Segoe UI",23,FontStyle.Bold),AutoSize=true,Location=new Point(0,0)});
        header.Controls.Add(new Label {Text="Use your installed Beat For Speed copy · Quest Pro + Quest 3",AutoSize=true,ForeColor=Color.FromArgb(153,174,191),Location=new Point(2,45)});layout.Controls.Add(header,0,0);
        layout.Controls.Add(InputRow("Game EXE or installation folder",game,"Browse game",()=>BrowseExe(game),"Detect Steam",DetectSteam,true),0,1);
        layout.Controls.Add(InputRow("Unity 6000.3.3f1 with Android Build Support",unity,"Browse Unity",()=>BrowseExe(unity),"Get Unity + setup",()=>Open("https://github.com/dr4lera/beat-for-speed-quest-builder/blob/main/SETUP.md#unity-and-android-tools")),0,2);
        layout.Controls.Add(InputRow("AssetRipper 2.0.0",ripper,"Browse extractor",()=>BrowseExe(ripper),"Download",Download),0,3);
        layout.Controls.Add(InputRow("Save Quest APK to",output,"Choose output",BrowseOutput,"Android SDK help",()=>Open("https://github.com/dr4lera/beat-for-speed-quest-builder/blob/main/SETUP.md#missing-android-sdk-platform-34")),0,4);
        var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight};
        build.Text="Build APK";build.Width=170;build.Height=44;build.BackColor=accent;build.ForeColor=Color.FromArgb(8,24,27);build.FlatStyle=FlatStyle.Flat;build.Font=new Font(Font,FontStyle.Bold);build.Click+=Build;
        cancel.Text="Cancel build";cancel.Width=140;cancel.Height=44;cancel.Enabled=false;Style(cancel);cancel.Click+=(_,_)=>cancellation?.Cancel();
        var folder=new Button {Text="Open output",Width=175,Height=44};Style(folder);folder.Click+=(_,_)=>OpenOutput();
        buttons.Controls.AddRange([build,cancel,folder]);layout.Controls.Add(buttons,0,5);
        var state=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};state.RowStyles.Add(new RowStyle(SizeType.Absolute,24));state.RowStyles.Add(new RowStyle(SizeType.Absolute,12));
        status.Text="Choose a game copy, then build locally.";status.Dock=DockStyle.Fill;progress.Dock=DockStyle.Fill;state.Controls.Add(status);state.Controls.Add(progress);layout.Controls.Add(state,0,6);
        logs.Multiline=true;logs.ReadOnly=true;logs.Dock=DockStyle.Fill;logs.ScrollBars=ScrollBars.Both;logs.BackColor=panel;logs.ForeColor=foreground;logs.Font=new Font("Consolas",9);logs.BorderStyle=BorderStyle.FixedSingle;layout.Controls.Add(logs,0,7);
        var footer=new LinkLabel {Text="Setup guide and source · No game assets are included with this builder",AutoSize=true,LinkColor=accent,Dock=DockStyle.Fill};footer.LinkClicked+=(_,_)=>Open("https://github.com/dr4lera/beat-for-speed-quest-builder#readme");layout.Controls.Add(footer,0,8);
        unity.Text=GameDiscovery.FindUnity();ripper.Text=File.Exists(ToolDownload.Extractor)?ToolDownload.Extractor:"";
        output.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"BeatForSpeedQuest-Pro-3.apk");
        DetectSteam();
        if(string.IsNullOrEmpty(unity.Text)) Log("Missing Unity editor: use Get Unity + setup, then install Android Build Support with SDK/NDK/OpenJDK.");
        if(string.IsNullOrEmpty(ripper.Text)) Log("Missing extractor: use Download extractor, or choose your installed AssetRipper 2.0.0.");
        FormClosing+=(_,_)=>cancellation?.Cancel();
    }
    Panel InputRow(string title,TextBox field,string first,Action action,string second,Action alternate,bool steam=false)
    {
        var row=new Panel {Dock=DockStyle.Fill};var grid=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=steam?3:2,Padding=new Padding(0,0,0,10)};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,185));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute,26));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,34));if(steam)grid.RowStyles.Add(new RowStyle(SizeType.Absolute,26));
        var label=new Label {Text=title,AutoSize=true,Dock=DockStyle.Fill};grid.Controls.Add(label,0,0);grid.SetColumnSpan(label,3);
        field.Dock=DockStyle.Fill;field.BackColor=panel;field.ForeColor=foreground;field.BorderStyle=BorderStyle.FixedSingle;field.Margin=new Padding(0,3,8,3);grid.Controls.Add(field,0,1);
        var b=new Button {Text=first,Dock=DockStyle.Fill,Margin=new Padding(0,0,8,0)};Style(b);b.Click+=(_,_)=>action();grid.Controls.Add(b,1,1);
        var a=new Button {Text=second,Dock=DockStyle.Fill,Margin=Padding.Empty};Style(a);a.Click+=(_,_)=>alternate();grid.Controls.Add(a,2,1);
        if(steam) {installs.Dock=DockStyle.Fill;installs.DropDownStyle=ComboBoxStyle.DropDownList;installs.BackColor=panel;installs.ForeColor=foreground;installs.SelectedIndexChanged+=(_,_)=>{if(installs.SelectedItem is GameInstall item)game.Text=item.Folder;};grid.Controls.Add(installs,0,2);grid.SetColumnSpan(installs,3);}
        row.Controls.Add(grid);return row;
    }
    void Style(Button b) {b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(66,84,99);b.BackColor=panel;b.ForeColor=foreground;}
    void BrowseExe(TextBox target)
    {using var dialog=new OpenFileDialog {Filter="Windows executable (*.exe)|*.exe",CheckFileExists=true};if(dialog.ShowDialog(this)==DialogResult.OK)target.Text=dialog.FileName;}
    void BrowseOutput()
    {using var dialog=new SaveFileDialog {Filter="Android APK (*.apk)|*.apk",FileName="BeatForSpeedQuest-Pro-3.apk",OverwritePrompt=true};if(dialog.ShowDialog(this)==DialogResult.OK)output.Text=dialog.FileName;}
    public void DetectSteam()
    {
        try {var found=GameDiscovery.DetectSteam();installs.Items.Clear();foreach(var item in found)installs.Items.Add(item);if(found.Count>0)installs.SelectedIndex=0;else Log("No installed Steam copy found. Browse to beatforspeed.exe instead.");}
        catch(Exception ex){Log("Steam detection: "+ex.Message);}
    }
    async void Download()
    {
        if(cancellation!=null)return;cancellation=new();SetBusy(true);
        try {ripper.Text=await ToolDownload.DownloadExtractor(Log,cancellation.Token);status.Text="Extractor ready.";}
        catch(Exception ex){status.Text="Download failed";Log(ex.Message+" Official download: https://github.com/AssetRipper/AssetRipper/releases/tag/2.0.0");}
        finally {cancellation.Dispose();cancellation=null;SetBusy(false);}
    }
    async void Build(object? sender,EventArgs args)
    {
        if(cancellation!=null)return;cancellation=new();SetBusy(true);
        try
        {
            var engine=new BuildEngine();engine.Progress+=Log;
            var apk=await engine.BuildAsync(new(game.Text,unity.Text,ripper.Text,output.Text,work),cancellation.Token);
            status.Text="Quest APK complete";Log("Ready to sideload: "+apk);
            MessageBox.Show(this,"Quest APK created:\n\n"+apk,"Build complete",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(OperationCanceledException){status.Text="Build cancelled";Log("Build cancelled. Its diagnostic files are kept in the build folder.");}
        catch(Exception ex){status.Text="Build needs attention";Log(ex.Message);MessageBox.Show(this,ex.Message,"Build needs attention",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{cancellation.Dispose();cancellation=null;SetBusy(false);}
    }
    void SetBusy(bool busy){build.Enabled=!busy;cancel.Enabled=busy;progress.Style=busy?ProgressBarStyle.Marquee:ProgressBarStyle.Blocks;progress.MarqueeAnimationSpeed=busy?30:0;}
    void Log(string text)
    {
        if(IsDisposed)return;if(InvokeRequired){BeginInvoke(()=>Log(text));return;}
        logs.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}\r\n");if(logs.TextLength>200000)logs.Text=logs.Text[^150000..];status.Text=text.Length>110?text[..110]+"…":text;
    }
    static void Open(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    void OpenOutput(){var directory=Path.GetDirectoryName(Path.GetFullPath(output.Text));if(Directory.Exists(directory))Open(directory!);}
    public void SaveUiSmoke(string folder)
    {
        Directory.CreateDirectory(folder);StartPosition=FormStartPosition.Manual;Location=new Point(-30000,-30000);ShowInTaskbar=false;Show();Application.DoEvents();PerformLayout();Application.DoEvents();
        using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(folder,"builder-ui.png"));
        File.WriteAllText(Path.Combine(folder,"builder-ui.json"),JsonSerializer.Serialize(new {title=Text,game=game.Text,unity=unity.Text,extractor=ripper.Text,output=output.Text,steamInstalls=installs.Items.Count,mainAction=build.Text}));
        Close();
    }
}
