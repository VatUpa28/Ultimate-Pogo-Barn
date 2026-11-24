using UnityEngine;

public class PlayerRaceProgress : MonoBehaviour
{
    [Min(0)] public int totalCheckpoints;
    public int nextCheckpoint = 1;
    public bool finished;
    public float finishTime;
    [SerializeField] private string playerName = "Player";
    private Map1RaceManager raceManager;
    private float startTime;
    private bool raceStarted;

    public string PlayerName => playerName;

    public void Initialize(Map1RaceManager manager, string displayName, int checkpointCount)
    {
        raceManager = manager;
        playerName = displayName;
        totalCheckpoints = Mathf.Max(0, checkpointCount);
        nextCheckpoint = 1;
        finished = false;
        finishTime = 0f;
        raceStarted = false;
    }

    private void Start()
    {
        if (!raceStarted)
            BeginRace();
    }

    public void BeginRace()
    {
        if (raceStarted)
            return;
        startTime = Time.time;
        raceStarted = true;
    }

    public void RegisterCheckpoint(int number)
    {
        if (finished || number != nextCheckpoint)
            return;
        nextCheckpoint++;
    }

    public void Finish()
    {
        if (finished || nextCheckpoint <= totalCheckpoints)
            return;
        finished = true;
        finishTime = Time.time - startTime;
        if (raceManager == null)
            raceManager = FindFirstObjectByType<Map1RaceManager>();
        if (raceManager != null)
            raceManager.ReportFinish(this, finishTime);
    }

    public string GetProgressLabel()
    {
        return finished
            ? playerName + ": finished in " + finishTime.ToString("F2") + "s"
            : playerName + ": checkpoint " + Mathf.Min(nextCheckpoint - 1, totalCheckpoints) + "/" + totalCheckpoints;
    }
}
