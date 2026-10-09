# Beat For Speed Quest APK Builder

A Windows GUI that builds a standalone Quest Pro / Quest 3 APK from your own Beat For Speed installation. Select the game EXE or automatically detect the Steam demo. The builder extracts local assets and compiles the Quest port with Unity.

**This is an APK builder.** The release does not include a game APK, songs, charts, models, recovered game code, or an AssetRipper binary.

## Download and use

Get `BeatForSpeedQuestBuilder.exe` from [Releases](https://github.com/dr4lera/beat-for-speed-quest-builder/releases). The Windows x64 EXE includes its .NET runtime.

1. Follow the [setup guide](SETUP.md) to install **Unity 6000.3.3f1** with Android Build Support, SDK, NDK, OpenJDK, and SDK platform 34.
2. Run the builder. Browse to your game EXE or select its detected Steam installation.
3. Use **Download extractor** or select an installed AssetRipper 2.0.0.
4. Choose an output path and click **Build APK**. Sideload the resulting APK onto your headset.

The GUI links directly to setup help for missing tools. Unity remains required to preserve the existing compiled runtime and rendering pipeline; an editor-free runtime replacement has not been substituted.

## Quest features

- Head lean steering, controller thumbsticks/handlebar tilt, controller/hand/gaze menu selection.
- Original locally extracted tracks, bike, songs, charts and environment assets.
- Quest shading, colored block shards/sparks, chart-driven colors and glows, City water.
- Corrected startup heading, seat offset and Quest recenter behavior.
- Local file picker for custom ZIP/BFS1 `.bfs` song packs and asynchronous importing.
- Android ARM64, Vulkan stereo rendering, mobile texture settings and adaptive render scale.

This is a reconstructed Quest runtime, rather than the complete desktop executable. Live audio mode is excluded. Desktop bloom/volumetric/blur effects are approximated for Quest; legacy cutpoint events without a recovered prefab mapping are not reproduced. Experimental BFS1 events are skipped. Performance depends on chart density and headset conditions; constant framerate is not guaranteed.

## Compatibility and verification

The personal port was tested with user feedback on Quest Pro for steering, recentering, local custom-song importing, shading, and chart effects. Quest 3 uses the same ARM64/OpenXR build but has not been physically tested.

Content extraction has been checked against playtest **0.7.92** and Steam demo **0.7.33 / Steam build 25225340**. They are different game builds; both expose the required four-song catalog, 24 theme definitions, 27 event mappings and 529 receiver bindings. See [verification notes](VERIFICATION.md) for the builder's build results and limits. Future Steam updates are not automatically guaranteed compatible.

## Source build

On Windows, install .NET SDK 10 and Python 3 (packaging only):

```powershell
python Tools/make_payload.py
dotnet publish BuilderApp/BuilderApp.csproj -c Release -o release/win-x64
```

The published EXE embeds only the authored Unity template. The C# asset pipeline extracts game content locally into each build job; Python is not required by end users.

Optional diagnostics:

```powershell
.\release\win-x64\BeatForSpeedQuestBuilder.exe --detect-steam
.\release\win-x64\BeatForSpeedQuestBuilder.exe --game 'D:\My Game\beatforspeed.exe' --unity 'C:\Program Files\Unity\Hub\Editor\6000.3.3f1\Editor\Unity.exe' --ripper 'D:\Tools\AssetRipper.GUI.Free.exe' --output 'D:\Builds\BeatForSpeedQuest.apk'
```

Use a terminal that waits for GUI executables when running diagnostics; the GUI is the intended user workflow.

## Credits and rights

Quest runtime and builder implementation developed for dr4lera with OpenAI Codex (GPT-6). No generated art/audio services were used for this builder. Beat For Speed content belongs to its respective creators and is extracted from the user's own installed copy. This project is unofficial and is not endorsed by the original game developers, Unity, Valve or Meta.

Authored code is MIT licensed. Third-party software has its own terms; see [third-party notices](THIRD-PARTY.md). The code license does not license the original game's assets or music. Generated APKs include local game content: obtain the relevant rights before distributing those APKs.
