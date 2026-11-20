using UnityEngine;

// The player-controller programmer can call ActivateAbility() when appropriate.
// This script does not implement movement or input controls.
public class UPBPogoAbilityRunner : MonoBehaviour
{
    public UPBPogoAbility ability;

    public void ActivateAbility()
    {
        if (ability != null)
            ability.Apply(gameObject);
    }
}
