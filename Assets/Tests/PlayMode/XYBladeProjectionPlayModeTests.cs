using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Independent projection regression test; does not depend on the main scene.
/// The editor geometry validator must also run against SampleScene.unity.
/// </summary>
public sealed class XYBladeProjectionPlayModeTests
{
    [UnityTest]
    public IEnumerator VerticalBladeIsDotAndPlanarColliderHasNoReach()
    {
        GameObject hilt = new GameObject("TestHiltRoot");
        try
        {
            Rigidbody2D body = hilt.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            GameObject bladeObject = Child(hilt.transform, "Blade");
            BladeVisualController blade = bladeObject.AddComponent<BladeVisualController>();
            blade.hiltTransform = hilt.transform;
            GameObject pivot = Child(bladeObject.transform, "BladeVisualPivot");
            blade.visualPivot = pivot.transform;
            GameObject coreObject = Child(pivot.transform, "BladeCore");
            blade.core = coreObject.AddComponent<SpriteRenderer>();
            GameObject contactObject = Child(bladeObject.transform, "BladeContact");
            blade.contactTransform = contactObject.transform;
            blade.contactCollider = contactObject.AddComponent<CapsuleCollider2D>();
            blade.contactCollider.isTrigger = true;

            blade.SetPose(90f, 0f);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(blade.GroundReach, Is.EqualTo(0f).Within(.0001f));
            Assert.That(blade.contactCollider.enabled, Is.False);
            Assert.That(blade.core.transform.localScale.y, Is.EqualTo(blade.coreWidth).Within(.0001f));
            Assert.That(blade.core.transform.localPosition.y, Is.EqualTo(0f).Within(.0001f));

            blade.SetPose(18f, 0f);
            yield return new WaitForFixedUpdate();
            yield return null;
            float expected = blade.bladeLength * Mathf.Cos(18f * Mathf.Deg2Rad);
            Assert.That(blade.GroundReach, Is.EqualTo(expected).Within(.002f));
            Assert.That(blade.contactCollider.enabled, Is.True);
            Assert.That(blade.contactCollider.size.y, Is.EqualTo(expected).Within(.002f));

            blade.SetPose(0f, 0f);
            yield return new WaitForFixedUpdate();
            Assert.That(blade.GroundReach, Is.EqualTo(blade.bladeLength).Within(.0001f));
        }
        finally
        {
            Object.Destroy(hilt);
        }
    }

    private static GameObject Child(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj;
    }
}
