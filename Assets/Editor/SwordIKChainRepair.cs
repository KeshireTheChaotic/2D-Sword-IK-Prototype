using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;

/// <summary>
/// Repairs the Unity 2018 -> 6.6 PSB importer migration: the sprite prefab's
/// generated IDs changed and SampleScene lost both IK effectors and shoulder
/// hinge components. This script binds by a verified bone hierarchy, never
/// by fragile generated Unity file IDs. It does not replace the sprite prefab.
/// </summary>
public static class SwordIKChainRepair
{
    public const string ScenePath = "Assets/Scenes/SampleScene.unity";
    public const string SpritePath = "Assets/Sprites/Sprite.psb";

    [MenuItem("Tools/Sword IK/Repair Arm Chains and Hinges")]
    public static void RepairScene()
    {
        RepairAndSave();
    }

    // Intended for Unity.exe -batchmode -quit -executeMethod SwordIKChainRepair.RepairBatch
    public static void RepairBatch()
    {
        RepairAndSave();
        Debug.Log("SWORD_IK_REPAIR_OK");
    }

    private static void RepairAndSave()
    {
        if (AssetDatabase.LoadMainAssetAtPath(SpritePath) == null)
            throw new InvalidOperationException("Sprite.psb failed to import. Resolve the PSD Importer before repairing IK.");

        bool openedHere;
        Scene scene = OpenTargetScene(out openedHere);
        try
        {
            GameObject player = FindSceneObject(scene, "Player");
            Transform rig = FindUniqueDescendant(player.transform, "RiggedSprite");
            IKManager2D manager = player.GetComponent<IKManager2D>();
            if (manager == null)
                throw new InvalidOperationException("Player is missing IKManager2D.");

            // Original rig from Unity 2018's imported Sprite.psb:
            // Left:  bone_1 / bone_3 / bone_4 / bone_5; flip=true
            // Right: bone_1 / bone_7 / bone_8 / bone_9; flip=false
            Transform leftShoulder = FindBoneByAncestry(rig, "bone_1", "bone_3");
            Transform leftHand = FindBoneByAncestry(rig, "bone_1", "bone_3", "bone_4", "bone_5");
            Transform rightShoulder = FindBoneByAncestry(rig, "bone_1", "bone_7");
            Transform rightHand = FindBoneByAncestry(rig, "bone_1", "bone_7", "bone_8", "bone_9");

            LimbSolver2D leftSolver = FindSolver(player.transform, "Left_IK");
            LimbSolver2D rightSolver = FindSolver(player.transform, "Right_IK");
            Transform leftTarget = FindUniqueDescendant(player.transform, "Left_IK_Target");
            Transform rightTarget = FindUniqueDescendant(player.transform, "Right_IK_Target");

            ConfigureChain(leftSolver, leftHand, leftTarget);
            ConfigureChain(rightSolver, rightHand, rightTarget);

            // Restore components originally added as overrides to the PSB instance.
            // The prefab reimport dropped these along with the old stripped bone IDs.
            EnsureHinge(leftShoulder, -110f, 50f);
            EnsureHinge(rightShoulder, -50f, 110f);

            if (!manager.solvers.Contains(leftSolver))
            {
                Undo.RecordObject(manager, "Reconnect left IK solver");
                manager.AddSolver(leftSolver);
                EditorUtility.SetDirty(manager);
            }
            if (!manager.solvers.Contains(rightSolver))
            {
                Undo.RecordObject(manager, "Reconnect right IK solver");
                manager.AddSolver(rightSolver);
                EditorUtility.SetDirty(manager);
            }

            ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the repaired SampleScene.");
            Debug.Log("Sword IK repaired and saved: both three-bone IK chains and shoulder hinge limits restored.");
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static Scene OpenTargetScene(out bool openedHere)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        openedHere = !scene.IsValid() || !scene.isLoaded;
        return openedHere ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : scene;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        GameObject result = null;
        foreach (GameObject obj in scene.GetRootGameObjects())
        {
            if (obj.name != name)
                continue;
            if (result != null)
                throw new InvalidOperationException("Multiple root objects named " + name);
            result = obj;
        }
        if (result == null)
            throw new InvalidOperationException("Missing scene root " + name);
        return result;
    }

    private static Transform FindUniqueDescendant(Transform root, string name)
    {
        Transform found = null;
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item.name != name)
                continue;
            if (found != null)
                throw new InvalidOperationException("Ambiguous transform named " + name + " under " + root.name);
            found = item;
        }
        if (found == null)
            throw new InvalidOperationException("Missing " + name + " under " + root.name + ". Check the sprite prefab import.");
        return found;
    }

    private static Transform FindBoneByAncestry(Transform rig, params string[] path)
    {
        Transform found = null;
        foreach (Transform candidate in rig.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name != path[path.Length - 1])
                continue;
            Transform item = candidate;
            bool matches = true;
            for (int i = path.Length - 1; i >= 0; --i)
            {
                if (item == null || item.name != path[i])
                {
                    matches = false;
                    break;
                }
                item = item.parent;
            }
            if (!matches)
                continue;
            if (found != null)
                throw new InvalidOperationException("Multiple imported bones match: " + string.Join("/", path));
            found = candidate;
        }
        if (found == null)
            throw new InvalidOperationException("Imported PSB bone not found: " + string.Join("/", path)
                + ". Check Sprite.psb and its rigged prefab; do not replace the bones with empty GameObjects.");
        return found;
    }

    private static LimbSolver2D FindSolver(Transform player, string name)
    {
        Transform obj = FindUniqueDescendant(player, name);
        LimbSolver2D result = obj.GetComponent<LimbSolver2D>();
        if (result == null)
            throw new InvalidOperationException(name + " has no LimbSolver2D.");
        return result;
    }

    private static void ConfigureChain(LimbSolver2D solver, Transform effector, Transform target)
    {
        IKChain2D chain = solver.GetChain(0);
        if (chain.effector != effector || chain.target != target || chain.transformCount != 3 || !solver.isValid)
        {
            Undo.RecordObject(solver, "Repair " + solver.name + " IK chain");
            chain.effector = effector;
            chain.target = target;
            chain.transformCount = 3;
            solver.Initialize();
            EditorUtility.SetDirty(solver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(solver);
        }
        if (!solver.isValid)
            throw new InvalidOperationException(solver.name + " still has an invalid IK chain after initialization.");
    }

    private static void EnsureHinge(Transform shoulder, float min, float max)
    {
        RotationLimitHinge hinge = shoulder.GetComponent<RotationLimitHinge>();
        if (hinge != null)
            return; // Respect an existing user's customized joint constraints.

        hinge = Undo.AddComponent<RotationLimitHinge>(shoulder.gameObject);
        hinge.axis = Vector3.forward;
        hinge.useLimits = true;
        hinge.min = min;
        hinge.max = max;
        hinge.zeroAxisDisplayOffset = 180f;
        hinge.defaultLocalRotation = shoulder.localRotation;
        EditorUtility.SetDirty(hinge);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hinge);
    }

    public static void ValidateScene(Scene scene)
    {
        GameObject player = FindSceneObject(scene, "Player");
        Transform rig = FindUniqueDescendant(player.transform, "RiggedSprite");
        Transform leftHand = FindBoneByAncestry(rig, "bone_1", "bone_3", "bone_4", "bone_5");
        Transform rightHand = FindBoneByAncestry(rig, "bone_1", "bone_7", "bone_8", "bone_9");
        Transform leftShoulder = FindBoneByAncestry(rig, "bone_1", "bone_3");
        Transform rightShoulder = FindBoneByAncestry(rig, "bone_1", "bone_7");
        LimbSolver2D left = FindSolver(player.transform, "Left_IK");
        LimbSolver2D right = FindSolver(player.transform, "Right_IK");
        IKManager2D manager = player.GetComponent<IKManager2D>();
        if (manager == null || manager.solvers.Count != 2 ||
            !manager.solvers.Contains(left) || !manager.solvers.Contains(right))
            throw new InvalidOperationException("The player must have exactly two registered IK solvers.");
        ValidateChain(left, leftHand, FindUniqueDescendant(player.transform, "Left_IK_Target"));
        ValidateChain(right, rightHand, FindUniqueDescendant(player.transform, "Right_IK_Target"));
        ValidateHinge(leftShoulder, -110f, 50f);
        ValidateHinge(rightShoulder, -50f, 110f);
        if (rig.GetComponentsInChildren<SpriteSkin>(true).Length < 2)
            throw new InvalidOperationException("PSB sprite skins were not imported. Check PSD Importer and Sprite.psb.");

        int missing = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                foreach (MonoBehaviour component in transform.GetComponents<MonoBehaviour>())
                    if (component == null)
                        ++missing;
        if (missing != 0)
            throw new InvalidOperationException("Missing MonoBehaviour references: " + missing);
    }

    private static void ValidateHinge(Transform shoulder, float expectedMin, float expectedMax)
    {
        RotationLimitHinge hinge = shoulder.GetComponent<RotationLimitHinge>();
        if (hinge == null)
            throw new InvalidOperationException("The original shoulder hinge component is missing from " + shoulder.name + ".");
        if (!hinge.useLimits || hinge.axis != Vector3.forward ||
            !Mathf.Approximately(hinge.min, expectedMin) || !Mathf.Approximately(hinge.max, expectedMax))
        {
            throw new InvalidOperationException(shoulder.name + " has malformed shoulder hinge settings. Expected axis (0, 0, 1), enabled limits "
                + expectedMin + " to " + expectedMax + "; existing settings were preserved.");
        }
    }

    private static void ValidateChain(LimbSolver2D solver, Transform expectedHand, Transform expectedTarget)
    {
        IKChain2D chain = solver.GetChain(0);
        if (chain.effector != expectedHand || chain.target != expectedTarget || chain.transformCount != 3)
            throw new InvalidOperationException(solver.name + " has incorrect effector, target, or chain count.");
        solver.Initialize();
        if (!solver.isValid || chain.transforms == null || chain.transforms.Length != 3 ||
            chain.transforms[2] != expectedHand)
            throw new InvalidOperationException(solver.name + " failed IK initialization.");
    }
}
