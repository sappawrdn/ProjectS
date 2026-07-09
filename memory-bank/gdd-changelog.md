# GDD Changelog: Abstract Prototype to PSX Hospital Horror

This document outlines the major conceptual and design shifts from the original RealityKit prototype GDD to the current Unity implementation.

## 1. Visual Theme & Environment
- **Old Concept:** Players woke up in an abstract "structure" that dynamically rearranged itself when unobserved. The environment was largely a procedural, featureless maze meant to force reliance on audio.
- **New Concept:** The game is now firmly set in a **PSX-style Abandoned Hospital**. We have integrated realistic, grungy props (`dnk_dev` Hospital Horror Pack) including hospital beds, wheelchairs, and medical trays. The environment is grounded, liminal, and highly atmospheric.

## 2. Atmosphere & Lighting
- **Old Concept:** Lighting was generic or unspecified, primarily serving as a backdrop for the spatial audio mechanics.
- **New Concept:** Lighting is now a core atmospheric driver. Ambient/bounce lighting has been disabled to create a pitch-black environment, contrasted heavily by a wide-angle flashlight. This creates extreme dark-light contrast, emphasizing claustrophobia and the PSX retro aesthetic.

## 3. Objective Design (Keys)
- **Old Concept:** Keys were conceptual triggers (likely audio-based) that caused the maze to shift and reveal new sections.
- **New Concept:** Keys are now highly visual, physical objects. They are represented as **Red Valves** (`lalve2` asset) that players must actively spot and collect among the hospital clutter.

## 4. The Exit Door
- **Old Concept:** The exit was entirely invisible and had to be navigated toward strictly "by ear" using audio beacons.
- **New Concept:** The exit is now a prominent visual landmark: a heavy, rusted metal door topped with a **glowing red EXIT neon sign**. While audio beacons can still exist, players are strongly guided by the visual glow of the neon in the dark corridors.

## 5. Monster & Scares Integration
- **Adjustment:** While the core mechanics of the monster (Static → Watcher → Hunter) and the proximity QTE remain identical, they are now contextualized within the hospital setting. Scares and encounters will leverage the hospital corridors and props rather than shifting abstract geometry.
- *Note: During the current development phase, monster AI and jumpscares are temporarily disabled (on "hold") to prioritize environment building and lighting polish, but the GDD reflects the intended final experience.*
