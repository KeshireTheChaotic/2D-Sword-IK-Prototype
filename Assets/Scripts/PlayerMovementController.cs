using UnityEngine;

/// <summary>
/// Top-down WASD locomotion with continuous mouse-relative turning. The aim vector
/// selects *desired* facing, never directly sets the rotation or weapon velocity.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovementController : MonoBehaviour
{
    public float runSpeed = 3f;
    [Tooltip("Base turn speed in degrees/second, reached at 90 degrees off the nose.")]
    public float turnSpeed = 100f;
    public float angularAcceleration = 350f;
    [Range(0f, 1f)] public float forwardMultiplier = .08f;
    public float rearMultiplier = 2f;
    public CombatStanceController combat;

    private Rigidbody2D body;
    private Vector2 movement;
    private Vector2 mouseDirection = Vector2.up;
    private float turnVelocity;
    private float lastTurnSign = 1f;

    private void Awake() { body = GetComponent<Rigidbody2D>(); }

    private void Update()
    {
        movement = Vector2.ClampMagnitude(new Vector2(
            Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
        Camera main = Camera.main;
        if (main == null) return;
        // ScreenToWorldPoint returns the camera's Z for an orthographic camera;
        // projecting to XY avoids an unintended rotation from depth.
        Vector3 point = main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 delta = (Vector2)point - (Vector2)transform.position;
        if (delta.sqrMagnitude > .0001f) mouseDirection = delta.normalized;
    }

    /// <summary>Continuous, smooth, bounded angular response at 0..180 degrees.</summary>
    public float TurnMultiplier(float absoluteAngle)
    {
        float canonical = AngleTurnResponse.EvaluateMultiplier(absoluteAngle);
        if (absoluteAngle <= 30f && !Mathf.Approximately(forwardMultiplier, .08f))
            return Mathf.Lerp(forwardMultiplier, .2f, Mathf.SmoothStep(0f, 1f, absoluteAngle / 30f));
        if (absoluteAngle >= 135f && !Mathf.Approximately(rearMultiplier, 2f))
            return Mathf.Lerp(1.5f, Mathf.Max(1.5f, rearMultiplier),
                Mathf.SmoothStep(0f, 1f, (absoluteAngle - 135f) / 45f));
        return canonical;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        body.linearVelocity = movement * runSpeed;
        // Calculate relative angle from the physics body's current facing.
        Vector2 forward = new Vector2(-Mathf.Sin(body.rotation * Mathf.Deg2Rad),
                                       Mathf.Cos(body.rotation * Mathf.Deg2Rad));
        float signedAngle = Vector2.SignedAngle(forward, mouseDirection);
        float absoluteAngle = Mathf.Abs(signedAngle);
        if (absoluteAngle > 1f && absoluteAngle < 178f)
            lastTurnSign = Mathf.Sign(signedAngle);
        float stateMultiplier = combat != null ? combat.BodyTurnMultiplier : 1f;
        turnVelocity = AngleTurnResponse.StepAngularVelocity(turnVelocity, signedAngle,
            turnSpeed, stateMultiplier, angularAcceleration, dt, lastTurnSign);
        if (Mathf.Abs(turnVelocity) > .0001f)
            body.MoveRotation(body.rotation + turnVelocity * dt);
    }
}
