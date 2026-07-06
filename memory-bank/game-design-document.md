# Game Design Document — Project S (Unity)

> **Source of truth.** If code disagrees with this file, the code is wrong (log it in implementation-plan.md).
> This is the reconciled design after a full RealityKit prototype + many playtest iterations. Trust it.

## Concept
First-person psychological horror; escape / survival. Single-player, iPhone, **landscape**. You wake inside
a structure that rearranges itself when your attention drifts. You cannot see the way out — you **hear** it.
Collect keys, evade an AI monster that escalates, survive close encounters, and reach the exit before the
catch counter ends your run.

## Design pillars (LOCKED — do not dilute)
1. **Navigate largely by ear.** Spatial audio (PHASE) is a core mechanic, not flavor. No compass, no minimap.
   *(Prototype gap: for sighted players nav was still visual; the intended endgame is audio beacons on the
   key/exit — see implementation-plan.md.)*
2. **The Apple frameworks ARE the mechanic** — Core Haptics + PHASE spatial audio. In Unity these come via
   Apple's official Unity plugins. Prove a framework can BE the mechanic, not background support.
3. **Near-zero HUD.** Fear is communicated through haptics + audio + a screen-edge vignette — never on-screen
   meters. (No insanity bar.)
4. **Accessibility is ground-up, not bolted on.** **Haptic-Primary Mode** is the headline — fully playable
   by Deaf/HoH AND visually impaired players.
5. **Evasion only.** No weapons. The only "attack" is the proximity-QTE stun, which buys escape, not a kill.
6. **iPhone-only, landscape.** The scare design depends on the iPhone Taptic Engine.

## Core loop
Listen/feel → Move → Find a key → Pickup drives a maze-switch that reveals the next section → the monster
escalates → close encounter → proximity QTE → win = breathing room (it loses you) / lose = +1 catch. Reach
the exit before 3 catches.

## The monster (predator, not scripted)
Key-gated tiers set the baseline; **insanity modulates aggression within a tier** (see below).
- **Static** — dormant / placed, not yet pursuing (0–1 keys).
- **Watcher** — teleports around you to unsettle (2 keys). Never inside QTE range (min distance). At high
  insanity: appears more often + biases to the nearest spot.
- **Hunter** — a **predator with awareness** (3 keys): chases fast when it can sense you; when it loses you
  it **creeps to your last-known spot and searches** (feels like it's hunting, not heat-seeking). Awareness
  has hysteresis (gains you within `sightRange`, drops you only beyond `loseRange`).
- Uses **NavMesh pathfinding** in Unity (the prototype hand-rolled A*).

## The breathing-room loop (the balance heart)
The game is about **surviving encounters, not out-running** (you can't out-run a crippled-speed player).
- **Win a proximity QTE** → the monster is stunned AND **thrown off** — it drops to searching your
  last-known spot for a few seconds instead of re-locking. This gives a real ebb (chase → win → dread →
  re-detect → chase). You can slip away even when slow.
- **Lose a QTE (catch)** → +1 catch, brief recoil (shoved back + short stun so you always get a fresh
  escape), monster stays aware (pressure continues). **Every catch is a survivable strike**, not a spiral.

## Insanity (fear) — internal only, drives + modulates
`insanity` (0..1): rises near the monster / while moving, decays (slowly) when calm. It:
- Drives the **heartbeat BPM + haptic/audio intensity + vignette** (never a meter).
- **Modulates the monster within its current tier:** Static → faint presence hints; Watcher → more frequent
  + closer; Hunter → wider senses (bigger effective `sightRange`), harder to lose.
- Loop stays fair: slip away (win QTE → distance) → insanity drains → the monster calms.

## Rearranging structure (perception layer)
The "structure changes when your attention drifts" conceit lives in the **environment** (not the monster —
keeping the monster a fair, accessible predator). Gated by **unobserved AND high insanity**:
- **Perception phantom** — a dark figure glimpses in where you're *not* looking and **vanishes the instant
  you turn to it** (Weeping-Angel). A ghost: no attack, no progress impact → accessibility-safe.
- **Micro-shift** — a ceiling light dies behind you, etc.
- *(Future: geometric rearrange / key relocation — needs audio beacons first so it stays fair.)*

## Structure — continuous sections
- Maze-switch on key pickup: **no doors, no loading screens**. Continuous spatial layout (3 sections in the
  prototype; Unity uses scene-authored levels — quantity is a level-design decision).
- **Section 3 is intentionally scare-free** — tension release toward the exit; the monster retires when you
  cross in. *(The owner also explored keeping the Hunter active through S3; scare-free is the recommended
  design — it preserves the fear rhythm and makes the S2 climax land. Pick per your final level design.)*

## Key economy
- **1 held + 2 found**, spread across sections (geometry gates order). Keys are the **only** pickup.

## QTE — proximity solve
- Needle-in-green-zone timing minigame. **3 hits = stun the monster (escape window); 3 fails = +1 catch**
  (+ recoil). Progress **saves within a session, does not reset**; QTE does **not** calm the vignette.
- *(A second "max-fear survival" QTE was designed but deferred — optional.)*

## Scare events (MVP, scripted one-shots)
- **Event B — False Catch (early):** a fake-out jumpscare (monster lunges in + haptic slam + fear spike,
  then vanishes) that spikes fear **without** advancing the catch counter. Time-based trigger (reliable).
- **Event C — The Reveal (mid):** a jumpscare on crossing into the next section (reliable, must-cross line).
  Pure jumpscare (no QTE) — the owner chose maximum jolt over the QTE resolution.

## Fear representation (NO meter UI)
- Only via the Core Haptics heartbeat + screen-edge vignette (breathes with insanity, decays naturally).

## Win / Lose
- **Win:** reach the exit at the end of the final section with catches < 3.
- **Lose:** catch counter reaches 3. Penalty ladder escalates: **heavier controls → reduced FOV → game over.**

## Accessibility (ground-up — the headline)
- **Haptic-Primary Mode** (see accessibility details in architecture.md): eyes-off play via a haptic
  "compass", gyro-aim, hold-to-walk, and auto-pickup. Playable by blind AND Deaf/HoH players.
- VoiceOver + Larger Text supported (respect system settings). Reduce Flashing option. Localization baseline.

## Scope discipline
Short runway. Cut anything not essential to the core loop. Nothing ships that dilutes by-ear navigation or
the ground-up accessibility promise.
