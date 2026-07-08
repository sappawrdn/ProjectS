# Progress Log — Project S (Unity)

> Live file. Read this FIRST each session. Add an entry: date, who, what changed, what's next.
> "Done" means built AND verified (on device for haptics/audio) against the GDD — not just written.

## Current status (start of the Unity build)
- **Pivoted from RealityKit/iOS to Unity.** Reason: RealityKit is AR-first; the prototype ended up
  hand-rolling collision, pathfinding, a level editor, and asset import — all first-class in Unity. Core
  Haptics + PHASE (the game's soul) are preserved via **Apple's official Unity plugins**.
- **The design is PROVEN** — a full Swift/RealityKit greybox validated the mechanics + balance through many
  playtest iterations. These docs (GDD + architecture + tuning numbers) are the reconciled source of truth.
  This is a **re-implementation in a better engine**, not a from-scratch design.
- **Nothing built in Unity yet.**
- **Next up:** Phase 0 — set up the Unity/URP project, import the Apple plugins, and **prove Core Haptics +
  PHASE on a real iPhone** before rebuilding gameplay. If the plugins fall short, reassess before committing.

## What's already designed & proven (from the prototype — port it, don't redesign)
- **Predator monster AI:** Static → Watcher → Hunter (key-gated); Hunter chases (NavMesh) when aware, stalks
  to last-known when it loses you; awareness hysteresis (sight/lose).
- **Breathing-room balance loop:** win QTE → monster loses you (searches last-known) = a fair cat-and-mouse
  ebb instead of a death spiral. Lose QTE (catch) → recoil + heavier controls + FOV reduction; 3 = game over.
- **Insanity** drives heartbeat/vignette AND modulates the monster per tier (fear makes it worse; calming
  helps). Never a meter.
- **Rearrange perception layer:** phantom that appears unobserved + vanishes when looked at; lights die
  behind you. Theme lives in the environment (monster stays a fair, accessible predator).
- **Scare events:** Event B (false catch, timed), Event C (reveal, on section entry) — pure jumpscares.
- **Haptic-Primary Mode** (headline): haptic hot/cold compass, gyro-aim, hold-to-walk, auto-pickup, boosted
  danger heartbeat — playable eyes-off. Plus a full haptic vocabulary (heartbeat, jumpscare, cold-open pulse,
  tick, buzz, confirm).
- **Front-end flow:** cold open (haptic pulse) → synopsis → menu → options → run → win/lose → menu.
- **Tuning numbers** that felt right (architecture.md → Tuning reference).

## Known open items to carry in
- Audio beacons on key/exit (makes by-ear nav real for everyone) — designed, not built.
- Real level design + proper room scale (proto ceiling was too low). Monster reveal/lighting polish. AppIcon.
- Confirm the Apple plugins cover the needed Core Haptics richness + PHASE features (Phase 0 de-risk).
- Decide S3: scare-free (recommended) vs Hunter-through-to-exit (owner explored both).

## Session log
<!-- Newest on top. Format: ### YYYY-MM-DD — Name / what changed / what's next -->

### 2026-07-08 — Sappa — Level 3 Layout & Custom Editor Tools (PUSHED to feature branch)
- **Editor Tooling (Props & Lighting):** Created `RandomizePropTool.cs` and `RandomizeClusterTool.cs` to randomly scatter hospital props across Level 3, preserving the user's base scale and orientation. 
- **Lighting Polish & Debugging:** Investigated URP point light limits on large single meshes (the black floor bug). Provided the optimal Forward+ pipeline switch solution, and reverted custom experimental lighting tools at the user's request, ensuring a clean and stable `LightingTool.cs`.
- **Level 3 Saved State:** The user successfully authored the prop and light layout using the tools and saved it to `PlacedObjects.unity`.
- **Source Control:** Pushed these changes to the `feature/level3-layout-updates` branch.
- **Next:** Proceed with whatever feature or polishing the user requests next (likely audio/haptics wiring, or tuning the current layout).

### 2026-07-08 — Sappa — Device wiring (haptics + touch) + monster redesign + ghost model (PUSHED)
- **Stage 1 haptics wired** (`84b1e8c`): `HapticManager` wraps Core Haptics (proven pattern) — heartbeat
  (reads `InsanitySystem.HeartbeatBpm` → identical to the vignette breathe), jumpscare slam, confirm, cold-open
  pulse. Self-bootstrap + DontDestroyOnLoad; editor no-ops (felt on device). Wired into Insanity/GameState/
  ScareDirector.
- **Mobile touch controls** (`84b1e8c`): `PlayerController` left-half move joystick + right-half look drag
  (EnhancedTouch); GameState + QTE take a screen tap (begin/restart/solve). Debug HUD respects `Screen.safeArea`
  (notch). **Verified on device** (build recipe in Phase 0 entry; build to `Builds/` → Append → Xcode Run).
- **Monster AI redesign** (`ea6c0e2`) — DESIGN PIVOT from the key-gated Static/Watcher/Hunter tiers (owner felt
  the teleporting Watcher was gamey). Now: **one persistent predator, hunts from run start but SOFT**; an
  `Aggression` value (0..1) ramps with time + keys and modulates speed (2.2→2.9), sight, hearing, and give-up.
  **Senses:** sight (LOS) + **hearing** (moving = heard without LOS; standing still = silent = the reliable
  hide) + **fear-as-volume-knob** (insanity widens both — but freezing still saves you, so no death spiral).
  Kept fair: hysteresis, breathing room, non-fatal catches. `Retire()` dormants it for the scare-free finale.
- **Ghost model** (`ea6c0e2`): designer's Mixamo FBX. `ProjectS > Set Up Ghost Monster` configures imports
  (Humanoid retarget, loop flags, 1K texture), builds material + Idle/Walk/Attack AnimatorController, swaps the
  capsule for the animated model. `MonsterVisual` drives it. **Caveats:** the ghost is MENU-APPLIED, not saved
  into `Level3.unity` (a fresh pull shows the red capsule → run the menu, or bake it into the scene + commit).
  The 2 unused turn FBX are gitignored (kept local, saves ~48 MB).
- **HONEST AI assessment (owner asked, no yes-man): ~6.5/10 now.** Good = the FAIR skeleton (sense-based,
  hysteresis, breathing room, aggression ramp — the hard part). Missing (why not 9-10), in impact order:
  1. **Audio** — it's currently SILENT; the by-ear pillar needs you to HEAR it coming. Biggest gap. **← next.**
  2. **Patrol/search** — on losing you it walks to the single last-known point then idles; doesn't wander/check
     nearby rooms. Predictable ("it always goes exactly where I was"). Needs a real search behaviour.
  3. **Spawn tuning** — currently centre-of-map ("away from player"), NOT optimized. Best = out of the player's
     line-of-sight, distanced so it reaches you in ~15-25 s, ideally a designed first-reveal.
  4. **Noise nuance** (run ≠ walk loudness) + a distraction/bait; occasional flank/cutoff (anticipate heading).
- **Next:** Stage 2 **AudioDirector** (ambient hum + monster sound that follows it + audio beacons on key/exit;
  Unity 3D AudioSource first for editor-verify, PHASE upgrade later). Then a monster **search/patrol** pass.

### 2026-07-08 — Sappa — Level3: designer top-down map → deterministic greybox (COMMITTED + PUSHED)
- **Designer handed a top-down PNG** (`~/Downloads/Level 3.png`, Start bottom-left / Finish top-right). Traced
  it deterministically (not the random maze): thresholded the grey walls (lum 83) vs black text, split each
  wall pixel H/V by run-length, connected-components → **55 wall centreline segments**. (Python one-off in
  `/tmp/ch4`; embedded the result as a `Seg[]` in `GreyboxRoomGenerator.cs`.)
- **`ProjectS > Build Level3 (designer PNG)`**: builds a NEW scene `Assets/Scenes/Level3.unity` (maze scene
  untouched) — floor + 55 **thin (0.25 m) walls** (drawing strokes are ~2 m; rendering thin reclaims that as
  corridor width → ~4 m typical corridors for hospital props) at **52×68 m**, 3 m walls. Full gameplay rig +
  **3 keys spread OFF the Start→Finish diagonal** + exit + baked NavMesh. Tune `Ch4WorldWidth` (one number) to
  rescale.
- **Verified playable** (Python flood-fill on the trace): all 3 keys + exit reachable from Start, no sealed
  dead zones. Caught + flagged that the raw trace nearly **sealed the Finish corner** (single sub-1.5 m choke);
  owner opened it manually in-scene → exit confirmed reachable in Play.
- **Self-contained greybox** (primitives only, no imported art) → clones 1:1. **COMMITTED + PUSHED** to
  `origin/main` (`e5fc57e`): `Level3.unity` + `Level3-NavMesh.asset` + generator. So the teammate gets an
  IDENTICAL level with just `git pull` — no PSX art needed (unlike the gitignored maze dressing). Owner edited
  walls manually after generating, so **`Level3.unity` (the scene) is now the source of truth, not the generator**.
- **`ProjectS > Skin Level3 (PSX + ceiling)`**: walls (`TileTextureBase`, per-wall tiling via
  MaterialPropertyBlock so long/short walls keep texel density) + floor (`FloorTile1`) + full-footprint ceiling
  slab (`Ceiling1`) + ~30 red ceiling point-lights on an 11 m grid + dark-red backrooms mood (dim sun/ambient).
  Skin references gitignored PSX art → **skinned scene NOT committed** (teammate re-runs Skin after getting art).
- **Owner: skin "cukup aman"; brightness to tune later.** Abandoned earlier `CH4-Map1` (.blend/.usdz/FBX import
  detour) left as harmless dead menu (`Build CH4-Map1 Greybox Scene`) — clean up later.
- **Level3 dressing menus** (adapted from the maze, non-grid): `Skin Level3 (PSX + ceiling)`,
  `Clad Level3 Walls (WallTemplate)` (both faces; `WallPanelFlip = true`), `Dress Level3 — Doors` (DoorType1
  slabs, ~1/5 m, skips perimeter), `Scatter Hospital Props (Level3)` (navmesh-sampled, wall-clearance ≥1.5 m).
  All read the live `H_`/`V_` walls so they respect manual edits; decorative (greybox keeps collision/navmesh).
- **PSXBackrooms un-gitignored + COMMITTED** (7.5 MB, `.meta` kept for stable GUIDs; only `Blender/` source
  `.blend` excluded). Owner SAVED the dressed `Level3.unity` (~38 MB) and it's committed too → teammate gets the
  **fully-dressed level 1:1 with just `git pull` + open** (no art download, no menu-running). `SETUP.md` (root)
  is the teammate guide. Note: `SampleScene` (maze dressing) still uncommitted — could now be committed too since
  the PSX art is in git, but left for later (owner focused on Level3).
- **Next:** hospital props + doors + EXIT-signs for Level3 (the maze dressing menus are maze-grid-coupled →
  need a Level3-adapted version, wall/ceiling placement without a grid), then lighting tune, then device pass
  (HapticManager/AudioDirector wiring the proven Core Haptics + PHASE).

### 2026-07-07 — Sappa — Maze generator + PSX art-dressing pipeline (editor)
- **Procedural maze** (`ProjectS > Generate Maze Level`): 7×7 cells @ 5 m corridors, recursive-backtracker
  + braided loops, full gameplay rig + keys/exit + baked NavMesh. Re-run = new layout.
- **Art dressing** (all in `GreyboxRoomGenerator.cs`, all bounds-based heuristics so they adapt to any FBX
  orientation/scale — no per-model guessing):
  - `Skin Maze (PSX + Backrooms mood)`: tile walls/floor/ceiling (runtime materials, not assets → fixes the
    magenta) + dark-red hospital lighting (dim sun, dark ambient, red ceiling point-lights; flashlight leads).
  - `Dress Maze — Ceiling + Doors`: auto-lay-flat ceiling lights/vents(embed 0.12)/sprinklers; dense doors
    (DoorType1 solid slabs only — DoorType2 has a see-through window; 2/wall on 85% of walls, auto-oriented
    tallest→up + thinnest→wall-normal, scaled 1.3×).
  - `Clad Walls (WallTemplate)`: tiles WallTemplate2 (flat panel; WallTemplate1 is a corner piece, skipped)
    across each wall face, scaled to fit. `WallPanelFlip` const if a panel faces into the wall.
  - `Fix PSX Model Scale` (normalizes giant FBX imports), `Scatter Hospital Props` (bounds-normalized).
- **Art is gitignored trial** (`Assets/PSXBackrooms/`, `Assets/LoafbrrAssets/` — Loafbrr dropped, owner
  disliked it). The dressed scene references it, so the SCENE isn't committed (regenerate via menus); only
  the tooling is. Blender 5.1.2 installed for `.blend` imports.
- **Neon EXIT signs done** (`ProjectS > Place Exit Signs`): 10 hung vertical + flush to ceiling, avoiding
  ceiling fixtures; emissive from `ExitSignRedTex` (white text glows readable, red glows red), NO point light
  (neon look without lighting the room). Added URP **Bloom** to InsanitySystem's runtime volume so emissives
  glow. Tunable consts: `ExitSignScale`, `ExitSignEuler`, `ExitSignCount`, emission intensity.
- **Playtest-ready.** Next: place the remaining props the owner will direct — **WallVent, Outlet, Sign1**
  (wall-mounted; use the door-style OrientAgainstWall + a height offset) and **HospitalBed/Chair/Tray**
  (floor furniture; `Scatter Hospital Props` exists but owner wants curated placement). Then: playtest-feedback
  tuning, and the device pass (HapticManager/AudioDirector wiring the proven Core Haptics + PHASE).

### 2026-07-06 — Sappa — Phase 1 chunks 1-2 (player + monster, editor-verified)
- **Chunk 1 — PlayerController** (`Assets/Scripts/Player/PlayerController.cs`): CharacterController FP move
  + look on the new Input System (WASD/mouse for editor iteration; touch comes at the device phase), gravity,
  flashlight, `controlFactor` + FOV-ladder fields (driven later by the catch system). Tuning from
  architecture.md. Verified walkable in Play Mode. `activeInputHandler` is **New Input System only** (=1) —
  old `Input.GetAxis` won't work; use `UnityEngine.InputSystem`.
- **Chunk 2 — MonsterAI FSM** (`Assets/Scripts/Monster/MonsterAI.cs`): key-gated Static/Watcher/Hunter.
  Hunter chases when aware (sight + line-of-sight, hysteresis 10/20m), creeps to last-known when it loses
  you; Watcher teleports around the player (min 4m); breathing-room hook `OnQteWon()`. Debug hotkeys
  1/2/3/Q. Verified in editor. Fix applied: on Hunter activation, seed last-known = player pos so it hunts
  toward you instead of standing still.
- **Tooling** (`Assets/Scripts/Editor/GreyboxRoomGenerator.cs`): menu `ProjectS > Create Greybox Test Setup`
  builds room + Player + Monster + auto-bakes & **persists** a NavMesh asset (`Assets/NavMeshData/`).
  Also `Create Greybox Room`, `Create Player`, `Bake NavMesh`. Monster is on the Ignore Raycast layer so it
  never blocks its own line-of-sight checks.
- **Chunk 3a DONE — core loop end-to-end winnable (editor-verified):**
  - `GameState.cs` (Core/): thin run-state (keys, catches, RunState), drives monster tier via
    `OnKeyCollected`, `TryExit()` wins if keys held, `AddCatch()` stub (Phase 2 wires it). Temp OnGUI
    dev readout (remove for near-zero-HUD ship).
  - `Key.cs` + `ExitDoor.cs` (Gameplay/): proximity pickup/exit (reliable with CharacterController; base
    for Haptic-Primary auto-pickup). Key has optional `_revealOnPickup` (maze-switch hook).
  - Generator: `ProjectS > Create Game Loop Test` = room + player + Static monster + GameState + 2 keys +
    exit. Verified: hold 1 → collect 2 (Static→Watcher→Hunter) → reach green exit = "YOU ESCAPED".
### 2026-07-07 — Sappa — Asset pipeline + level-design tooling
- **Blender 5.1.2 installed** (`/Applications/Blender.app`, via `brew install --cask blender`) → Unity can now
  auto-import `.blend`. Best practice: still prefer FBX/GLB downloads; use `.blend` when it's the only option.
- **Generator refactor** (`GreyboxRoomGenerator.cs`) for moving greybox → real art levels:
  - `Create Gameplay Actors` — spawns Player+Monster+GameState+all systems (no walls/keys/exit) to drop into
    any environment. `Spawn Key`/`Spawn Exit` at the scene-view focus. `Bake NavMesh (Selected)` bakes+persists
    a navmesh on any environment root (e.g. an imported art level). Per-object navmesh asset naming.
  - Workflow: drop an art level → Bake NavMesh (Selected) → Create Gameplay Actors → move Player/Monster onto
    the floor → Spawn Key×2 + Exit → Play.
- **Trial art imported (gitignored, not final):** `Assets/LoafbrrAssets/` (Loafbrr backrooms kit, CC0, 250
  modular prefabs + `TstLevel` sample, MeshColliders) and `Assets/PSXBackrooms/` (PSX pack: 16 FBX pieces +
  textures auto-hooked + `Hallways.blend`). Both in `.gitignore` under "temporary imported art packs" — the
  final art direction gets its own commit decision (size/LFS). `Assets/_Project/` holds our own final-asset
  folders (committed).
- **Procedural Maze Generator DONE** (`GreyboxRoomGenerator.cs` → `ProjectS > Generate Maze Level`): builds a
  greybox backrooms maze (8×8 cells @ 3.5m, walls 3m) via recursive backtracker + braiding (loops for evasion),
  full gameplay rig + keys at opposite corners + exit at far corner + baked NavMesh. Re-run = fresh layout.
  Verified playable + fun. **This is the level-design approach: design/tune the maze in greybox, skin with art
  later** by swapping walls at the same grid positions.
- **Art direction:** dropped Loafbrr (owner disliked the look). Using **PSXBackrooms** (`Assets/PSXBackrooms/`,
  gitignored trial) — 16 FBX pieces + textures auto-hooked. Skinning is a later step; needs the PSX wall piece
  width measured so the maze CellSize matches for clean tiling.
- **Next session — driven by the friends' playtest (planned for ~2026-07-08):** collect their feedback, then
  TUNE the maze feel (CellSize, MazeW/H, BraidChance, monster speed — all const/[SerializeField]) + general
  polish. After the maze feels right: (1) SKIN it with PSX pieces, (2) get a **monster model** (env packs are
  environment only), (3) HapticManager/AudioDirector (device) to fire heartbeat/jumpscare/beacons. Event C
  reveal unlocks once real sections exist.

### 2026-07-06 — Sappa — Phase 1 chunk 3b (front-end flow, editor-verified)
- GameState is now the game-flow orchestrator: **MainMenu → Playing → Won/Lost → replay**. Menu freezes
  player+monster; [Enter] begins the run (and starts ScareDirector's Event B timer via `OnRunStarted`);
  [R] on the end screen reloads the scene for a clean reset. IMGUI screens ("PROJECT S", "YOU ESCAPED"/
  "CAUGHT") are greybox stand-ins for the real cold-open/menu/options.
- ScareDirector: Event B timer starts on run begin (not in the menu); FireEventB gated to Playing.
- Verified: menu → Enter → play loop → win/lose → R → fresh restart.
- **The greybox is now a complete, replayable game.** Core loop + fear layer + flow all in.
- **Next options (bigger, fresh-session work):** Phase 4 accessibility (Haptic-Primary Mode + audio beacons
  — the headline; device-heavy), a HapticManager/AudioDirector pass to make heartbeat/jumpscare/beacons fire
  on device (wraps the proven plugins), or real level design (unlocks Event C + section reveal + backrooms feel).

### 2026-07-06 — Sappa — Phase 3 chunk 3 (ScareDirector, editor-verified) — PHASE 3 COMPLETE
- `ScareDirector.cs` (Gameplay/): **Event B — False Catch** (timed ~5s, reliable): monster lunges 1.2m in
  front of you (frozen → QTE suppressed via IsBusy), fear spikes to 0.9, held 0.45s, then it vanishes
  (teleports far). No catch-counter change. Debug key J. **Event C** (reveal on section entry) is stubbed
  as `TriggerReveal()` — call it from a must-cross trigger once real levels exist.
- Added `MonsterAI.TeleportTo`/`FaceInstant`, `InsanitySystem.Spike`. Generator adds ScareDirector.
- Verified: wait ~5s (or press J) → monster slams into view + vignette flare, then vanishes; catches stay 0.
- **✅ PHASE 3 COMPLETE** (Insanity + Rearrange + Scares). The greybox now has the full fear layer.
- **Next options:** Phase 1 chunk 3b (front-end flow: cold-open → menu → options → run → win/lose → replay
  reset), or Phase 4 (accessibility headline: Haptic-Primary Mode + audio beacons — device-heavy), or start
  real level design (unlocks Event C + section reveal). Recommend chunk 3b (thin, makes it feel like a game)
  or a haptic/audio wiring pass (a `HapticManager`/`AudioDirector` wrapping the proven plugins, so heartbeat
  + jumpscare slam + beacons actually fire on device).

### 2026-07-06 — Sappa — Phase 3 chunk 2 (RearrangeSystem, editor-verified)
- `RearrangeSystem.cs` (Gameplay/): perception phantom (dark capsule spawns behind you, vanishes as you turn
  to face it) + ceiling point-light death behind you. Gated by unobserved + insanity ≥ 0.5. Uses the
  `ForwardDot` primitive with **hysteresis**: spawns at dot < 0 (behind), vanishes at dot > 0.93 (nearly
  facing, inside the ~60° FOV) — the fix for "phantom vanished before I could see it" (unobservedDot 0.3 ≈ 72°
  is outside the 60° FOV, so a single threshold popped it off-screen). Debug: press P to force a phantom.
- Generator adds ceiling point lights (light-death targets) + RearrangeSystem to the GameState object.
- Verified: press P → glimpse a figure at the screen edge as you turn, then it vanishes.
- **Next — Phase 3 remaining:** ScareDirector (Event B false-catch ~5s; Event C reveal on section entry —
  needs section geometry, better after real levels). Then Phase 1 chunk 3b (front-end flow) or Phase 4
  (accessibility: Haptic-Primary Mode, audio beacons).

### 2026-07-06 — Sappa — Phase 3 chunk 1 (InsanitySystem, editor-verified)
- `InsanitySystem.cs` (Core/): insanity 0..1 rises near a live monster (`closeness*0.22/s` within 6m) + while
  moving (`0.06/s`), decays idle (`0.07/s`). Drives a runtime URP **Vignette** (isolated Volume so no shared
  profile is dirtied; breathes at the heartbeat rate, peak opacity 0.30→0.75) and feeds `MonsterAI.SetInsanity`
  (wider sight, quicker Watcher). Exposes `HeartbeatBpm` (60+90·insanity) for the on-device haptic/audio heartbeat.
  Enables `renderPostProcessing` on Camera.main at runtime. Dev readout (no meter in the ship build).
- Generator adds InsanitySystem to the GameState object. Verified: vignette breathes + darkens with fear,
  insanity rises near the monster / decays when calm.
- **Next — Phase 3 remaining:** RearrangeSystem (perception phantom that vanishes when looked at + a light
  dying behind you; gated by unobserved + high insanity) and ScareDirector (Event B false-catch ~5s, Event C
  reveal on section entry). Then Phase 1 chunk 3b (front-end flow) or Phase 4 (accessibility).

### 2026-07-06 — Sappa — Phase 2 (QTE + catch/recoil ladder, editor-verified)
- `QTEController.cs` (Gameplay/): proximity trigger (≤1.6m, non-Static, monster not busy) → needle dial;
  tap [Space] in the green. 3 hits = stun + `OnQteWon` breathing room; 3 fails = `AddCatch` + monster
  `Recoil` + player catch ladder. Cooldown 1.5s. IMGUI dial is a greybox stand-in. Tuning from architecture.md.
- MonsterAI: added `Stun`/`Recoil`/`SetFrozen`/`IsBusy`; QTE won't (re)trigger while stunned/frozen so a
  catch gives a real escape window.
- PlayerController: `SetInputEnabled` (freeze during QTE), `ApplyCatch` (FOV ladder always; speed penalty
  behind `_applyControlPenalty`, **default OFF for greybox** — design wants it ON later).
- **Bugs fixed this session (all verified):** (1) Hunter sat still on tier-switch → seed last-known =
  player pos. (2) NavMesh baked only in memory → generator now persists a NavMeshData asset. (3) **Monster
  capsule body-jammed the player's CharacterController** (could look, couldn't walk) → `Physics.IgnoreCollision`
  in MonsterAI.Awake + trigger collider + agent stoppingDistance 1.2m. Diagnosed via [Move] logs (input read,
  cc enabled, pos frozen = physical block).
- **Loop now fully win AND lose:** collect keys → escalate → QTE encounter → win (breathing room) or lose
  (catch ladder) → 3 catches = CAUGHT, or reach exit = ESCAPED.
- **Next options:** Phase 1 chunk 3b (menu ↔ run ↔ end-screen + replay reset), or Phase 3 (Insanity →
  heartbeat/vignette + monster modulation; Rearrange; ScareDirector). Recommend chunk 3b next (thin, closes
  the front-end flow) then Phase 3. Section reveal (real maze-switch) + real levels are level-design tasks.

### 2026-07-06 — Sappa — Phase 1 chunks 1-2 (player + monster, editor-verified)

### 2026-07-06 — Sappa — Phase 0 setup (project + plugins)
- **Repo hygiene:** added Unity + Claude `.gitignore` (ignores local `CLAUDE.md`/`.claude/`; keeps
  `memory-bank/` tracked). Note: commits in this repo intentionally have NO Claude co-author.
- **Phase 0 #1 DONE (verified from file):** iOS Build Support installed; platform switched to iOS.
  Player Settings → **Target Device: iPhone Only** (`targetDevice: 0`), **landscape-lock** (Auto Rotation,
  portrait disabled, both landscapes on). Env confirmed: Unity `6000.4.3f1`, Xcode 26.3, Apple Silicon.
- **Phase 0 #2 DONE (verified, 0 console errors):** built Apple Unity Plugins via `build.py`
  (`Core 3.2.0`, `CoreHaptics 1.3.1`, `PHASE 1.2.7`, `Accessibility 1.1.4`) for iOS+macOS. Vendored the
  4 `.tgz` into `LocalPackages/`, manifest uses **relative** `file:../LocalPackages/*.tgz` (portable for
  teammates on clone). `Assets/Apple Plug-In Support/` (auto-generated editor libs) is gitignored.
  - Known harmless warning: "no macOS native library for Apple.Accessibility" → only affects Editor
    Play Mode; Accessibility works on-device (iOS lib present).
  - Plugin source cloned at `~/Documents/apple-unityplugins` (outside repo). Min OS: plugins need iOS 15.6+
    — **TODO:** bump project's iOS min from 15.0 → 15.6 before device build (avoids deployment-target warning).
- **Phase 0 #3 DONE — Core Haptics PROVEN ON DEVICE (2026-07-06):** built `HeartbeatHapticTest.cs`
  (`Assets/Scripts/DeRisk/`), ran on a real iPhone — heartbeat lub-dub felt clearly, rate + intensity
  scale with insanity (auto-sweep). **The game's soul works in Unity.** ✅
  - **Gotcha (important):** the PHASE plug-in's `PHASEBuildStep.OnProcessEntitlements` injects two
    paid-only entitlements into EVERY iOS build — `com.apple.developer.coremotion.head-pose` and
    `com.apple.developer.spatial-audio.profile-access`. A free "Personal Team" can't sign them → Xcode
    fails ("requires a provisioning profile with Head Pose and Spatial Audio Profile features").
    Fix committed: `Assets/Scripts/Editor/StripPaidEntitlements.cs` — a `[PostProcessBuild]` that strips
    both after every build (our design pans audio from the listener transform, not physical head-pose).
    Set `STRIP=false` there if we ever go paid + want AirPods head-tracking.
  - Device build recipe: Unity `File > Build Profiles > iOS > Build` → open `Builds/Unity-iPhone.xcodeproj`
    → Signing & Capabilities: auto-signing + Personal Team → Clean Build Folder → Run. Trust the dev
    profile on-device (Settings > General > VPN & Device Management). Free-account apps expire in 7 days.
- **Phase 0 #4 DONE — PHASE PROVEN ON DEVICE (2026-07-06):** imported the plug-in's official "PHASE Demo"
  sample (Package Manager > Apple.PHASE > Samples), built to a real iPhone with headphones — spatial audio
  direction/distance clearly audible. **The by-ear navigation foundation works in Unity.** ✅
  - Sample lives at `Assets/Samples/Apple.PHASE/1.2.7/PHASE Demo/` — keep as the reference for how to wire
    a PHASE node-graph (Sampler → Spatial Mixer + PHASESource/PHASEListener) when we author our own audio.
  - Confirmed our `StripPaidEntitlements.cs` auto-strips the paid entitlements on every build (worked
    hands-off on this build).
- **Phase 0 #5 — N/A:** both plugins met the design's needs, so no fallback (native Unity audio / simpler
  haptics) is required.

- **✅ PHASE 0 COMPLETE.** Project setup + both Apple frameworks de-risked on device. The "Apple frameworks
  ARE the mechanic" pillar is confirmed portable to Unity. Ready for Phase 1.
- **Next — Phase 1 (greybox core loop):** greybox level (NavMesh baked) → PlayerController (CharacterController
  + touch look + flashlight) → MonsterAI FSM (Static/Watcher/Hunter, NavMeshAgent, awareness) → keys +
  sections + win/lose. Build it end-to-end winnable & losable in the editor (no device needed for this phase).
  Housekeeping first: restore SampleScene (or a new greybox scene) to the build list; decide whether to keep
  the PHASE demo sample in-repo or gitignore `Assets/Samples/`.

### (seed) — Pivot from RealityKit to Unity
- Prototyped the full game in Swift/RealityKit; validated mechanics + balance; hit RealityKit's AR-first
  limits (hand-rolled engine features, painful .usdz import). Decided to pivot to Unity, keeping Core
  Haptics + PHASE via Apple's Unity plugins. Wrote this doc set as the design bible for the Unity build.
- Next: Phase 0 — Unity/URP setup + import Apple plugins + prove haptics/PHASE on device.
