# Setup and missing dependencies

The Windows builder creates a standalone Quest APK from your own installed game. Unity is required to compile the same Quest runtime; the builder EXE includes its .NET runtime. Users do not need Python or a separate .NET installation.

## Unity and Android tools

1. Install [Unity Hub](https://unity.com/download).
2. Open the [Unity 6000.3.3f1 release page](https://unity.com/releases/editor/whats-new/6000.3.3f1) and use **Install with Unity Hub**. Use this exact editor version.
3. In Hub, open the editor's menu → **Add modules**, then install **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK**.
4. Complete Unity's license activation if Hub requests it. The builder cannot activate Unity for you.
5. In the builder, choose `Unity.exe` from the editor's `Editor` folder if it was not detected automatically.

See Unity's official [Android dependency setup](https://docs.unity3d.com/6000.3/Documentation/Manual/android-sdksetup.html) for screenshots and supported bundled tools. The builder checks prerequisites before extraction.

## Missing Android SDK platform 34

The APK targets Android API 34. If the builder reports `SDK/platforms/android-34/android.jar` missing, install **Android SDK Platform 34** into the SDK bundled with the selected Unity editor.

With Unity's SDK manager installed, run this command in PowerShell, adjusting the editor path if needed:

```powershell
$bfsEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.3f1\Editor'
$env:JAVA_HOME = "$bfsEditor\Data\PlaybackEngines\AndroidPlayer\OpenJDK"
& "$bfsEditor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools\latest\bin\sdkmanager.bat" --sdk_root="$bfsEditor\Data\PlaybackEngines\AndroidPlayer\SDK" 'platforms;android-34'
```

SDK command-line tools sometimes use a versioned folder instead of `latest`; choose the `bin/sdkmanager.bat` actually present under `cmdline-tools`. Accept the Android licenses when prompted. If Windows denies writes into the editor's SDK folder, run that installation command from an administrator PowerShell. Follow [Android's official sdkmanager documentation](https://developer.android.com/tools/sdkmanager) if the tools are missing. The command above uses the tools bundled with this Unity version.

If the missing file is `NDK/source.properties` or `OpenJDK/bin/java.exe`, return to Unity Hub → Add modules and install the corresponding bundled Android component.

## AssetRipper

Click **Download extractor** in the builder to obtain AssetRipper 2.0.0 from its official GitHub release. Alternatively, extract [AssetRipper 2.0.0 for Windows x64](https://github.com/AssetRipper/AssetRipper/releases/tag/2.0.0), then select `AssetRipper.GUI.Free.exe` using **Browse extractor**.

The automatic download uses HTTPS and checks the release asset's SHA-256 when GitHub supplies a digest. If a firewall or GitHub rate limit prevents downloading, use the manual release link above.

## Game copy and building

Install the Beat For Speed Steam demo or use your owned playtest installation. Click **Detect Steam install**, or browse to `beatforspeed.exe`. This does not launch the desktop game.

Choose an APK output location outside the game installation, then click **Build APK**. Extraction and a first Unity build can take a substantial amount of time and disk space. Keep at least 25 GB free on the system/build drive. Unity may need internet access for its packages on the first build.

The application writes separate build jobs and logs under `%LOCALAPPDATA%\BeatForSpeedQuestBuilder\Builds`. Check `builder.log`, `assetripper.log`, and `unity-build.log` if a job fails. Source files are read locally; the builder does not upload your game or charts.

## Installing on Quest Pro or Quest 3

Enable Developer Mode and USB debugging on the headset, then use [SideQuest's setup guide](https://sidequestvr.com/setup-howto) or Android ADB to sideload the generated APK. Open **Beat For Speed Quest** from Unknown Sources. Your device and account must satisfy Meta's current developer setup requirements.

Custom songs: in the game choose **Import songs → Choose local .bfs file**. Select a pack stored on the headset. Both legacy ZIP packs and BFS1 packs are supported; unsupported experimental chart events are skipped.

## Troubleshooting

- **Unity license error:** open Unity Hub, activate your license, and open the editor once before retrying.
- **No Steam install found:** use Browse game EXE. Steam libraries on additional drives are scanned, but a moved/non-Steam installation may need manual selection.
- **Extraction fails:** confirm the required game data folder sits beside the selected EXE. Use AssetRipper 2.0.0. New game versions may change content formats.
- **Compilation fails:** use Unity 6000.3.3f1 and its bundled Android tools; inspect the job's Unity log. Do not select the old broken recovered project.
- **Build cancelled:** diagnostics remain in that job folder; a retry creates a fresh job.

The tool recreates a Quest runtime around locally extracted content. It does not grant permission to redistribute the generated game APK or its music/assets.
