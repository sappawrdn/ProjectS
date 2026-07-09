# Game Design Document — Project S (Unity)

> **Source of truth.** If code disagrees with this file, the code is wrong (log it in implementation-plan.md).
> This document has been updated to reflect the shift towards a **PSX Abandoned Hospital Horror** visual identity, while preserving the core audio-haptic vision.

## Concept
First-person psychological horror; escape / survival. Single-player, iPhone, **landscape**. You wake inside a desolate, retro PSX-style **Abandoned Hospital**. Pitch-black surroundings are pierced only by a harsh flashlight. Exploration is driven by visual cues (finding specific physical objects like valves) fused with spatial audio (PHASE). Evade an AI monster that escalates, survive close encounters, and reach the exit (a heavy red-neon door) before the catch counter ends your run.

## Design pillars (LOCKED)
1. **PSX Liminal Atmosphere (New).** The visual identity is classic PSX horror. Harsh lighting (bright flashlight vs pitch-black environment), grungy textures (hospital beds, wheelchairs, tiles), and realistic props form the foundation of tension.
2. **The Apple frameworks ARE the mechanic.** Core Haptics + PHASE spatial audio remain core. Even with a stronger visual identity, threat detection still relies heavily on hearing and the Taptic Engine.
3. **Near-zero HUD.** Fear is communicated through haptics (heartbeat) + audio + a screen-edge vignette — never on-screen meters or sanity bars.
4. **Accessibility is ground-up.** **Haptic-Primary Mode** is the headline — fully playable by Deaf/HoH AND visually impaired players.
5. **Evasion only.** No weapons. The only "attack" is the proximity-QTE stun, which buys escape time, not a kill.
6. **iPhone-only, landscape.** The scare design depends on the iPhone Taptic Engine.

## Core loop
Listen/feel & Explore visually → Find a physical key (e.g., Red Valve) → Pickup triggers area progression → Monster escalates → Close encounter → Proximity QTE → Win = breathing room (it loses you) / Lose = +1 catch. Reach the Neon Exit Door before 3 catches.

## The monster (predator, not scripted)
Not a ghost that spawns and despawns, but a pursuing AI entity. Escalates in 3 tiers (gated by key pickups):
- **Static** — Dormant / placed, not yet pursuing (0–1 keys).
- **Watcher** — Teleports around you to unsettle (2 keys). Never inside QTE range. At high insanity: appears more often + biases closer.
- **Hunter** — A **predator with awareness** (3 keys): chases fast when it can sense you; when it loses you it **creeps to your last-known spot and searches**. Awareness has hysteresis.
- Uses **NavMesh pathfinding** in Unity.

## The breathing-room loop (the balance heart)
The game is about **surviving encounters, not out-running**.
- **Win a proximity QTE** → The monster is stunned AND **thrown off** — it drops to searching your last-known spot for a few seconds. This gives a fair ebb and flow (chase → win → dread → re-detect).
- **Lose a QTE (catch)** → +1 catch, brief recoil (shoved back + short stun so you get a fresh escape). Monster stays aware. **3 catches = Game Over.** Every catch is a survivable strike, not a spiral.

## Insanity (fear) — internal only, drives + modulates
`insanity` (0..1): Rises near the monster / while moving, decays (slowly) when calm. It:
- Drives the **heartbeat BPM + haptic/audio intensity + vignette** (never a meter).
- **Modulates the monster:** Static → stronger presence signals; Watcher → more frequent + closer; Hunter → wider senses (`sightRange` expands), harder to lose.
- Loop stays fair: slip away (win QTE → run) → insanity drains → the monster calms.

## Environment & Props (Hospital Theme)
A major shift from abstract structures to a definitive environment:
- **Interactive Props:** Hospital beds, wheelchairs, medical trays, and decaying doors scatter the level.
- **Exit Door:** No longer just an abstract zone; it is a heavy metal door adorned with a **glowing red EXIT neon sign** that stands out in the dark.
- **Keys:** Keys have physical forms (e.g., `lalve2` Valve) that must be visually located among the hospital clutter.

## QTE — proximity solve
- Needle-in-green-zone timing minigame. **3 hits = stun the monster (escape window); 3 fails = +1 catch** (+ recoil). Progress **saves within a session, does not reset**; QTE does **not** calm the vignette.

## Scare events (MVP, scripted one-shots)
- **Event B — False Catch (early):** A fake-out jumpscare (monster lunges in + haptic slam + fear spike, then vanishes) that spikes fear **without** advancing the catch counter.
- **Event C — The Reveal (mid):** A pure jumpscare upon crossing into a new section boundary.

## Win / Lose
- **Win:** Reach the Exit Door at the end of the final section with catches < 3.
- **Lose:** Catch counter reaches 3. Penalty ladder escalates: **heavier controls → reduced FOV → game over.**

## Accessibility (ground-up — the headline)
- **Haptic-Primary Mode**: Eyes-off play via a haptic "compass", gyro-aim, hold-to-walk, and auto-pickup.
- VoiceOver + Larger Text supported. Reduce Flashing option.
