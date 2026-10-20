using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity 6.6 import smoke test. Non-destructive; does not save the scene.
/// </summary>
public static class SwordIKUpgradeValidation
{
    [MenuItem("Tools/Sword IK/Validate Upgrade")]
    public static void Validate()
    {
        if (AssetDatabase.LoadMainAssetAtPath(SwordIKChainRepair.SpritePath) == null)
            throw new InvalidOperationException("Sprite.psb did not import.");

        bool openedHere;
        Scene scene = SwordIKChainRepair.OpenTargetScene(out openedHere);
        try
        {
            SwordIKChainRepair.ValidateScene(scene);
            Debug.Log("SWORD_IK_VALIDATION_OK: both chains, manager, targets, shoulder hinges, imported SpriteSkins and scene scripts valid.");
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
