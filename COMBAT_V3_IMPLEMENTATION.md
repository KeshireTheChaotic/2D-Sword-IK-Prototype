# 2D Sword IK — Shared-Grip Combat v3 (Unity 6000.6.6f1)

**Repository baseline:** `KeshireTheChaotic/2D-Sword-IK-Prototype`, `master` SHA `07472f3e71f68867baa4acca6c9d4b8660a236fd` (the commit titled **Repair IK Chains**).

**Deployment:** The GitHub integration is presently read-only for this repository (403 on branch creation). This archive contains a local feature-branch patch, **not** a remote commit. It preserves the repository's newer `SwordIKChainRepair.cs` and `SwordIKUpgradeValidation.cs` instead of replacing them with older revisions from the previous combat package.

## Implementation

- `PlayerMovementController`: continuously seeks the mouse heading with physics-timestep turning. Turning is **0.08× straight ahead**, **1× sideways**, **2× behind**, with a smooth interpolated response and acceleration limit. Combat phases apply additional multipliers; mouse flicks cannot teleport torso or blade.
- `HandGripFollower`: both IK targets seek **one** `Lightsaber_Hilt/SharedGrip`; when dropped they seek **one** unarmed rest pose. Both targets share the same reach clamp. Old independent `HandManager` components are disabled by the Unity scene installer.
- `CombatStanceController`: forward/left/right/rear stances, LMB hold-to-windup/release-to-strike, **forward spring** before the outward swing, RMB delayed guard, timed parry, guard break and recovery, drop/pickup and blade ignition.
- `WeaponPoseMotor`: physical dynamic hilt with capped PD position/torque, upright idle projection, sideways guard, tilted windup, spring forward **0.15 world units**, and one committed opposite-side slash. Rest position remains on the finishing side during an active combo.
- `BladeVisualController`: blade is a separate child from the hilt; F toggles ignition, visual core/glow extend and retract, the physical collision proxy is projected into the ground plane.
- `BladeContactSensor`: one hit per defender per swing, relative physical hilt-point velocity determines impact. Perfect parry requires true blade intersection, compatible blade elevation, recent defender guard-ready time, and an incoming committed attack. A static RMB guard can block but cannot perfect-parry indefinitely.
- `ArmPhysicsBlend`: IK yields to physical arm Rigidbody2D/HingeJoint2D chains during guard break, then blends back. Joints use the same hilt grip anchor.
- `SwordIKCombatInstaller`: repairs/reuses the original two-bone-arm IK chains, disables old hotspot controllers, adds player/weapon controllers and generates a simple saber visual. Installer saves the scene.
- `SwordIKCombatValidation`: validates rig references, shared hilt grip, joint anchors, nondecreasing turn response and parry-window progression.

## Combos and parry timing

| Swing in uninterrupted alternating sequence | Perfect-parry timing allowance |
|---|---:|
| 1 | 120 ms |
| 2 | 155 ms |
| 3 | 190 ms |
| 4 | 225 ms |
| 5 | 260 ms |
| 6+ | 280 ms (cap) |

An attack sequence continues when the following windup **begins** no later than 650 ms after the prior recovery completes and originates from the opposite physical lateral side. A miss still counts. Parry, stagger/guard break, deactivation and dropping reset the sequence. A genuine guard held for 380 ms after transition also resets it; guard taps cannot reset it. Guard-placement parry opportunities cannot be rearmed more often than once every 420 ms.

An attacker is parried only after **actual blade-to-blade trigger intersection**, while the defender is guarding and their recent completed guard placement falls in the attacker's current parry window. Perfect parry interrupts the swing and applies controlled hilt recoil. An ordinary guard instead consumes stability and can break into arm ragdoll.

## Install to the current repository

1. Commit or stash changes. Ensure the local HEAD is the baseline SHA above. Run `git lfs pull` before first import if the PSB is an LFS pointer.
2. Extract this ZIP **outside** the repository and run from PowerShell:

   ```powershell
   .\Apply-Combat-V3.ps1 -ProjectPath 'D:\Library\Development\2D-Sword-IK-Prototype'
   ```

   The script creates `feature/shared-grip-combo-parry-v3` and copies the combat source and Unity `.meta` sidecars without clobbering existing GUIDs. It does **not** delete the original sprite, its `.meta`, or the repaired IK classes.
3. Open Unity **6000.6.6f1** and wait for the scripts to compile. Open `Assets/Scenes/SampleScene.unity`, then use **Tools → Sword IK → Install Stance Combat + Lightsaber** (idempotent as intended). It writes a scene hierarchy and generates `Assets/Generated/SaberPixel.png`.
4. Run **Tools → Sword IK → Validate Stance Combat**, then enter Play Mode. Because the default scene has one player, **two-character perfect-parry integration needs a second combatant**; the validator is static, not a substitute for actual collider timing/physics tests.
5. Commit the installer-generated scene and pixel asset after validation; push the feature branch when ready:

   ```powershell
   git add Assets/Scripts Assets/Editor Assets/Scenes/SampleScene.unity Assets/Generated COMBAT_V3_IMPLEMENTATION.md
   git --no-pager diff --cached --check
   git commit -m 'Implement shared-grip stance combat with combo parries'
   git push -u origin feature/shared-grip-combo-parry-v3
   ```

   The supplied PowerShell installer also supports `-RunUnity` (requires Unity closed), `-Commit` and `-Push`, if you want to perform those phases automatically.

## Controls

- **WASD / arrows:** move character
- **Mouse:** aim; body turns gradually with angle-dependent speed; left/right torso-relative stance
- **LMB hold / release:** windup, short forward spring, committed swing away from current grip side
- **RMB hold:** sideways guard; changing side takes time; recent completed guard placement can perfect-parry a colliding active blade
- **F:** ignite/retract blade independently of physical hilt
- **G:** drop hilt (physical object)
- **R:** retrieve dropped hilt (prototype pickup)
- **H during guarding:** debug guard-break impulse

## Important limitations

- **No Unity Editor executable in this environment.** Static checks pass; actual C# compilation, scene deserialization, sprite deformation, object anchors, frame-by-frame weapon response and parry integration still require Unity Play Mode.
- A single SampleScene combatant cannot demonstrate two-saber parries. Duplicate/configure a second combatant and its own hilt for live parry testing, or add a purpose-built test harness.
- The blade uses placeholder tinted sprites (not true volumetric geometry or HDR bloom); virtual elevation is approximate. Blade contact uses physics triggers, not yet swept continuous-time collider casting.
- The prototype ragdoll on PSB bone transforms may need additional `HingeJoint2D` anchor/mass/simulation adjustments depending on sprite geometry and imported rotations. No claim of an empirically tested physical reaction is made here.
- `ParryWindowSeconds` changes only timing, **not guard coverage or collision detection**. Mouse motion does not create attack impulse.
