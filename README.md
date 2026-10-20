# 2D Sword IK Prototype

A 2D sword / arm inverse-kinematics prototype. The original project was authored with Unity **2018.3.6f1** and 2018 preview packages. This migration targets **Unity 6000.6.6f1** (Unity 6.6, released 2026-10-15).

## Requirements

- Unity Hub + Unity Editor **6000.6.6f1**.
- The project uses the **Built-in Render Pipeline** and Unity's **legacy Input Manager** axes (`Horizontal`, `Vertical`) and mouse input. Keep **Active Input Handling = Input Manager (Old)** or **Both** if Unity prompts.
- Official Unity 6.6 packages: **2D Animation 16.0.1** and **PSD Importer 15.0.1**. IK ships inside 2D Animation; the obsolete `com.unity.2d.ik` package is deliberately removed.
- Git LFS (`git lfs install && git lfs pull`) for the original binary assets.

## Upgrade / validation

1. Work on a separate branch. Ensure Git LFS has fetched the original files.
2. Open this project via Unity Hub using **6000.6.6f1**. Let Package Manager resolve dependencies, import the PSB, and upgrade scene/project serialization. Commit Unity-generated project setting upgrades and `Packages/packages-lock.json` separately after inspection.
3. Open **Assets/Scenes/SampleScene.unity** and run **Tools > Sword IK > Validate Upgrade**. This verifies 1 IK manager, 2 limb solvers, valid effector/target chains, absence of missing MonoBehaviours, and PSB importer success. Run the same check in batchmode with:

   `Unity.exe -batchmode -quit -projectPath "<project-path>" -executeMethod SwordIKUpgradeValidation.Validate -logFile "<project-path>/upgrade-validation.log"`

4. Enter Play mode. Confirm arrow/WASD movement, mouse rotation, both hand targets, the character sprite, 2D IK deformation, and hinge limits. Review any migration warnings.
5. Commit only after passing the Editor test. The legacy GitHub-for-Unity Editor plug-in was removed because it bundles Unity-2018-era editor assemblies; use GitHub Desktop or native Git instead.

## Historical assets and known migration risks

- **Preserved:** `Assets/Sprites/Sprite.psb` and `.meta`, `Assets/Scenes/SampleScene.unity`, existing bone bindings, scene object references, IK script GUIDs, and hinge components.
- **The package's IKManager2D and LimbSolver2D script GUIDs remain identical to the old scene GUIDs.** Their serialized IK fields remain supported. Nevertheless, the 2018 PSB importer can regenerate different internal prefab file IDs when upgraded to 15.x; inspect the scene after reimport before saving.
- Rotation limit inspector scripts are intentionally retained; Unity's editor must compile them before their behavior can be confirmed.
- This source-only migration cannot demonstrate successful Unity Editor import, animation/IK runtime correctness, or build success without running the Unity 6.6 Editor.
- Unity package lockfile is **not** fabricated. Unity writes a resolved lockfile when the project is actually imported.

## Sources

- Unity 6000.6.6f1: https://unity.com/releases/editor/whats-new/6000.6.6f1
- Unity 6.6 2D Animation: https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/packages-all/pack-safe/com-unity-2d-animation
- Unity 6.6 PSD Importer: https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/packages-all/pack-safe/com-unity-2d-psdimporter
