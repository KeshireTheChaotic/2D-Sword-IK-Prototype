using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.IK;

/// <summary>Idempotent scene setup for the Unity 6.6 prototype. Creates a
/// separate physics-driven Hilt and extensible Blade, one SHARED grip target,
/// and articulated guard-break arms. Never deletes/overwrites the imported PSB.</summary>
public static class SwordIKCombatInstaller
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string WhiteSpritePath = "Assets/Generated/SaberPixel.png";

    [MenuItem("Tools/Sword IK/Install Stance Combat + Lightsaber")]
    public static void InstallMenu() { Install(); }

    public static void InstallBatch() { Install(); Debug.Log("SWORD_IK_COMBAT_INSTALL_OK"); }

    public static void Install()
    {
        // Uses the existing migration repair; it validates PSB bone ancestry and
        // restores exactly the rig originally authored before the 2018->6.6 import.
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("SampleScene has unsaved changes. Save or revert it before running the combat installer.");
            SwordIKChainRepair.ValidateScene(scene);
            GameObject player = FindRoot(scene, "Player");
            PlayerMovementController locomotion = Required<PlayerMovementController>(player);
            // Legacy SampleScene serialized turnSpeed = 4 degrees/frame.
            // Upgrade that value to a time-based pivot speed in degrees/second.
            if (locomotion.turnSpeed < 20f) locomotion.turnSpeed = 100f;
            CombatStanceController combat = Ensure<CombatStanceController>(player);
            ArmPhysicsBlend arms = Ensure<ArmPhysicsBlend>(player);
            HandGripFollower follower = Ensure<HandGripFollower>(player);
            IKManager2D manager = Required<IKManager2D>(player);
            Rigidbody2D playerBody = Required<Rigidbody2D>(player);
            if (player.GetComponent<Collider2D>() == null)
            {
                CircleCollider2D collider = Undo.AddComponent<CircleCollider2D>(player);
                collider.radius = .28f;
            }

            Transform leftTarget = Unique(player.transform, "Left_IK_Target");
            Transform rightTarget = Unique(player.transform, "Right_IK_Target");
            foreach (HandManager oldFollower in player.GetComponentsInChildren<HandManager>(true))
            {
                Undo.RecordObject(oldFollower, "Disable old hotspot follower");
                oldFollower.enabled = false;
            }

            GameObject hilt = GetOrCreateRoot(scene, "Lightsaber_Hilt");
            Rigidbody2D weaponBody = Ensure<Rigidbody2D>(hilt);
            weaponBody.bodyType = RigidbodyType2D.Dynamic;
            weaponBody.mass = .6f;
            weaponBody.gravityScale = 0f;
            weaponBody.linearDamping = 4f;
            weaponBody.angularDamping = 4f;
            weaponBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (hilt.GetComponent<Collider2D>() == null)
            {
                CapsuleCollider2D collider = Undo.AddComponent<CapsuleCollider2D>(hilt);
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(.13f, .28f);
                collider.offset = new Vector2(0f, -.15f);
            }
            if (hilt.transform.position.sqrMagnitude < .000001f)
            {
                hilt.transform.position = player.transform.TransformPoint(new Vector3(0f, .32f, 0f));
                hilt.transform.rotation = player.transform.rotation;
            }

            Sprite pixel = EnsurePixelSprite();
            SpriteRenderer hiltVisual = Ensure<SpriteRenderer>(GetChild(hilt.transform, "HiltVisual").gameObject);
            hiltVisual.sprite = pixel;
            hiltVisual.color = new Color(.42f,.47f,.53f,1f);
            hiltVisual.sortingOrder = 24;
            hiltVisual.transform.localPosition = new Vector3(0f,-.15f,0f);
            hiltVisual.transform.localScale = new Vector3(.14f,.35f,1f);

            Transform bladeTransform = GetChild(hilt.transform,"Blade");
            BladeVisualController blade = Ensure<BladeVisualController>(bladeTransform.gameObject);
            Transform visualPivot = GetChild(bladeTransform, "BladeVisualPivot");
            Transform glowTransform = FindOrReparentChild(bladeTransform, visualPivot, "BladeGlow");
            Transform coreTransform = FindOrReparentChild(bladeTransform, visualPivot, "BladeCore");
            SpriteRenderer glow = Ensure<SpriteRenderer>(glowTransform.gameObject);
            SpriteRenderer core = Ensure<SpriteRenderer>(coreTransform.gameObject);
            glow.sprite = pixel;
            core.sprite = pixel;
            glow.color = new Color(.05f,.85f,.56f,.31f);
            core.color = new Color(.73f,1f,.92f,1f);
            glow.sortingOrder = 25;
            core.sortingOrder = 26;

            // Contact is a separate object. It follows hilt yaw in the ground
            // plane while the visible blade uses virtual elevation and tilt.
            Transform contact = FindOrReparentChild(hilt.transform, bladeTransform, "BladeContact");
            CapsuleCollider2D trigger = Ensure<CapsuleCollider2D>(contact.gameObject);
            trigger.direction = CapsuleDirection2D.Vertical;
            trigger.isTrigger = true;
            BladeContactSensor sensor = Ensure<BladeContactSensor>(contact.gameObject);
            // Both hands share one actual point on the hilt. There are no
            // independent grip locations and no nearest-hotspot selection.
            Transform sharedGrip = GetChild(hilt.transform,"SharedGrip");
            sharedGrip.localPosition = new Vector3(0f,-.07f,0f);
            sharedGrip.localRotation = Quaternion.identity;
            Transform commonRest = GetChild(player.transform,"UnarmedSharedRest");
            commonRest.localPosition = new Vector3(0f,.24f,0f);

            WeaponPoseMotor motor = Ensure<WeaponPoseMotor>(hilt);
            motor.character = player.transform;
            motor.combat = combat;
            motor.sharedGrip = sharedGrip;
            motor.blade = blade;
            blade.core = core;
            blade.glow = glow;
            blade.visualPivot = visualPivot;
            blade.contactCollider = trigger;
            blade.contactTransform = contact;
            blade.contactSensor = sensor;
            sensor.weapon = motor;
            sensor.owner = combat;

            combat.weapon = motor;
            combat.armPhysics = arms;
            combat.movement = locomotion;
            locomotion.combat = combat;
            arms.manager = manager;

            LimbSolver2D leftSolver = Required<LimbSolver2D>(Unique(player.transform,"Left_IK").gameObject);
            LimbSolver2D rightSolver = Required<LimbSolver2D>(Unique(player.transform,"Right_IK").gameObject);
            Transform rig = Unique(player.transform,"RiggedSprite");
            Transform leftUpper = ExactBone(rig,"bone_1","bone_3");
            Transform leftFore = ExactBone(rig,"bone_1","bone_3","bone_4");
            Transform leftHand = ExactBone(rig,"bone_1","bone_3","bone_4","bone_5");
            Transform rightUpper = ExactBone(rig,"bone_1","bone_7");
            Transform rightFore = ExactBone(rig,"bone_1","bone_7","bone_8");
            Transform rightHand = ExactBone(rig,"bone_1","bone_7","bone_8","bone_9");
            ConfigureArm(arms.left,leftSolver,leftUpper,leftFore,leftHand,playerBody,weaponBody,sharedGrip, -110f,50f);
            ConfigureArm(arms.right,rightSolver,rightUpper,rightFore,rightHand,playerBody,weaponBody,sharedGrip,-50f,110f);

            follower.leftTarget = leftTarget;
            follower.rightTarget = rightTarget;
            follower.sharedGrip = sharedGrip;
            follower.commonRest = commonRest;
            follower.arms = arms;
            follower.combat = combat;

            EditorUtility.SetDirty(combat);
            EditorUtility.SetDirty(motor);
            EditorUtility.SetDirty(blade);
            EditorUtility.SetDirty(sensor);
            EditorUtility.SetDirty(arms);
            EditorUtility.SetDirty(follower);
            EditorUtility.SetDirty(locomotion);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save scene after combat installation");
            SwordIKCombatValidation.ValidateScene(scene);
            Debug.Log("Stance lightsaber v3 installed: single shared grip, angle-dependent body turn, alternating combo, expanding physical parry windows, independent hilt/blade and ragdoll guard breaks. Test Play mode.");
        }
        finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true); }
    }

    private static void ConfigureArm(ArmPhysicsBlend.ArmChain arm, LimbSolver2D solver,
        Transform upper, Transform fore, Transform hand, Rigidbody2D playerBody,
        Rigidbody2D hiltBody, Transform grip, float min, float max)
    {
        arm.solver = solver;
        arm.shoulderLimit = upper.GetComponent<RotationLimitHinge>();
        arm.upperArm = ArmBody(upper);
        arm.forearm = ArmBody(fore);
        arm.hand = ArmBody(hand);
        arm.shoulderJoint = Hinge(upper,playerBody,min,max);
        arm.elbowJoint = Hinge(fore,arm.upperArm,-145f,30f);
        arm.wristJoint = Hinge(hand,arm.forearm,-80f,80f);
        arm.gripJoint = Ensure<FixedJoint2D>(hand.gameObject);
        arm.gripJoint.autoConfigureConnectedAnchor = false;
        arm.gripJoint.connectedBody = hiltBody;
        arm.gripJoint.anchor = Vector2.zero;
        arm.gripJoint.connectedAnchor = grip.localPosition;
        arm.gripJoint.enableCollision = false;
        arm.gripJoint.enabled = false;
    }

    private static Rigidbody2D ArmBody(Transform bone)
    {
        Rigidbody2D body = Ensure<Rigidbody2D>(bone.gameObject);
        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = false;
        body.gravityScale = 0f;
        body.mass = .15f;
        body.linearDamping = 1.6f;
        body.angularDamping = 2.6f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        return body;
    }

    private static HingeJoint2D Hinge(Transform bone,Rigidbody2D connected,float min,float max)
    {
        HingeJoint2D joint = Ensure<HingeJoint2D>(bone.gameObject);
        joint.connectedBody = connected;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = connected.transform.InverseTransformPoint(bone.position);
        joint.enableCollision = false;
        joint.useLimits = true;
        joint.limits = new JointAngleLimits2D { min=min,max=max };
        return joint;
    }

    private static Sprite EnsurePixelSprite()
    {
        string absolutePixelPath = Path.Combine(Application.dataPath, "Generated", "SaberPixel.png");
        if (!File.Exists(absolutePixelPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePixelPath));
            Texture2D tex = new Texture2D(8,8,TextureFormat.RGBA32,false);
            Color32[] pixels = new Color32[64];
            for(int i=0;i<pixels.Length;i++) pixels[i] = new Color32(255,255,255,255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(absolutePixelPath,tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(WhiteSpritePath,ImportAssetOptions.ForceUpdate);
        }
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
        if (importer.textureType != TextureImporterType.Sprite ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 8f) || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 8f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        if (sprite == null) throw new InvalidOperationException("Could not import generated saber pixel sprite");
        return sprite;
    }

    private static GameObject FindRoot(Scene scene,string name)
    {
        foreach(GameObject root in scene.GetRootGameObjects()) if(root.name==name) return root;
        throw new InvalidOperationException("Scene root not found: "+name);
    }
    private static GameObject GetOrCreateRoot(Scene scene,string name)
    {
        GameObject found = null;
        foreach(GameObject root in scene.GetRootGameObjects()) if(root.name==name)
        {
            if (found != null) throw new InvalidOperationException("Duplicate scene root: " + name);
            found = root;
        }
        if (found != null) return found;
        GameObject obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj,"Create "+name);
        SceneManager.MoveGameObjectToScene(obj,scene);
        return obj;
    }
    private static Transform GetChild(Transform parent,string name)
    {
        Transform t = null;
        for (int i = 0; i < parent.childCount; ++i)
            if (parent.GetChild(i).name == name)
            {
                if (t != null) throw new InvalidOperationException("Duplicate child " + name + " under " + parent.name);
                t = parent.GetChild(i);
            }
        if(t!=null) return t;
        GameObject obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj,"Create "+name);
        obj.transform.SetParent(parent,false);
        return obj.transform;
    }
    private static Transform FindOrReparentChild(Transform legacyParent, Transform desiredParent, string name)
    {
        Transform existing = desiredParent.Find(name);
        Transform legacy = legacyParent.Find(name);
        if (existing != null && legacy != null && existing != legacy)
            throw new InvalidOperationException("Duplicate hierarchy object: " + name);
        Transform result = existing != null ? existing : legacy;
        if (result == null) result = GetChild(desiredParent, name);
        else if (result.parent != desiredParent)
        {
            Undo.SetTransformParent(result, desiredParent, "Normalize combat hierarchy");
            result.localPosition = Vector3.zero;
            result.localRotation = Quaternion.identity;
        }
        return result;
    }
    private static Transform Unique(Transform root,string name)
    {
        Transform found = null;
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            if(t.name==name) { if(found!=null) throw new InvalidOperationException("Ambiguous "+name); found=t; }
        if(found==null) throw new InvalidOperationException("Missing "+name);
        return found;
    }
    private static Transform ExactBone(Transform root,params string[] path)
    {
        Transform found = null;
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if(t.name!=path[path.Length-1]) continue;
            Transform p=t; bool matches=true;
            for(int i=path.Length-1;i>=0;i--) { if(p==null||p.name!=path[i]) { matches=false;break; } p=p.parent; }
            if (!matches) continue;
            if(found!=null) throw new InvalidOperationException("Ambiguous bone "+string.Join("/",path));
            found=t;
        }
        if(found==null) throw new InvalidOperationException("Missing bone "+string.Join("/",path));
        return found;
    }
    private static T Ensure<T>(GameObject obj) where T:Component
    {
        T[] components = obj.GetComponents<T>();
        if (components.Length > 1)
            throw new InvalidOperationException(obj.name + " has duplicate " + typeof(T).Name + " components");
        T component = components.Length == 1 ? components[0] : null;
        return component != null ? component : Undo.AddComponent<T>(obj);
    }
    private static T Required<T>(GameObject obj) where T:Component
    {
        T component = obj.GetComponent<T>();
        if(component==null) throw new InvalidOperationException(obj.name+" missing "+typeof(T).Name);
        return component;
    }
}
