using System.Collections;
using UnityEngine;

public class Map1RaceManager : MonoBehaviour
{
    [Header("Race Setup")]
    [Min(1)] public int countdownSeconds = 3;
    public UPBRaceState State = UPBRaceState.Waiting;

    [Header("Race Result")]
    public string winner = "";
    public float winningTime;
    public int countdownNumber;

    private bool countdownStarted;

    // Connect this method to a Start button, or call it from another race-start script.
    public void StartRaceCountdown()
    {
        if (State != UPBRaceState.Waiting || countdownStarted)
            return;

        countdownStarted = true;
        StartCoroutine(CountdownRoutine());
    }

    // Coroutine concept: the countdown runs over multiple frames.
    private IEnumerator CountdownRoutine()
    {
        State = UPBRaceState.Countdown;

        for (int number = countdownSeconds; number > 0; number--)
        {
            countdownNumber = number;
            yield return new WaitForSeconds(1f);
        }

        countdownNumber = 0;
        State = UPBRaceState.Racing;

        PlayerRaceProgress[] racers = FindObjectsOfType<PlayerRaceProgress>();
        foreach (PlayerRaceProgress racer in racers)
            racer.BeginRace();

        Debug.Log("GO!");
    }

    public void ReportFinish(PlayerRaceProgress player, float time)
    {
        if (State != UPBRaceState.Racing || player == null)
            return;

        // First valid player to finish wins.
        State = UPBRaceState.Finished;
        winner = player.gameObject.name;
        winningTime = time;

        Debug.Log("Race winner: " + winner + " | Time: " + winningTime.ToString("F2") + " seconds");
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(15, 15, 320, 25), "FARMYARD RUSH");

        if (State == UPBRaceState.Waiting)
            GUI.Label(new Rect(15, 40, 400, 25), "Waiting to start");
        else if (State == UPBRaceState.Countdown)
            GUI.Label(new Rect(15, 40, 400, 30), countdownNumber.ToString());
        else if (State == UPBRaceState.Racing)
            GUI.Label(new Rect(15, 40, 400, 25), "GO!");
        else if (State == UPBRaceState.Finished)
            GUI.Label(new Rect(15, 40, 450, 30),
                "Winner: " + winner + " - " + winningTime.ToString("F2") + "s");
    }
}
