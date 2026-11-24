using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class BarnBuildRounds
{
    private readonly List<Vector3> courseFlags = new List<Vector3>();
    private readonly HashSet<int> reachedFlags = new HashSet<int>();
    private readonly List<Transform> sawBlades = new List<Transform>();
    private Transform flagGroup;
    private readonly List<Transform> checkpointMarkers = new List<Transform>();
    private ParticleSystem courseParticles;
    private Material courseParticleMaterial;
    private Texture2D menuButton;
    private Texture2D menuHover;
    private Texture2D courseCursor;
    private int score;
    private float runTime;
    private float noticeUntil;
    private string notice;
    private bool checkpointNotice;
    private float buildYaw;
    private float buildPitch;
    private float buildZoom = 1f;
    private bool sectionAddedThisRound;
    private const int ObstaclePlacementLimit = 3;
    private int obstaclePlacements;

    private static readonly Piece[] Blueprints =
    {
        Piece.Grass, Piece.Ramp, Piece.Bridge, Piece.Chicane, Piece.Terrace, Piece.Runway, Piece.Gap
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

    private void CheckCourseFlag(Vector3 from, Vector3 to)
    {
        for (int i = 0; i < courseFlags.Count; i++)
        {
            if (reachedFlags.Contains(i)) continue;
            Vector3 flag = courseFlags[i];
            float front = flag.z - 0.65f;
            if (to.z < front || from.z >= front) continue;
            float fraction = (front - from.z) / (to.z - from.z);
            Vector3 crossing = Vector3.Lerp(from, to, fraction);
            if (Mathf.Abs(crossing.x - flag.x) > CellSize * (SideColumn + 0.5f) ||
                crossing.y < flag.y - 0.2f || crossing.y > flag.y + 3.6f) continue;

            reachedFlags.Add(i);
            score += 100;
            notice = "Checkpoint " + (i + 1) + "   +100";
            checkpointNotice = true;
            noticeUntil = Time.unscaledTime + 2.5f;
            ShowCheckpointFlag(checkpointMarkers[i], true);
            BurstAt(crossing, new Color(0.28f, 1f, 0.52f), 36, true);
            BurstAt(flag + new Vector3(CellSize * 1.7f, 1.65f, 0f), new Color(1f, 0.81f, 0.2f), 28);
            if (i != courseFlags.Count - 1) continue;
            if (score >= 1000)
            {
                RemoveRider();
                phase = RoundPhase.Won;
            }
            else EndRide();
            return;
        }
    }

    private void RespawnAtFlag(Vector3 death)
    {
        float nearest = float.PositiveInfinity;
        Vector3 destination = spawnPoint;
        foreach (int index in reachedFlags)
        {
            Vector3 flag = courseFlags[index];
            Vector3 point = flag + new Vector3(0f, 0.58f, -0.9f);
            float distance = (point - death).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            destination = point;
        }
        SpawnRider(destination);
        notice = "Back on the course";
        checkpointNotice = false;
        noticeUntil = Time.unscaledTime + 1.5f;
    }

    private void ResetCourse(bool showMenu)
    {
        RemoveRider();
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
        reachedFlags.Clear();
        sawBlades.Clear();
        tileHeights.Clear();
        trackColliders.Clear();
        obstacles.Clear();
        hazardCells.Clear();
        score = 0;
        runTime = 0f;
        round = 1;
        noticeUntil = 0f;
        sectionAddedThisRound = false;
        obstaclePlacements = 0;
        nextHeight = 0f;
        nextRow = -(SectionRows / 2);
        firstRow = nextRow;
        if (startingPlatform && startingPlatform.childCount > 0) RegisterStartingPlatform();
        else AppendSection(Piece.Grass);
        AddCourseFlag();
        if (showMenu) phase = RoundPhase.Menu;
        else BeginRide();
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
            if (characterSelection.Draw(button, menuHeading)) phase = RoundPhase.Menu;
            return;
        }

        string timer = string.Format("{0:00}:{1:00}", (int)runTime / 60, (int)runTime % 60);
        if (phase == RoundPhase.Menu || phase == RoundPhase.Won)
        {
            float width = Mathf.Min(420, Screen.width - 24);
            var panel = new Rect((Screen.width - width) / 2f, Screen.height * 0.24f, width, 245);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 22, panel.y + 18, panel.width - 44, panel.height - 36));
            var heading = new GUIStyle(menuHeading) {fontSize = 30, alignment = TextAnchor.MiddleCenter};
            GUILayout.Label(phase == RoundPhase.Menu ? "Ultimate Pogo Barn" : "Course Complete!", heading);
            GUILayout.Space(10);
            if (phase == RoundPhase.Menu)
            {
                if (GUILayout.Button("Play", button, GUILayout.Height(47))) ResetCourse(false);
                if (GUILayout.Button("Character Selection", button, GUILayout.Height(43)))
                {
                    characterSelection.Open();
                    phase = RoundPhase.SelectingCharacter;
                }
            }
            else
            {
                GUILayout.Label(score + " points   •   " + timer);
                if (GUILayout.Button("Restart", button, GUILayout.Height(43))) ResetCourse(false);
                if (GUILayout.Button("Main Menu", button, GUILayout.Height(43))) ResetCourse(true);
            }
            GUILayout.EndArea();
            return;
        }

        GUI.Box(new Rect((Screen.width - 310) * .5f, 12, 310, 46),
                "TIME   " + timer + "     SCORE   " + score + " / 1000", menuHeading);
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

        if (phase == RoundPhase.Choosing)
        {
            DrawBlueprints(button);
            return;
        }

        GUILayout.BeginArea(new Rect(14, 12, MenuWidth - 28, 305), GUI.skin.box);
        GUILayout.Label("Round " + round, menuHeading);
        if (phase == RoundPhase.Riding)
        {
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
            if (!PlacementComplete)
            {
                GUILayout.Label(IsSection ? "Place one platform at the end of the course."
                    : $"Place the same obstacle up to three times   {obstaclePlacements}/{ObstaclePlacementLimit}");
                GUILayout.Label("WASD pan / Right drag orbit / Scroll zoom /Q/E height");
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

            if ((sectionAddedThisRound || obstaclePlacements > 0) &&
                GUILayout.Button("Start Round", button, GUILayout.Height(38)))
                BeginNextRound();
        }

        GUILayout.EndArea();
    }

    private void BeginNextRound()
    {
        if (!sectionAddedThisRound)
        {
            if (obstaclePlacements == 0) return;
            AppendSection(Piece.Grass);
            AddCourseFlag();
        }

        foreach (int index in reachedFlags)
            ShowCheckpointFlag(checkpointMarkers[index], false);
        reachedFlags.Clear();

        round++;
        sectionAddedThisRound = false;
        obstaclePlacements = 0;
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
        float width = Mathf.Min(Screen.width - 30f, 980f);
        float cardWidth = (width - 46f) / 4f;
        float cardHeight = Mathf.Min(262f, Screen.height * .48f);
        float left = (Screen.width - width) * .5f;
        float top = (Screen.height - cardHeight) * .48f;
        GUI.Box(new Rect(left, top - 54, width, cardHeight + 68), GUIContent.none);
        const string title = "Choose one platform or obstacle";
        GUI.Label(new Rect(left + 17, top - 41, width - 30, 34), title, menuHeading);

        var categoryStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        categoryStyle.normal.textColor = new Color(.65f,.78f,.9f);
        for (int i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            bool platform = IsPlatform(offer.piece, offer.custom);
            string label = offer.custom != null
                ? (string.IsNullOrWhiteSpace(offer.custom.displayName) ? offer.custom.prefab.name : offer.custom.displayName)
                : (offer.piece == Piece.Grass ? "Platform" : offer.piece.ToString());
            Rect card = new Rect(left + 14 + i * (cardWidth + 6), top, cardWidth, cardHeight);
            Color previous = GUI.color;
            GUI.color = PiecePhotoBackground;
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(card.x + 10, card.y + 5, card.width - 20, 22), platform ? "Platform - 1 placement" : "Obstacle - up to 3", categoryStyle);
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
        if (piece == Piece.Gap) return row != SectionRows / 2;
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
