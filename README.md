# GravityShift (Unity)

This repository contains the **GravityShift** Unity project.  
The **Unity project root** is the Git repository root (the folder that directly contains `Assets/`, `Packages/`, and `ProjectSettings/`).

## Required Repository Structure

Your repo root should look like this:

- `Assets/` — game content and scripts (**commit**, including all `.meta` files)
- `Packages/` — package configuration (**commit**)
- `ProjectSettings/` — project configuration (**commit**)
- `Docs/` — documentation/checklists (**optional but recommended**)
- `.gitignore` — Unity ignore rules (**commit**)
- `README.md` — this file (**commit**)

### Do **NOT** commit these (generated / local files)

These must stay out of GitHub (and are excluded by `.gitignore`):

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `Obj/`
- `Build/` / `Builds/`
- IDE files like `*.csproj`, `*.sln`, `*.slnx`, `.vs/`, `.vsconfig`

> Unity regenerates these automatically on each machine.

## Opening the Project

1. Clone the repository:
   - GitHub Desktop: **File → Clone Repository**
   - Command line: `git clone <repo-url>`
2. In **Unity Hub**, click **Add**, and select the repo root folder (the folder that contains `Assets/`).
3. Open the project.
4. Open the main scene:
   - `Assets/Scenes/SampleScene.unity`

## Controls (Input System)

Controls are defined in:
- `Assets/InputSystem_Actions.inputactions`

### Keyboard & Mouse (default)
- Move: **W/A/S/D** or **Arrow Keys**
- Look: **Mouse**
- Jump: **Space**
- Sprint: **Left Shift**
- Crouch: **C**
- Interact: **E**
- Attack: **Left Mouse Button** or **Enter**
- Previous: **1**
- Next: **2**

### Gamepad (if used)
- Move: **Left Stick**
- Look: **Right Stick**
- Jump: **South Button** (A / Cross depending on controller)
- Attack: **Right Trigger**
- Sprint: **Left Stick Press**
- Crouch: **Right Stick Press**
- Interact: **West Button** (X / Square depending on controller)
- Previous: **Left Shoulder**
- Next: **Right Shoulder**

> If controls differ on your machine, treat `Assets/InputSystem_Actions.inputactions` as the source of truth.

## Suggested Commit Style (for grading)

To show proper version control usage, commit in small, meaningful steps:
- One feature / fix / level tweak per commit
- Clear messages, e.g.:
  - `feat: add gravity switch mechanic`
  - `feat: add checkpoint and kill zone`
  - `fix: prevent player sticking on moving platforms`
  - `polish: improve HUD and feedback`

## Troubleshooting

### The project opens with missing references
- Ensure **all** `.meta` files are committed (Unity relies on them for stable GUIDs).
- Ensure you did **not** commit generated folders like `Library/`.

### First open takes a long time
- Unity is importing assets and rebuilding `Library/` locally. This is normal.
