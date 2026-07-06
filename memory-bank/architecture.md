# Architecture — Project S (Unity)

> The technical map: how each system works (engine-agnostic, proven in the prototype) + how to build it in
> Unity + the **playtested tuning numbers**. `CLAUDE.md` imports this. Keep it current.

## Rules on every change
- **No magic numbers** — all tuning as `[SerializeField]`. Starting values are in the Tuning reference below.
- **Greybox before art.** Prove the mechanic, then dress it.
- **No meter UI** — fear is haptic + audio + vignette only.
- Use Unity built-ins (NavMesh, CharacterController, scene editor, URP). Don't hand-roll what the engine gives.

## Prototype → Unity mapping (stop hand-rolling)
| Concern | RealityKit prototype (hand-rolled) | Unity (use this) |
|---|---|---|
| Player movement + collision | manual per-axis AABB grid vs walls | **CharacterController** (capsule) + colliders |
| Monster navigation | hand-written A* on a voxel grid | **NavMesh + NavMeshAgent** (bake the level) |
| Levels | ASCII strings / .usdz voxelised | **Scene editor** (place walls/props; designer works in Unity) |
| Lighting | fought RealityKit lights | **URP** (spotlight flashlight + baked/point lights, dark & moody) |
| Haptics | Core Haptics native | **Apple.CoreHaptics** Unity plugin |
| Spatial audio | PHASE native | **Apple.PHASE** Unity plugin |
| Camera look | drag gesture → yaw/pitch | Touch look control (right zone) + optional gyro |

## Suggested structure (MonoBehaviours)
There is NO god-object here (the prototype had one — avoid it). Split by system:
- **GameState** — run flags (playing/paused/won/lost), catch count, key count, insanity value. Thin.
- **PlayerController** — CharacterController movement, look, controlFactor penalty, FOV lerp.
- **MonsterAI** — the FSM (Static/Watcher/Hunter) driving a NavMeshAgent; awareness + breathing-room.
- **InsanitySystem** — accumulation, decay, and it feeds MonsterAI (modulation) + heartbeat + vignette.
- **QTEController** — the needle minigame + resolution (win → stun+lose-awareness, lose → +catch+recoil).
- **ScareDirector** — one-shot scripted scares (Event B time-based, Event C on section entry).
- **RearrangeSystem** — perception phantom + light flicker, gated by unobserved + insanity.
- **HapticPrimaryController** — the eyes-off cue system (compass, grab buzz, confirm, danger boost).
- **HapticManager / AudioDirector** — wrap the Apple Core Haptics + PHASE plugins (one place).
- **LevelManager** — key placement, section reveal (maze-switch), exit / win trigger.

## The one reusable primitive: "observed"
Many systems ask *"is X inside the player's view?"* (rearrange, compass alignment, phantom vanish):
`observed(worldPos)` = `dot(cameraForwardXZ, normalize(worldPos - cameraPos).xz) > unobservedDot`.
Below the threshold = behind/beside you = "unobserved" = free to change / phantom may appear.

## System notes (the HOW, proven)
- **Monster FSM:** key count sets the tier (`OnKeyCollected`: 2→Watcher, 3→Hunter). Watcher teleports to
  spawn points (min distance from player so it never hands out a free QTE; insanity → more often + nearer).
  Hunter: NavMeshAgent to the player when `aware`, else to `lastKnownPos` (search). Awareness hysteresis:
  gain within `sightRange (+ insanity*bonus)`, drop beyond `loseRange`. After a WON QTE, force a
  `searchTimer` (ignore the player, creep to last-known) = the breathing room.
- **Catch ladder:** non-fatal catch → recoil (shove monster back + brief stun) + `controlFactor` makes
  controls heavier + FOV constricts (lerped, walls-closing-in). 3 catches → game over.
- **Insanity:** builds near monster / while moving, decays slowly when calm. Drives heartbeat BPM +
  vignette breathe + audio/haptic intensity, and modulates the monster per tier.
- **QTE:** needle sweeps 360°; tap when it's in the green zone; need 3 hits (stun) or 3 fails (+catch).
  Reset `moveInput` to zero when the QTE closes (prototype bug: stale on-screen-joystick input made the
  player drift afterward — in Unity, make sure held input doesn't carry through overlay transitions).
- **Scares:** Event B fires ~5s in (time-based = reliable); Event C fires on crossing into the next
  section (must-cross line = reliable). Both are pure jumpscares (haptic slam + monster lunge + fear spike,
  then vanish). Event B must NOT trigger the QTE (freeze/suppress it during the fake).
- **Rearrange:** at high insanity, a phantom appears unobserved and vanishes when looked at; a light dies
  behind you. No progress impact. Keep spawns on the floor / valid nav positions.

## Full flow (fill in as systems connect)
Key pickup → LevelManager (reveal next section) → MonsterAI.OnKeyCollected (tier up) → InsanitySystem
modulates → encounter → QTEController → win (breathing room) or lose (catch + recoil ladder) → win/lose screen.

---

## Tuning reference (PLAYTESTED — these took many iterations; start here, all `[SerializeField]`)

**Player**
- Eye height `1.6 m`; capsule radius ~`0.3 m`; base move speed `2.5 m/s`.
- `controlFactor` (speed × this): 0 catches → `1.0`, 1 → `0.6`, 2+ → `0.45`. *(Note: crippled speed drops
  below monster speed — that's fine BECAUSE the breathing-room loop makes escape possible without out-running.)*
- FOV lose-ladder: base `60°`, `−9°` per catch, floor `38°`, lerp speed `4` (smooth "walls closing in").

**Monster**
- `chaseSpeed 2.6` (a touch above the player — real threat, still maneuverable; 2.2 felt slow, 3.0 too fast).
- `stalkSpeed 0.9` (slow creep to last-known when it's lost you).
- `sightRange 10` + `insanity * sightInsanityBonus(6)` → calm 10 m, panic 16 m. `loseRange 20` (hysteresis).
- Watcher: base interval `4 s` × `(1 − 0.5*insanity)`; `watcherMinDistance 4 m` (never inside QTE range).
- After won QTE: `winStunSeconds 4` + `winSearchSeconds 4` (breathing room).

**QTE**
- Sweep `1.7 s` per 360°, green zone `46°` → tap window ~`0.22 s` (0.21 was brutal, 0.36 too easy).
- Need `3` hits or `3` fails. `proximityRadius 1.6 m` (fires the encounter). `qteCooldown 1.5 s`.

**Catch / recoil**
- 3 catches = game over. `catchRecoilDistance 6 m`, `catchStunSeconds 2`, plus the control + FOV ladder.

**Insanity**
- Near (`<6 m`): `+closeness * 0.22 /s` (closeness = (6−dist)/6). Moving: `+0.06 /s`. Idle: `−0.07 /s`.
- Heartbeat BPM = `60 + 90 * insanity`. Vignette opacity breathe `0.30 → 0.75`. Haptic-Primary danger
  boost `×1.25`.

**Rearrange (perception)**
- `rearrangeInsanity 0.5` (min fear), `unobservedDot 0.3` (~72° cone), phantom interval `5 s`, distance
  `7 m`, lifetime `4 s`, light-flicker interval `8 s`.

**Scares**
- Event B delay `~5 s`; Event C on section-entry line. `jumpscareHold 0.45 s`, `jumpscareInsanity 0.9`,
  monster-in-front distance `1.2 m`.

**Haptic-Primary** (full vocabulary in the Accessibility section below)
- Compass: `alignThreshold 0.5` (warm cone), ping period `0.12 s` (dead-on) → `0.6 s` (warm edge), silent
  when cold. Auto-pickup dwell `0.6 s`. Gyro yaw sign is device-dependent (flip if inverted).

**World**
- Prototype room was `20 m` grid — in Unity, level scale is a design decision (the proto ceiling was low
  ~2–3 m; aim ~3 m+ for backrooms feel).

---

## Accessibility — haptic vocabulary & Haptic-Primary Mode (the headline)
Fear + information travel through **touch + sound**, not the screen. All haptics via `Apple.CoreHaptics`.

**Haptic vocabulary** (must feel DISTINCT from each other):
- **Heartbeat** — a deep "lub-dub" (two events); intensity + rate scale with insanity. The fear channel.
- **Jumpscare** — a violent sharp slam + short rumble. For scares.
- **Cold-open pulse** — one deliberate low thump felt in the dark before the title (launch).
- **Compass tick** — crisp, light; fired faster the more you face the objective (Haptic-Primary).
- **Grab buzz** — soft continuous "you can pick up" (Haptic-Primary, near a key).
- **Confirm** — strong double thump (pickup done).

**Haptic-Primary Mode** (toggle; default experience unchanged when off) — makes the run playable eyes-off:
1. **Gyro-aim** — turning the phone/body aims the camera (like a compass); on-screen look-drag is disabled
   in this mode. Recalibrates each run; the mapping sign is device-dependent (flip if inverted).
2. **Hold-to-walk** — touch-and-hold anywhere = walk forward in the facing direction; release = stop. The
   on-screen joystick is hidden in this mode. (Single Taptic Engine can't do left/right, so: aim by
   turning, walk by holding.)
3. **Objective compass** — hot/cold to the nearest key, then the exit: `dot(facing, dirToTarget)` → tick
   faster when aimed at it, **silent when facing away** (so "wrong way" is unmistakable).
4. **Grab-ready + auto-pickup** — near a key: grab buzz; after a ~0.6 s dwell, auto-collect + confirm thump
   (no button-hunting).
5. **Danger channel** — the heartbeat is boosted (×1.25) so proximity/threat is felt.
Full eyes-off loop: turn to aim (compass) → hold to walk → buzz = arrived → release → auto-grab → repeat.

**Why Predator, not Weeping-Angel, for the monster:** a "safe only while you look at it" monster would
REQUIRE sight → it would break the by-ear pillar AND kill Haptic-Primary (a blind player can't look at it).
So the monster is a fair, accessible **predator**; the perception/attention theme lives in the ENVIRONMENT
(the rearrange layer) instead — which stays navigable by ear.

**Navigate-by-ear (PHASE):** ambient `hum`, the monster's `entity` sound (source follows it), and the
low-latency `heartbeat`. **The key follow-up: audio beacons on the key/exit** so sighted+hearing players
home in by ear too (unifying the default mode with Haptic-Primary). Verify PHASE plugin coverage first.

**Also:** VoiceOver + Larger Text (respect system settings, semantic UI, no hardcoded font sizes), a Reduce
Flashing option (dampen screen grain/flicker), and a localization baseline.
