# Verification

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
