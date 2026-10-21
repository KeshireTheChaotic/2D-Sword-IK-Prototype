using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger-based contact: once per defender per committed attack. A perfect
/// parry requires physical blade intersection, compatible height, a recent
/// guard transition, and an active opposing attack. Mouse speed is irrelevant.
/// </summary>
[RequireComponent(typeof(CapsuleCollider2D))]
public sealed class BladeContactSensor : MonoBehaviour
{
    public WeaponPoseMotor weapon;
    public CombatStanceController owner;
    public float impactMultiplier = 1.1f;
    public float maximumImpact = 12f;
    public float minimumImpact = .1f;
    public float maximumBladeElevationDifference = 32f;
    private readonly HashSet<EntityId> struck = new HashSet<EntityId>();

    public void StartNewStrike() { struck.Clear(); }
    private void OnTriggerEnter2D(Collider2D other) { ProcessHit(other); }
    private void OnTriggerStay2D(Collider2D other) { ProcessHit(other); }

    private void ProcessHit(Collider2D other)
    {
        if (owner == null || weapon == null || !owner.IsActiveSwing || !weapon.BladeActive)
            return;
        if (other == null || !other.enabled) return;
        BladeContactSensor otherBlade = other.GetComponent<BladeContactSensor>();
        CombatStanceController defender = otherBlade != null ? otherBlade.owner :
            other.GetComponentInParent<CombatStanceController>();
        if (defender == null || defender == owner || struck.Contains(defender.GetEntityId())) return;

        // Do not award an impossible guard or parry from blades crossing only
        // in the 2D projection but not at a compatible virtual elevation.
        bool bladeContact = otherBlade != null && otherBlade.weapon != null &&
            otherBlade.weapon.BladeActive &&
            weapon.blade != null && otherBlade.weapon.blade != null &&
            Mathf.Abs(weapon.blade.Elevation - otherBlade.weapon.blade.Elevation) <=
                maximumBladeElevationDifference;
        // A physical blade proxy at a different virtual height is a clean miss,
        // not a body hit or an ordinary block.
        if (otherBlade != null && !bladeContact) return;
        Rigidbody2D attackerBody = weapon.HiltBody;
        if (attackerBody == null) return;
        Vector2 contact = other.ClosestPoint(transform.position);
        Vector2 attackerVelocity = attackerBody.GetPointVelocity(contact);
        Rigidbody2D otherBody = otherBlade != null && otherBlade.weapon != null ?
            otherBlade.weapon.HiltBody : defender.GetComponent<Rigidbody2D>();
        Vector2 relative = attackerVelocity -
            (otherBody != null ? otherBody.GetPointVelocity(contact) : Vector2.zero);
        float impulse = Mathf.Clamp(relative.magnitude * attackerBody.mass * impactMultiplier,
            0f, maximumImpact);
        if (impulse < minimumImpact) return;
        Vector2 incoming = relative.sqrMagnitude > .0001f ? relative.normalized :
            (Vector2)weapon.transform.up;
        struck.Add(defender.GetEntityId());
        SwordImpactResult result = defender.ReceiveImpact(impulse, contact, incoming,
            bladeContact, owner);
        if (result == SwordImpactResult.Parried)
            owner.ReceiveParry(impulse, -incoming);
    }
}
