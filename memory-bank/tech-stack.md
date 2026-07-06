# Tech Stack — Project S (Unity)

> Stable file. Read once per session for context. This project pivoted from RealityKit/iOS to **Unity**
> because RealityKit is AR-first — we ended up hand-rolling collision, pathfinding, a level editor, and
> asset import. Unity gives all of those for free. The Apple-specific soul (Core Haptics + PHASE) is
> preserved via **Apple's official Unity plugins**.

## Platform & target
- **Platform:** iOS, **iPhone only**. iPad excluded on purpose — the scare design depends on the iPhone
  Taptic Engine.
- **Orientation:** Landscape (lock it).
- **Engine:** **Unity** (the owner is experienced in Unity 6 / URP / WebGL). Use **URP** for rendering
  (dark, moody backrooms; controllable lighting).

## The 3 core "mechanic" frameworks (via Apple Unity Plugins)
Repo: **`github.com/apple/unityplugins`** ("Apple Unity Plug-ins", official, open-source).
1. **Unity** replaces RealityKit as the 3D engine + physics (NavMesh) + monster AI host.
2. **`Apple.CoreHaptics`** — escalating heartbeat + QTE feedback + the Haptic-Primary cue vocabulary.
   Treated as a **primary information channel**, not flavor.
3. **`Apple.PHASE`** — spatial audio as the **navigation system** (orient + sense the monster by ear).
   *(PHASE is NOT in Unity natively — this plugin is why the pivot keeps the game's soul.)*
- Also useful: **`Apple.Accessibility`** (VoiceOver, Dynamic Type), `Apple.Core` (dependency).

> **Note on the "Apple frameworks ARE the mechanic" pillar:** in Unity you use Core Haptics + PHASE via
> plugin (2 of the original 3), and Unity replaces RealityKit. Confirm this satisfies the challenge premise
> if this is an Academy deliverable (it's a judgment call — verify with the Academy, not just technically).

## ⚠️ De-risk the plugins FIRST (before rebuilding the game)
The plugins carry the headline feature. Prove them on a real iPhone in a tiny scene **before** porting
gameplay:
1. Import `Apple.Core` + `Apple.CoreHaptics` + `Apple.PHASE` (build native libs via their build script;
   needs Xcode + Apple Silicon Mac). Check the repo's README + recent GitHub Issues for Unity-version
   support and known bugs.
2. **Core Haptics test:** play a heartbeat "lub-dub" (transient + continuous) and confirm it feels right on
   device. Verify dynamic intensity works (we scale intensity by insanity).
3. **PHASE test:** one spatial source + a moving listener; confirm you can hear direction/distance on
   headphones. This is the by-ear navigation foundation.
4. If either can't do what the design needs, decide fallbacks (native Unity spatial audio; simpler haptics)
   BEFORE committing the whole rebuild.

## Device / play conditions
- Handheld iPhone. **Headphones strongly recommended** (spatial audio is the compass).
- **Haptics + real spatial audio only feel right on a real device.** The simulator/editor won't reproduce
  the Taptic Engine — but Unity's **play-in-editor + Scene view** DO let you see geometry/AI live (a big
  win over the RealityKit prototype, where debugging was screenshot-only).

## Input
- Movement: on-screen virtual control (left zone). Look: touch (right zone).
- **Haptic-Primary Mode** swaps controls: **gyro-aim** (turn the phone) + **hold-to-walk** (touch-and-hold
  anywhere = walk forward) + **auto-pickup**. Use Unity's Input System; gyro via `Input.gyro` / device
  attitude.

## Build & test
- Build target iOS (Xcode). Test gameplay/AI in the **editor**; test haptics/PHASE **on device**.
- Keep the RealityKit Swift prototype repo as a **reference archive** — it holds the proven design + the
  exact tuning numbers (see architecture.md) + this doc set.

## Assets
- Backrooms textures, a monster model, and audio (`hum` ambient, `entity` loop, `heartbeat`) exist from the
  prototype. **Blender → Unity import is far smoother than → RealityKit** (the .usdz material/Z-up pain that
  triggered this pivot won't recur). Re-import art natively into Unity.
