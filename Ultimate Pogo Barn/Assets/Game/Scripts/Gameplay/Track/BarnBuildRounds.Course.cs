using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class BarnBuildRounds
{
    private readonly List<Vector3> courseFlags = new List<Vector3>();
    private readonly HashSet<int>[] reachedFlags = { new HashSet<int>(), new HashSet<int>() };
    private readonly List<int> checkpointFirstPlayers = new List<int>();
    private readonly List<Transform> sawBlades = new List<Transform>();
    private Transform flagGroup;
    private readonly List<Transform> checkpointMarkers = new List<Transform>();
    private ParticleSystem courseParticles;
    private Material courseParticleMaterial;
    private Texture2D menuButton;
    private Texture2D menuHover;
    private Texture2D courseCursor;
    private readonly int[] scores = new int[2];
    private int winningPlayer = -1;
    private int firstPlayerAtEnd = -1;
    private float runTime;
    private float noticeUntil;
    private string notice;
    private bool checkpointNotice;
    private float buildYaw;
    private float buildPitch;
    private float buildZoom = 1f;
    private bool sectionAddedThisRound;
    private bool initialBuildPending;
    private const int ObstaclePlacementLimit = 3;
    private const int WinningScore = 1000;
    private int obstaclePlacements;

    private static readonly Piece[] Blueprints =
    {
        Piece.Grass, Piece.Ramp, Piece.Bridge, Piece.Chicane, Piece.Terrace, Piece.Runway
    };

    private void SetupCourse()
    {
        menuButton = Resources.Load<Texture2D>("BarnCourse/UI/button_rectangle_depth_flat");
        menuHover = Resources.Load<Texture2D>("BarnCourse/UI/button_rectangle_depth_gloss");
        courseCursor = Resources.Load<Texture2D>("BarnCourse/Cursor/pointer_a");
        if (courseCursor) Cursor.SetCursor(courseCursor, Vector2.zero, CursorMode.Auto);
        var flags = new GameObject("Course Flags");
        flags.transform.SetParent(transform, false);
        flagGroup = flags.transform;
        AddCourseFlag();
        phase = RoundPhase.Menu;
    }

    private void AddCourseFlag()
    {
        var position = CellPosition(new Vector2Int(courseColumn, nextRow - 1), nextHeight);
        int index = courseFlags.Count;
        courseFlags.Add(position);
        checkpointFirstPlayers.Add(-1);
        var marker = new GameObject("Checkpoint " + (index + 1));
        marker.transform.SetParent(flagGroup, false);
        marker.transform.position = position;
        checkpointMarkers.Add(marker.transform);
        ShowCheckpointFlag(marker.transform, false);
    }

    private void ShowCheckpointFlag(Transform marker, bool reached)
    {
        foreach (Transform child in marker)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        Vector3 position = marker.position;
        string name = reached ? "flagGreen" : "flagCheckers";
        GameObject flag = Resources.Load<GameObject>("BarnCourse/Racing/" + name);
        if (!flag) return;
        var model = Instantiate(flag, marker);
        Bounds bounds = GetBounds(model);
        if (bounds.size.y <= 0.01f) return;
        model.transform.localScale *= 2.4f / bounds.size.y;
        bounds = GetBounds(model);
        model.transform.position += new Vector3(position.x + CellSize * 1.7f - bounds.center.x,
                                                position.y - bounds.min.y, position.z - bounds.center.z);
    }

    private void CheckCourseFlag(int playerIndex, Vector3 from, Vector3 to)
    {
        HashSet<int> playerFlags = reachedFlags[playerIndex];
        for (int i = 0; i < courseFlags.Count; i++)
        {
            if (playerFlags.Contains(i)) continue;
            Vector3 flag = courseFlags[i];
            float front = flag.z - 0.65f;
            if (to.z < front || from.z >= front) continue;
            float fraction = to.z > from.z ? Mathf.Clamp01((front - from.z) / (to.z - from.z)) : 1f;
            Vector3 crossing = Vector3.Lerp(from, to, fraction);
            if (Mathf.Abs(crossing.x - flag.x) > CellSize * (SideColumn + 0.5f) ||
                crossing.y < flag.y - 0.2f || crossing.y > flag.y + 3.6f) continue;

            playerFlags.Add(i);
            if (checkpointFirstPlayers[i] < 0)
            {
                checkpointFirstPlayers[i] = playerIndex;
                scores[playerIndex] += 150;
                notice = "Player " + (playerIndex + 1) + " reached checkpoint first: +150";
            }
            else
            {
                scores[playerIndex] += 100;
                notice = "Player " + (playerIndex + 1) + " checkpoint: +100";
            }
            checkpointNotice = true;
            noticeUntil = Time.unscaledTime + 2.5f;
            ShowCheckpointFlag(checkpointMarkers[i], true);
            BurstAt(crossing, new Color(0.28f, 1f, 0.52f), 36, true);
            BurstAt(flag + new Vector3(CellSize * 1.7f, 1.65f, 0f), new Color(1f, 0.81f, 0.2f), 28);
            bool finished = i == courseFlags.Count - 1;
            if (finished)
            {
                int finishBonus = firstPlayerAtEnd < 0 ? 120 : 60;
                if (firstPlayerAtEnd < 0) firstPlayerAtEnd = playerIndex;
                scores[playerIndex] += finishBonus;
                playerFinishedRound[playerIndex] = true;
                notice = "Player " + (playerIndex + 1) + " finished this round: +" + finishBonus;
                noticeUntil = Time.unscaledTime + 2f;
                RemoveRider(playerIndex);
            }

            if (scores[playerIndex] >= WinningScore)
            {
                winningPlayer = playerIndex;
                RemoveRiders();
                phase = RoundPhase.Won;
                notice = "Player " + (playerIndex + 1) + " reached " + WinningScore + " points and wins!";
                noticeUntil = Time.unscaledTime + 3f;
                return;
            }

            if (finished)
            {
                ResolveRideIfComplete();
                return;
            }
        }
    }

    private void ResolveRideIfComplete()
    {
        if (!playerFinishedRound[0] || !playerFinishedRound[1])
            return;

        if (winningPlayer >= 0)
        {
            phase = RoundPhase.Won;
            return;
        }

        phase = RoundPhase.RoundSummary;
        summaryRemaining = 2.5f;
    }

    private void RespawnAtFlag(int playerIndex, Vector3 death)
    {
        float nearest = float.PositiveInfinity;
        Vector3 destination = spawnPoint;
        foreach (int index in reachedFlags[playerIndex])
        {
            Vector3 flag = courseFlags[index];
            Vector3 point = flag + new Vector3(0f, 0.58f, -0.9f);
            float distance = (point - death).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            destination = point;
        }
        SpawnRider(playerIndex, destination + (playerIndex == 0 ? Vector3.left : Vector3.right) * 0.72f);
        notice = "Player " + (playerIndex + 1) + " back on the course";
        checkpointNotice = false;
        noticeUntil = Time.unscaledTime + 1.5f;
    }

    private void ResetCourse(bool showMenu)
    {
        RemoveRiders();
        preview.SetActive(false);
        foreach (Transform item in pieces)
        {
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
        foreach (Transform item in flagGroup)
        {
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
        courseFlags.Clear();
        checkpointMarkers.Clear();
        checkpointFirstPlayers.Clear();
        reachedFlags[0].Clear();
        reachedFlags[1].Clear();
        sawBlades.Clear();
        tileHeights.Clear();
        trackColliders.Clear();
        obstacles.Clear();
        hazardCells.Clear();
        scores[0] = 0;
        scores[1] = 0;
        winningPlayer = -1;
        firstPlayerAtEnd = -1;
        runTime = 0f;
        round = 1;
        initialBuildPending = true;
        noticeUntil = 0f;
        placedThisRound = false;
        sectionAddedThisRound = false;
        obstaclePlacements = 0;
        nextHeight = 0f;
        nextRow = -(SectionRows / 2);
        firstRow = nextRow;
        if (startingPlatform && startingPlatform.childCount > 0) RegisterStartingPlatform();
        else AppendSection(Piece.Grass);
        AddCourseFlag();
        if (showMenu) phase = RoundPhase.Menu;
        else BeginBuilderTurn(0);
    }

    private void BurstAt(Vector3 point, Color tint, int count, bool checkpointRing = false)
    {
        if (!courseParticles)
        {
            var effects = new GameObject("Checkpoint and Splash Effects");
            effects.transform.SetParent(transform, false);
            courseParticles = effects.AddComponent<ParticleSystem>();
            courseParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var settings = courseParticles.main;
            settings.loop = false;
            settings.playOnAwake = false;
            settings.simulationSpace = ParticleSystemSimulationSpace.World;
            settings.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
            settings.gravityModifier = 0.53f;
            settings.maxParticles = 140;
            var emission = courseParticles.emission;
            emission.enabled = false;
            var color = courseParticles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] {new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1)},
                         new[] {new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1)});
            color.color = new ParticleSystem.MinMaxGradient(fade);
            courseParticleMaterial = new Material(Shader.Find("Pogo Barn/Cartoon Puff"));
            effects.GetComponent<ParticleSystemRenderer>().sharedMaterial = courseParticleMaterial;
        }
        if (!courseParticles.isPlaying) courseParticles.Play();
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector2 spread = checkpointRing ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) : Random.insideUnitCircle;
            var particle = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = checkpointRing
                    ? new Vector3(spread.x * 4.2f, 0.9f, spread.y * 4.2f)
                    : new Vector3(spread.x * 3f, Random.Range(1f, 4.5f), spread.y * 3f),
                startColor = Color.Lerp(tint, Color.white, Random.value * 0.68f),
                startSize = checkpointRing ? .30f : Random.Range(0.10f, 0.33f),
                startLifetime = checkpointRing ? .9f : Random.Range(.28f, .65f)
            };
            courseParticles.Emit(particle, 1);
        }
    }

    private GUIStyle CourseButton()
    {
        var style = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        if (menuButton)
        {
            style.normal.background = menuButton;
            style.active.background = menuHover ? menuHover : menuButton;
            style.hover.background = style.active.background;
            style.border = new RectOffset(8, 8, 8, 8);
        }
        return style;
    }

    private void DrawCourseUI()
    {
        var button = CourseButton();
        if (phase == RoundPhase.SelectingCharacter)
        {
            if (activeCharacterSelection.Draw(button, menuHeading)) phase = RoundPhase.Menu;
            return;
        }

        if (phase == RoundPhase.Countdown)
        {
            DrawPlayerStatus();
            Rect countdownPanel = new Rect(Screen.width * .35f, Screen.height * .38f, Screen.width * .30f, 120f);
            GUI.Box(countdownPanel, GUIContent.none);
            var countdownStyle = new GUIStyle(menuHeading) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
            GUI.Label(countdownPanel, Mathf.CeilToInt(countdownRemaining).ToString(), countdownStyle);
            return;
        }

        if (phase == RoundPhase.RoundSummary)
        {
            DrawRoundSummary();
            return;
        }

        if (phase == RoundPhase.Choosing)
        {
            DrawBlueprints(button);
            return;
        }

        if (phase == RoundPhase.Placing)
        {
            DrawPlayerStatus();
            Rect banner = new Rect(Screen.width * .25f, Screen.height - 78f, Screen.width * .5f, 58f);
            GUI.Box(banner, "Player " + (builderPlayerIndex + 1) + " places one piece  •  " + Mathf.CeilToInt(buildTurnRemaining) + "s remaining");
            return;
        }

        string timer = string.Format("{0:00}:{1:00}", (int)runTime / 60, (int)runTime % 60);
        if (phase == RoundPhase.Menu || phase == RoundPhase.Won)
        {
            float width = Mathf.Min(480, Screen.width - 24);
            float height = phase == RoundPhase.Won ? 350f : 245f;
            var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, GUIContent.none);
            if (phase == RoundPhase.Won)
            {
                DrawScoreBar(panel, 0, "Player 1", scores[0], new Color(.30f, .73f, 1f));
                DrawScoreBar(panel, 1, "Player 2", scores[1], new Color(1f, .63f, .28f));
            }
            GUILayout.BeginArea(new Rect(panel.x + 22, panel.y + 18, panel.width - 44, panel.height - 36));
            var heading = new GUIStyle(menuHeading) {fontSize = 30, alignment = TextAnchor.MiddleCenter};
            GUILayout.Label(phase == RoundPhase.Menu ? "Ultimate Pogo Barn" : "Player " + (winningPlayer + 1) + " wins!", heading);
            GUILayout.Space(10);
            if (phase == RoundPhase.Menu)
            {
                if (GUILayout.Button("Play", button, GUILayout.Height(47))) ResetCourse(false);
                if (GUILayout.Button("Customize Player 1", button, GUILayout.Height(40)))
                {
                    activeCharacterSelection = characterSelection;
                    characterSelection.Open();
                    phase = RoundPhase.SelectingCharacter;
                }
                if (GUILayout.Button("Customize Player 2", button, GUILayout.Height(40)))
                {
                    activeCharacterSelection = characterSelectionPlayer2;
                    characterSelectionPlayer2.Open();
                    phase = RoundPhase.SelectingCharacter;
                }
            }
            else
            {
                GUILayout.Space(124);
                GUILayout.Label("P1 " + scores[0] + "   •   P2 " + scores[1] + " points   •   " + timer);
                if (GUILayout.Button("Restart", button, GUILayout.Height(43))) ResetCourse(false);
                if (GUILayout.Button("Main Menu", button, GUILayout.Height(43))) ResetCourse(true);
            }
            GUILayout.EndArea();
            return;
        }

        GUI.Box(new Rect((Screen.width - 310) * .5f, 12, 310, 46),
                "TIME " + timer + "   P1 " + scores[0] + "   P2 " + scores[1], menuHeading);
        if (phase == RoundPhase.Riding)
            DrawPlayerStatus();
        if (Time.unscaledTime < noticeUntil)
        {
            Rect banner = new Rect((Screen.width - 300) * .5f, 62, 300, 38);
            Color original = GUI.color;
            GUI.color = checkpointNotice ? new Color(0.07f, 0.48f, 0.27f, 0.95f) : new Color(0.10f, 0.20f, 0.32f, 0.95f);
            GUI.DrawTexture(banner, Texture2D.whiteTexture);
            GUI.color = original;
            var centered = new GUIStyle(menuHeading) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(banner, notice, centered);
        }

        if (phase == RoundPhase.Riding)
            return;

        GUILayout.BeginArea(new Rect(14, 12, MenuWidth - 28, 305), GUI.skin.box);
        GUILayout.Label("Round " + round, menuHeading);
        if (phase == RoundPhase.Riding)
        {
            GUILayout.Label("Reach flags for points. P1: WASD + Space   •   P2: Arrows + Enter");
            var controller = rider != null ? rider.GetComponent<PlayerController>() : null;
            if (controller != null && controller.IsCharging)
            {
                Rect meter = GUILayoutUtility.GetRect(1f, 10f, GUILayout.ExpandWidth(true));
                Color original = GUI.color;
                GUI.color = new Color(0.09f, 0.17f, 0.25f);
                GUI.DrawTexture(meter, Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(0.40f, 0.85f, 1f), new Color(0.45f, 1f, 0.56f), controller.JumpCharge);
                meter.width *= controller.JumpCharge;
                GUI.DrawTexture(meter, Texture2D.whiteTexture);
                GUI.color = original;
            }
        }
        else
        {
            if (!placedThisRound)
            {
                GUILayout.Label(IsSection ? "Place one platform at the end of the course."
                    : $"Place the same obstacle up to three times   {obstaclePlacements}/{ObstaclePlacementLimit}");
                GUILayout.Label("WASD pan · Right drag orbit · Scroll zoom · Q/E height");
                if (IsWall) GUILayout.Label("R rotates the wall.");
                if (obstaclePlacements == 0 && GUILayout.Button("Change piece", button, GUILayout.Height(34)))
                {
                    preview.SetActive(false);
                    phase = RoundPhase.Choosing;
                }
            }
            else
            {
                GUILayout.Label(sectionAddedThisRound ? "Platform ready" : "All three obstacles placed");
            }

            GUILayout.Label("Player " + (builderPlayerIndex + 1) + " is placing. The next player starts automatically.");
        }

        GUILayout.EndArea();
    }

    private void DrawPlayerStatus()
    {
        DrawPlayerStatusPanel(0, new Rect(14f, Screen.height - 92f, 250f, 76f));
        DrawPlayerStatusPanel(1, new Rect(Screen.width - 264f, Screen.height - 92f, 250f, 76f));
    }

    private void DrawPlayerStatusPanel(int playerIndex, Rect panel)
    {
        GUI.Box(panel, GUIContent.none);
        string name = "P" + (playerIndex + 1);
        GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 22f),
            name + "  •  respawns " + respawnsRemaining[playerIndex] + "  •  " + scores[playerIndex] + " pts");
        GameObject target = playerIndex == 0 ? rider : riderPlayer2;
        PlayerController controller = target != null ? target.GetComponent<PlayerController>() : null;
        Rect bar = new Rect(panel.x + 10f, panel.y + 40f, panel.width - 20f, 20f);
        GUI.color = new Color(.08f, .12f, .17f, .9f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        if (controller != null && controller.IsCharging)
        {
            bar.width *= controller.JumpCharge;
            GUI.color = Color.Lerp(new Color(.36f, .78f, 1f), new Color(.42f, 1f, .52f), controller.JumpCharge);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + 10f, panel.y + 39f, panel.width - 20f, 20f), "Jump charge");
    }

    private void DrawRoundSummary()
    {
        float width = Mathf.Min(560f, Screen.width - 32f);
        float height = 250f;
        Rect panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
        GUI.Box(panel, GUIContent.none);
        var heading = new GUIStyle(menuHeading) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, width - 36f, 38f), "Round " + round + " scores", heading);
        DrawScoreBar(panel, 0, "Player 1", scores[0], new Color(.30f, .73f, 1f));
        DrawScoreBar(panel, 1, "Player 2", scores[1], new Color(1f, .63f, .28f));
        var note = new GUIStyle(menuHeading) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(panel.x + 20f, panel.yMax - 42f, width - 40f, 28f), "Next obstacle turns begin shortly", note);
    }

    private void DrawScoreBar(Rect panel, int index, string label, int score, Color color)
    {
        float y = panel.y + 82f + index * 64f;
        GUI.Label(new Rect(panel.x + 26f, y, 90f, 28f), label);
        Rect bar = new Rect(panel.x + 118f, y + 3f, panel.width - 178f, 22f);
        GUI.color = new Color(.08f, .12f, .17f, .95f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        bar.width *= Mathf.Clamp01(score / 1000f);
        GUI.color = color;
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.xMax - 56f, y, 48f, 28f), score.ToString());
    }

    private void BeginBuilderTurn(int playerIndex)
    {
        builderPlayerIndex = playerIndex;
        placedThisRound = false;
        obstaclePlacements = 0;
        buildTurnRemaining = buildTurnSeconds;
        if (playerIndex == 0)
        {
            sectionAddedThisRound = false;
            foreach (int index in reachedFlags[0]) ShowCheckpointFlag(checkpointMarkers[index], false);
            foreach (int index in reachedFlags[1]) ShowCheckpointFlag(checkpointMarkers[index], false);
            reachedFlags[0].Clear();
            reachedFlags[1].Clear();
        }

        OfferBuildPieces();
    }

    private void AdvanceBuilderTurn()
    {
        if (builderPlayerIndex == 0)
        {
            BeginBuilderTurn(1);
            return;
        }

        if (!sectionAddedThisRound)
        {
            AppendSection(Piece.Grass);
            AddCourseFlag();
        }
        for (int i = 0; i < checkpointFirstPlayers.Count; i++)
            checkpointFirstPlayers[i] = -1;
        if (initialBuildPending)
            initialBuildPending = false;
        else
            round++;
        BeginRide();
    }

    private void OfferBuildPieces()
    {
        offers.Clear();
        var trackPool = new List<(Piece piece, PrefabOption custom)>();
        foreach (Piece piece in Blueprints) trackPool.Add((piece, null));
        foreach (PrefabOption option in customPrefabs)
            if (option != null && IsPlatformPrefab(option.prefab)) trackPool.Add((Piece.Grass, option));
        DrawFrom(trackPool, 2);

        var obstaclePool = new List<(Piece piece, PrefabOption custom)>
        {
            (Piece.Saw, null), (Piece.Spikes, null), (Piece.Wall, null), (Piece.Barrier, null),
            (Piece.Barrel, null), (Piece.Crate, null), (Piece.Posts, null)
        };
        foreach (PrefabOption option in customPrefabs)
            if (option != null && option.prefab && !IsPlatformPrefab(option.prefab)) obstaclePool.Add((Piece.Barrel, option));
        DrawFrom(obstaclePool, 2);
        preview.SetActive(false);
        phase = RoundPhase.Choosing;
        buildTurnRemaining = buildTurnSeconds;
    }

    private void DrawFrom(List<(Piece piece, PrefabOption custom)> pool, int count)
    {
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            offers.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }

    private void DrawBlueprints(GUIStyle button)
    {
        Color previousBackground = GUI.color;
        GUI.color = new Color(.07f, .11f, .16f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousBackground;

        float width = Mathf.Min(Screen.width - 30f, 980f);
        float cardWidth = (width - 46f) / 4f;
        float cardHeight = Mathf.Min(262f, Screen.height * .48f);
        float left = (Screen.width - width) * .5f;
        float top = (Screen.height - cardHeight) * .48f;
        GUI.Box(new Rect(left, top - 54, width, cardHeight + 68), GUIContent.none);
        string title = "Player " + (builderPlayerIndex + 1) + " — choose one piece   " + Mathf.CeilToInt(buildTurnRemaining) + "s";
        GUI.Label(new Rect(left + 17, top - 41, width - 30, 34), title, menuHeading);

        var categoryStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        categoryStyle.normal.textColor = new Color(.65f,.78f,.9f);
        for (int i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            bool platform = offer.custom != null ? IsPlatformPrefab(offer.custom.prefab)
                : System.Array.IndexOf(Blueprints, offer.piece) >= 0;
            string label = offer.custom != null
                ? (string.IsNullOrWhiteSpace(offer.custom.displayName) ? offer.custom.prefab.name : offer.custom.displayName)
                : (offer.piece == Piece.Grass ? "Platform" : offer.piece.ToString());
            Rect card = new Rect(left + 14 + i * (cardWidth + 6), top, cardWidth, cardHeight);
            Color previous = GUI.color;
            GUI.color = PiecePhotoBackground;
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(card.x + 10, card.y + 5, card.width - 20, 22), platform ? "Platform · 1 placement" : "Obstacle · up to 3", categoryStyle);
            Texture photo = GetPiecePhoto(offer.piece, offer.custom);
            if (photo != null)
                GUI.DrawTexture(new Rect(card.x + 5, card.y + 26, card.width - 10, card.height - 73), photo, ScaleMode.ScaleToFit, false);

            bool clickedPhoto = GUI.Button(new Rect(card.x, card.y, card.width, card.height - 43f), GUIContent.none, GUIStyle.none);
            bool clickedName = GUI.Button(new Rect(card.x + 5, card.yMax - 43f, card.width - 10, 37), label, button);
            if (!clickedPhoto && !clickedName) continue;
            if (offer.custom != null) ChoosePrefab(offer.custom);
            else Choose(offer.piece);
            phase = RoundPhase.Placing;
        }

    }

    private static bool HasSectionTile(Piece piece, int column, int row)
    {
        if (row == 0 || row == SectionRows - 1) return true;
        if (piece == Piece.Bridge) return Mathf.Abs(column) <= 1 || (row != 2 && column == 2);
        if (piece == Piece.Runway) return Mathf.Abs(column) <= 1;
        if (piece == Piece.Chicane) return row < 3 ? column >= -1 : column <= 1;
        return true;
    }

    private static bool IsRampRow(Piece piece, int row)
        => (piece == Piece.Ramp && row == 2) || (piece == Piece.Terrace && (row == 1 || row == 3));

    private static float SectionElevation(Piece piece, int row)
    {
        if (piece == Piece.Ramp) return row > 2 ? RampRise : 0f;
        if (piece == Piece.Terrace) return (row > 1 ? RampRise : 0f) + (row > 3 ? RampRise : 0f);
        return 0f;
    }
}
