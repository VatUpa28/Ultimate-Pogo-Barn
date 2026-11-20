using UnityEngine;

public class Map1RaceManager : MonoBehaviour
{
    public string winner = "";
    public float winningTime;
    private bool raceOver;

    public void ReportFinish(PlayerRaceProgress player, float time)
    {
        if (raceOver) return;
        raceOver = true;
        winner = player.gameObject.name;
        winningTime = time;
        Debug.Log("Race winner: " + winner + " | Time: " + winningTime.ToString("F2") + " seconds");
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(15, 15, 320, 25), "FARMYARD RUSH");
        if (raceOver)
            GUI.Label(new Rect(15, 40, 400, 30), "Winner: " + winner + " - " + winningTime.ToString("F2") + "s");
    }
}
