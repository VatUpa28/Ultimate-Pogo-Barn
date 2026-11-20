using UnityEngine;

// Strategy Pattern: each ability asset supplies a different implementation.
public abstract class UPBPogoAbility : ScriptableObject
{
    public abstract void Apply(GameObject player);
}
