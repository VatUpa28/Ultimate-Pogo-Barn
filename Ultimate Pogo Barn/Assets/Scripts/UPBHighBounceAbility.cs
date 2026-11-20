using UnityEngine;

[CreateAssetMenu(menuName = "Ultimate Pogo Barn/Abilities/High Bounce")]
public class UPBHighBounceAbility : UPBPogoAbility
{
    public float upwardImpulse = 12f;

    public override void Apply(GameObject player)
    {
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
            body.AddForce(Vector3.up * upwardImpulse, ForceMode.Impulse);
    }
}
