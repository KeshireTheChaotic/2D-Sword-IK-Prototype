using UnityEngine;

/// <summary>
/// Renders a pseudo-3D lightsaber in an orthographic, top-down 2D scene.
/// 0 degrees of elevation lies in the XY ground plane; 90 degrees points
/// along the camera's viewing axis and projects to an illuminated dot.
/// The blade remains separate from the hilt and can ignite/retract.
/// </summary>
public sealed class BladeVisualController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer core;
    public SpriteRenderer glow;
    public Transform visualPivot;
    public CapsuleCollider2D contactCollider;
    public Transform contactTransform;
    public BladeContactSensor contactSensor;

    [Header("Blade")]
    [Min(0f)] public float bladeLength = 1.55f;
    [Min(0.01f)] public float ignitionSeconds = 0.20f;
    public bool startsActive = true;

    [Header("Visual size in world units")]
    [Min(0.001f)] public float coreWidth = 0.055f;
    [Min(0.001f)] public float glowWidth = 0.20f;

    [Header("2D ground-plane contact")]
    [Min(0.001f)] public float contactWidth = 0.13f;
    [Min(0f)] public float minContactLength = 0.03f;

    // Old serialized properties are preserved so other code or existing
    // prefabs referring to them do not break. They no longer affect rendering.
    [HideInInspector] public float heightProjection = 0.70f;
    [HideInInspector] public float minimumVisualFraction = 0.12f;

    public bool IsActive { get; private set; }
    public float Extension { get; private set; }
    public float Elevation => elevation;

    // Default idle pose: blade points along the viewing axis, not screen-up.
    private float elevation = 90f;
    private float tilt;

    private void Awake()
    {
        IsActive = startsActive;
        Extension = IsActive ? 1f : 0f;

        if (contactCollider != null)
            contactCollider.direction = CapsuleDirection2D.Vertical;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
    }

    /// <param name="pitch">Elevation above the XY ground plane: 0..90 degrees.</param>
    /// <param name="roll">Additional in-screen visual rotation, in degrees.</param>
    public void SetPose(float pitch, float roll)
    {
        elevation = Mathf.Clamp(pitch, 0f, 90f);
        tilt = Mathf.Clamp(roll, -35f, 35f);
    }

    private void Update()
    {
        Extension = Mathf.MoveTowards(
            Extension,
            IsActive ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, ignitionSeconds));
    }

    // Render after other Update scripts have positioned the hilt and pose.
    private void LateUpdate()
    {
        UpdateVisual();
    }

    // Keep 2D physics geometry synchronized with the physics time step.
    private void FixedUpdate()
    {
        UpdateContact();
    }

    private float ProjectedGroundLength()
    {
        float radians = elevation * Mathf.Deg2Rad;
        return bladeLength * Mathf.Max(0f, Mathf.Cos(radians)) * Extension;
    }

    private void UpdateVisual()
    {
        Transform hilt = transform.parent;
        if (hilt == null)
            return; // Blade root should be a child of the hilt.

        // Use the hilt's XY facing. Virtual height is NOT added to screen Y.
        // At 90 degrees, projectedLength is zero regardless of hilt facing.
        Vector2 direction = hilt.up;
        float facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        Transform pivot = visualPivot != null ? visualPivot : transform;
        pivot.rotation = Quaternion.Euler(0f, 0f, facing + tilt);

        float projectedLength = ProjectedGroundLength();

        // For a vertical blade, draw a small round cap at the emitter rather
        // than stretching the sprite into a screen-space beam.
        UpdateRenderer(core, projectedLength, coreWidth, Extension);
        UpdateRenderer(glow, projectedLength, glowWidth, Extension);
    }

    private void UpdateContact()
    {
        if (contactCollider == null)
            return;

        Transform hilt = transform.parent;
        if (hilt == null)
        {
            contactCollider.enabled = false;
            return;
        }

        // Never let the collider inherit the decorative visual tilt.
        // A separate contactTransform should be a sibling of visualPivot,
        // with the collider beneath it and an unscaled parent hierarchy.
        Transform collisionPivot = contactTransform != null
            ? contactTransform
            : contactCollider.transform;

        // Prefer an independent collision pivot. Using the collider's own
        // transform as a fallback also works when it is a distinct child.
        if (collisionPivot != transform && collisionPivot != visualPivot)
            collisionPivot.SetPositionAndRotation(transform.position, hilt.rotation);
        else
        {
            // A shared pivot cannot rotate independently for visuals and physics.
            contactCollider.enabled = false;
            return;
        }

        float projectedLength = ProjectedGroundLength();

        // A vertical 2D capsule must be at least as tall as it is wide.
        // Very short upright blades should not acquire fictitious ground reach.
        bool hasReach = projectedLength >= Mathf.Max(minContactLength, contactWidth);
        contactCollider.enabled = IsActive && Extension >= 0.95f && hasReach;

        float capsuleLength = Mathf.Max(contactWidth, projectedLength);
        contactCollider.size = new Vector2(contactWidth, capsuleLength);
        contactCollider.offset = new Vector2(0f, capsuleLength * 0.5f);
    }

    private static void UpdateRenderer(
        SpriteRenderer renderer, float projectedLength, float fullWidth, float extension)
    {
        if (renderer == null)
            return;

        renderer.enabled = extension > 0.001f;
        if (!renderer.enabled)
            return;

        float diameter = Mathf.Max(0.001f, fullWidth) * extension;
        float visibleLength = Mathf.Max(projectedLength, diameter);

        Transform target = renderer.transform;
        // A dot stays centered at the emitter. As elevation decreases,
        // the longer beam grows outward, rather than shifting by its dot size.
        Vector3 local = target.localPosition;
        local.x = 0f;
        local.y = projectedLength * 0.5f;
        target.localPosition = local;
        target.localRotation = Quaternion.identity;

        // Normalize by the sprite's actual imported world-unit dimensions.
        // The sprite should be centered, preferably with rounded ends.
        Vector2 spriteSize = renderer.sprite != null
            ? renderer.sprite.bounds.size
            : Vector2.one;

        target.localScale = new Vector3(
            diameter / Mathf.Max(0.0001f, spriteSize.x),
            visibleLength / Mathf.Max(0.0001f, spriteSize.y),
            target.localScale.z);
    }
}
