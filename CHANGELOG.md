# PetAI — Changelog

> Track of all changes shipped on the `feature/stay-guard-restricted-roam` branch
> on top of upstream `main` (v5.1.1). Current head: **v5.1.63**.

This fork extends the original PetAI library with a richer pet command set,
an aim-and-throw fetch mechanic, smarter AI pathing, an in-world pet tracker,
and a redesigned pet profile / command dialog UI.

65 commits, 25 files touched, +2002 / −86 lines.

---

## Highlights

- **New commands: `roam` and `guard`** — pets now hold a position or patrol a radius around it, with explicit walk animation and downed/ wound-healing awareness.
- **Aim-and-throw fetch with the Chewing Bone** — charge-throw arc, custom crosshair, dog carries the bone back in its mouth, drops it at the player's feet.
- **Smart pet pathing** — tamed pets avoid walls, pits, fire and lava when navigating.
- **Pet map tracker** — a brand-new waypoint-based tracker that lists tamed pets under a dedicated *Owned creatures* subcategory and updates four times per second.
- **Combat etiquette** — enemies ignore mortally-wounded tamed pets, and wild wolves randomly (50/50) skip tamed dogs/wolves.
- **Pet profile overhaul** — animal type, gender, obedience, domestication and generation are now surfaced through a redesigned dialog that matches the command dialog style.

---

## AI Tasks & Pet Behavior

### `AiTaskRestrictedRoam` — merged stay/guard
- `AiTaskStay` and `AiTaskGuard` merged into a single `AiTaskRestrictedRoam` task with a unified configuration model.
- New `roam` and `guard` commands wired up, including a downed-state check (pets stop following orders while their owner is downed) and a wound-healing pause.
- Walk animation and forward-control plumbing fixed (`"Walk"` capitalization regression) and a `NullReferenceException` in the guard task resolved.

### `AiTaskPetFlee` — passive flee
- New `petflee` AI task added to both `dog-adult.json` and `dog-pup.json` via `assets/petai/patches/entities/dog-flee.json`.
- Flees for a configurable duration and range, then returns to its prior task — used to make dogs non-confrontational in `PASSIVE` mode.

### Smart pathing
- `AiTaskPetSeekEntity` (new, 134 lines) introduces navigation that steers tamed pets around walls, pits, fire and lava, rather than pathing straight into them.

### Combat targeting
- `AiTaskBaseTargetable.IsTargetableEntity` patch — the method was declared `virtual` and is now prefixed, letting pets be filtered out of hostile AI targeting.
- `AiTaskBase.ContinueExecute` postfix clears an active AI target after a fight so pets don't re-engage corpses.
- Enemies now ignore tamed pets that are mortally wounded.
- Wild wolves randomly skip tamed dogs/wolves (50% chance), reducing friendly-fire incidents.

---

## Fetch Mechanic — Chewing Bone

The big new gameplay loop. The Chewing Bone is now a chargeable throwable
that nearby dogs can fetch and return.

### Throw arc & UI
- Aim animation + custom crosshair (`assets/petai/textures/gui/chewingbone-crosshair.png`) while charging the throw.
- Tamed arc with a rock-like trajectory so the bone actually lands where the player aims.
- Right-click always throws (no accidental charge cancellation).
- Crosshair simplified to a plus sign for readability against varied terrain.

### Dog fetch & return
- A thrown bone broadcasts a "fetch" notification to nearby tamed dogs.
- The fetching dog picks the bone up when it reaches it, even when wolftaming's own task gave up (race-condition fix).
- The dog carries the bone back in its mouth and drops it at the player's feet.
- Fetch drop distance was iterated through several values (1 → 2 → randomized → locked at 2 blocks) to land on a comfortable feel.
- Pickup-radius bumped from 2.0 → 3.0 (small hitbox tolerance) and held at 3.0 / 100 ms for stability.
- PetAI's bone pickup now wins the race against `wolftaming.GetToy()` so the fetch doesn't get stolen by the upstream behavior.

### Bone item behavior
- Throw damages only the bone that was actually thrown — used bones never stack.
- Durability is ticked via a Harmony prefix on `ItemDogToy` (after iterating through both a `CollectibleBehavior` and an `OnHeldInteractStart`/`OnHeldInteractStop` approach — the prefix is the design that stuck).
- Diagnostic logging added and later removed once the throw-damage path was verified.

### Hunting dog parity
- New `HuntingDogFetchSpeedPatch` slows hunting-dog fetch speed to match tamed wolves, with the patch deferred to runtime so the wolftaming assembly is guaranteed to be loaded first.

---

## Pet Map Tracker (new system)

A complete in-world waypoint tracker for tamed pets.

- `PetMapMarkerTracker` (new, 590 lines) maintains a live list of tamed pets, groups their waypoints, and syncs them with the world map.
- `MapMarkerConfig` and `MapMarkerModSystem` provide the registration and configuration plumbing.
- All tamed-pet waypoints are bucketed under a dedicated *Owned creatures* subcategory on the world map.
- Tracker tick interval reduced from 2 s → 0.5 s for near-realtime updates.
- `WaypointUtil` helper added with shared geo/position math used by the tracker.

---

## GUI — Pet Profile & Command Dialog

- Command dialog UI elements shrunk and visually tightened (`TaskSelection.cs`).
- Pet profile dialog restyled to match the command dialog style; "Health" swapped for "Generation".
- Pet profile now shows **animal type** and **gender** alongside name and obedience.
- Friendly species names are read from the entity (`wolf`, `Hunter dog`) via `entity.GetName()` — no more hardcoded wolf/dog branching.
- A new `lang`-based type library enumerates all wolftaming animal variants, so the profile reads the proper localized type.
- Display names for animal types renamed and the obsolete *Pekingese* type entries removed.
- Hover overlay re-enabled to show obedience/domestication progress (was previously hidden).

---

## Localization

- `en.json` expanded with 59 new/changed lines covering new commands, tooltips, the `petflee`/`restrictedroam` task names, and per-variant animal type labels.
- Animal type library is now lang-driven so other languages can be added without code changes.

---

## Tooling & Project Hygiene

- `AGENTS.md` added with build/deploy notes, key file map, feature-branch overview, and known-issue investigations.
- `Directory.Build.props` and `.opencode`/VS Code workspace configured for `net10.0`, Release builds, and the `%appdata%\VintagestoryData\Mods\` deploy path (which differs from the older `Vintagestory\Mods\` location).
- `build.sh` and `release.sh` helpers present.
- `release.sh`-style release notes pipeline ready for tagged releases.

---

## Versioning

- Started this fork on upstream `v5.1.1` and bumped incrementally to **v5.1.63** as features landed.
- Mod folder name in the Mods directory follows `petai-v5.1.X` (remember: `VintagestoryData\Mods\`, not `Vintagestory\Mods\`).

---

## Notable Reverts (to keep the history honest)

These are commits that were tried and rolled back before the current behavior settled — kept in history for reproducibility and regression bisects:

- `revert: back to 5.1.23 (be3881b) — throw in front, dog doesn't fetch, durability ticks` (fetch was reintroduced later)
- `revert: back to 5.1.17 (CollectibleBehavior approach)` (Harmony prefix won)
- `revert: drop 5.1.36 pickup-radius + 'task gave up' helper` (later re-tuned to 3.0 / 100 ms)
- `revert: use entity.GetName() for species, drop wolf/dog hardcoding` (later re-applied once the lang library landed)
- `chore: bump to 5.1.42 after reverting retry-pickup` (the retry-pickup helper was ultimately dropped for a simpler radius-based approach)

---

## Files Added

- `PetAI/src/Entity/AITask/AiTaskPetFlee.cs`
- `PetAI/src/Entity/AITask/AiTaskPetSeekEntity.cs`
- `PetAI/src/Item/BehaviorChewingBoneAimThrow.cs`
- `PetAI/src/Item/ChewingBoneCrosshair.cs`
- `PetAI/src/Patches/ChewingBoneStackingPatch.cs`
- `PetAI/src/Patches/HuntingDogFetchSpeedPatch.cs`
- `PetAI/src/Systems/MapMarkerConfig.cs`
- `PetAI/src/Systems/MapMarkerModSystem.cs`
- `PetAI/src/Systems/PetMapMarkerTracker.cs`
- `PetAI/src/Systems/WaypointUtil.cs`
- `PetAI/assets/petai/patches/entities/dog-flee.json`
- `PetAI/assets/petai/patches/entities/dog-guard.json`
- `PetAI/assets/petai/textures/gui/chewingbone-crosshair.png`
- `AGENTS.md`

## Files Renamed

- `PetAI/src/Entity/AITask/AiTaskStay.cs` → `AiTaskRestrictedRoam.cs`

## Files Significantly Rewritten

- `PetAI/src/Entity/Behavior/BehaviorPetInventory.cs`
- `PetAI/src/Entity/Behavior/BehaviorReceiveCommand.cs`
- `PetAI/src/Entity/Behavior/BehaviorTameable.cs`
- `PetAI/src/Gui/PetProfile.cs`
- `PetAI/src/Gui/TaskSelection.cs`
- `PetAI/src/Systems/Patches.cs` (141 → 244 lines)
- `PetAI/src/Systems/PetAI.cs`
- `PetAI/assets/petai/lang/en.json`
