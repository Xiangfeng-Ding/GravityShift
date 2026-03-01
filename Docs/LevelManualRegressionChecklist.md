# GravityShift Level Manual Regression Checklist
Scope: `Assets/Scripts` current implementation (including recent trigger and respawn stability fixes).
Goal: Verify "completable path + soft-lock prevention + trigger reliability" level by level, covering the full manual regression run.

## 0. Pre-Run Setup
- [x] Use Unity Play Mode; confirm the scene is auto-started by `GameDirector`.
- [x] Open Console with `Clear On Play` disabled to retain error logs.
- [x] Record HUD at the start of each run: `Need X`, `Flip CD`, `Checkpoints`.
- [x] Confirm basic controls: `WASD`, `Space`, `F`, `R`, `Esc`, `N`, `O`.
- [x] If a freeze or dead-end occurs mid-run, press `R` to restart and reproduce once before filing as a defect.

## 1. Full Execution Matrix (24 Runs)
Note: Each entry requires at least one full clear + one mid-run death and respawn.
- [x] L1 Easy Adventure
- [x] L1 Normal Adventure
- [x] L1 Hard Adventure
- [x] L1 Easy Challenge
- [x] L1 Normal Challenge
- [x] L1 Hard Challenge
- [x] L2 Easy Adventure
- [x] L2 Normal Adventure
- [x] L2 Hard Adventure
- [x] L2 Easy Challenge
- [x] L2 Normal Challenge
- [x] L2 Hard Challenge
- [x] L3 Easy Adventure
- [x] L3 Normal Adventure
- [x] L3 Hard Adventure
- [x] L3 Easy Challenge
- [x] L3 Normal Challenge
- [x] L3 Hard Challenge
- [x] L4 Easy Adventure
- [x] L4 Normal Adventure
- [x] L4 Hard Adventure
- [x] L4 Easy Challenge
- [x] L4 Normal Challenge
- [x] L4 Hard Challenge

## 2. Universal Pass Criteria (Required for Every Run)
- [x] No `Exception`, `NullReference`, `MissingReference`, or assertion errors in Console.
- [x] No `LevelBuilder ... missing wiring` warnings (Pressure / Laser / Chain / Rhythm).
- [x] `F` flip is usable outside Anchor zones, disabled inside, and restored upon exit.
- [x] At least one death and respawn occurs; player can continue progressing after respawn without losing key interactions.
- [x] No key gate (Local Gate / Pressure Gate / Rhythm Gate / Laser Gate) shows "open prompt but still blocking".
- [x] Crystal collection counter increments correctly -- no duplicate counting or missed counts.
- [x] After meeting the global crystal requirement, the Energy Gate opens and the Exit can be completed.

## 3. L1 Step-by-Step Script (Five-Zone Tutorial Flow)

### 3.1 Main Path
- [x] Zone 1: Complete basic movement, jump, flip to ceiling practice pad and return to ground.
- [x] Zone 2: Collect local crystals and open `Zone2 Gate` (requirement: 2, placed: 3).
- [x] Zone 3: While inverted, player can only stand on `StickySurface`; stepping on non-sticky surfaces causes a slide-off.
- [x] Zone 4: Pressure gate can be triggered by a crate or by the player standing on the pressure plate (soft-lock fallback).
- [x] Zone 5: Open local gate first, then trigger pressure gate, pass through high/low barriers and return to main path.
- [x] After collecting enough global crystals, the final Energy Gate opens and the run completes.

### 3.2 Soft-Lock Regression
- [x] After pushing the crate out of the usable area, the player can still progress by standing on the pressure plate.
- [x] Dying and respawning in the Sticky zone does not leave residual rules; the puzzle can be re-solved normally.
- [x] Dying and respawning at the edge of a Gravity Anchor zone does not cause a permanent flip lock.

## 4. L2 Step-by-Step Script (Advanced Combined Flow)

### 4.1 Main Path
- [x] Clear the basic Laser Cage and floating platform section.
- [x] Complete the chain mechanism `A -> B` (B starts locked; A unlocks B).
- [x] Clear the bounce pad sky section: `MegaBounce_A -> SkyRoute -> MegaBounce_B -> landing loop`.
- [x] Complete the rhythm section: trigger A, pass Gate A, then trigger B, pass Gate B.
- [x] Clear the Rotor Corridor (rotating obstacle corridor) and return to the main route.
- [x] Reach the crystal threshold, open the final gate, and complete the level.

### 4.2 Soft-Lock Regression
- [x] Waiting inside a rhythm trigger zone (without repeatedly entering/exiting) does not cause switch misfires.
- [x] Entering the B switch trigger zone while it is locked, then having A unlock it, allows B to fire normally without extra workarounds.
- [x] High-speed passes over bounce pads do not cause missed bounces that break the required route.

## 5. L3 Step-by-Step Script (High-Pressure Challenge Flow)

### 5.1 Main Path
- [x] All L2 skill checkpoints completed.
- [x] Rhythm section features tighter timing windows and additional moving platforms; still fully completable.
- [x] Rotor Corridor is denser (includes extra hazard arms); route remains readable and consistently passable.
- [x] Player can reach the exit within the time limit after meeting the crystal requirement.

### 5.2 Soft-Lock Regression
- [x] At least 2 mid-run deaths do not cause state corruption; level remains completable.
- [x] After respawn, rhythm chains reset correctly -- no "gate state mismatched with HUD" occurrence.

## 6. L4 Step-by-Step Script (Boss Combined Mechanics)

### 6.1 Main Path
- [x] Complete the pre-boss challenge section and enter the Boss zone.
- [x] Boss chain requires A before B; B must remain locked until A is completed.
- [x] After A succeeds, B becomes triggerable and Gate A/B open accordingly.
- [x] Flip is disabled inside the Boss Anchor zone and restored upon exit.
- [x] After completing the Boss section, proceed through the final gate and complete the run.

### 6.2 Soft-Lock Regression
- [x] Dying and respawning inside the Boss zone allows the chain to be re-executed; no permanent lock-out.
- [x] Repeatedly entering and exiting the Anchor zone edge does not cause "flip still locked after leaving".

## 7. Targeted Stability Regression (At Least Once per Major Version)
- [x] Switch trigger reliability: when `OnTriggerEnter` is missed, `OnTriggerStay` successfully catches it as a fallback.
- [x] No duplicate triggers: a single entry into a trigger zone must not activate a switch or bounce pad more than once.
- [x] A locked switch entered while locked can be triggered once normally after being unlocked, without requiring the player to exit and re-enter.
- [x] After a chain reset (e.g., death and respawn), the same switch can be triggered again normally.
- [x] Pressure plate occupant cleanup: if a crate or player is abnormally removed, the pressure plate state recovers automatically.
- [x] KillZone / Checkpoint / Crystal triggers are not missed during high-speed movement.
- [x] When multiple KillZones overlap, a single fall counts as one death and triggers one respawn only.
- [x] Collapse platforms always enter the countdown when stepped on -- no "stand on it and nothing happens" case.
- [x] Gate initialization timing: gate state after level load must match the pressure plate state; gates must not open incorrectly due to component creation order.
- [x] Gravity flip posture stability: after flipping, the character must not remain "sideways/prone" for an extended time and should quickly return to upright.
- [x] Moving platform carry cleanup: after death/respawn or leaving a platform, the player must not be "remotely dragged" by the platform.
- [x] When respawning inside an Anchor/Sticky zone, zone rules must take effect immediately with no brief inactive window.

## 8. Per-Run Record Template
- [x] Run info: Level= , Difficulty= , Mode=
- [x] HUD values: Need= , FlipCD= , Checkpoints=
- [x] Outcome: Pass / Fail
- [x] Failure location:
- [x] Reproducibility: Always / Sometimes / Once
- [x] Console error keywords:
- [x] Notes and screenshot numbers:
