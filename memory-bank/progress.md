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
