using UnityEngine;

// Strategy Pattern: interchangeable ScriptableObject behaviors.
public abstract class UPBPogoAbility : ScriptableObject
{
    public abstract void Apply(GameObject player);
}
