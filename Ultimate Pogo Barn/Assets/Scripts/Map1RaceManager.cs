
using UnityEngine;

public class Map1RaceManager : MonoBehaviour
{
    [Header("Local Players")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform player1Spawn;
    [SerializeField] private Transform player2Spawn;
    [SerializeField] private bool spawnPlayersOnStart = true;

    [Header("Race Results")]
    public string winner = "";
    public float winningTime;
    public UPBRaceState currentState = UPBRaceState.Waiting;

    private readonly PlayerRaceProgress[] players = new PlayerRaceProgress[2];
    private PlayerInputActions[] inputActions;

    private void Start()
    {
        if (!spawnPlayersOnStart || playerPrefab == null)
            return;

        if (player1Spawn == null)
            player1Spawn = GameObject.Find("Player1Spawn")?.transform;
        if (player2Spawn == null)
            player2Spawn = GameObject.Find("Player2Spawn")?.transform;

        if (player1Spawn == null || player2Spawn == null)
        {
            Debug.LogError("Assign Player1Spawn and Player2Spawn before starting the local race.", this);
            return;
        }

        int totalCheckpoints = GetTotalCheckpoints();
        SpawnPlayer(0, "Player 1", player1Spawn, totalCheckpoints);
        SpawnPlayer(1, "Player 2", player2Spawn, totalCheckpoints);
        StartRace();
    }

    private int GetTotalCheckpoints()
    {
        UPBCheckpointGate[] gates = FindObjectsByType<UPBCheckpointGate>(FindObjectsSortMode.None);
        int total = 0;
        foreach (UPBCheckpointGate gate in gates)
            total = Mathf.Max(total, gate.checkpointNumber);
        return total;
    }

    private void SpawnPlayer(int index, string playerName, Transform spawn, int totalCheckpoints)
    {
        GameObject instance = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        instance.name = playerName;

        PlayerController controller = instance.GetComponent<PlayerController>();
        if (controller != null)
        {
            inputActions ??= new PlayerInputActions[2];
            inputActions[index] = new PlayerInputActions();
            var actions = inputActions[index].GameActions;
            controller.SetLocalInputActions(
                index == 0 ? actions.P1Move : actions.P2Move,
                index == 0 ? actions.P1Jump : actions.P2Jump);
        }
        else
        {
            Debug.LogError("The assigned player prefab needs a PlayerController component.", instance);
        }

        PlayerRaceProgress progress = instance.GetComponent<PlayerRaceProgress>();
        if (progress == null)
            progress = instance.AddComponent<PlayerRaceProgress>();
        progress.Initialize(this, playerName, totalCheckpoints);
        players[index] = progress;
    }

    public void StartRace()
    {
        if (currentState != UPBRaceState.Waiting)
            return;

        currentState = UPBRaceState.Racing;
        foreach (PlayerRaceProgress player in players)
            if (player != null)
                player.BeginRace();
        Debug.Log("Race started!");
    }

    public void ReportFinish(PlayerRaceProgress player, float time)
    {
        if (currentState != UPBRaceState.Racing)
            return;

        currentState = UPBRaceState.Finished;
        winner = player.PlayerName;
        winningTime = time;
        Debug.Log("Race winner: " + winner + " | Time: " + winningTime.ToString("F2") + " seconds");
    }

    private void OnDestroy()
    {
        if (inputActions == null)
            return;
        foreach (PlayerInputActions actions in inputActions)
            actions?.Dispose();
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(15, 15, 320, 25), "ULTIMATE POGO BARN");
        GUI.Label(new Rect(15, 40, 400, 25), currentState == UPBRaceState.Finished
            ? "Winner: " + winner + " - " + winningTime.ToString("F2") + "s"
            : currentState == UPBRaceState.Racing ? "Race in progress!" : "Waiting to start...");

        for (int i = 0; i < players.Length; i++)
        {
            PlayerRaceProgress player = players[i];
            if (player == null)
                continue;
            GUI.Label(new Rect(15, 70 + i * 22, 400, 22), player.GetProgressLabel());
        }
    }
}
