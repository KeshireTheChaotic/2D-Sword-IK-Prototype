using UnityEngine;

/// <summary>
/// Moves an IK hand target toward the combat hotspot nearest the mouse.
/// Kept as a Transform-driven IK target (no Rigidbody2D required).
/// </summary>
public class HandManager : MonoBehaviour
{
    public Transform[] targets;
    public float speed = 1f;
    public float minDistance = 0.05f;

    private void Update()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null || targets == null || targets.Length == 0)
            return;

        Transform closest = GetClosestTarget(activeCamera);
        if (closest == null)
            return;

        Vector3 destination = closest.position;
        Vector3 current = transform.position;
        if ((destination - current).sqrMagnitude <= minDistance * minDistance)
            return;

        // The original code moved toward a normalized direction vector,
        // not the destination world position. This caused incorrect targeting.
        transform.position = Vector3.MoveTowards(current, destination, speed * Time.deltaTime);
    }

    private Transform GetClosestTarget(Camera activeCamera)
    {
        Vector3 mouse = activeCamera.ScreenToWorldPoint(Input.mousePosition);
        Transform closest = null;
        float bestDistance = float.PositiveInfinity;

        foreach (Transform candidate in targets)
        {
            if (candidate == null)
                continue;

            float distance = (candidate.position - mouse).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }
}
