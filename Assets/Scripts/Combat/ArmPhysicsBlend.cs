using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>Turns imported skeletal arm bones into temporary articulated
/// Rigidbody2D chains when a guard breaks, then blends back to 2D IK.
/// Rigidbody segments are disabled outside of ragdoll to avoid fighting SpriteSkin.</summary>
public sealed class ArmPhysicsBlend : MonoBehaviour
{
    [Serializable]
    public sealed class ArmChain
    {
        public LimbSolver2D solver;
        public Behaviour shoulderLimit;
        public Rigidbody2D upperArm;
        public Rigidbody2D forearm;
        public Rigidbody2D hand;
        public HingeJoint2D shoulderJoint;
        public HingeJoint2D elbowJoint;
        public HingeJoint2D wristJoint;
        public FixedJoint2D gripJoint;
    }

    public IKManager2D manager;
    public ArmChain left = new ArmChain();
    public ArmChain right = new ArmChain();
    public float blendToIKSeconds = .55f;
    public float maxBreakImpulse = 10f;
    public float settleLinearSpeed = .35f;
    public float settleAngularSpeed = 45f;
    public float settleHoldSeconds = .12f;
    public float maximumRagdollSeconds = 1.25f;

    public bool IsRagdoll { get; private set; }
    public bool IsRecovering { get; private set; }
    public bool IsSettled => !IsRagdoll || settledClock >= settleHoldSeconds || ragdollClock >= maximumRagdollSeconds;
    public float RecoveryProgress => IsRecovering
        ? Mathf.Clamp01(recoveryClock / Mathf.Max(.01f, blendToIKSeconds)) : 1f;
    private float recoveryClock;
    private float ragdollClock;
    private float settledClock;
    private Vector3 leftRecoveryPosition;
    private Vector3 rightRecoveryPosition;

    private void Start()
    {
        SetArmSimulation(left, false);
        SetArmSimulation(right, false);
        if (manager != null) manager.weight = 1f;
    }

    public void TriggerGuardBreak(Vector2 contactPoint, Vector2 worldDirection,
        float impulse, Rigidbody2D hilt)
    {
        if (IsRagdoll) return;
        IsRagdoll = true;
        IsRecovering = false;
        ragdollClock = settledClock = 0f;
        if (manager != null) manager.weight = 0f;
        ReleaseWeaponGrip();
        SetArmSimulation(left, true);
        SetArmSimulation(right, true);
        ApplyArmImpulse(left, contactPoint, worldDirection, impulse);
        ApplyArmImpulse(right, contactPoint, worldDirection, impulse);
    }

    private void ApplyArmImpulse(ArmChain arm, Vector2 contactPoint,
        Vector2 direction, float strength)
    {
        if (arm.hand == null) return;
        float distance = Vector2.Distance(arm.hand.position, contactPoint);
        float proximity = Mathf.Lerp(.45f, 1f, 1f - Mathf.Clamp01(distance / 1.2f));
        Vector2 force = Vector2.ClampMagnitude(direction.normalized * strength * proximity, maxBreakImpulse);
        arm.hand.AddForceAtPosition(force, contactPoint, ForceMode2D.Impulse);
        if (arm.forearm != null)
            arm.forearm.AddForceAtPosition(force * .35f, contactPoint, ForceMode2D.Impulse);
    }

    public void BeginRecovery()
    {
        if (!IsRagdoll) return;
        ReleaseWeaponGrip();
        leftRecoveryPosition = left.hand != null ? left.hand.position : transform.position;
        rightRecoveryPosition = right.hand != null ? right.hand.position : transform.position;
        SetArmSimulation(left, false);
        SetArmSimulation(right, false);
        IsRagdoll = false;
        IsRecovering = true;
        recoveryClock = 0f;
        if (manager != null) manager.weight = 0f;
    }

    public bool TryGetRecoveryTarget(bool leftArm, out Vector3 position)
    {
        position = leftArm ? leftRecoveryPosition : rightRecoveryPosition;
        return IsRecovering;
    }

    public void ReleaseWeaponGrip()
    {
        if (left.gripJoint != null) left.gripJoint.enabled = false;
        if (right.gripJoint != null) right.gripJoint.enabled = false;
    }

    private void FixedUpdate()
    {
        if (IsRagdoll)
        {
            ragdollClock += Time.fixedDeltaTime;
            bool slow = ArmIsSlow(left) && ArmIsSlow(right);
            settledClock = slow ? settledClock + Time.fixedDeltaTime : 0f;
        }
        if (!IsRecovering) return;
        recoveryClock += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(recoveryClock / Mathf.Max(.01f, blendToIKSeconds));
        if (manager != null) manager.weight = t * t * (3f - 2f * t);
        if (t >= 1f) IsRecovering = false;
    }

    private bool ArmIsSlow(ArmChain arm)
    {
        if (arm == null) return true;
        Rigidbody2D[] bodies = { arm.upperArm, arm.forearm, arm.hand };
        foreach (Rigidbody2D body in bodies)
            if (body != null && (body.linearVelocity.magnitude > settleLinearSpeed ||
                Mathf.Abs(body.angularVelocity) > settleAngularSpeed)) return false;
        return true;
    }

    private static void SetArmSimulation(ArmChain arm, bool simulated)
    {
        if (arm == null) return;
        if (arm.shoulderLimit != null) arm.shoulderLimit.enabled = !simulated;
        if (arm.solver != null) arm.solver.enabled = !simulated;
        Rigidbody2D[] bodies = { arm.upperArm, arm.forearm, arm.hand };
        foreach (Rigidbody2D body in bodies)
        {
            if (body == null) continue;
            if (!simulated)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            if (simulated)
            {
                body.position = body.transform.position;
                body.rotation = body.transform.eulerAngles.z;
            }
            body.simulated = simulated;
        }
        if (arm.gripJoint != null && !simulated) arm.gripJoint.enabled = false;
    }
}
