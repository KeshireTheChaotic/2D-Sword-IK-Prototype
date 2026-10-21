using UnityEngine;

/// <summary>
/// Pseudo-3D saber projected onto Unity's XY gameplay plane. Elevation is measured
/// above XY: 0 degrees is a full blade; 90 degrees faces the orthographic camera
/// and is shown as a small emitter dot with zero ground-plane collision reach.
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
    [Tooltip("Physical hilt, NOT HiltVisual or SharedGrip. Assigned by scene migration.")]
    public Transform hiltTransform;

    [Header("Blade")]
    [Min(0f)] public float bladeLength = 1.55f;
    [Min(0.01f)] public float ignitionSeconds = 0.20f;
    public bool startsActive = true;

    [Header("Projected visual diameter (world units)")]
    [Min(0.001f)] public float coreWidth = 0.055f;
    [Min(0.001f)] public float glowWidth = 0.20f;

    [Header("2D ground-plane contact")]
    [Min(0.001f)] public float contactWidth = 0.13f;
    [Min(0f)] public float minContactLength = 0.03f;

    // Preserve legacy serialized fields for compatibility. Neither affects projection.
    [HideInInspector] public float heightProjection = 0.70f;
    [HideInInspector] public float minimumVisualFraction = 0.12f;

    public bool IsActive { get; private set; }
    public float Extension { get; private set; }
    public float Elevation => elevation;
    public float GroundReach => bladeLength * Mathf.Max(0f,
        Mathf.Cos(elevation * Mathf.Deg2Rad)) * Extension;

    private float elevation = 90f;
    private float tilt;
    private bool warnedHierarchy;

    private void Awake()
    {
        IsActive = startsActive;
        Extension = IsActive ? 1f : 0f;
        if (contactCollider != null)
            contactCollider.direction = CapsuleDirection2D.Vertical;
        ResolveHilt();
    }

    private Transform ResolveHilt()
    {
        if (hiltTransform != null) return hiltTransform;
        WeaponPoseMotor motor = GetComponentInParent<WeaponPoseMotor>();
        if (motor != null) hiltTransform = motor.transform;
        return hiltTransform;
    }

    public void SetActive(bool active) => IsActive = active;

    /// <param name="pitch">Degrees above XY ground: 0..90.</param>
    /// <param name="roll">Decorative rotation within the XY screen plane.</param>
    public void SetPose(float pitch, float roll)
    {
        elevation = Mathf.Clamp(pitch, 0f, 90f);
        tilt = Mathf.Clamp(roll, -35f, 35f);
    }

    private void Update()
    {
        Extension = Mathf.MoveTowards(Extension, IsActive ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, ignitionSeconds));
    }

    private void LateUpdate()
    {
        Transform hilt = ResolveHilt();
        if (hilt == null) return;

        // Z-axis angle only. Simulated height must never be added to screen Y.
        Vector2 direction = (Vector2)hilt.up;
        float facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        Transform pivot = visualPivot != null ? visualPivot : transform;
        pivot.rotation = Quaternion.Euler(0f, 0f, facing + tilt);

        float projectedLength = GroundReach;
        UpdateRenderer(core, projectedLength, coreWidth, Extension);
        UpdateRenderer(glow, projectedLength, glowWidth, Extension);
    }

    private void FixedUpdate()
    {
        if (contactCollider == null) return;
        Transform hilt = ResolveHilt();
        Transform collisionPivot = contactTransform != null
            ? contactTransform : contactCollider.transform;

        // Never use a decorative pivot or any of its scaled descendants as physics.
        bool invalidHierarchy = hilt == null ||
            collisionPivot == transform || collisionPivot == visualPivot ||
            (visualPivot != null && collisionPivot.IsChildOf(visualPivot)) ||
            (collisionPivot != contactCollider.transform &&
             !contactCollider.transform.IsChildOf(collisionPivot));
        if (invalidHierarchy)
        {
            contactCollider.enabled = false;
            if (!warnedHierarchy)
            {
                warnedHierarchy = true;
                Debug.LogWarning("Blade contact requires a separate unscaled collision branch and a physical hilt transform.", this);
            }
            return;
        }

        // Separate 2D contact from the decorative projection/roll.
        collisionPivot.SetPositionAndRotation(transform.position, hilt.rotation);
        float reach = GroundReach;
        float length = Mathf.Max(contactWidth, reach);
        contactCollider.size = new Vector2(contactWidth, length);
        contactCollider.offset = new Vector2(0f, length * 0.5f);
        contactCollider.enabled = IsActive && Extension >= .95f &&
            reach >= Mathf.Max(minContactLength, contactWidth);
    }

    private static void UpdateRenderer(SpriteRenderer renderer,
        float projectedLength, float fullWidth, float extension)
    {
        if (renderer == null) return;
        renderer.enabled = extension > .001f;
        if (!renderer.enabled) return;

        float diameter = Mathf.Max(.001f, fullWidth) * extension;
        float visibleLength = Mathf.Max(projectedLength, diameter);
        Transform t = renderer.transform;
        Vector3 p = t.localPosition;
        p.x = 0f;
        p.y = projectedLength * .5f;
        t.localPosition = p;
        t.localRotation = Quaternion.identity;
        Vector2 bounds = renderer.sprite != null ? renderer.sprite.bounds.size : Vector2.one;
        t.localScale = new Vector3(
            diameter / Mathf.Max(.0001f, bounds.x),
            visibleLength / Mathf.Max(.0001f, bounds.y), t.localScale.z);
    }
}
