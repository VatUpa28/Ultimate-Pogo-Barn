using System.Collections;
using UnityEngine;

// Coroutine example: merge this timed countdown into the main race manager.
public class UPBRaceCountdownExample : MonoBehaviour
{
    public UPBRaceState State = UPBRaceState.Waiting;
    public int countdownSeconds = 3;

    public void BeginCountdown()
    {
        if (State == UPBRaceState.Waiting)
            StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        State = UPBRaceState.Countdown;
        for (int n = countdownSeconds; n > 0; n--)
        {
            Debug.Log(n);
            yield return new WaitForSeconds(1f);
        }
        Debug.Log("GO!");
        State = UPBRaceState.Racing;
    }
}
