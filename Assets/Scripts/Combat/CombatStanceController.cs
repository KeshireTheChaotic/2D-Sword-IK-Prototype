using UnityEngine;

public enum SwordStance { Forward, Left, Right, Rear }
public enum SwordPhase { Neutral, GuardTransition, Guard, Windup, Spring, Swing, Recovery, GuardBroken, Parried, Dropped }
public enum SwordImpactResult { Ignored, Hit, Blocked, Parried, GuardBroken }

/// <summary>Samples input in Update and advances combat in FixedUpdate.</summary>
public sealed class CombatStanceController : MonoBehaviour
{
    public WeaponPoseMotor weapon;
    public ArmPhysicsBlend armPhysics;
    public MonoBehaviour movement;
    [Header("Torso-relative stance")]
    public float sideBoundary = 40f, rearBoundary = 140f, sectorHysteresis = 12f, sectorDwellSeconds = .12f;
    [Header("Weapon phase timings")]
    public float guardTransitionSeconds = .25f, minWindupSeconds = .12f, fullWindupSeconds = .35f;
    public float springSeconds = .09f, swingSeconds = .45f, recoverySeconds = .33f;
    public float brokenSeconds = .70f, parriedSeconds = .40f;
    [Header("Combo parry timing")]
    public float baseParryWindowSeconds = .120f, addedWindowPerSwingSeconds = .035f;
    public float maxParryWindowSeconds = .280f, comboContinuationSeconds = .65f;
    public float guardResetHoldSeconds = .38f, parryRearmSeconds = .42f, counterWindowSeconds = .35f;
    [Header("Body turning multipliers")]
    [Range(0f, 1f)] public float guardTurnMultiplier = .4f, windupTurnMultiplier = .3f;
    [Range(0f, 1f)] public float swingTurnMultiplier = .15f, recoveryTurnMultiplier = .55f;
    [Header("Guard, pickup and damage")]
    public float maxGuardStability = 7f, guardRestorePerSecond = 2.5f;
    public float health = 100f, incomingDamageMultiplier = .8f, pickupRange = 1.2f;
    public bool useInjectedInput;

    public SwordPhase Phase { get; private set; } = SwordPhase.Neutral;
    public SwordStance Stance { get; private set; } = SwordStance.Forward;
    public SwordStance CommittedStance { get; private set; } = SwordStance.Forward;
    public Vector2 AimDirection { get; private set; } = Vector2.up;
    public float PhaseTime { get; private set; }
    public float GuardStability { get; private set; }
    public int ComboCount { get; private set; }
    public int AttackStartSide { get; private set; } = -1;
    public int RestSide { get; private set; } = -1;
    public bool IsActiveSwing => Phase == SwordPhase.Swing;
    public bool CounterWindowActive => simulationClock <= counterReadyUntil;
    public float WindupProgress => Mathf.Clamp01(PhaseTime / Mathf.Max(.01f, fullWindupSeconds));
    public float SpringProgress => Mathf.Clamp01(PhaseTime / Mathf.Max(.01f, springSeconds));
    public float SwingProgress => Mathf.Clamp01(PhaseTime / Mathf.Max(.01f, swingSeconds));
    public float ParryWindowSeconds => CalculateParryWindow(ComboCount, baseParryWindowSeconds,
        addedWindowPerSwingSeconds, maxParryWindowSeconds);

    public static float CalculateParryWindow(int comboCount, float baseSeconds,
        float incrementSeconds, float capSeconds) => Mathf.Min(capSeconds,
        baseSeconds + Mathf.Max(0, comboCount - 1) * incrementSeconds);

    public float BodyTurnMultiplier
    {
        get
        {
            switch (Phase)
            {
                case SwordPhase.Guard:
                case SwordPhase.GuardTransition: return guardTurnMultiplier;
                case SwordPhase.Windup: return windupTurnMultiplier;
                case SwordPhase.Spring:
                case SwordPhase.Swing: return swingTurnMultiplier;
                case SwordPhase.Recovery: return recoveryTurnMultiplier;
                case SwordPhase.GuardBroken:
                case SwordPhase.Parried: return .1f;
                default: return 1f;
            }
        }
    }

    private SwordStance candidate;
    private float candidateTime, simulationClock, guardPlacedAt = float.NegativeInfinity;
    private float nextParryReadyAt, comboDeadline = float.NegativeInfinity;
    private float sustainedGuardTime, counterReadyUntil = float.NegativeInfinity;
    private bool attackDown, attackUp, attackHeld, guardHeld, toggle, drop, retrieve;
    private bool releaseQueued, queuedAfterGuard, swingCompleted, pendingComboContinuation;
    private int previousSwingStartSide;

    private void Awake()
    {
        GuardStability = maxGuardStability;
        candidate = Stance;
        CommittedStance = Stance;
    }

    private void Update()
    {
        if (useInjectedInput) return;
        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            Vector2 delta = (Vector2)activeCamera.ScreenToWorldPoint(Input.mousePosition) - (Vector2)transform.position;
            if (delta.sqrMagnitude > .0001f) AimDirection = delta.normalized;
        }
        attackDown |= Input.GetMouseButtonDown(0);
        attackUp |= Input.GetMouseButtonUp(0);
        attackHeld = Input.GetMouseButton(0);
        guardHeld = Input.GetMouseButton(1);
        toggle |= Input.GetKeyDown(KeyCode.F);
        drop |= Input.GetKeyDown(KeyCode.G);
        retrieve |= Input.GetKeyDown(KeyCode.R);
    }

    /// <summary>Test/AI input for a separately controlled second combatant.</summary>
    public void SetInjectedInput(Vector2 aim, bool pressed, bool held, bool released,
        bool guard, bool toggleBlade = false, bool dropWeapon = false, bool retrieveWeapon = false)
    {
        useInjectedInput = true;
        if (aim.sqrMagnitude > .0001f) AimDirection = aim.normalized;
        attackDown |= pressed;
        attackUp |= released;
        attackHeld = held;
        guardHeld = guard;
        toggle |= toggleBlade;
        drop |= dropWeapon;
        retrieve |= retrieveWeapon;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        simulationClock += dt;
        PhaseTime += dt;
        UpdateStance(dt);
        ProcessCommands();
        AdvanceTimeline(dt);
        attackDown = attackUp = toggle = drop = retrieve = false;
    }

    private void ProcessCommands()
    {
        if (toggle && weapon != null)
        {
            weapon.ToggleBlade();
            if (!weapon.BladeActive) ResetCombo();
        }
        if (drop && weapon != null && Phase != SwordPhase.Dropped) DropWeapon();
        if (retrieve && Phase == SwordPhase.Dropped && weapon != null && weapon.TryPickup(pickupRange))
            ChangePhase(SwordPhase.Neutral);
        if (Phase == SwordPhase.Dropped) return;

        // Defense wins simultaneous input. Attack can queue until guard release.
        if (guardHeld)
        {
            if (attackDown) queuedAfterGuard = true;
            if (Phase == SwordPhase.Neutral && weapon != null && weapon.BladeActive) StartGuard();
        }
        else
        {
            if (Phase == SwordPhase.Guard || Phase == SwordPhase.GuardTransition)
            {
                guardPlacedAt = float.NegativeInfinity;
                ChangePhase(SwordPhase.Neutral);
            }
            if ((attackDown || queuedAfterGuard) && CanWindup())
            {
                queuedAfterGuard = false;
                StartWindup();
            }
        }
        if (attackUp && Phase == SwordPhase.Windup) releaseQueued = true;
    }

    private void AdvanceTimeline(float dt)
    {
        switch (Phase)
        {
            case SwordPhase.GuardTransition:
                if (!guardHeld) ChangePhase(SwordPhase.Neutral);
                else if (PhaseTime >= guardTransitionSeconds)
                {
                    ChangePhase(SwordPhase.Guard);
                    sustainedGuardTime = 0f;
                    guardPlacedAt = simulationClock >= nextParryReadyAt ? simulationClock : float.NegativeInfinity;
                }
                break;
            case SwordPhase.Guard:
                GuardStability = Mathf.Min(maxGuardStability, GuardStability + guardRestorePerSecond * dt);
                sustainedGuardTime += dt;
                if (sustainedGuardTime >= guardResetHoldSeconds && ComboCount > 0) ResetCombo();
                if (guardHeld && Stance != CommittedStance && Stance != SwordStance.Rear) StartGuard();
                break;
            case SwordPhase.Windup:
                if (releaseQueued && PhaseTime >= minWindupSeconds) StartSpring();
                break;
            case SwordPhase.Spring:
                if (PhaseTime >= springSeconds) StartStrike();
                break;
            case SwordPhase.Swing:
                if (PhaseTime >= swingSeconds)
                {
                    RestSide = -AttackStartSide;
                    swingCompleted = true;
                    ChangePhase(SwordPhase.Recovery);
                }
                break;
            case SwordPhase.Recovery:
                if (PhaseTime >= recoverySeconds)
                {
                    if (swingCompleted)
                    {
                        comboDeadline = simulationClock + comboContinuationSeconds;
                        swingCompleted = false;
                    }
                    if (guardHeld && weapon != null && weapon.BladeActive) StartGuard();
                    else ChangePhase(SwordPhase.Neutral);
                }
                break;
            case SwordPhase.GuardBroken:
                if (PhaseTime >= brokenSeconds && (armPhysics == null || armPhysics.IsSettled))
                {
                    if (armPhysics != null) armPhysics.BeginRecovery();
                    if (weapon != null) weapon.SetMotorEnabled(true);
                    ChangePhase(SwordPhase.Recovery);
                }
                break;
            case SwordPhase.Parried:
                if (PhaseTime >= parriedSeconds) ChangePhase(SwordPhase.Recovery);
                break;
        }
        if (ComboCount > 0 && Phase == SwordPhase.Neutral && simulationClock > comboDeadline) ResetCombo();
        if (Phase != SwordPhase.Guard)
            GuardStability = Mathf.Min(maxGuardStability, GuardStability + guardRestorePerSecond * .25f * dt);
    }

    private bool CanWindup() => Phase == SwordPhase.Neutral && !guardHeld && weapon != null &&
        weapon.BladeActive && !weapon.IsDropped;

    private void StartWindup()
    {
        releaseQueued = !attackHeld;
        AttackStartSide = GetPhysicalGripSide();
        pendingComboContinuation = ComboCount > 0 && simulationClock <= comboDeadline &&
            previousSwingStartSide != 0 && AttackStartSide != previousSwingStartSide;
        CommittedStance = AttackStartSide < 0 ? SwordStance.Left : SwordStance.Right;
        ChangePhase(SwordPhase.Windup);
    }

    private void StartSpring()
    {
        releaseQueued = false;
        guardPlacedAt = float.NegativeInfinity;
        ChangePhase(SwordPhase.Spring);
    }

    private void StartStrike()
    {
        ComboCount = pendingComboContinuation ? ComboCount + 1 : 1;
        previousSwingStartSide = AttackStartSide;
        pendingComboContinuation = false;
        comboDeadline = float.NegativeInfinity;
        ChangePhase(SwordPhase.Swing);
        if (weapon != null) weapon.BeginStrike();
    }

    private void StartGuard()
    {
        CommittedStance = Stance == SwordStance.Rear ? SwordStance.Forward : Stance;
        guardPlacedAt = float.NegativeInfinity;
        sustainedGuardTime = 0f;
        ChangePhase(SwordPhase.GuardTransition);
    }

    private void UpdateStance(float dt)
    {
        float angle = Vector2.SignedAngle(transform.up, AimDirection);
        float side = sideBoundary + (Stance == SwordStance.Forward ? sectorHysteresis : -sectorHysteresis);
        float rear = rearBoundary + (Stance == SwordStance.Rear ? -sectorHysteresis : sectorHysteresis);
        SwordStance next = Mathf.Abs(angle) >= rear ? SwordStance.Rear :
            Mathf.Abs(angle) <= side ? SwordStance.Forward : angle > 0f ? SwordStance.Left : SwordStance.Right;
        if (next != candidate) { candidate = next; candidateTime = 0f; }
        else candidateTime += dt;
        if (candidate != Stance && candidateTime >= sectorDwellSeconds) Stance = candidate;
    }

    private int GetPhysicalGripSide()
    {
        if (weapon != null)
        {
            Vector3 sample = weapon.sharedGrip != null ? weapon.sharedGrip.position : weapon.transform.position;
            float x = transform.InverseTransformPoint(sample).x;
            if (Mathf.Abs(x) > .07f) return x < 0f ? -1 : 1;
        }
        return RestSide != 0 ? RestSide : Stance == SwordStance.Right ? 1 : -1;
    }

    public SwordImpactResult ReceiveImpact(float impulse, Vector2 contactPoint,
        Vector2 incomingDirection, bool bladeIntercepted = false, CombatStanceController attacker = null)
    {
        if (Phase == SwordPhase.Dropped || Phase == SwordPhase.GuardBroken || Phase == SwordPhase.Parried)
            return SwordImpactResult.Ignored;
        Vector2 offset = contactPoint - (Vector2)transform.position;
        bool facing = offset.sqrMagnitude > .0001f && Vector2.Dot((Vector2)transform.up, offset.normalized) > -.05f;
        bool guardPresent = Phase == SwordPhase.Guard && bladeIntercepted && facing &&
            weapon != null && weapon.BladeActive;
        if (!guardPresent)
        {
            health = Mathf.Max(0f, health - Mathf.Max(0f, impulse) * incomingDamageMultiplier);
            Rigidbody2D torso = GetComponent<Rigidbody2D>();
            if (torso != null) torso.AddForce(incomingDirection.normalized * Mathf.Min(impulse * .15f, 3f), ForceMode2D.Impulse);
            return SwordImpactResult.Hit;
        }

        bool timed = attacker != null && attacker.IsActiveSwing && simulationClock >= guardPlacedAt &&
            simulationClock - guardPlacedAt <= attacker.ParryWindowSeconds && simulationClock >= nextParryReadyAt;
        if (timed)
        {
            guardPlacedAt = float.NegativeInfinity;
            nextParryReadyAt = simulationClock + parryRearmSeconds;
            counterReadyUntil = simulationClock + counterWindowSeconds;
            GuardStability = Mathf.Min(maxGuardStability, GuardStability + guardRestorePerSecond * .25f);
            if (weapon != null) weapon.AddRecoilAt(contactPoint, incomingDirection, impulse * .5f);
            return SwordImpactResult.Parried;
        }
        GuardStability -= Mathf.Max(0f, impulse);
        if (GuardStability > 0f)
        {
            weapon.AddRecoilAt(contactPoint, incomingDirection, impulse);
            return SwordImpactResult.Blocked;
        }
        GuardStability = 0f;
        ResetCombo();
        ChangePhase(SwordPhase.GuardBroken);
        weapon.SetMotorEnabled(false);
        if (armPhysics != null) armPhysics.TriggerGuardBreak(contactPoint, incomingDirection, impulse, weapon.HiltBody);
        if (weapon.HiltBody != null)
            weapon.HiltBody.AddForceAtPosition(incomingDirection.normalized * Mathf.Min(impulse, 14f), contactPoint, ForceMode2D.Impulse);
        return SwordImpactResult.GuardBroken;
    }

    public void ReceiveParry(float impulse, Vector2 deflection)
    {
        if (Phase != SwordPhase.Swing) return;
        ResetCombo();
        swingCompleted = false;
        if (weapon != null)
        {
            weapon.AddRecoil(deflection, impulse);
            if (weapon.HiltBody != null)
                weapon.HiltBody.AddForce(deflection.normalized * Mathf.Min(impulse * .45f, 4f), ForceMode2D.Impulse);
        }
        ChangePhase(SwordPhase.Parried);
    }

    public void ResetCombo()
    {
        ComboCount = 0;
        comboDeadline = float.NegativeInfinity;
        previousSwingStartSide = 0;
        pendingComboContinuation = false;
    }

    private void DropWeapon()
    {
        releaseQueued = queuedAfterGuard = false;
        ResetCombo();
        if (armPhysics != null)
        {
            armPhysics.ReleaseWeaponGrip();
            if (armPhysics.IsRagdoll) armPhysics.BeginRecovery();
        }
        weapon.Drop();
        ChangePhase(SwordPhase.Dropped);
    }

    private void ChangePhase(SwordPhase phase) { Phase = phase; PhaseTime = 0f; }
}
