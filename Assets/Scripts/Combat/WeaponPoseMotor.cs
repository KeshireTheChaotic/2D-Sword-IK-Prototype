using UnityEngine;

/// <summary>A dynamic, force/torque-limited hilt. Mouse delta is never applied to
/// the rigidbody. Blade is an independent child whose scale controls ignition.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class WeaponPoseMotor : MonoBehaviour
{
    public CombatStanceController combat;
    public Transform character;
    public BladeVisualController blade;
    public Transform sharedGrip;

    [Header("Hilt servomotor")]
    public float positionSpring = 115f;
    public float positionDamping = 19f;
    public float maximumForce = 100f;
    public float angularSpring = 22f;
    public float angularDamping = 1.7f;
    public float maximumTorque = 22f;
    public float maxAngularSpeed = 440f;
    public float maxLinearSpeed = 12f;
    public bool preserveIgnitionOnDrop = true;

    [Header("Weapon poses in degrees")]
    public float idleElevation = 78f;
    public float blockElevation = 18f;
    public float windupElevation = 40f;
    public float strikeElevation = 14f;
    public float windupTilt = 14f;
    public float strikeArc = 155f;

    public Rigidbody2D HiltBody { get; private set; }
    public bool IsDropped { get; private set; }
    public bool BladeActive => blade != null && blade.IsActive;

    private bool motorEnabled = true;
    private Vector2 recoilPosition;
    private float recoilAngular;

    private void Awake()
    {
        HiltBody = GetComponent<Rigidbody2D>();
        HiltBody.gravityScale = 0f;
        HiltBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // The held hilt overlaps its owner at the default grip location.
        // Exclude self-collisions so the weapon servo cannot push its own player.
        Collider2D hiltCollider = GetComponent<Collider2D>();
        if (character != null)
        {
            foreach (Collider2D ownerCollider in character.GetComponentsInChildren<Collider2D>(true))
            {
                if (hiltCollider != null) Physics2D.IgnoreCollision(ownerCollider, hiltCollider, true);
                if (blade != null && blade.contactCollider != null)
                    Physics2D.IgnoreCollision(ownerCollider, blade.contactCollider, true);
            }
        }
    }

    public void ToggleBlade() { if (blade != null) blade.SetActive(!blade.IsActive); }
    public void BeginStrike()
    {
        if (blade != null && blade.contactSensor != null) blade.contactSensor.StartNewStrike();
    }

    public void SetMotorEnabled(bool enabled) { motorEnabled = enabled && !IsDropped; }

    public void Drop()
    {
        IsDropped = true;
        motorEnabled = false;
        if (!preserveIgnitionOnDrop && blade != null) blade.SetActive(false);
        HiltBody.angularDamping = 2f;
        HiltBody.AddForce((Vector2)transform.right * .35f, ForceMode2D.Impulse);
        HiltBody.AddTorque(0.15f, ForceMode2D.Impulse);
        HiltBody.linearDamping = 2f;
    }

    public bool TryPickup(float maximumRange)
    {
        if (!IsDropped || character == null || HiltBody == null) return false;
        Vector2 gripDestination = character.TransformPoint(new Vector3(0f, .25f, 0f));
        if (Vector2.Distance(HiltBody.position, gripDestination) > Mathf.Max(0f, maximumRange))
            return false;
        IsDropped = false;
        motorEnabled = true;
        HiltBody.linearDamping = 4f;
        HiltBody.angularDamping = 4f;
        // The finite servo retrieves the nearby hilt; pickup never teleports it.
        return true;
    }

    public void Pickup() { TryPickup(float.PositiveInfinity); }

    public void AddRecoil(Vector2 direction, float impulse)
    {
        recoilPosition += Vector2.ClampMagnitude(direction * (impulse * .04f), .3f);
        recoilAngular += Mathf.Clamp(impulse * 4.5f * Mathf.Sign(Vector2.SignedAngle(Vector2.up, direction)), -35f, 35f);
    }

    public void AddRecoilAt(Vector2 contactPoint, Vector2 direction, float impulse)
    {
        AddRecoil(direction, impulse);
        if (HiltBody == null) return;
        Vector2 lever = contactPoint - HiltBody.worldCenterOfMass;
        float signed = lever.x * direction.y - lever.y * direction.x;
        recoilAngular += Mathf.Clamp(signed * impulse * 10f, -35f, 35f);
    }

    private void FixedUpdate()
    {
        if (combat == null || character == null || HiltBody == null) return;
        float dt = Time.fixedDeltaTime;
        recoilPosition = Vector2.MoveTowards(recoilPosition, Vector2.zero, dt * 1.0f);
        recoilAngular = Mathf.MoveTowards(recoilAngular, 0f, dt * 125f);
        float side = combat.RestSide; // -1 left, +1 right
        // While out of a combo, mouse-side stance selects the preferred grip;
        // after a swing, keep the actual finishing side for a natural reversal.
        if (combat.Phase == SwordPhase.Neutral && combat.ComboCount == 0)
        {
            if (combat.Stance == SwordStance.Left) side = -1f;
            else if (combat.Stance == SwordStance.Right) side = 1f;
        }
        if (combat.Phase == SwordPhase.Guard || combat.Phase == SwordPhase.GuardTransition)
            side = combat.CommittedStance == SwordStance.Left ? -1f :
                   combat.CommittedStance == SwordStance.Right ? 1f : combat.RestSide;
        if (combat.Phase == SwordPhase.Windup || combat.Phase == SwordPhase.Spring ||
            combat.Phase == SwordPhase.Swing)
            side = combat.AttackStartSide;

        float localYaw = side * 67f;
        float elevation = idleElevation;
        float tilt = 0f;
        Vector2 hiltLocal = new Vector2(side * .22f, .32f);
        switch (combat.Phase)
        {
            case SwordPhase.GuardTransition:
            case SwordPhase.Guard:
                elevation = blockElevation;
                hiltLocal = new Vector2(side * .27f, .43f);
                break;
            case SwordPhase.Windup:
                elevation = Mathf.Lerp(idleElevation, windupElevation, combat.WindupProgress);
                localYaw = side * 97f;
                hiltLocal = new Vector2(side * .34f, .25f);
                tilt = -side * windupTilt;
                break;
            case SwordPhase.Spring:
                elevation = windupElevation;
                localYaw = side * 97f;
                hiltLocal = new Vector2(side * .34f,
                    .25f + combat.SpringProgress * .15f);
                tilt = -side * windupTilt;
                break;
            case SwordPhase.Swing:
                float t = combat.SwingProgress;
                float smooth = t * t * (3f - 2f * t);
                // Left side (-1) swings toward right (+1), and vice versa.
                // The arc, force and phase duration are independent of mouse delta.
                localYaw = side * (97f - strikeArc * smooth);
                elevation = strikeElevation;
                hiltLocal = new Vector2(Mathf.Lerp(side * .34f, -side * .34f, smooth),
                    .40f + .15f * (1f - smooth));
                tilt = Mathf.Lerp(-side * windupTilt, side * 5f, smooth);
                break;
            case SwordPhase.Recovery:
                elevation = Mathf.Lerp(strikeElevation, idleElevation,
                    Mathf.Clamp01(combat.PhaseTime / Mathf.Max(.01f, combat.recoverySeconds)));
                localYaw = side * 67f;
                hiltLocal = new Vector2(side * .22f, .32f);
                break;
            case SwordPhase.Parried:
                elevation = strikeElevation;
                break;
            case SwordPhase.GuardBroken:
                elevation = blockElevation;
                break;
            case SwordPhase.Dropped:
                elevation = 8f;
                break;
        }
        if (blade != null) blade.SetPose(elevation, tilt);
        if (!motorEnabled || IsDropped) return;

        Vector2 destination = (Vector2)character.TransformPoint(hiltLocal) + recoilPosition;
        Vector2 error = destination - HiltBody.position;
        Vector2 force = error * positionSpring - HiltBody.linearVelocity * positionDamping;
        HiltBody.AddForce(Vector2.ClampMagnitude(force, maximumForce), ForceMode2D.Force);
        HiltBody.linearVelocity = Vector2.ClampMagnitude(HiltBody.linearVelocity, maxLinearSpeed);

        float desiredAngle = character.eulerAngles.z + localYaw + recoilAngular;
        float deltaAngle = Mathf.DeltaAngle(HiltBody.rotation, desiredAngle);
        float torque = deltaAngle * angularSpring - HiltBody.angularVelocity * angularDamping;
        HiltBody.AddTorque(Mathf.Clamp(torque, -maximumTorque, maximumTorque), ForceMode2D.Force);
        HiltBody.angularVelocity = Mathf.Clamp(HiltBody.angularVelocity, -maxAngularSpeed, maxAngularSpeed);
    }
}
