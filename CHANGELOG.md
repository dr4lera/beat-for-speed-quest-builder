# Changelog

## 1.0.1

- Quest runtime 0.1.4.5 fixes invisible obstacle meshes that caused unexpected HP loss.
- Full-size original sedans replace bomb obstacles. Unified lane spacing and a narrower rider hit area make gaps rideable.
- Original crash particles and glitch crash audio now play on obstacle impacts.
- Adaptive view distance increased to 220 m on Quest Pro and 280 m on Quest 3.
- Seat height is two feet higher than the previous release, including after recentering; automatic XR camera transform updates are disabled so custom tracking controls the calibrated view.
- Health starts at 100, cannot become negative, and zero HP still ends the ride.

## 1.0.0

- Windows self-contained Quest APK builder GUI.
- Game EXE selection and Steam demo/playtest detection across Steam libraries.
- Unity/Android prerequisite validation and direct dependency/setup links.
- Optional download of the official AssetRipper 2.0.0 distribution.
- Fresh per-job extraction, authored Quest runtime template, Android compilation and APK SHA-256 output.
- Local content extraction, with no original game content bundled in the builder.
