using UnityEngine;

/// <summary>
/// Legacy hotspot follower. Disabled in the combat scene. Preserved for older
/// scenes, with camera depth excluded from 2D hotspot selection.
/// </summary>
public class HandManager : MonoBehaviour
{
    public Transform[] targets;
    public float speed = 1f;
    public float minDistance = .05f;

    private void Update()
    {
        Camera camera = Camera.main;
        if (camera == null || targets == null || targets.Length == 0) return;
        Transform closest = GetClosestTarget(camera);
        if (closest == null) return;
        Vector3 destination = closest.position;
        Vector3 current = transform.position;
        destination.z = current.z;
        if (((Vector2)(destination - current)).sqrMagnitude <= minDistance * minDistance) return;
        transform.position = Vector3.MoveTowards(current, destination, speed * Time.deltaTime);
    }

    private Transform GetClosestTarget(Camera camera)
    {
        Vector2 mouse = camera.ScreenToWorldPoint(Input.mousePosition);
        Transform closest = null;
        float best = float.PositiveInfinity;
        foreach (Transform t in targets)
        {
            if (t == null) continue;
            float distance = ((Vector2)t.position - mouse).sqrMagnitude;
            if (distance < best) { best = distance; closest = t; }
        }
        return closest;
    }
}
