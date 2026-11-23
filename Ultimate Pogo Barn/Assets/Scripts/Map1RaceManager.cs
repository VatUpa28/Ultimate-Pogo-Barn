
using UnityEngine;

public class Map1RaceManager : MonoBehaviour
{
    public string winner = "";
    public float winningTime;

    public UPBRaceState currentState = UPBRaceState.Waiting;

    public void StartRace()
    {
        if (currentState != UPBRaceState.Waiting)
            return;

        currentState = UPBRaceState.Racing;
        Debug.Log("Race started!");
    }

    public void ReportFinish(PlayerRaceProgress player, float time)
    {
        if (currentState != UPBRaceState.Racing)
            return;

        currentState = UPBRaceState.Finished;

        winner = player.gameObject.name;
        winningTime = time;

        Debug.Log("Race winner: " + winner + " | Time: " + winningTime.ToString("F2") + " seconds");
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(15, 15, 320, 25), "FARMYARD RUSH");

        if (currentState == UPBRaceState.Waiting)
            GUI.Label(new Rect(15, 40, 400, 30), "Waiting to start...");

        else if (currentState == UPBRaceState.Racing)
            GUI.Label(new Rect(15, 40, 400, 30), "Race in progress!");

        else if (currentState == UPBRaceState.Finished)
            GUI.Label(new Rect(15, 40, 400, 30),
                "Winner: " + winner + " - " + winningTime.ToString("F2") + "s");
    }
}
