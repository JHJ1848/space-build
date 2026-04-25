# space-build

Unity 6 URP prototype for a 3D world-building game. The current MVP is `scene showcase + minimal building gameplay`, built on real assets from `models/scene`.

## Current MVP

- Main scene: `UnityProject/Assets/Scenes/BootstrapWorld.unity`
- Required environment assets:
  - `models/scene/stone/stone01.fbx`
  - `models/scene/tree/tree01.glb`
- Player source asset:
  - `models/player/20260411144703_885c4d6d.fbx`
- Runtime baseline:
  - `TopDownFollowCamera`
  - `SimpleCharacterMover`

## What is implemented now

- Unity project uses `URP`.
- Bootstrap imports the real scene models into Unity asset folders.
- `BootstrapWorld` is generated around the real stone and tree assets.
- Player roaming is available on the current MVP control baseline.
- The environment showcase baseline is in place.
- `MinimalBuildSystem` is attached to the generated scene and uses real `stone01` / `tree01` models plus the imported base-building set (`powerstation`, `solar_panel`, `water_equipment`) as placeable objects.
- Players can place, rotate, and remove build pieces inside the MVP scene.
- Scene bootstrap now auto-places optional landmark assets from `Assets/Art/Environment/Landmarks` when available.

## MVP loop

- `Showcase`: enter the scene, roam, inspect terrain, lighting, player, stone, and tree assets.
- `Build`: select `stone01`, `tree01`, or one of the 3 base-building pieces, aim at the ground, place a piece, rotate it, and delete the nearest placed piece.
- Scope remains local and offline.

## Controls

- `TopDownFollowCamera`
  - always follows player as center target
  - `Mouse Wheel`: zoom in/out
  - `Right Mouse Button`: rotate camera yaw
- `SimpleCharacterMover`
  - `WASD`: immediate planar move at constant speed, no acceleration or auto-turn
  - `Space`: jump
  - `Shift`: sprint
- `MinimalBuildSystem`
  - `1 - 9`: switch selected placeable object from catalog
  - `1`: `stone01`
  - `2`: `tree01`
  - `3`: `powerstation`
  - `4`: `solar_panel`
  - `5`: `water_equipment`
  - `R`: rotate the next placement
  - `Left Mouse Button`: place selected model
  - `Middle Mouse Button` or `X`: delete nearest placed model

## Authority map

- Foundation-pass scene truth comes from `SceneBootstrap` plus a fresh bootstrap rerun, not from stale serialized-scene drift.
- `README.md` and `MEMORY.md` should stay aligned with that generated baseline.
- `FreeFlyCamera` may still exist in-repo as a debug or inspection utility, but it is not the MVP baseline contract.

## Recommended model source structure

Place new source models under `models/` and rerun bootstrap:

- `models/player/` and `models/player/anims/`
- `models/scene/tree/`
- `models/scene/stone/`
- `models/scene/landmark/`
- `models/scene/terrain/`
- `models/scene/props/`
- `models/building/floor|wall|stairs|pillar|roof/`
- `models/building/base-building/`
- `models/building/props/`
- `models/building/utility/`

Detailed checklist: `models/RESOURCE_CHECKLIST.md`.

## Real asset dependency

- This MVP depends on the real source assets under `models/scene`.
- `stone01.fbx` is imported through Unity FBX import.
- `tree01.glb` requires a glTF import path in Unity.
- Placeholder trees or rocks are not valid MVP output.

## Dependencies

- Unity Hub
- Unity Editor `6000.4.0f1`
- URP package
- glTF importer package for Unity
  - current project expectation: `com.atteneder.gltfast`

## Bootstrap

Unity menu:

- `Tools/Project Bootstrap/Run Full Bootstrap`
- `Tools/Project Bootstrap/Import External Models`
- `Tools/Project Bootstrap/Configure URP If Installed`
- `Tools/Project Bootstrap/Rebuild Bootstrap Scene`

Batch command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe" `
  -batchmode `
  -projectPath "C:\Users\jinghongjie\Desktop\space-build\UnityProject" `
  -executeMethod SpaceBuild.Editor.BootstrapProject.RunBatchBootstrap `
  -quit `
  -logFile "C:\Users\jinghongjie\Desktop\space-build\unity-bootstrap.log"
```

Notes:

- Do not use `6000.0.41f1` for the current batch bootstrap path. That older editor line can fail during package resolution on this project.
- `6000.4.0f1` is the verified local baseline for rerun-bootstrap and URP package import.

## Validation

Check the MVP with these quick passes:

- Open `BootstrapWorld.unity` and confirm `stone01` and `tree01` are visible.
- Enter Play Mode and confirm the top-down follow camera and player movement both work.
- Confirm the player model appears near the center area.
- Confirm the stylized sky/day-night cycle is present in the scene.
- Confirm the scene is using real imported environment assets, not fallback primitives.
- Confirm `MinimalBuildSystem` is present on `BuildSystemRoot`.
- Verify keys `1-5` map to `stone01`, `tree01`, `powerstation`, `solar_panel`, and `water_equipment`.
- Verify all 5 catalog entries can be placed and the nearest placed object can be removed without errors.

## Known limitations

- `stone01.fbx` references external textures that are not present beside the source asset, so rock material quality is currently limited.
- `tree01.glb` is physically small at source scale, so scene placement may need scale-up to read as a tree.
- Build placement is intentionally simple: no grid snapping, no save/load, and no resource economy yet.
- No harvesting, survival, save/load, networking, or production-ready asset pipeline is included yet.
