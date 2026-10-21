using UnityEngine;

/// <summary>
/// XY top-down locomotion and bounded, angle-dependent Z-axis body turning.
/// Local +Y is gameplay forward on the current master scene; RiggedSprite is
/// separately rotated 180 degrees to align its artwork with that convention.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovementController : MonoBehaviour
{
    public float runSpeed = 3f;
    [Tooltip("Base degrees per second at 90 degrees off facing.")]
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
        Camera camera = Camera.main;
        if (camera == null) return;
        // Ignore the camera's Z depth in all aim and stance calculations.
        Vector2 delta = (Vector2)camera.ScreenToWorldPoint(Input.mousePosition) - body.position;
        if (delta.sqrMagnitude > .0001f) mouseDirection = delta.normalized;
    }

    public float TurnMultiplier(float absoluteAngle)
    {
        float angle = Mathf.Clamp(Mathf.Abs(absoluteAngle), 0f, 180f);
        float canonical = AngleTurnResponse.EvaluateMultiplier(angle);
        if (angle <= 30f && !Mathf.Approximately(forwardMultiplier, .08f))
            return Mathf.Lerp(forwardMultiplier, .2f, Mathf.SmoothStep(0f, 1f, angle / 30f));
        if (angle >= 135f && !Mathf.Approximately(rearMultiplier, 2f))
            return Mathf.Lerp(1.5f, Mathf.Max(1.5f, rearMultiplier),
                Mathf.SmoothStep(0f, 1f, (angle - 135f) / 45f));
        return canonical;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        body.linearVelocity = movement * runSpeed;
        float r = body.rotation * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
        float signed = Vector2.SignedAngle(forward, mouseDirection);
        float absolute = Mathf.Abs(signed);
        if (absolute > 1f && absolute < 178f)
            lastTurnSign = Mathf.Sign(signed);

        float stateMultiplier = combat != null ? combat.BodyTurnMultiplier : 1f;
        float speedLimit = Mathf.Max(0f, turnSpeed) * TurnMultiplier(absolute) *
            Mathf.Max(0f, stateMultiplier);
        float sign = AngleTurnResponse.ResolveTurnSign(signed, lastTurnSign);
        float desiredVelocity = absolute < .1f ? 0f : sign * Mathf.Min(speedLimit, absolute / dt);
        turnVelocity = Mathf.MoveTowards(turnVelocity, desiredVelocity,
            Mathf.Max(0f, angularAcceleration) * dt);
        if (Mathf.Abs(turnVelocity) > .0001f)
            body.MoveRotation(body.rotation + turnVelocity * dt);
    }
}
