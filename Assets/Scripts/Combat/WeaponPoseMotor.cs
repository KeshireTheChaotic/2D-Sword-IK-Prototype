using UnityEngine;

/// <summary>
/// Independently simulated 2D hilt. Held movement is a bounded XY force/torque
/// servo, never transform parenting or cursor displacement.
/// </summary>
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

    [Header("Weapon poses in degrees above the XY plane")]
    [Range(0f, 90f)] public float idleElevation = 90f;
    [Range(0f, 90f)] public float blockElevation = 18f;
    [Range(0f, 90f)] public float windupElevation = 40f;
    [Range(0f, 90f)] public float strikeElevation = 14f;
    public float windupTilt = 14f;
    public float strikeArc = 155f;

    public Rigidbody2D HiltBody { get; private set; }
    public bool IsDropped { get; private set; }
    public bool BladeActive => blade != null && blade.IsActive;

    private bool motorEnabled = true;
    private Vector2 recoilPosition;
    private float recoilAngular;
    private Collider2D hiltCollider;

    private void Awake()
    {
        HiltBody = GetComponent<Rigidbody2D>();
        HiltBody.gravityScale = 0f;
        HiltBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        hiltCollider = GetComponent<Collider2D>();
        if (hiltCollider != null) hiltCollider.enabled = true;
        if (transform.parent != null)
            Debug.LogError("Lightsaber_Hilt must be an independent scene root. Run Tools > Sword IK > Apply XY Combat Fix.", this);

        if (blade != null) blade.hiltTransform = transform;
        if (character != null)
        {
            foreach (Collider2D owner in character.GetComponentsInChildren<Collider2D>(true))
            {
                if (hiltCollider != null && owner != hiltCollider)
                    Physics2D.IgnoreCollision(owner, hiltCollider, true);
                if (blade != null && blade.contactCollider != null && owner != blade.contactCollider)
                    Physics2D.IgnoreCollision(owner, blade.contactCollider, true);
            }
        }
    }

    public void ToggleBlade() { if (blade != null) blade.SetActive(!blade.IsActive); }
    public void BeginStrike()
    {
        if (blade != null && blade.contactSensor != null)
            blade.contactSensor.StartNewStrike();
    }

    public void SetMotorEnabled(bool enabled) => motorEnabled = enabled && !IsDropped;

    public void Drop()
    {
        IsDropped = true;
        motorEnabled = false;
        if (!preserveIgnitionOnDrop && blade != null) blade.SetActive(false);
        if (hiltCollider != null) hiltCollider.enabled = true;
        HiltBody.angularDamping = 2f;
        HiltBody.linearDamping = 2f;
        HiltBody.AddForce((Vector2)transform.right * .35f, ForceMode2D.Impulse);
        HiltBody.AddTorque(.15f, ForceMode2D.Impulse);
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
        return true; // Finite force-controlled retrieval; no teleport.
    }

    public void Pickup() { TryPickup(float.PositiveInfinity); }

    public void AddRecoil(Vector2 direction, float impulse)
    {
        recoilPosition += Vector2.ClampMagnitude(direction * (impulse * .04f), .3f);
        recoilAngular += Mathf.Clamp(impulse * 4.5f *
            Mathf.Sign(Vector2.SignedAngle(Vector2.up, direction)), -35f, 35f);
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
        recoilPosition = Vector2.MoveTowards(recoilPosition, Vector2.zero, dt);
        recoilAngular = Mathf.MoveTowards(recoilAngular, 0f, dt * 125f);
        float side = combat.RestSide;
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
                hiltLocal = new Vector2(side * .34f, .25f + combat.SpringProgress * .15f);
                tilt = -side * windupTilt;
                break;
            case SwordPhase.Swing:
                float t = combat.SwingProgress;
                float smooth = t * t * (3f - 2f * t);
                localYaw = side * (97f - strikeArc * smooth);
                elevation = strikeElevation;
                hiltLocal = new Vector2(Mathf.Lerp(side * .34f, -side * .34f, smooth),
                    .40f + .15f * (1f - smooth));
                tilt = Mathf.Lerp(-side * windupTilt, side * 5f, smooth);
                break;
            case SwordPhase.Recovery:
                elevation = Mathf.Lerp(strikeElevation, idleElevation,
                    Mathf.Clamp01(combat.PhaseTime / Mathf.Max(.01f, combat.recoverySeconds)));
                hiltLocal = new Vector2(side * .22f, .32f);
                break;
            case SwordPhase.Parried: elevation = strikeElevation; break;
            case SwordPhase.GuardBroken: elevation = blockElevation; break;
            case SwordPhase.Dropped: elevation = 8f; break;
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
