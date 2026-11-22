# Lighthouse Game: MVP scripts setup

## Quick setup (recommended)

There's now a **Lighthouse** menu in the editor's top bar:

1. Open `SampleScene` and choose **Lighthouse > Set Up Game Scene**. It adds every missing game component to the GameManager object, removes broken "missing script" components, and fills empty slots by name (the bgm, the `PianoC/F/G_1-5` note clips, and the ship prefabs in `Assets/GameObjects`). It never overwrites a slot that's already filled, so you can run it again any time. **Save the scene afterwards.**
2. Choose **Lighthouse > Create Main Menu Scene**. It makes `Assets/Scenes/MainMenu.unity` and puts it first in the build scene list.
3. Press Play in `MainMenu`.

The manual steps below are still there if you'd rather wire things by hand.

## Important: commit your .meta files

The repo's `.gitignore` used to exclude `*.meta`. Unity stores the ID of every script, model, sound and material in those files, and scenes and prefabs point at assets by those IDs. Without them, anyone who clones the repo gets "missing script" components and empty slots.

This branch removes that line. **Chloe:** your local `.meta` files will now appear as new files in GitHub Desktop. Commit all of them (Assets and ProjectSettings) so everyone's scene matches yours. Everyone else: after pulling them, close and reopen Unity.


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

## Sea monster and thunderstorms

Both are spawned by **ObstacleManager**, so no extra setup is needed. Tune them in its Inspector.

**Sea monster** (`SeaMonster.cs`). By default 1 monster appears on turn 6, at least 3 hexes from any ship.
- Each turn it shows a **red arrow and a red hex** where it will move next turn.
- At the start of the next turn it moves there. If a ship is on that hex, it **attacks** instead: the ship takes 1 damage and the monster stays put.
- Ships can't sail into the monster's hex; it counts like a rock.
- It chases the nearest ship 60% of the time and wanders otherwise (**Chase Chance** on the component).
- Placeholder: a purple capsule. Assign **Monster Prefab** when there's a model; the prefab can carry its own `SeaMonster` component to tweak.

**Thunderstorms** (`Thunderstorm.cs`). By default one storm of 2–4 hexes every 5 turns, starting on turn 10.
- **Warning turn:** small grey clouds and grey hexes. Ships can still pass.
- **Active (3 turns):** dark clouds, purple hexes, and a lightning flash when it breaks. Entering costs 1 HP. A ship sitting there when it breaks also takes 1 damage.
- Then it clears by itself.
- Placeholder: a flattened sphere. Assign **Storm Prefab** for a cloud model (its first child gets scaled and recoloured).

Hazards never appear in the first 2 columns (**Spawn Safe Columns**), so new ships always get a turn to react.

**New sound slots on GameAudio:** Monster Appears, Monster Roar (attack), Storm Warning, Thunder, and Storm Ambience (a loop that fades in while a storm is active). Thunder plays once per storm, not once per hex.

## Map and camera

- **MapBackground:** add it to any object and it lays a flat map under the grid, sized to fit. Drop the painted sea map into **Map Texture** (or a full material into **Map Material**). With nothing assigned you get a plain parchment-coloured sea. It keeps the texture's aspect ratio.
- **Semi-transparent grid:** for the "grid over a map" look, give HexGridSystem's **Tile Material** a transparent unlit material (URP: Surface Type = Transparent) and lower **Default Color**'s alpha to about 0.4.
- **CameraRig:** put it on the Main Camera. It tilts the camera (55° by default, Inscryption-style) and pulls it back to fit the whole grid. Right-click the component and choose **Frame Grid Now** to preview. Keep **Yaw** at 0, because the game treats +Z as up on screen.

## Pause menu and the keeper's letter

- **Esc** pauses: time, the music and therefore the turn timer all stop. The pause menu has Resume, Restart, Main Menu, Keeper's Letter and a volume setting (saved between sessions).
- **TutorialLetter** shows a 5-page letter from the previous keeper when the level starts, with the game paused until it's closed. Enter, Space or Next turns the page; Backspace goes back; Esc skips. The pages are editable in the Inspector, and `{goal}` / `{wrecks}` are filled in from GameManager.
- By default the letter shows automatically only the first time someone plays, but **always in the editor** so it's easy to test. Use **Lighthouse > Show Tutorial Letter Again** to reset it. It can always be reopened from the pause menu.
- Add a page-turn sound in **Page Turn Sound** if Vi makes one.

## Game feel, turn warning, results and the bell tower

**Lighthouse > Set Up Game Scene** adds all of these. To add them by hand: put FeedbackFX on the GameManager object, and BellTower on **its own** empty object.

- **FeedbackFX:** camera shake, a burst of spray and floating text ("-1", "Wrecked!", "Home!", "Something stirs...") when ships are hit, wrecked or saved, the monster appears, or a storm breaks. Strengths and colours are in the Inspector. **Splash Prefab** takes a real particle effect if you have one.
- **Turn warning:** in the last 2 seconds of each turn the HUD timer bar turns red and pulses. GameAudio ticks once a second if you assign **Turn Warning Tick**.
- **Results screen:** shows ships home, wrecked, hits taken and turns. A win gets 1–3 stars (3 = no wrecks, 2 = one wreck), and the best rating is remembered per scene.
- **BellTower:** a lighthouse with five bells beside the grid. A bell swings and glows when its key is played, and all the bells take on the current chord's colour (C gold, F teal, G pink) with a small shiver when the chord changes. That's the in-world "the bells swap to the new key" moment. It places itself just off the grid's bottom-left corner (turn off **Auto Place** to position it yourself), and CameraRig keeps it in shot. **Tower Prefab** and **Bell Prefab** take real models; bells swing around their pivot, so a bell model should hang down from its origin.
