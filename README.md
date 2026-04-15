# space-build

Unity 6 URP prototype for a 3D world-building game.

## Current direction

The project is being built around these short-term goals:

- role movement works reliably
- map loads into a clean flat test space
- top-down follow camera stays centered on the player
- build placement stays available for later expansion

## Local workspace note

The local project currently contains more Unity assets and generated files than are synced in this initial remote bootstrap. The remote repository has been initialized with core project metadata and key runtime code first because the local shell environment is currently unstable for a normal `git push` workflow.

## Unity version

- `6000.0.41f1`

## Open locally

Open this folder in Unity Hub:

- `UnityProject`

Then open:

- `Assets/Scenes/BootstrapWorld.unity`

## Current controls

- `WASD`: move character
- `Space`: jump
- `Shift`: sprint
- `Mouse Wheel`: zoom camera
- `Right Mouse Button`: rotate camera
- `1-9`: switch build item
- `R`: rotate next placement
- `Left Mouse Button`: place
- `Middle Mouse Button` or `X`: delete nearest placed item
