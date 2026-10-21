using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// In-place, idempotent migration for the pushed master scene. Reuses the
/// existing Unity objects, references, PSB bones and .meta GUIDs. Never creates
/// a replacement weapon or a second IK rig.
/// </summary>
public static class SwordIKXYSceneRepair
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const float Tolerance = .001f;

    [MenuItem("Tools/Sword IK/Apply XY Combat Fix")]
    public static void ApplyMenu()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Save scene changes first",
                    "SampleScene has unsaved changes. Save them and rerun this fix so there is a clean rollback point.",
                    "OK");
                return;
            }
            ApplyToScene(scene, true);
            ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save repaired SampleScene.");
            Debug.Log("SWORD_IK_XY_FIX_OK: unscaled physical blade, world-root hilt, 90-degree idle pose and BoxCollider2D obstacle.");
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Tools/Sword IK/Validate XY Combat Geometry")]
    public static void ValidateMenu()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            ValidateScene(scene);
            Debug.Log("SWORD_IK_XY_VALIDATION_OK");
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// Safe to invoke before or after the combat installer. When requireHilt is
    /// false, an uninstalled original IK scene is a no-op.
    /// </summary>
    public static bool ApplyToScene(Scene scene, bool requireHilt = false)
    {
        GameObject player = FindGameObject(scene, "Player");
        GameObject hiltObject = FindGameObject(scene, "Lightsaber_Hilt");
        if (player == null) throw new InvalidOperationException("SampleScene is missing Player.");
        if (hiltObject == null)
        {
            if (requireHilt) throw new InvalidOperationException("Scene has no Lightsaber_Hilt to repair.");
            ConvertLegacyCube(scene);
            return false;
        }

        Transform hilt = hiltObject.transform;
        Transform visual = FindDescendant(hilt, "HiltVisual");
        Transform blade = FindDescendant(hilt, "Blade");
        Transform shared = FindDescendant(hilt, "SharedGrip");
        Transform visualPivot = FindDescendant(hilt, "BladeVisualPivot");
        Transform contact = FindDescendant(hilt, "BladeContact");
        WeaponPoseMotor motor = hilt.GetComponent<WeaponPoseMotor>();
        BladeVisualController bladeController = blade != null ? blade.GetComponent<BladeVisualController>() : null;
        Collider2D collider = hilt.GetComponent<Collider2D>();
        if (visual == null || blade == null || shared == null || visualPivot == null || contact == null ||
            motor == null || bladeController == null || collider == null ||
            hilt.GetComponent<Rigidbody2D>() == null)
            throw new InvalidOperationException("Existing combat objects are incomplete. Do not recreate them automatically; restore scene references first.");

        // Capture what was wrong before normalizing; do not overwrite custom
        // visual dimensions when the hierarchy is already correct.
        bool bladeNestedUnderVisual = blade.IsChildOf(visual);
        bool visualNestedUnderGrip = visual.IsChildOf(shared);
        bool oldHierarchy = bladeNestedUnderVisual || visualNestedUnderGrip || hilt.parent != null;
        Vector3 worldHiltPosition = hilt.position;
        Quaternion worldHiltRotation = hilt.rotation;

        if (hilt.parent != null)
        {
            Undo.SetTransformParent(hilt, null, "Move independent hilt to scene root");
            hilt.SetPositionAndRotation(worldHiltPosition, worldHiltRotation);
        }

        MoveToParent(visual, hilt);
        MoveToParent(shared, hilt);
        MoveToParent(blade, hilt);
        MoveToParent(visualPivot, blade);
        MoveToParent(contact, blade);

        Record(visual, "Normalize visual transform");
        if (oldHierarchy)
        {
            visual.localPosition = new Vector3(0f, -.15f, 0f);
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(.14f, .35f, 1f);
        }

        Record(shared, "Normalize physical grip point");
        if (oldHierarchy || shared.localPosition.sqrMagnitude < .000001f)
            shared.localPosition = new Vector3(0f, -.07f, 0f);
        shared.localRotation = Quaternion.identity;
        shared.localScale = Vector3.one;

        ResetLocal(blade);
        ResetLocal(visualPivot);
        ResetLocal(contact);

        Record(bladeController, "Bind blade to physics hilt");
        bladeController.hiltTransform = hilt;
        bladeController.contactTransform = contact;
        Record(motor, "Correct idle pose and blade references");
        motor.idleElevation = 90f;
        motor.sharedGrip = shared;
        motor.blade = bladeController;
        motor.character = player.transform;

        Record(collider, "Enable dropped hilt collision");
        collider.enabled = true;
        if (collider is CapsuleCollider2D capsule)
            capsule.isTrigger = false;

        ConvertLegacyCube(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static void MoveToParent(Transform child, Transform parent)
    {
        if (child.parent != parent)
            Undo.SetTransformParent(child, parent, "Normalize saber object hierarchy");
    }

    private static void ResetLocal(Transform t)
    {
        Record(t, "Set unscaled XY combat transform");
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    private static void ConvertLegacyCube(Scene scene)
    {
        GameObject cube = FindGameObject(scene, "Cube");
        if (cube == null) return;
        BoxCollider old = cube.GetComponent<BoxCollider>();
        if (old == null) return;
        BoxCollider2D replacement = cube.GetComponent<BoxCollider2D>();
        if (replacement == null) replacement = Undo.AddComponent<BoxCollider2D>(cube);
        Record(replacement, "Use XY obstacle collider");
        replacement.size = new Vector2(old.size.x, old.size.y);
        replacement.offset = new Vector2(old.center.x, old.center.y);
        replacement.isTrigger = old.isTrigger;
        Undo.DestroyObjectImmediate(old);
    }

    public static void ValidateScene(Scene scene)
    {
        GameObject player = FindGameObject(scene, "Player");
        GameObject hiltObject = FindGameObject(scene, "Lightsaber_Hilt");
        if (player == null || hiltObject == null)
            throw new InvalidOperationException("Player or Lightsaber_Hilt missing.");
        Transform hilt = hiltObject.transform;
        if (hilt.parent != null || !ContainsSceneRoot(scene, hiltObject))
            throw new InvalidOperationException("Lightsaber_Hilt must be an independent scene root.");
        if (hilt.GetComponent<Rigidbody2D>() == null)
            throw new InvalidOperationException("Lightsaber_Hilt has no Rigidbody2D.");
        if (hilt.GetComponent<Collider2D>() == null || !hilt.GetComponent<Collider2D>().enabled)
            throw new InvalidOperationException("Dropped hilt needs an enabled Collider2D.");
        if (!Near(hilt.localScale, Vector3.one))
            throw new InvalidOperationException("Physical hilt root must be unscaled.");

        Transform blade = RequireDirect(hilt, "Blade");
        Transform visual = RequireDirect(hilt, "HiltVisual");
        Transform grip = RequireDirect(hilt, "SharedGrip");
        Transform pivot = RequireDirect(blade, "BladeVisualPivot");
        Transform contact = RequireDirect(blade, "BladeContact");
        RequireDirect(pivot, "BladeCore");
        RequireDirect(pivot, "BladeGlow");
        if (visual.IsChildOf(grip) || blade.IsChildOf(visual) || contact.IsChildOf(visual))
            throw new InvalidOperationException("Visual transforms cannot parent the physics blade.");
        foreach (Transform t in new[] { blade, pivot, contact, grip })
            if (!Near(t.localScale, Vector3.one))
                throw new InvalidOperationException(t.name + " must have unit local scale.");

        BladeVisualController controller = blade.GetComponent<BladeVisualController>();
        WeaponPoseMotor motor = hilt.GetComponent<WeaponPoseMotor>();
        if (controller == null || motor == null || controller.hiltTransform != hilt ||
            controller.contactTransform != contact || motor.blade != controller || motor.sharedGrip != grip)
            throw new InvalidOperationException("Saber controller or shared-grip references invalid.");
        if (Mathf.Abs(motor.idleElevation - 90f) > Tolerance)
            throw new InvalidOperationException("Idle elevation must be exactly 90 degrees.");
        CapsuleCollider2D hitbox = contact.GetComponent<CapsuleCollider2D>();
        if (hitbox == null || !hitbox.isTrigger || controller.contactCollider != hitbox)
            throw new InvalidOperationException("Blade contact must have its own trigger CapsuleCollider2D.");
        GameObject cube = FindGameObject(scene, "Cube");
        if (cube != null && (cube.GetComponent<BoxCollider>() != null ||
                             cube.GetComponent<BoxCollider2D>() == null))
            throw new InvalidOperationException("Cube obstacle must use BoxCollider2D, not BoxCollider.");
        if (player.transform == hilt.parent)
            throw new InvalidOperationException("Player must not parent the weapon.");
    }

    private static bool ContainsSceneRoot(Scene scene, GameObject obj)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root == obj) return true;
        return false;
    }

    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < Tolerance * Tolerance;

    private static Transform RequireDirect(Transform parent, string name)
    {
        Transform found = null;
        foreach (Transform child in parent)
            if (child.name == name)
            {
                if (found != null) throw new InvalidOperationException("Duplicate " + name + " under " + parent.name);
                found = child;
            }
        if (found == null) throw new InvalidOperationException(parent.name + " has no direct child " + name);
        return found;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform result = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name)
            {
                if (result != null) throw new InvalidOperationException("Duplicate descendant named " + name);
                result = t;
            }
        return result;
    }

    private static GameObject FindGameObject(Scene scene, string name)
    {
        GameObject result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                {
                    if (result != null)
                        throw new InvalidOperationException("Multiple scene objects named " + name + ". No automatic duplicate removal performed.");
                    result = t.gameObject;
                }
        return result;
    }

    private static void Record(UnityEngine.Object obj, string operation) => Undo.RecordObject(obj, operation);
}
