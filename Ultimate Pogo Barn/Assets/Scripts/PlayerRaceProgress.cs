using UnityEngine;

public class PlayerRaceProgress : MonoBehaviour
{
    [Header("Checkpoint Setup")]
    [Min(1)] public int totalCheckpoints = 3;
    [Min(1)] public int nextCheckpoint = 1;

    [Header("Race Status")]
    public bool finished;
    public float finishTime;

    private float raceStartTime;
    private bool raceStarted;

    public void BeginRace()
    {
        nextCheckpoint = 1;
        finished = false;
        finishTime = 0f;
        raceStartTime = Time.time;
        raceStarted = true;
    }

    public void RegisterCheckpoint(int checkpointNumber)
    {
        if (!raceStarted || finished)
            return;

        // Only accept checkpoints in the correct order.
        if (checkpointNumber == nextCheckpoint)
            nextCheckpoint++;
    }

    public void Finish()
    {
        if (!raceStarted || finished)
            return;

        // The player must pass every checkpoint before the finish line.
        if (nextCheckpoint <= totalCheckpoints)
            return;

        finished = true;
        finishTime = Time.time - raceStartTime;
        raceStarted = false;

        Map1RaceManager manager = FindObjectOfType<Map1RaceManager>();
        if (manager != null)
            manager.ReportFinish(this, finishTime);
    }
}
