# BREACHPOINT

BREACHPOINT is a single-player first-person shooter for Windows PC, built with Unity 6 and the High Definition Render Pipeline (HDRP). The project focuses on responsive movement, satisfying gunplay, and handcrafted combat encounters.

Designed as a compact portfolio game, BREACHPOINT targets a linear 10–20 minute experience built around traversal, enemy encounters, and a final objective. Development prioritizes gameplay feel, clear audiovisual feedback, and maintainable C# systems.

## Gameplay systems

- First-person movement with sprinting, jumping, crouching, and sliding.
- Hitscan shooting with aiming, ammunition, reloading, and camera and weapon recoil.
- Weapon animation, sound, muzzle flashes, tracers, and impact effects.
- Shared health and damage systems.
- Enemy AI with perception, patrols, investigation, pursuit, search, and combat behavior.

## Architecture

Input, movement, weapon logic, enemy behavior, and presentation are separated into focused components. ScriptableObjects provide reusable gameplay configuration, while VContainer supports dependency injection for shared services and system composition.

## Technology

Unity 6 · C# · HDRP · Input System · Cinemachine · AI Navigation · Animation Rigging · VContainer · UniTask · DOTween

## Status

In development. Current work focuses on refining core gameplay and enemy behavior before completing the level and final polish.
