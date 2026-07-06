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

### (seed) — Pivot from RealityKit to Unity
- Prototyped the full game in Swift/RealityKit; validated mechanics + balance; hit RealityKit's AR-first
  limits (hand-rolled engine features, painful .usdz import). Decided to pivot to Unity, keeping Core
  Haptics + PHASE via Apple's Unity plugins. Wrote this doc set as the design bible for the Unity build.
- Next: Phase 0 — Unity/URP setup + import Apple plugins + prove haptics/PHASE on device.
