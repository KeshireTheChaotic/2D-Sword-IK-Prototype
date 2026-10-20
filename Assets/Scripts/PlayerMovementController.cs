using UnityEngine;

/// <summary>
/// Mouse-facing 2D movement controller. Uses Unity's legacy Input Manager
/// (Horizontal/Vertical axes), which is kept enabled during the upgrade.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovementController : MonoBehaviour
{
    public float runSpeed = 1.0f;
    public float turnSpeed = 1.0f;

    private Rigidbody2D body;
    private Vector2 movement;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Read input in Update, then apply it to physics in FixedUpdate.
        // normalized prevents diagonal movement from exceeding runSpeed.
        movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        movement = Vector2.ClampMagnitude(movement, 1f);

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return;

        Vector3 mouseWorld = activeCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector3 target = mouseWorld - transform.position;
        if (target.sqrMagnitude <= 0.000001f)
            return;

        float angle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg + 90f;
        Quaternion desiredRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        float speedFactor = Vector3.Dot(transform.up, target) < 0f ? 0.5f : 1f;
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, desiredRotation, turnSpeed * speedFactor);
    }

    private void FixedUpdate()
    {
        // Unity 6 uses Rigidbody2D.linearVelocity (formerly velocity).
        body.linearVelocity = movement * runSpeed;
    }
}
