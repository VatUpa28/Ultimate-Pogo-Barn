using UnityEngine;
public class UPBFinishGate : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<UPBRacerTag>() != null)
            Debug.Log("Finish gate crossed.", other);
    }
}