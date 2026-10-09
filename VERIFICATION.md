# Verification

## Builder 1.0.1 / Quest 0.1.4.4

- Release EXE and its embedded source template compile; the template content audit passes with no game files included.
- Fresh playtest content preparation and APK compilation passed through the released builder. The updated C# asset pipeline was then run again on the original export, preserving both HitExplode particle systems/renderers while removing desktop scripts; the final runtime rebuilt successfully in that project.
- Final builder-project APK signing verification passes (APK Signature Scheme v2). Four songs, 1,238 chart events and 943 scoring notes pass editor validation.
- Obstacle diagnosis reproduced zero visible Sting renderers before the fix and two afterward. Health validation confirms 100 starting HP and 100 HP after 100 missed notes.
- Final cars use the original sedan's full dimensions (2.42 m wide / 5.41 m long). Shared 1.5 m chart lane spacing, mesh-derived obstacle collision and a 0.30 m rider body hit width leave room through a two-lane car gap.
- Personal APK 0.1.4.4 installed and relaunched on Quest Pro; desktop APK replaced. Latest car spacing/crash effects/distance adjustments await the user's headset feedback. Quest 3 has not been physically tested. Adaptive maximum draw distance is 220 m on Pro and 280 m on Quest 3; load can reduce it.
- The same runtime source is embedded in the GUI builder. The Steam demo contains the required original sedan and effect assets; prior fresh demo end-to-end checks are recorded below.

## Builder 1.0.0 checks

- Windows self-contained release EXE compiles successfully with .NET SDK 10.
- GUI rendering checked at its default window size; primary action is **Build APK** and dependency/setup buttons are visible.
- Steam detection selects the locally installed demo (app ID 4550260), including its library path.
- Official AssetRipper download completed successfully through the same downloader used by the GUI.
- Missing Android SDK and wrong Unity-version checks return setup guidance before creating a build. Output inside the source game folder is rejected.
- GitHub Actions Windows source-build check passed.
- Fresh Steam demo asset export passed the C# preparation pipeline: four songs, 100 prefabs, 24 themes, 27 event mappings, 529 bindings.
- Demo-generated theme/mapping/binding JSON exactly matches the existing Quest port's data.
- Clean demo Unity project built successfully: zero Android build errors, ARM64, API 29 minimum, API 34 target, Vulkan/OpenXR; APK signing verification passes.
- Editor validation passed: four songs, 1,238 chart events, 943 hit notes, valid effect receiver paths, forward bike alignment, custom-pack extraction round trip and traversal rejection.
- Embedded-template audit: no original content directories, generated charts/theme JSON, APK/BFS files, game binaries, audio, models or textures.
- Universal Modder publication lint on the source distribution against the installed playtest: zero failures and zero warnings.

- Full playtest path passed from the installed game through the builder's AssetRipper subprocess/API, C# preparation and fresh Unity project to a signed APK: zero Android build errors; four songs, 1,238 events and 943 hit notes validated, along with all 529 receiver bindings and forward bike orientation.
- The builder verified its source assembly/metadata hashes were unchanged across both successful builds.

## Hardware scope

The underlying personal Quest runtime has been tested on Quest Pro with user-confirmed steering, smoothness, bike alignment, recentering, chart effects and local BFS1 importing. The fresh builder-produced APK has not had a new headset session: the headset was disconnected during builder checks. Quest 3 has not been physically tested. Build/package success does not establish a locked framerate or compatibility with every future game update.

## Input versions

Steam demo 0.7.33, Steam build 25225340. Playtest 0.7.92. The native assemblies differ; compatibility was checked through the exported content schema rather than assuming the files are identical. Live audio mode is excluded from the port.
