using UnityEngine;

public class PlayerRaceProgress : MonoBehaviour
{
    [Min(1)] public int totalCheckpoints = 3;
    public int nextCheckpoint = 1;
    public bool finished;
    public float finishTime;
    private float startTime;

    private void Start() { startTime = Time.time; }
    public void RegisterCheckpoint(int number)
    {
        if (finished || number != nextCheckpoint) return;
        nextCheckpoint++;
    }
    public void Finish()
    {
        if (finished || nextCheckpoint <= totalCheckpoints) return;
        finished = true;
        finishTime = Time.time - startTime;
        Map1RaceManager manager = FindFirstObjectByType<Map1RaceManager>();
        if (manager != null) manager.ReportFinish(this, finishTime);
    }
}
