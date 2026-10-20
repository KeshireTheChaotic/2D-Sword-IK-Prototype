using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.IK;

/// <summary>
/// Repeatable smoke test for the original scene after Unity 6.6 imports it.
/// Run from Tools > Sword IK > Validate Upgrade or -executeMethod SwordIKUpgradeValidation.Validate.
/// This intentionally doesn't save or rewrite the scene/prefab.
/// </summary>
public static class SwordIKUpgradeValidation
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SpritePath = "Assets/Sprites/Sprite.psb";

    [MenuItem("Tools/Sword IK/Validate Upgrade")]
    public static void Validate()
    {
        if (AssetDatabase.LoadMainAssetAtPath(SpritePath) == null)
            throw new InvalidOperationException("PSB importer did not produce the sprite asset: " + SpritePath);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            List<IKManager2D> managers = new List<IKManager2D>();
            List<LimbSolver2D> solvers = new List<LimbSolver2D>();
            int missingScripts = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                managers.AddRange(root.GetComponentsInChildren<IKManager2D>(true));
                solvers.AddRange(root.GetComponentsInChildren<LimbSolver2D>(true));
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    foreach (MonoBehaviour script in child.GetComponents<MonoBehaviour>())
                    {
                        if (script == null)
                            ++missingScripts;
                    }
                }
            }

            if (missingScripts > 0)
                throw new InvalidOperationException("Missing MonoBehaviour scripts in SampleScene: " + missingScripts);
            if (managers.Count != 1)
                throw new InvalidOperationException("Expected one IKManager2D, found " + managers.Count);
            if (solvers.Count != 2)
                throw new InvalidOperationException("Expected two LimbSolver2D components, found " + solvers.Count);
            if (managers[0].solvers.Count != 2)
                throw new InvalidOperationException("IKManager2D lost solver references during the upgrade");

            foreach (LimbSolver2D solver in solvers)
            {
                IKChain2D chain = solver.GetChain(0);
                if (chain.effector == null || chain.target == null)
                    throw new InvalidOperationException("IK chain effector/target unresolved on " + solver.name);
                solver.Initialize();
                if (!solver.isValid)
                    throw new InvalidOperationException("IK solver chain invalid on " + solver.name);
                if (!managers[0].solvers.Contains(solver))
                    throw new InvalidOperationException("IK manager does not reference " + solver.name);
            }

            Debug.Log("Sword IK Unity 6.6 upgrade validation passed: 1 manager, 2 valid limb solvers, PSB imported, no missing scene scripts.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
