using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RaceFinishLine : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerRaceProgress progress = other.GetComponentInParent<PlayerRaceProgress>();
        if (progress != null)
            progress.Finish();
    }
}
