using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RaceCheckpoint : MonoBehaviour
{
    public int checkpointNumber = 1;
    private void Reset() { GetComponent<Collider>().isTrigger = true; }
    private void OnTriggerEnter(Collider other)
    {
        PlayerRaceProgress progress = other.GetComponentInParent<PlayerRaceProgress>();
        if (progress != null) progress.RegisterCheckpoint(checkpointNumber);
    }
}
