# Codex Implementation Plan — Shared-Grip Lightsaber Combat v3

**Repository:** `https://github.com/KeshireTheChaotic/2D-Sword-IK-Prototype`  
**Local project:** `D:\Library\Development\2D-Sword-IK-Prototype` (confirm actual checkout)  
**Verified baseline:** `master` at `07472f3e71f68867baa4acca6c9d4b8660a236fd` (*Repair IK Chains*, observed 2026-10-20)  
**Unity:** `6000.6.6f1` with 2D Animation `16.0.1` and PSD Importer `15.0.1`  
**Deliverable branch:** `feature/shared-grip-combat-v3` (or an equivalent non-conflicting branch)  
**Status:** Implementation plan; features below are NOT assumed to be merged or Play Mode verified.

## Mission and source-of-truth rules

Implement a playable, deterministic-feeling top-down 2.5D lightsaber prototype where two IK hands converge on one shared hilt grip; body turning is continuously driven by mouse angle but highly damped in front; LMB launches windup/forward-spring/opposite-side attacks; RMB presents a physical guard; alternating committed strikes become progressively easier to perfect-parry; guard breaks produce a real physics response; blade and hilt remain independent objects so the blade retracts and the hilt can be dropped.

Inspect the actual working tree, the exact tracked scene, current Unity package APIs, and any uncommitted changes **before editing**. `master` baseline may have advanced. Preserve user changes; never reset, clean, or overwrite them. Existing repository code is authoritative for what is implemented. This plan is authoritative for requested behavior; `docs/STANCE_COMBAT_DESIGN.md` is historical and conflicts with the new *one shared grip* rule. Previous chat-generated ZIPs are **reference implementations**, not proof of correctness. If a v3 ZIP is available locally, review it and selectively reuse safe, compiling code; do not blindly overlay it or replace newer `SwordIKChainRepair.cs`.

**Important current state:** The repository has `Assets/Editor/SwordIKChainRepair.cs`, `SwordIKUpgradeValidation.cs`, `Assets/Scenes/SampleScene.unity`, `Assets/Scripts/PlayerMovementController.cs`, `Assets/Scripts/HandManager.cs`, custom `RotationLimitHinge.cs`, `Assets/Sprites/Sprite.psb` with LFS. The baseline `PlayerMovementController` rotates toward the mouse per rendered frame, and `HandManager` independently selects the nearest hotspot. It does **not** yet have the playable combat logic. The scene already contains two repaired `LimbSolver2D` arm chains and shoulder hinges. Do not regenerate imported bones, replace PSB sprite GUIDs, or discard existing prefab-instance overrides.

## Working method and integration controls

1. Confirm checkout and record `git status --short`, HEAD, `git lfs status`, Unity editor version, current scene and script references. If dirty, preserve files and work on a suitable branch; never delete user changes. Do not commit cache/log/editor-user files.
2. Create/checkout the feature branch and write a short implementation ledger (`docs/COMBAT_V3_LEDGER.md`) with workstream statuses `NOT STARTED / IN PROGRESS / VERIFIED / BLOCKED`; update it at each gate.
3. Implement small, independently reviewable commits; isolate pure combat logic from MonoBehaviour glue for fast EditMode tests. Keep Unity asset `.meta` GUIDs stable.
4. A main integrator must own scene/prefab edits and class contracts. Parallel agents/worktrees are optional only for **non-overlapping** scopes (e.g. pure combat rules, weapon view, physics/tests); define interfaces first. Merge completed worktrees serially and remove only the worktrees Codex created. Do not parallel-edit `SampleScene.unity` or `PlayerMovementController.cs`.
5. Use **targeted tests** for each workstream, not blanket test-suite rewrites. Repair or replace any test that asserts the obsolete nearest-hotspot or instant-turning behavior. Add Editor, EditMode and PlayMode tests where applicable. Compile in Unity after each dependent slice.
6. Do not claim success from static scripts alone; run Unity import, compile, scene validator, and at least one actual two-combatant Play Mode interaction. If Unity cannot run (licensing/Editor unavailable), mark explicit `BLOCKED - Unity validation`, deliver code and exact commands, and do not assert full completion.
7. Commit and push the feature branch if local repository credentials allow. Do not commit directly to `master`; do not create a PR/merge unless requested or project workflow clearly authorizes it. A GitHub app previously returned HTTP 403 to branch creation; using local git/CLI may work but must be verified, not assumed.

## P0 — Preserve and validate the migrated IK rig

**Source:** `SwordIKChainRepair.cs`, `SwordIKUpgradeValidation.cs`, `SampleScene.unity`, PSB prefab/meta.

- Verify 2 `LimbSolver2D` and 1 `IKManager2D` with valid chains, bindings and `SpriteSkin` import. Known source bone paths: left `bone_1/bone_3/bone_4/bone_5`, right `bone_1/bone_7/bone_8/bone_9`. Shoulder hinge limits originally left `[-110°, +50°]` and right `[-50°, +110°]`; respect existing valid settings.
- Run `Tools > Sword IK > Validate Upgrade` if Unity is present. Fix actual errors before combat wiring. Maintain a non-destructive, repeatable repair command and avoid repatching a healthy rig.
- Inventory all existing components on Player, IK targets, camera, and colliders. Ensure the new installer is idempotent.

**Gate P0:** Existing scene compiles and validates with zero missing scripts and two functional rig chains; modified scripts do not corrupt the scene.

## P1 — Shared grip + angle-dependent body turning

**Candidate files:** `PlayerMovementController.cs`, `Assets/Scripts/Combat/SharedGripFollower.cs`, pure `AngleTurnResponse.cs`, `CombatStanceController.cs` (basic stance state).

- Create exactly **one actual `SharedGrip` Transform** following the held hilt. Both IK target transforms pursue exactly the same world-space target with the same reach clamp; distinct solver targets may be retained to avoid overwriting scene references, but their desired positions are identical. Existing `HandManager` hotspot movement must be disabled or retired when combat mode is installed. Respect achievable bone lengths and anatomical hinges; clamp if unreachable instead of snapping/stretching. IK yields only during physics reaction.
- Derive player-local signed mouse angle using current `Rigidbody2D` body rotation. Let `a=abs(deltaAngle)` in `[0°,180°]`. Smoothly and monotonically interpolate speed multipliers across calibration points: `0°=0.08x`, `30°≈0.2x`, `60°≈0.7x`, `90°=1.0x`, `135°≈1.5x`, `180°=2.0x`. Use deterministic interpolation preserving monotonicity rather than linearizing all intermediate points.
- Apply acceleration/deceleration limits and `Time.fixedDeltaTime`; use `Rigidbody2D.MoveRotation` in `FixedUpdate`. Maintain last selected turn sign at `180°` to prevent flip-flopping. Physics motion uses `linearVelocity`; movement diagonals normalized. No rigidbody-vs-transform double driving, no body rotation based on cursor delta. During attack/guard, phase multipliers further restrict turning; body turning never adds direct hit impulse.
- Stance selection follows cursor sector **relative to torso**, with configurable sector boundaries, hysteresis and dwell; do not automatically force stance to forward as the torso turns. The body is allowed to keep turning gradually.

**Gate P1:** both hand target world positions converge within `0.01` units while controlled; mouse dead zone is stable; target speed increases from front to side to rear, capped acceleration independent of render FPS; rapid cursor sweeps do not snap character facing.

## P2 — Independently modeled hilt and pseudo-3D blade

**Candidate files:** `WeaponPoseMotor.cs`, `BladeVisualController.cs`, `WeaponEquipmentController.cs`, `SwordIKCombatInstaller.cs`.

Create the scene hierarchy using an Editor setup command (no fragile manual YAML patch):

```text
Player [Rigidbody2D, PlayerMovementController, CombatStanceController,
        IKManager2D, SharedGripFollower, ArmPhysicsBlend]
  RiggedSprite [preserve imported SpriteSkin bones/hinges]
  Left_IK / Right_IK [preserve solver components]
  Left_IK_Target / Right_IK_Target [both pursue SharedGrip]
  UnarmedGripRest (optional)
Lightsaber_Hilt [separate GameObject + Rigidbody2D + motor]
  HiltVisual
  SharedGrip
  Blade [separate child GameObject]
    BladeVisualPivot
      BladeCore
      BladeGlow
    BladeContact [Collider2D + BladeContactSensor]
```

- Idle blade points nearly upright (starting elevation `78°`); guard sideways (`18°`); windup tilts backward (`40°` elevation and ~`14°` lateral tilt); swing projects down (`14°` elevation). Separate simulation yaw, elevation/pitch, roll and a visual projection of 3D blade length onto 2D ground (`L*cos(elevation)`). Hilt physically moves with capped spring-damper force/torque, and virtual height gates collision; cursor delta does not directly set angular velocity.
- `F`: activate/retract blade through a **scale animation** along its local length (roughly `0.20s`) while its base stays at emitter. Keep blade geometry separate from hilt. Collider enabled only when deployed enough and combat-valid, independent of visuals.
- `G`: drop hilt; disable motor and held IK link, preserve dynamic hilt physics and current ignition state as a configurable option. `R`: prototype retrieve/pickup after range/ownership checks, do not teleport a remote hilt from unlimited distance. Avoid hilt pushing its owner: configure `Physics2D.IgnoreCollision` or layers appropriately, while still colliding with opponents and world as needed. Disable any hand-grip `FixedJoint2D` in ordinary held IK mode to avoid physics-vs-IK control conflict.
- Installer must run twice without duplicates and save stable scene references plus `.meta` assets. Use placeholder art if necessary; do not block playability on new textures/shader/bloom.

**Gate P2:** hilt persists when blade retracts; blade animation really scales out/in, physical contact disabled when inactive; dropped hilt falls/moves independently; both hands still seek same grip in held mode.

## P3 — Deterministic stance combat and alternating committed swings

**Candidate files:** `CombatStanceController.cs`, a pure `AttackTimeline.cs`, `WeaponPoseMotor.cs`, `CombatInputRouter.cs` if needed.

- LMB press begins `Windup`, LMB release (after minimum windup) transitions into `ForwardSpring`, then `Swing`, `FollowThrough` (can be included in swing), `Recovery`, `Ready`. Input must be sampled in `Update`, resolved consistently in `FixedUpdate` for physics. Do not allow click spam or mouse flicks to bypass recovery.
- Capture initial swing side from **actual hilt/shared grip** in player-local coordinates at windup/commitment; use last committed lateral side if near center (`deadzone` defined). Swing **away from that side**. A left-start swing finishes on right, right-start finishes on left. Keep final grip on the finishing side so the next swing naturally alternates.
- Add a small **forward grip spring** (~`0.15` Unity world units) *before* the outward arc; keep torso lunge optional and separate. Windup physically retracts and tilts the saber. On release lock arc direction, end points, and a bounded attack timeline. Pose motor enforces finite force, velocity and acceleration; a target trajectory can curve but cannot instantly reverse after commit.
- RMB guards with finite side-switch transition (~`250ms`) and physical blade presentation; ordinary held guard blocks but does not continually perfect-parry. LMB + RMB explicit input priority (defense first, optional queued attack after guard release). No exploits via cancel/spam.
- Do not use `HandManager`'s nearest-hotspot choice to determine strike side once equipped. Data-driven exposed tuning fields for timings and stance offsets, no hard-coded frame counts.

**Gate P3:** two successive uninterrupted attacks visually alternate direction; forward spring is visible before each committed swing; mouse flicks cannot invert an active swing or alter impact energy; transitions honor cooldowns.

## P4 — Combo predictability, blade contacts, and perfect parries

**Candidate files:** `ComboTracker.cs` (pure), `ParryResolver.cs` (pure), `BladeContactSensor.cs`, `CombatStanceController.cs`.

- Track *per-attacker* consecutive committed alternating swings, irrespective of hit/miss, increment only when next windup starts within `0.65s` after **previous recovery ends**. If not alternating or expired, restart at 1. Do not increment every frame or on repeated trigger callbacks. Once an attack is committed, count it even if it misses; interruption by a successful parry or guard break resets the chain. Reset on drop/deactivation/stagger, or an actual **sustained** guard reset (`>=0.38s` after guard transition), not brief RMB taps.
- Parry windows: `windowSeconds = min(0.120 + 0.035*(max(1,comboCount)-1),0.280)`. Expected at swing counts 1–6+: `120, 155, 190, 225, 260, 280` ms. Note the jump from swing 5→6 is capped (not 295).
- Perfect parry only if (a) incoming attack currently active and committed, (b) defender completed an eligible guard placement/reorientation within attacker's window before contact, (c) colliders actually intersect or a swept contact verifies crossing, (d) virtual blade elevations overlap, (e) defender not stunned/disarmed/broken, (f) `>=0.42s` since prior parry rearm. An already-held stationary guard **blocks** rather than infinite-parries. Time allowance changes **timing only**, never physical collision range or guard angle.
- Active blade needed on both sides for blade-vs-blade; inactive blade cannot block, parry or strike. Weapon hit should be processed once per defender per committed swing despite both `OnTriggerEnter2D` and `OnTriggerStay2D`; clear contact dedup on new swing. Ensure one consistent server/owner of each contact resolution to avoid symmetric double parries. Use actual physics contact-point relative velocity (with sane cap) rather than mouse movement. Enable continuous/swept checks if tunneling observed at fast swing speeds; add tests for grazing/high-elevation misses.
- On successful perfect parry, redirect the attacker's hilt, interrupt swing, trigger counter window and reset attacker's combo. An ordinary block consumes guard stability/recoils; only break if actual impact exceeds stability. Damage and hit-stop optional but must not bypass collision validation.

**Gate P4:** pure EditMode tests verify progression and cap, misses increment, consecutive opposite-side rule, reset/timeout, parry rearm; PlayMode with two independently controlled combatants proves actual blade contact requirement and no parry from static guard or inactive blades.

## P5 — Block recoil, articulated guard break, recovery, and safety

**Candidate files:** `ArmPhysicsBlend.cs`, `WeaponPoseMotor.cs`, `BladeContactSensor.cs`, Editor installer/validator.

- An ordinary block applies **spring-damped hilt recoil** without turning off IK. A guard break: capture hit point, normal, actual relative velocity, cap impulse, disable authoritative IK and `RotationLimitHinge` before activating arm physics, then apply force/torque into appropriate arm chains and hilt.
- Use a prebuilt, correctly configured set of `Rigidbody2D`/`HingeJoint2D` components for shoulders, elbows, wrists (or a separate physics proxy coupled to the skinned rig if dynamic Rigidbody2D on imported bones proves unstable). Never allow IK solving, manual transforms, and active physics to drive the same bone at the same time. Review arm mass, anchors, angular limits, 2D collision exclusion and self-collision.
- Physically release the arms outward under the **incoming collision impulse**, not a canned outward animation or mouse delta. Both hands may separate while ragdolled; during recovery they reacquire the single shared grip. If hilt actually drops, keep that state independent of arm reacquisition.
- When impact settles, re-establish stable recovery targets at current hand positions and smoothly blend IK weight from 0→1 (initial ~`0.55s`). No snapping, explosive joints or solver invalidation. Physics reaction and recovery need explicit state transitions and owner-collision exclusions.

**Gate P5:** weak contact recoils without ragdoll; forced guard break throws arms according to contact direction; physics is stable for repeated breaks; both hands return toward one shared grip without snapping. Validate at multiple render frame rates and fixed timesteps.

## P6 — Editor integration, two-combatant test scene, and final validation

**Candidate files:** `SwordIKCombatInstaller.cs`, `SwordIKCombatValidation.cs`, `Assets/Scenes/DuelTestScene.unity` or an equivalent scripted PlayMode harness, `Assets/Tests/EditMode` and `Assets/Tests/PlayMode`, `README.md`, `docs/COMBAT_V3_LEDGER.md`.

- Maintain `Tools > Sword IK > Repair Arm Chains and Hinges`, `Tools > Sword IK > Validate Upgrade`. Add `Install Stance Combat + Lightsaber` and `Validate Stance Combat` without duplicating objects/components after repeated runs. The installer must not unexpectedly close or overwrite an unrelated dirty scene.
- Build a playable **two-combatant** duel test harness (a second rigged combatant and hilt, isolated inputs/test-driver). If Unity's Input Manager cannot drive two local players from one keyboard/mouse, use explicit injected input/AI commands for the second combatant. Use physics and the actual blade colliders, not mocked collision callbacks, for interaction tests.
- Instrument debug gizmos/logged telemetry for signed body angle, rotational velocity, requested vs actual hilt pose, grip divergence, stance/attack phase, combo count, effective parry window, parry timing reason, impact strength, IK/physics ownership and ragdoll recovery. Keep debug features editor-only/toggleable.
- Test fixture coverage: 0/30/60/90/135/180-degree rotation curve; 180-degree sign stability; fixed timestep independence; both target positions equal; unreachable grip clamp; opposite-side attack direction from physical grip; attack/cancel spam; 120/155/190/225/260/280 ms progression and cap; max 650-ms continuation; missed strikes; guard reset 380 ms; parry rearm 420 ms; stationary guard is ordinary block; physical contact/elevation; disabled blade; drop/pickup; weak/strong impact; repeated ragdoll/recovery; setup tool idempotency; two-character scene.
- Review `.gitignore`: existing tracked `Logs/` and `UserSettings/` should be excluded from feature commits and ideally cleaned only by explicit housekeeping; avoid unrelated churn. Ensure LFS pointer is fetched and files are not corrupted. Do not commit `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/` or machine-local editor paths.
- Run Unity targeted EditMode/PlayMode tests and Editor validators. Run a simple build if build settings support it; otherwise report why not. Note precisely which tests passed, failed or could not run, and provide repro steps.

**Final gate:** Unity compiles without new errors; shared-grip player scene passes validation; duel scene demonstrates block/parry/guard-break with real weapon contact; timed tests pass; feature branch is committed and pushed if permitted; docs reflect implementation not historical design.

## High-risk technical decisions requiring explicit verification

1. **IK/physics authority:** the existing `RotationLimitHinge` updates transforms in LateUpdate, and Unity's IK also changes transforms. Explicitly disable these scripts when physical arm bodies are simulated.
2. **Prefab import identity:** preserve PSB bone hierarchy, sprite skin, imported prefab GUIDs and existing scene overrides. Prefer Editor API wiring to string YAML replacement.
3. **Contact model:** 2.5D render projection cannot be taken as actual 3D height physics. Define virtual collision-height interval and test both 2D intersection and height overlap.
4. **Parry contact asymmetry:** trigger callback order and duplicate callbacks should not determine winner. Use a single deterministic resolver for each contact pair and swing ID.
5. **Input/state determinism:** sample inputs in Update, advance time-based combat intents deterministically, apply rigidbody motion in FixedUpdate; explicitly order transitions to avoid releasing LMB on the same frame as starting windup.
6. **Transient vs persistent tuning:** settings should be serialized into reusable components or ScriptableObject data; document all defaults, especially the rotation curve and combo formula.
7. **Legacy design conflict:** replace obsolete portions of `docs/STANCE_COMBAT_DESIGN.md` that require separate grips or instantaneous body facing. Preserve useful block/guard concepts with clear labels.
8. **Testing availability:** if Unity lacks a license or editor, stop short of claiming runtime completion, list the missing tests and exact local Unity commands.

## Checkpoint / commit structure

- `combat: verify Unity 6 IK rig and establish contracts`
- `combat: add shared hilt grip and angle-aware body turning`
- `combat: add independent hilt, retractable blade and drop/retrieve`
- `combat: implement stance windup, forward spring and alternate strikes`
- `combat: implement collision-based guard and predictive parry windows`
- `combat: add physical guard breaks and IK reacquisition`
- `test: add two-combatant scene, validators and targeted tests`
- `docs: reconcile design spec and publish verified status`

Do not force these commit boundaries if a smaller change is required to keep every commit compiling, but record the above gates in the ledger.

## Definition of done

- [ ] P0 healthy original rig preserved
- [ ] P1 overlapping IK targets and angle-dependent rotation verified
- [ ] P2 hilt independent, blade ignition/retraction and drop behavior verified
- [ ] P3 alternate-direction strike and forward spring verified
- [ ] P4 capped increasing parry timing and real collision verified
- [ ] P5 block recoil, physics guard break and smooth recovery verified
- [ ] P6 two-combatant scenario, editor smoke validation and targeted tests verified
- [ ] Historical docs reconciled, ledger compact, README updated
- [ ] Commits on feature branch; push/PR status accurately reported

## Copy/paste kickoff prompt for Codex

> Implement `docs/CODEX_IMPLEMENTATION_PLAN_COMBAT_V3.md` in the current repository from start to finish. First verify the actual git HEAD/working tree and the repaired IK baseline; do not overwrite local changes. Create `feature/shared-grip-combat-v3` and a compact ledger. Follow P0–P6 in dependency order, with focused commits and Unity validation at each checkpoint. Existing `SwordIKChainRepair.cs` and `SampleScene` rig must be preserved. The prior combat ZIP, if available, is reference code only—review, do not blindly overwrite. Implement: overlapping shared IK grip, angle-dependent smooth body turning, separate hilt/blade with scale-based ignition and drop, windup/forward-spring/opposite-side alternating strikes, physical guards and contact-based parries with 120→280 ms growing window, physical guard-break ragdoll with IK recovery, and two-combatant duel tests. Use parallel subagents/worktrees only for truly independent areas and merge at verified boundaries. Run targeted tests, Editor validation, and a real physics PlayMode test wherever Unity is available. If tooling or Unity licensing blocks a step, mark it BLOCKED with evidence, continue safe work, and never claim a test passed without running it. Finish by updating docs, listing actual test results and residual risks, committing on the feature branch, and pushing if permitted. Do not merge into master without explicit approval.
