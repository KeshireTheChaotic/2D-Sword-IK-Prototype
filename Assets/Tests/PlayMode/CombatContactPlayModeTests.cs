using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatContactPlayModeTests
{
    private sealed class Fighter
    {
        public GameObject root;
        public CombatStanceController combat;
        public WeaponPoseMotor weapon;
        public BladeVisualController blade;
        public Rigidbody2D hiltBody;
        public CapsuleCollider2D contact;
    }

    [UnityTest]
    public IEnumerator StaticGuardBlocksOnlyWhenBladesActuallyContact()
    {
        Fighter attacker = CreateFighter("Attacker", new Vector2(-3f, 0f));
        Fighter defender = CreateFighter("Defender", new Vector2(3f, 0f));
        defender.combat.maxGuardStability = 50f;
        yield return HoldGuard(defender, .45f); // older than every perfect-parry window
        yield return EnterSwing(attacker);

        float stability = defender.combat.GuardStability;
        float health = defender.combat.health;
        yield return FixedFrames(3);
        Assert.That(defender.combat.GuardStability, Is.GreaterThanOrEqualTo(stability),
            "No overlap must not consume guard stability.");
        Assert.That(defender.combat.health, Is.EqualTo(health).Within(.001f));

        stability = defender.combat.GuardStability;
        OverlapBlades(attacker, defender, 4f);
        yield return FixedFrames(3);
        Assert.That(defender.combat.GuardStability, Is.LessThan(stability));
        Assert.That(defender.combat.Phase, Is.EqualTo(SwordPhase.Guard));
        Object.Destroy(attacker.root);
        Object.Destroy(defender.root);
    }

    [UnityTest]
    public IEnumerator InactiveOrHeightSeparatedBladesCannotResolveContact()
    {
        Fighter attacker = CreateFighter("Attacker", new Vector2(-3f, 0f));
        Fighter defender = CreateFighter("Defender", new Vector2(3f, 0f));
        yield return EnterSwing(attacker);
        float health = defender.combat.health;

        attacker.blade.SetActive(false);
        OverlapBlades(attacker, defender, 5f);
        yield return FixedFrames(3);
        Assert.That(defender.combat.health, Is.EqualTo(health).Within(.001f));

        attacker.blade.SetActive(true);
        attacker.blade.SetPose(14f, 0f);
        defender.blade.SetPose(78f, 0f);
        yield return null;
        OverlapBlades(attacker, defender, 5f);
        yield return FixedFrames(3);
        Assert.That(defender.combat.health, Is.EqualTo(health).Within(.001f));
        Object.Destroy(attacker.root);
        Object.Destroy(defender.root);
    }

    [UnityTest]
    public IEnumerator StrongPhysicalBlockBreaksGuardAndTransfersContactImpulseToArms()
    {
        Fighter attacker = CreateFighter("Attacker", new Vector2(-3f, 0f));
        Fighter defender = CreateFighter("Defender", new Vector2(3f, 0f));
        ArmPhysicsBlend arms = defender.root.AddComponent<ArmPhysicsBlend>();
        ConfigureTestArms(arms, defender.root.transform);
        defender.combat.armPhysics = arms;
        defender.combat.maxGuardStability = .1f;
        yield return HoldGuard(defender, .45f);
        yield return EnterSwing(attacker);
        OverlapBlades(attacker, defender, 8f);
        yield return FixedFrames(3);

        Assert.That(defender.combat.Phase, Is.EqualTo(SwordPhase.GuardBroken));
        Assert.That(arms.IsRagdoll, Is.True);
        Assert.That(arms.left.hand.linearVelocity.sqrMagnitude + arms.right.hand.linearVelocity.sqrMagnitude,
            Is.GreaterThan(.01f));
        Assert.That(arms.left.gripJoint.enabled || arms.right.gripJoint.enabled, Is.False);
        Object.Destroy(attacker.root);
        Object.Destroy(defender.root);
    }

    private static Fighter CreateFighter(string name, Vector2 position)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        Rigidbody2D torso = root.AddComponent<Rigidbody2D>();
        torso.gravityScale = 0f;
        CombatStanceController combat = root.AddComponent<CombatStanceController>();
        combat.useInjectedInput = true;

        GameObject hilt = new GameObject(name + "_Hilt");
        hilt.transform.SetParent(root.transform, false);
        Rigidbody2D hiltBody = hilt.AddComponent<Rigidbody2D>();
        hiltBody.gravityScale = 0f;
        WeaponPoseMotor weapon = hilt.AddComponent<WeaponPoseMotor>();
        GameObject grip = new GameObject("SharedGrip");
        grip.transform.SetParent(hilt.transform, false);
        weapon.sharedGrip = grip.transform;

        GameObject bladeObject = new GameObject("Blade");
        bladeObject.transform.SetParent(hilt.transform, false);
        BladeVisualController blade = bladeObject.AddComponent<BladeVisualController>();
        GameObject contactObject = new GameObject("BladeContact");
        contactObject.transform.SetParent(bladeObject.transform, false);
        CapsuleCollider2D contact = contactObject.AddComponent<CapsuleCollider2D>();
        contact.isTrigger = true;
        contact.direction = CapsuleDirection2D.Vertical;
        contact.size = new Vector2(.2f, 1f);
        BladeContactSensor sensor = contactObject.AddComponent<BladeContactSensor>();

        weapon.character = root.transform;
        weapon.combat = combat;
        weapon.blade = blade;
        blade.contactCollider = contact;
        blade.contactTransform = contactObject.transform;
        blade.contactSensor = sensor;
        sensor.weapon = weapon;
        sensor.owner = combat;
        combat.weapon = weapon;
        weapon.SetMotorEnabled(false);
        return new Fighter { root = root, combat = combat, weapon = weapon, blade = blade,
            hiltBody = hiltBody, contact = contact };
    }

    private static IEnumerator HoldGuard(Fighter fighter, float seconds)
    {
        fighter.combat.SetInjectedInput(Vector2.up, false, false, false, true);
        int frames = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
        yield return FixedFrames(frames);
    }

    private static IEnumerator EnterSwing(Fighter fighter)
    {
        fighter.combat.SetInjectedInput(Vector2.up, true, true, false, false);
        yield return new WaitForFixedUpdate();
        fighter.combat.SetInjectedInput(Vector2.up, false, false, true, false);
        for (int i = 0; i < 30 && !fighter.combat.IsActiveSwing; ++i)
            yield return new WaitForFixedUpdate();
        Assert.That(fighter.combat.IsActiveSwing, Is.True, "Injected press/release must reach Swing.");
    }

    private static void OverlapBlades(Fighter attacker, Fighter defender, float attackerSpeed)
    {
        attacker.weapon.SetMotorEnabled(false);
        defender.weapon.SetMotorEnabled(false);
        attacker.hiltBody.position = Vector2.zero;
        defender.hiltBody.position = Vector2.zero;
        attacker.hiltBody.linearVelocity = Vector2.right * attackerSpeed;
        defender.hiltBody.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static IEnumerator FixedFrames(int count)
    {
        for (int i = 0; i < count; ++i) yield return new WaitForFixedUpdate();
    }

    private static void ConfigureTestArms(ArmPhysicsBlend arms, Transform parent)
    {
        arms.left = CreateArm("Left", parent);
        arms.right = CreateArm("Right", parent);
        arms.maximumRagdollSeconds = .25f;
    }

    private static ArmPhysicsBlend.ArmChain CreateArm(string name, Transform parent)
    {
        Rigidbody2D upper = CreateBody(name + "Upper", parent, new Vector2(name == "Left" ? -.2f : .2f, .1f));
        Rigidbody2D fore = CreateBody(name + "Fore", upper.transform, Vector2.up * .15f);
        Rigidbody2D hand = CreateBody(name + "Hand", fore.transform, Vector2.up * .15f);
        FixedJoint2D grip = hand.gameObject.AddComponent<FixedJoint2D>();
        grip.enabled = false;
        return new ArmPhysicsBlend.ArmChain { upperArm = upper, forearm = fore, hand = hand, gripJoint = grip };
    }

    private static Rigidbody2D CreateBody(string name, Transform parent, Vector2 localPosition)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.simulated = false;
        return body;
    }
}
