# Lighthouse Game: MVP scripts setup

Drop all the `.cs` files into `Assets/Scripts/` and **replace** the old HexGridSystem, ShipManager, NoteInputHandler and TimeManager.
If an old `ShipSpawner.cs` exists, delete it.

## New files

| File | What it does |
| --- | --- |
| `ShipTypes.cs` | `ShipType` enum and `ShipControls`, the one table of which key steers which ship |
| `Ship.cs` | Component for ship prefabs: type, HP, hexes per turn |
| `Obstacle.cs` | Base class for anything that damages ships (rocks now, monster/storm later) |
| `ObstacleManager.cs` | Places rocks (hand-placed list + random ones) and tracks what's on each hex |
| `GameManager.cs` | Win (save N ships) / lose (X wrecks), and R to restart |
| `GameHUD.cs` | Builds a placeholder HUD in code: saved/wrecked, chord + turn timer bar, flashing keys, game over |
| `GameAudio.cs` | SFX hooks: slosh, crack, shipwreck, saved, gear click, ambience |
| `BillboardConstraint.cs` | Moved out of ShipManager.cs (Unity wants one MonoBehaviour per file with the same name) |

## Scene setup (about 5 minutes)

1. Keep your existing objects: **HexGridSystem**, **TimeManager** (with its AudioSource + bgm clip), **NoteInputHandler** (with its AudioSource + note clips), **ShipManager**.
2. Add empty GameObjects with **ObstacleManager**, **GameManager**, **GameHUD** and **GameAudio** on them (they can all go on one "Game" object).
3. References find each other automatically if left empty. NoteInputHandler's old "Ship Spawner" slot is now "Ship Manager".
4. On **ShipManager**, the old 6-slot prefab array is replaced by a **Spawn Table**. Each entry has a type, an optional prefab, and the turn it unlocks on. With no prefab assigned you get coloured placeholder boxes, so it's playable straight away.
5. Add the scene to **File > Build Profiles > Scene List** or the R-to-restart won't work.
6. Requires TextMeshPro essentials (already used before) and the Input System (already used).

## Rules I picked (all tweakable in the Inspector)

- **1 turn = 1 chord = 8 s.** Ships advance on every chord change. Enter still advances a turn, in the editor only.
- **Steering:** "up" = north-east, "down" = south-east, nothing pressed = straight east. Last key pressed in a turn wins. Steering off the top or bottom edge is ignored (the ship goes straight).
- **Sailing off the right edge = saved.**
- **Hitting a rock, or a hex another ship is in or entering = 1 damage, and the ship stays where it is.** It bumps forward and back so it's visible. If it doesn't steer away, it'll hit again next turn.
- **Stats:** dinghy 1 HP / 1 hex, sloop 2 HP / 2 hexes (steering applies to the first hex), galleon 3 HP / 1 hex. To change them, add a `Ship` component to the prefab.
- **Ship types unlock over time** (DinghyA from turn 0, then a new type every 3 turns) so players learn one key pair at a time.
- **Spawn** every 2 turns on a free left-edge hex. It never stacks ships on one hex.
- **Win:** save 10 ships. **Lose:** 3 wrecks.
- **Rocks:** 6 random + any hand-placed coords, kept 2 columns away from the spawn edge. Set a non-zero seed for a fixed layout. Turn on "Enable Click Select" on the grid to log hex coords when you click.

## Things to check in Unity

- **Model facing:** ships face their heading with `LookRotation`, assuming the model's front is +Z. If they sail sideways, set **Model Yaw Offset** (90 or -90) on ShipManager.
- **Arrow material:** the preview arrows use `Sprites/Default`. If they show up pink, assign any unlit material that supports vertex colour to **Arrow Material**.
- **▲ ▼ glyphs:** if the labels show boxes, the TMP font lacks them. Change the text in `ShipManager.UpdateLabel` to "Up"/"Dn".
- **Pacing:** with grid radius 5, a ship takes about 10 turns (80 s) to cross. If that feels slow, try grid radius 4 or `turnsPerSpawn` 1.
- **Camera:** game logic assumes +Z is "up" on screen.

## Main menu

`MainMenu.cs` builds the whole menu in code: title, Play / How to Play / Quit, a How to Play panel with every ship's keys, fade in/out, and optional menu music. Enter also starts the game. Quit is hidden in WebGL builds, where it does nothing.

1. **File > New Scene**, save it as `Assets/Scenes/MainMenu.unity`.
2. Add an empty GameObject called `Menu` with the **MainMenu** component. The scene's default camera is enough.
3. On MainMenu, check **Game Scene Name** matches the gameplay scene (default `SampleScene`). Drop Vi's menu theme into **Menu Music** if it's ready.
4. In **File > Build Profiles > Scene List**, put `MainMenu` **first** and `SampleScene` after it.
5. After a level ends, **M** goes back to the menu and **R** restarts. If you rename the menu scene, change **Menu Scene Name** on GameManager to match.

**For the keeper's study later:** put a collider and a **MenuInteractable** on any 3D object (window = Play, bed = Quit, letter = How to Play) and set its hover text. It lights up on hover and runs its action on click. Turn off **Show Buttons** on MainMenu once the study objects replace the buttons, and lower **Background Dim** so the scene shows through.
