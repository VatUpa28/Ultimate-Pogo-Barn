using UnityEngine;
public class UPBTrackPiece : MonoBehaviour
{
    public Transform ConnectorStart;
    public Transform ConnectorEnd;
    private void OnValidate()
    {
        if (ConnectorStart == null) ConnectorStart = transform.Find("Connector_Start");
        if (ConnectorEnd == null) ConnectorEnd = transform.Find("Connector_End");
    }
}