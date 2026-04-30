# CupkekGames SceneManagement

Scene-loading utilities for CupkekGames packages. Standalone — useful with or without the Sequencer package. Optional Addressables support gated by `versionDefine`.

## What's inside

**Runtime** (`CupkekGames.SceneManagement.asmdef`)

- `SceneSO` + `SceneDatabase` — ScriptableObject scene references
- `SceneLoader` / `SceneLoaderStartup` — non-addressable scene-loading flow
- `SceneLoaderAddressable` / `SceneLoaderAddressableStartup` — addressable scene-loading flow
- `SceneLoadRequest` — async load/unload request abstraction
- `InitializationLoader` — first-scene boot helper

**Editor** (`CupkekGames.SceneManagement.Editor.asmdef`)

- Inspector helpers for `SceneSO` / `SceneDatabase`

## Dependencies

- `com.unity.addressables` (optional — gated by versionDefine; addressable loaders only compile when present)
