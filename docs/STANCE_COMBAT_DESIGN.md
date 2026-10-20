# Stance-directed procedural sword fighting — technical design

## Prototype's intended interaction

- **Left mouse**: attack. A short click performs a committed strike from the current stance; holding winds up. Releasing commits the attack. Mouse direction/drag biases strike direction. Keep command input distinct from visual solver state.
- **Right mouse**: defend. Hold to present the sword/guard toward the mouse, within the currently selected stance's permitted guard arc. Press just before contact to attempt a precision parry; sustained hold is a normal block.
- **Stance**: High/Overhead, Middle/Forward, Left Guard, Right Guard (expand later to Low). Stance determines available guard cone, attack windups, reachable blade angles, preferred elbows and balance. Hotspots are stance anchor offsets, not final combat logic.
- **Simultaneous LMB + RMB**: default defense takes priority; queued attack can be executed when guard is released. Avoid making both arms seek the same nearest hotspot.
- **Mouse position**: convert screen position to player-local angle and radius on the XY plane; dead zone, constrained reach, velocity-limited wrist/weapon rotation and hysteresis between stance zones prevent oscillation.

## Rig architecture (next implementation)

```
Player Rigidbody2D, Movement, StanceController
  Sword Pivot / Weapon Rigidbody2D (held: kinematic or motor-followed)
    PrimaryGrip Transform
    SecondaryGrip Transform
    BladeCollider2D (actual contact region)
  Left Arm (LimbSolver2D) -> left grip-follow target
  Right Arm (LimbSolver2D) -> right grip-follow target
  Shoulder and elbow limits / optional HingeJoint2D for ragdoll state
```

**Weapon-first:** compute sword pose from stance, input and world-space mouse position. Then place both hand targets on the weapon's grip transforms. This guarantees hands stay coordinated. Keep the torso's yaw and each hand's target separate from hit reaction physics.

## Finite state machine

`Ready → Windup → Active → Recovery → Ready` (LMB)

`Ready → Guard → ParryWindow / Block → Guard` (RMB)

`Block → GuardBroken → PhysicsReaction → Recovering → Ready` (successful break)

Gate state transitions by attack recovery, equipment, stamina/guard stability, and animation/IK blending. Do not trigger hit detection from mouse input alone; weapon contacts drive combat resolution.

## Combat direction and block failure

Compute a guard facing vector in player-local space. Evaluate contact angle (normal of incoming weapon relative to the sword's guard), blade-vs-blade contact point, momentum/relative velocity, attack strength and guard stability. A guard break occurs when the resulting load exceeds stance-adjusted guard capacity; a precise parry has a time-limited larger capacity/deflection factor. Damage, guard stability and parry outcome are deterministic physics/gameplay state, not tied to how far a mouse button was held.

## Physics-driven block-break reaction

**Important distinction:** setting `IKManager2D.weight = 0` is NOT a ragdoll; it merely stops IK influence. True physics motion requires physical bodies and joints.

1. On a broken block, sample the collision contact's point, normal and impulse from `Collision2D` (or compute an impulse from relative velocity/mass for non-dynamic sword contacts). Convert to a clamped torque and force; reflect realistic impact direction, not always away from the mouse.
2. Transfer the current posed arm joint transforms into a prebuilt **2D physical arm** (Rigidbody2D upper arm/forearm and HingeJoint2D shoulder/elbow, with constrained angular limits). Prefer enabling simulated bodies rather than rebuilding joints every hit.
3. Stop procedural IK and disable script-based `RotationLimitHinge` on the ragdolled arm *before* the physics step that applies the impact. Never allow IK, manual transform rotation, and dynamic Rigidbody2D to drive the same bone concurrently.
4. Apply the hit impulse at the contact point using `Rigidbody2D.AddForceAtPosition(..., ForceMode2D.Impulse)`. Secondary impulse to shoulder/torso can accompany severe hits. Use 2D joint motors/limits or springs for passive resistance; tune per stance.
5. Enter `PhysicsReaction` for a minimum stun duration. Compute recovery from angular speed, contact forces and remaining guard balance. Never snap immediately back to the resting pose.
6. Recover by fading dynamic motion into a measured stable pose, switching bodies back to kinematic/non-simulated, restoring joint scripts, placing the IK targets at the current hands, and blending IK weight from 0 to 1 over a tunable interval.
7. For lighter non-breaking blocks, keep IK active and use a **spring-damper / physics proxy target** with velocity and angular damping to create recoil while remaining in guard. This is not a full ragdoll, but gives responsive impact without toggling physics bones for every hit.

Start with the physics-target recoil, then add articulated ragdoll only to the impacted arm. Optionally drive both arms when a two-handed grip is forcibly released. Keep all physical bodies under one control authority at a time and test fixed timestep behavior at different frame rates.

## Recommended incremental slices

1. Fix existing IK chains and importer-generated shoulder constraints; verify in the Unity 6.6 editor.
2. Replace `HandManager`'s nearest-hotspot selection with distinct left/right authored targets driven by one `StanceController`; eliminate target hopping using hysteresis and continuous interpolation.
3. Add weapon pivot and grip transforms, attacks/guards, stance rules and input precedence; add visual gizmos for cones/arcs/reach and stance transitions.
4. Add blade/guard colliders and collision event routing. Introduce impact strength/guard-stability thresholds and clear event telemetry.
5. Add spring-based recoil and hit-stop for normal blocked blows.
6. Add true physical arm state on guard break (upper and lower rigidbodies, HingeJoint2D, collision-group policy, recovery blend), then stress-test at different physics time steps and impacts.

## Current repository scope

The present repository has no complete sword model, weapon hitboxes, block-resolution pipeline, or articulated physical arm bodies. None of the stance combat/ragdoll described here is claimed as implemented by the rig repair. This is a scoped implementation proposal, not a claim of present functionality.
