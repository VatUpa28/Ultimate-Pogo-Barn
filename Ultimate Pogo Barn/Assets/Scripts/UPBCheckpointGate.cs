using UnityEngine;
public class UPBCheckpointGate : MonoBehaviour
{
    public int CheckpointIndex;
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<UPBRacerTag>() != null)
            Debug.Log("Checkpoint reached: " + CheckpointIndex, other);
    }
}