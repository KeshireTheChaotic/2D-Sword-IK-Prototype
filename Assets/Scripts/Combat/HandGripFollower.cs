using UnityEngine;

/// <summary>
/// BOTH IK targets seek exactly the same point. A single shared grip target
/// follows the physical hilt, so the two arms never choose different hotspots.
/// When dropped, the target settles near the torso instead of chasing the hilt.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class HandGripFollower : MonoBehaviour
{
    public Transform leftTarget;
    public Transform rightTarget;
    public Transform sharedGrip;
    public Transform commonRest;
    public float trackingSpeed = 1f;
    public float maximumReachFromTorso = 1f;
    public ArmPhysicsBlend arms;
    public CombatStanceController combat;
    private Vector3 commonTarget;
    private bool initialized;

    private void LateUpdate()
    {
        if (leftTarget == null || rightTarget == null) return;
        if (arms != null && arms.IsRagdoll) return;
        Vector3 requested;
        if (combat != null && combat.Phase == SwordPhase.Dropped)
            requested = commonRest != null ? commonRest.position : transform.TransformPoint(0f, .25f, 0f);
        else
            requested = sharedGrip != null ? sharedGrip.position : transform.TransformPoint(0f, .3f, 0f);

        // Use ONE reachable destination for both hands. Even if the hilt is
        // knocked outside arm reach, targets remain coincident.
        Vector3 center = transform.position;
        Vector3 displacement = requested - center;
        displacement.z = 0f;
        if (displacement.magnitude > maximumReachFromTorso)
            requested = center + displacement.normalized * maximumReachFromTorso;
        else
            requested.z = center.z;
        if (!initialized)
        {
            commonTarget = (leftTarget.position + rightTarget.position) * .5f;
            initialized = true;
        }
        commonTarget = Vector3.MoveTowards(commonTarget, requested,
            Mathf.Max(0f, trackingSpeed) * Time.deltaTime);
        if (arms != null && arms.IsRecovering)
        {
            float t = arms.RecoveryProgress;
            Vector3 leftStart;
            Vector3 rightStart;
            if (arms.TryGetRecoveryTarget(true, out leftStart) &&
                arms.TryGetRecoveryTarget(false, out rightStart))
            {
                // Start IK at the current physical hand positions, then converge
                // both targets onto the one shared grip without a solver snap.
                float smooth = t * t * (3f - 2f * t);
                leftTarget.position = Vector3.Lerp(leftStart, commonTarget, smooth);
                rightTarget.position = Vector3.Lerp(rightStart, commonTarget, smooth);
                return;
            }
        }
        // The IK targets are exactly coincident even during guard transitions.
        // Servo travel time comes from the single target, not independent arms.
        leftTarget.position = commonTarget;
        rightTarget.position = commonTarget;
    }
}
