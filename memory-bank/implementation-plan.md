# Implementation Plan — Project S (Unity)

> The ordered roadmap for the Unity build. Ordering logic: **de-risk the headline (Apple plugins) first**,
> then **close the core loop** greybox, then layer systems, then accessibility, then content, then polish.
> The DESIGN is already proven (RealityKit prototype) — this is a re-implementation in a better engine, so
> most systems port fast. Tick items off in progress.md; verify against the GDD.

Legend: `[ ]` not started · `[~]` partial · `[x]` done & verified

## Phase 0 — Project setup + plugin de-risk (DO THIS FIRST)
- `[ ]` **Unity project + URP**, iOS build target, landscape lock, iPhone-only.
- `[ ]` **Apple Unity Plugins** (`github.com/apple/unityplugins`): import `Apple.Core`, `Apple.CoreHaptics`,
  `Apple.PHASE` (+ `Apple.Accessibility`). Build native libs; check README + Issues for version/bugs.
- `[ ]` **Prove Core Haptics on device** — heartbeat lub-dub with dynamic intensity. This is the soul; if it
  can't do it, stop and reassess before building further.
- `[ ]` **Prove PHASE on device** — one spatial source + moving listener, audible direction/distance on
  headphones. The by-ear foundation.
- `[ ]` Decide fallbacks if a plugin falls short (native Unity spatial audio / simpler haptics).

## Phase 1 — Greybox core loop (must be end-to-end winnable & losable)
- `[ ]` **Greybox level** in the scene editor (a few connected rooms/corridors; NavMesh baked).
- `[ ]` **PlayerController** — CharacterController move + touch look; flashlight (spotlight on camera).
- `[ ]` **MonsterAI FSM** — Static → Watcher → Hunter (key-gated), NavMeshAgent chase; awareness
  (sight/lose hysteresis) + stalk-to-last-known.
- `[ ]` **Keys + sections + win/lose** — 1 held + 2 found; pickup reveals next area; reach exit = win;
  3 catches = game over. Menu ↔ run ↔ end-screen navigation; replay resets cleanly.

## Phase 2 — Encounter + balance (port the proven loop)
- `[ ]` **QTEController** — needle-in-green-zone; 3 hits = stun, 3 fails = +catch. Clear held input on close.
- `[ ]` **Breathing room** — win QTE → stun + force the monster to search last-known (it loses you).
- `[ ]` **Catch ladder** — recoil on non-fatal catch + heavier controls (`controlFactor`) + FOV reduction.
- `[ ]` Apply the **tuning numbers** from architecture.md; re-feel on device (they're a strong starting point).

## Phase 3 — Fear systems
- `[ ]` **InsanitySystem** — accumulation/decay; drives heartbeat + vignette; **modulates the monster per
  tier** (Watcher freq/nearness, Hunter effective sight).
- `[ ]` **RearrangeSystem** — perception phantom (unobserved + scared, vanish-on-look) + a light dying behind
  you. No progress impact; keep spawns on valid floor.
- `[ ]` **ScareDirector** — Event B (false catch, ~5s, no counter) + Event C (reveal, on section entry).

## Phase 4 — Accessibility (the headline — not optional)
- `[ ]` **Haptic-Primary Mode** (see accessibility in architecture.md / GDD): haptic **compass** (hot/cold),
  **gyro-aim**, **hold-to-walk**, **auto-pickup**, boosted **danger heartbeat**. Gate behind a toggle so the
  default experience is unchanged.
- `[ ]` **Audio beacons** on the key/exit (PHASE) — makes "navigate by ear" real for EVERYONE and unifies
  with Haptic-Primary. This closes the biggest prototype gap; do it once PHASE is solid.
- `[ ]` **VoiceOver / Larger Text / Reduce Flashing** options; localization baseline.

## Phase 5 — Front-end & flow
- `[ ]` Cold open (haptic pulse before text) → synopsis (first launch) → menu → options. Win/lose screens.
- `[ ]` Options: Haptic-Primary, Reduce Flashing, audio/headphone check, language.

## Phase 6 — Content, polish & ship
- `[ ]` **Real level design** in the scene editor (the designer works directly in Unity now — no .usdz
  handoff pain). Backrooms art pass; monster reveal (silhouette, don't over-light it).
- `[ ]` **Tuning pass** (re-feel all numbers on device), jumpscare polish (audio sting), **AppIcon**.
- `[ ]` **Device QA** — full playthrough, normal + eyes-off (Haptic-Primary), all scares, win + lose paths.
- `[ ]` Landscape lock, build/scheme sanity, archive.

## What was PROVEN in the prototype (trust these)
Predator AI (chase/stalk/last-known + awareness), the breathing-room loop, insanity-modulates-per-tier,
recoil-on-catch + FOV ladder, QTE feel (~0.22s window), rearrange perception layer, and the full
Haptic-Primary control scheme (compass + gyro + hold-to-walk + auto-pickup). The **tuning numbers** in
architecture.md are the ones that felt right.

## What's still OPEN (design/verify in Unity)
- Audio beacons for by-ear nav (designed, not built). Real level layouts + scale (proto ceiling was too low).
- Monster reveal/lighting polish. AppIcon + art. Whether S3 is scare-free vs Hunter-through (recommend
  scare-free). Confirm Apple-plugin coverage on device.
