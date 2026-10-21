using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.IK;

/// <summary>Checks scene wiring only. Play Mode physics, timing, appearance,
/// collisions and script compilation must also be tested in Unity 6.6.</summary>
public static class SwordIKCombatValidation
{
    [MenuItem("Tools/Sword IK/Validate Stance Combat")]
    public static void Validate() { ValidateBatch(); }

    public static void ValidateBatch()
    {
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        try
        {
            ValidateScene(scene);
        }
        finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true); }
    }

    public static void ValidateScene(Scene scene)
    {
            GameObject player = scene.GetRootGameObjects().Single(x => x.name == "Player");
            GameObject hilt = scene.GetRootGameObjects().Single(x => x.name == "Lightsaber_Hilt");
            CombatStanceController stance = Require<CombatStanceController>(player);
            PlayerMovementController locomotion = Require<PlayerMovementController>(player);
            ArmPhysicsBlend arms = Require<ArmPhysicsBlend>(player);
            HandGripFollower follower = Require<HandGripFollower>(player);
            WeaponPoseMotor motor = Require<WeaponPoseMotor>(hilt);
            Rigidbody2D body = Require<Rigidbody2D>(hilt);
            Transform blade = hilt.transform.Find("Blade");
            if (blade == null) throw new InvalidOperationException("Missing separate Blade object");
            BladeVisualController visual = Require<BladeVisualController>(blade.gameObject);
            Transform pivot = blade.Find("BladeVisualPivot");
            Transform contact = blade.Find("BladeContact");
            if (contact == null || contact.GetComponent<BladeContactSensor>() == null ||
                contact.GetComponent<CapsuleCollider2D>() == null ||
                !contact.GetComponent<CapsuleCollider2D>().isTrigger)
                throw new InvalidOperationException("Blade contact trigger is missing");
            if (stance.weapon != motor || stance.movement != locomotion || locomotion.combat != stance ||
                stance.armPhysics != arms || motor.combat != stance || motor.character != player.transform ||
                motor.blade != visual || motor.GetComponent<Rigidbody2D>() != body)
                throw new InvalidOperationException("Combat controller reference mismatch");
            if (visual.core == null || visual.glow == null || visual.contactCollider == null ||
                visual.contactSensor == null || visual.contactTransform != contact ||
                pivot == null || visual.visualPivot != pivot ||
                visual.core.transform.parent != pivot || visual.glow.transform.parent != pivot)
                throw new InvalidOperationException("Blade sprites/contact wiring incomplete");
            if (arms.manager == null || arms.left.hand == null || arms.right.hand == null ||
                arms.left.solver == null || arms.right.solver == null ||
                arms.left.gripJoint == null || arms.right.gripJoint == null)
                throw new InvalidOperationException("Arm physics / IK wiring incomplete");
            if (motor.sharedGrip == null || follower.sharedGrip != motor.sharedGrip ||
                follower.leftTarget == null || follower.rightTarget == null)
                throw new InvalidOperationException("Hand grip follower mismatch");
            if (player.GetComponentsInChildren<HandManager>(true).Any(m => m.enabled))
                throw new InvalidOperationException("Old hotspot HandManager still enabled");
            if (hilt.transform.Find("SharedGrip") != motor.sharedGrip)
                throw new InvalidOperationException("Hilt must own exactly one shared grip");
            if (scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                .Count(x => x.name == "SharedGrip") != 1)
                throw new InvalidOperationException("Scene must contain exactly one SharedGrip");
            AssertSingle<CombatStanceController>(player);
            AssertSingle<ArmPhysicsBlend>(player);
            AssertSingle<HandGripFollower>(player);
            AssertSingle<WeaponPoseMotor>(hilt);
            AssertSingle<BladeVisualController>(blade.gameObject);
            AssertSingle<BladeContactSensor>(contact.gameObject);
            if (arms.left.gripJoint.connectedAnchor != arms.right.gripJoint.connectedAnchor)
                throw new InvalidOperationException("Arm ragdoll joints do not share the same grip anchor");
            if (locomotion.turnSpeed <= 0f || locomotion.angularAcceleration <= 0f)
                throw new InvalidOperationException("Invalid continuous body rotation tuning");
            // Validate canonical v3 timing without relying on mutable Inspector
            // values; then separately assert monotonicity for tuned parameters.
            float[] expected = { .120f, .155f, .190f, .225f, .260f, .280f };
            for (int count = 1; count <= expected.Length; count++)
            {
                float actual = CombatStanceController.CalculateParryWindow(count,
                    .120f, .035f, .280f);
                if (Mathf.Abs(actual - expected[count - 1]) > .0001f)
                    throw new InvalidOperationException("Parry timing formula incorrect on swing " + count);
            }
            float previousWindow = 0f;
            for (int count = 1; count <= 12; count++)
            {
                float actual = CombatStanceController.CalculateParryWindow(count,
                    stance.baseParryWindowSeconds, stance.addedWindowPerSwingSeconds,
                    stance.maxParryWindowSeconds);
                if (actual < previousWindow || actual > stance.maxParryWindowSeconds + .0001f)
                    throw new InvalidOperationException("Configured parry windows must be monotonic and capped");
                previousWindow = actual;
            }
            float prev = 0f;
            for (int angle = 0; angle <= 180; angle += 5)
            {
                float turn = locomotion.TurnMultiplier(angle);
                if (turn < prev - .0001f)
                    throw new InvalidOperationException("Body rotation speed must increase with angle");
                prev = turn;
            }
            SwordIKChainRepair.ValidateScene(scene);
            Debug.Log("SWORD_IK_COMBAT_VALIDATION_OK: v3 shared grip, angle-based turn, expanding parry window, hilt, blade, 2 IK arms and physical joints");
    }

    private static T Require<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        if (component == null) throw new InvalidOperationException(obj.name + " missing " + typeof(T).Name);
        return component;
    }

    private static void AssertSingle<T>(GameObject obj) where T : Component
    {
        if (obj.GetComponents<T>().Length != 1)
            throw new InvalidOperationException(obj.name + " must have exactly one " + typeof(T).Name);
    }
}
