# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PROJECT-O is a Unity-based rhythm game (similar to SDVX/maimai style) built with Unity 6 + URP. It supports PC standalone, macOS, and WebGL. Audio is handled entirely by FMOD Studio; there is no Unity Audio component.

## No CLI Build Commands

This is a Unity project — all builds, tests, and scene runs happen through the **Unity Editor GUI**. There is no command-line build or test runner. Open `PROJECT-O.slnx` in Rider/VS to edit C# with IDE support.

- **Play in Editor**: Open `Assets/Scenes/InGame.unity` and press Play.
- **Test from LevelEditor**: Load both `LevelEditor` and `InGame` scenes additively; the `GameManager` detects `isTest` via `SceneManager.GetSceneByName("LevelEditor").isLoaded`.
- **Restart InGame**: Press F5 (outside test mode).
- **Exit to song list / editor**: Press Escape.

## Scene Flow

```
FirstLoading → Menu → FreePlay → InGame → Result
                                 ↑
                          LevelEditor (additive, for testing charts)
```

- **FirstLoading**: Initialises `SettingsManager` (singleton, DontDestroyOnLoad) and `LocalizationManager`.
- **Menu**: `MenuManager` / `CircleMenuController` — main title screen.
- **FreePlay**: `LoadAllJSONs` reads `StreamingAssets/songList.json`, populates `songDictionary`, then `SongListShower` / `HorizontalSongListShower` render the song list. On song select, `SettingsManager.fileName` / `Info` are set before scene load.
- **InGame**: `LoadManager` reads the chart JSON into `List<NoteClass>` → `NoteGenerator` spawns prefabs → `LineInputChecker` tracks time and dispatches input → `JudgementManager` scores notes → `GameManager.isLevelEnd` triggers result transition.
- **LevelEditor**: `LevelEditer` + `SaveManager` for chart authoring. Can preview in InGame by additively loading InGame scene.

## Key Singletons (all use `Instance` pattern, destroyed on duplicate)

| Class | Scene | Role |
|---|---|---|
| `SettingsManager` | FirstLoading (DontDestroyOnLoad) | Persists `GameSettings` to `StreamingAssets/settings.json`; holds selected song info between scenes |
| `GameManager` | InGame | Level-end flag, scene routing, test-mode detection |
| `LoadManager` | InGame | Deserialises chart JSON into `List<NoteClass>` |
| `NoteGenerator` | InGame | Spawns note prefabs; calculates `fallTime` from speed |
| `LineInputChecker` | InGame | Tracks `currentTime`; dispatches `Judge`/`UpJudge` to JudgementManager |
| `JudgementManager` | InGame | Scoring, combo, rate calculation, long-note state machine |
| `MusicPlayer` | InGame | FMOD event playback with sync offset |

## Chart Format

Charts live in `Assets/StreamingAssets/Charts/<fileLocation>/<DIFFICULTY>.json`.  
`songList.json` is the index; each entry points to a `fileLocation` folder and one `difficulty`.

```json
{
  "notes": [
    {
      "beat": 4.0,
      "position": 2,
      "type": "normal",
      "width": 1.0,
      "length": 4.0,
      "tick": 0,
      "angle": 0.0,
      "speed": 1.0
    }
  ]
}
```

**Note types**: `normal`, `long` (hold start), `null` (hold body — generated at runtime, not stored), `hold` / `bell` (wide auto-hit bell), `rbell` (red bell — punishes overlap), `avoid` (dodge note), `leftarrow`, `rightarrow`.

Difficulty tiers (index 0–3): `ADVANCED`, `EXPERT`, `MASTER`, `LUNATIC`.

## Input System

Uses Unity Input System (`MainInputAction.inputactions`).  
- **PC/macOS/WebGL**: Input System callbacks in `LineInputChecker`.  
- **Windows standalone**: Input is polled on a dedicated high-frequency thread (`ChartPlayWorker` at 8000 Hz) via `InputThreadDivider`; actions that touch Unity API are marshalled back via `EnqueueMainThreadAction`.  
- **Controller**: Joystick buttons 1–4 are polled in `Update()`.  
- Default keys: D / F / J / K (overridable in settings).

## Timing & Judgement Windows (ms)

| Judgement | Window |
|---|---|
| PerfectP | ±25 |
| Perfect | ±50 |
| Great | ±70 |
| Good | ±110 |
| Bad | ±160 |
| Miss | > 200 after note time |

`currentTime` is seconds elapsed since `Play()`. Notes store `ms` (milliseconds from chart start + 1000 ms offset).

## Platform Conditionals

The codebase uses `#if` guards frequently:
- `UNITY_STANDALONE_WIN` — high-frequency input thread.
- `UNITY_STANDALONE_OSX || UNITY_WEBGL` — Input System callbacks + `Time.time` for clock.
- WebGL uses `UnityWebRequest` (async/await) instead of `File.*` for all asset loading.

## Adding a New Song

1. Create `Assets/StreamingAssets/Charts/<folderName>/<DIFFICULTY>.json`.
2. Add jacket image to `Assets/Resources/Images/Jackets/<eventName>/<DIFFICULTY>.png` (loaded via `Resources.Load`).
3. Add entry to `Assets/StreamingAssets/songList.json`.
4. Add the FMOD event `event:/<eventName>` in the FMOD project and rebuild banks.

## Key Dependencies

- **FMOD for Unity** — all audio (music playback, preview, SFX). No Unity AudioSource.
- **DOTween** — some UI animations (`Assets/Resources/DOTweenSettings.asset`).
- **TextMeshPro** — all in-game text.
- **Unity Input System 1.19** — all input handling.
- **URP 17.3** — two render pipeline assets: `PC_RPAsset` and `Mobile_RPAsset`.
