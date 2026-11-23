using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class BarnBuildRounds : MonoBehaviour
{
    private const float CellSize = 2.7f;
    private const float PanelWidth = 240f;
    private const float RampLowEdge = 0.24f * CellSize / 2f;
    private const float RampRise = 0.54f * CellSize / 2f;
    private const int ObstacleLayer = 2; // Ignore Raycast
    private static readonly Vector3 CameraOffset = new Vector3(0f, 7.5f, -17f);
    private static readonly string[] ModelNames =
    {
        "block-grass", "block-grass-large-slope", "barrel", "crate", "poles", "trap-spikes", "brick"
    };
    private const int SectionRows = 5;
    private const int SideColumn = 2;

    private enum Piece { Grass, Ramp, Barrel, Crate, Posts, Spikes, Wall }

    [System.Serializable]
    private class PrefabOption
    {
        public GameObject prefab;
        public string displayName;
        public Texture2D thumbnail;
    }

    [Tooltip("Drag project prefabs here to add them to the obstacle menu.")]
    [SerializeField] private List<PrefabOption> customPrefabs = new List<PrefabOption>();

    private readonly HashSet<Collider> trackColliders = new HashSet<Collider>();
    private PrefabOption selectedPrefab;

    private readonly Dictionary<Vector2Int, float> tileHeights = new Dictionary<Vector2Int, float>();
    private readonly HashSet<Vector2Int> obstacles = new HashSet<Vector2Int>();
    private readonly GameObject[] models = new GameObject[ModelNames.Length];
    private readonly Texture2D[] icons = new Texture2D[ModelNames.Length];

    [Header("Scene Layout")]
    [SerializeField] private Transform startingPlatform;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private BarnBackdrop sceneBackdrop;
    [SerializeField] private GameObject pogoPlayerPrefab;
    [SerializeField] private Font menuFont;
    private GUISkin menuSkin;
    private GUIStyle menuHeading;

    private enum RoundPhase { Riding, Choosing, Placing }
    private RoundPhase phase;
    private GameObject rider;
    private int wallQuarterTurns;
    private bool IsWall => selectedPrefab == null && selectedPiece == Piece.Wall;
    private Vector3 spawnPoint;
    private BarnBackdrop backdrop;
    private bool riderInWater;
    private Quaternion buildCameraRotation;
    private int firstRow = -(SectionRows / 2);
    private readonly List<(Piece piece, PrefabOption custom)> offers = new List<(Piece, PrefabOption)>();

    private Camera mapCamera;
    private Vector3 cameraOffset = CameraOffset;
    private int courseColumn;
    private Transform pieces;
    private GameObject preview;
    private Material modelMaterial;
    private Material previewMaterial;
    private Vector2 menuScroll;
    private Vector2Int selectedCell;
    private Vector3 viewCenter;
    private Piece selectedPiece;
    private int round = 1;
    private bool placedThisRound;
    private int nextRow = -(SectionRows / 2);
    private float nextHeight;
    private Vector3 hoverPosition;
    private Quaternion hoverRotation = Quaternion.identity;
    private bool validPlacement;

    private bool IsSection => selectedPrefab == null && selectedPiece <= Piece.Ramp;

    private float MenuWidth => Mathf.Min(PanelWidth, Screen.width * 0.4f);

    private void Start()
    {
        for (int i = 0; i < ModelNames.Length; i++)
        {
            models[i] = Resources.Load<GameObject>("BarnPieces/Models/" + ModelNames[i]);
            if (models[i] == null)
            {
                Debug.LogError("Missing Barn Build Sandbox asset: " + ModelNames[i], this);
                enabled = false;
                return;
            }
        }

        string[] iconNames = { "block-grass", "block-grass-large-slope", "barrel", "crate", "poles", "trap-spikes", "brick" };
        for (int i = 0; i < iconNames.Length; i++)
            icons[i] = Resources.Load<Texture2D>("BarnPieces/Icons/" + iconNames[i]);

        modelMaterial = new Material(Shader.Find("Pogo Barn/Cartoon Lit") ?? Shader.Find("Universal Render Pipeline/Lit"));
        modelMaterial.mainTexture = Resources.Load<Texture2D>("BarnPieces/colormap");
        previewMaterial = new Material(Shader.Find("Pogo Barn/Cartoon Lit") ?? Shader.Find("Universal Render Pipeline/Lit"));
        pieces = new GameObject("Map Pieces").transform;
        pieces.SetParent(transform);

        if (sceneCamera != null)
        {
            mapCamera = sceneCamera;
            cameraOffset = mapCamera.transform.position;
        }
        else
        {
            CreateCameraAndLight();
        }

        if (startingPlatform != null && startingPlatform.childCount > 0)
            RegisterStartingPlatform();
        else
            AppendSection(Piece.Grass);

        backdrop = sceneBackdrop != null ? sceneBackdrop : gameObject.AddComponent<BarnBackdrop>();
        backdrop.Build(modelMaterial, mapCamera);
        buildCameraRotation = mapCamera.transform.rotation;
        Choose(Piece.Grass);
        if (pogoPlayerPrefab != null)
            BeginRide();
        else
            phase = RoundPhase.Placing;
    }

    private void CreateCameraAndLight()
    {
        var cameraObject = new GameObject("Map Camera");
        cameraObject.transform.SetParent(transform);
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.fieldOfView = 47f;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(0.65f, 0.86f, 0.98f);
        mapCamera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        mapCamera.transform.SetPositionAndRotation(CameraOffset, Quaternion.LookRotation(-CameraOffset));

        var lightObject = new GameObject("Sun");
        lightObject.transform.SetParent(transform);
        lightObject.transform.rotation = Quaternion.Euler(48f, -26f, 0f);
        lightObject.AddComponent<Light>().type = LightType.Directional;
    }

    private void RegisterStartingPlatform()
    {
        courseColumn = Mathf.RoundToInt(startingPlatform.position.x / CellSize);
        nextRow = int.MinValue;
        firstRow = int.MaxValue;
        foreach (Transform tile in startingPlatform)
        {
            if (!tile.gameObject.activeInHierarchy)
                continue;

            Vector3 position = tile.position;
            var cell = new Vector2Int(Mathf.RoundToInt(position.x / CellSize),
                                      Mathf.RoundToInt(position.z / CellSize));
            tile.position = CellPosition(cell, position.y);
            tileHeights[cell] = position.y;
            firstRow = Mathf.Min(firstRow, cell.y);
            RegisterTrackTile(tile.gameObject);

            if (cell.y >= nextRow)
            {
                nextRow = cell.y + 1;
                nextHeight = position.y;
            }
        }

        if (nextRow == int.MinValue)
        {
            nextRow = -(SectionRows / 2);
            firstRow = nextRow;
            AppendSection(Piece.Grass);
        }
    }

    private void Update()
    {
        if (mapCamera == null || preview == null)
            return;

        if (phase == RoundPhase.Riding)
        {
            mapCamera.rect = new Rect(0f, 0f, 1f, 1f);
            if (rider != null)
            {
                Vector3 position = rider.transform.position;
                if (!riderInWater && position.y - 0.5f <= backdrop.SurfaceHeight)
                {
                    backdrop.Splash(position);
                    riderInWater = true;
                }
                if (position.y < spawnPoint.y - 7f)
                    SpawnRider();
            }
            return;
        }

        float menuFraction = MenuWidth / Screen.width;
        mapCamera.rect = new Rect(menuFraction, 0f, 1f - menuFraction, 1f);
        MoveCamera();
        if (phase != RoundPhase.Placing)
            return;

        if (IsWall && !placedThisRound && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            wallQuarterTurns = (wallQuarterTurns + 1) % 4;

        Mouse mouse = Mouse.current;
        bool pointerOverMap = mouse != null && mapCamera.pixelRect.Contains(mouse.position.ReadValue());
        validPlacement = false;
        if (pointerOverMap)
        {
            Ray ray = mapCamera.ScreenPointToRay(mouse.position.ReadValue());
            UpdatePlacement(ray);
            if (mouse.leftButton.wasPressedThisFrame && !placedThisRound && validPlacement)
                PlacePiece();
        }

        preview.SetActive(pointerOverMap && !placedThisRound);
        preview.transform.SetPositionAndRotation(hoverPosition + hoverRotation * Vector3.up * 0.035f, hoverRotation);
        previewMaterial.color = validPlacement ? new Color(0.28f, 0.88f, 0.36f) : new Color(0.94f, 0.18f, 0.16f);
    }

    private void LateUpdate()
    {
        if (phase == RoundPhase.Riding && mapCamera != null)
            FollowRider(false);
    }

    private void UpdatePlacement(Ray ray)
    {
        if (IsSection)
        {
            hoverPosition = CellPosition(new Vector2Int(courseColumn, nextRow), nextHeight);
            hoverRotation = Quaternion.identity;
            Vector3 center = hoverPosition + new Vector3(0f, 0.35f, CellSize * (SectionRows - 1) * 0.5f);
            Vector3 size = new Vector3(CellSize * (SideColumn * 2 + 1), 0.8f, CellSize * SectionRows);
            validPlacement = new Bounds(center, size).IntersectRay(ray);
            return;
        }

        bool hitTrack = RaycastTrack(ray, 300f, out RaycastHit hit);
        var gridPlane = new Plane(Vector3.up, Vector3.up * viewCenter.y);
        if (!hitTrack && !gridPlane.Raycast(ray, out _))
            return;

        gridPlane.Raycast(ray, out float distance);
        Vector3 point = hitTrack ? hit.point : ray.GetPoint(distance);
        selectedCell = new Vector2Int(Mathf.RoundToInt(point.x / CellSize),
                                      Mathf.RoundToInt(point.z / CellSize));
        hoverPosition = CellPosition(selectedCell, point.y);
        hoverRotation = Quaternion.identity;

        if (!hitTrack || !tileHeights.TryGetValue(selectedCell, out float height))
            return;

        Vector3 aboveCenter = CellPosition(selectedCell, height + 2f);
        if (RaycastTrack(new Ray(aboveCenter, Vector3.down), 3f, out RaycastHit surface))
        {
            hoverPosition = surface.point;
            hoverRotation = Quaternion.FromToRotation(Vector3.up, surface.normal);
            if (IsWall)
                hoverRotation *= Quaternion.Euler(0f, wallQuarterTurns * 90f, 0f);
            validPlacement = hit.normal.y > 0.5f && !obstacles.Contains(selectedCell);
        }
    }

    private bool RaycastTrack(Ray ray, float distance, out RaycastHit hit)
    {
        if (!Physics.Raycast(ray, out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;
        if (trackColliders.Contains(hit.collider))
            return true;

        float nearest = float.PositiveInfinity;
        foreach (RaycastHit candidate in Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers,
                                                            QueryTriggerInteraction.Ignore))
        {
            if (candidate.distance < nearest && trackColliders.Contains(candidate.collider))
            {
                hit = candidate;
                nearest = candidate.distance;
            }
        }

        return nearest < float.PositiveInfinity;
    }

    private void MoveCamera()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector3 direction = Vector3.zero;
            if ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)) direction += Vector3.left;
            if ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)) direction += Vector3.right;
            if ((keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)) direction += Vector3.forward;
            if ((keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)) direction += Vector3.back;
            viewCenter += direction.normalized * (8.5f * Time.unscaledDeltaTime);
        }

        float trackCenter = courseColumn * CellSize;
        viewCenter.x = Mathf.Clamp(viewCenter.x, trackCenter - CellSize * (SideColumn + 0.5f), trackCenter + CellSize * (SideColumn + 0.5f));
        viewCenter.z = Mathf.Clamp(viewCenter.z, firstRow * CellSize, (nextRow + SectionRows - 1) * CellSize);
        float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        mapCamera.transform.position = Vector3.Lerp(mapCamera.transform.position, viewCenter + cameraOffset, blend);
    }

    private void OnGUI()
    {
        if (mapCamera == null)
            return;

        GUISkin originalSkin = GUI.skin;
        if (menuSkin == null && menuFont != null)
        {
            menuSkin = Instantiate(originalSkin);
            menuSkin.font = menuFont;
            menuSkin.label.font = menuFont;
            menuSkin.label.fontSize = 13;
            menuSkin.label.wordWrap = true;
            menuSkin.button.font = menuFont;
            menuSkin.button.fontSize = 13;
        }

        if (menuSkin != null)
            GUI.skin = menuSkin;
        if (menuHeading == null)
            menuHeading = new GUIStyle(GUI.skin.label) { fontSize = 17 };

        try
        {
            DrawMenu();
        }
        finally
        {
            GUI.skin = originalSkin;
        }
    }

    private void DrawMenu()
    {

        if (pogoPlayerPrefab != null)
        {
            DrawRoundUI();
            return;
        }

        GUILayout.BeginArea(new Rect(0f, 0f, MenuWidth, Screen.height), GUI.skin.box);
        menuScroll = GUILayout.BeginScrollView(menuScroll);
        GUILayout.Label("Build Round " + round, menuHeading);
        GUI.enabled = !placedThisRound;
        GUILayout.Label("Platforms");
        GUILayout.BeginHorizontal();
        DrawChoice(Piece.Grass);
        DrawChoice(Piece.Ramp);
        GUILayout.EndHorizontal();

        GUILayout.Label("Obstacles");
        GUILayout.BeginHorizontal();
        DrawChoice(Piece.Spikes);
        DrawChoice(Piece.Barrel);
        DrawChoice(round % 2 == 1 ? Piece.Crate : Piece.Posts);
        GUILayout.EndHorizontal();
        DrawChoice(Piece.Wall);
        foreach (PrefabOption option in customPrefabs)
        {
            if (option != null && option.prefab != null)
                DrawPrefabChoice(option);
        }
        GUI.enabled = true;

        GUILayout.Space(6f);
        if (placedThisRound)
        {
            if (GUILayout.Button("End Round", GUILayout.Height(28f)))
            {
                round++;
                placedThisRound = false;
                Choose(Piece.Grass);
            }
        }
        else
        {
            GUILayout.Label(IsSection
                ? "Click the green preview to place the whole section at the front."
                : "Place an obstacle on an empty square.");
        }

        if (GUILayout.Button("Go to Front"))
            FocusFront();
        if (GUILayout.Button("Return to Start"))
            viewCenter = Vector3.zero;
        GUILayout.Label("Arrow keys move the camera.");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void BeginRide()
    {
        phase = RoundPhase.Riding;
        preview.SetActive(false);
        var spawnCell = new Vector2Int(courseColumn, firstRow);
        bool foundClearCell = false;
        float bestScore = float.PositiveInfinity;
        foreach (Vector2Int cell in tileHeights.Keys)
        {
            if (obstacles.Contains(cell))
                continue;
            float score = (cell.y - firstRow) * 3f + Mathf.Abs(cell.x - courseColumn);
            if (score >= bestScore)
                continue;
            bestScore = score;
            spawnCell = cell;
            foundClearCell = true;
        }

        if (!foundClearCell && !tileHeights.ContainsKey(spawnCell))
        {
            foreach (Vector2Int cell in tileHeights.Keys)
            {
                spawnCell = cell;
                break;
            }
        }

        spawnPoint = CellPosition(spawnCell, tileHeights[spawnCell] + (foundClearCell ? 0.58f : 3f));
        SpawnRider();
        FollowRider(true);
    }

    private void SpawnRider()
    {
        RemoveRider();
        riderInWater = false;
        rider = Instantiate(pogoPlayerPrefab, spawnPoint, Quaternion.identity, transform);
    }

    private void RemoveRider()
    {
        if (rider == null)
            return;
        rider.SetActive(false);
        Destroy(rider);
        rider = null;
    }

    private void FollowRider(bool immediate)
    {
        if (rider == null)
            return;

        Vector3 target = rider.transform.position + Vector3.up * 1.15f;
        float blend = immediate ? 1f : 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        Vector3 offset = new Vector3(0f, 2.05f, -6.3f);
        Vector3 desired = Vector3.Lerp(mapCamera.transform.position, target + offset, blend);
        desired.x = target.x;
        Vector3 cameraRay = desired - target;
        if (Physics.SphereCast(target, 0.22f, cameraRay.normalized, out RaycastHit hit, cameraRay.magnitude,
                               ~0, QueryTriggerInteraction.Ignore))
            desired = target + cameraRay.normalized * Mathf.Max(0.45f, hit.distance - 0.2f);

        desired.y = Mathf.Max(desired.y, target.y + 0.35f);
        mapCamera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(new Vector3(0f, -0.24f, 1f), Vector3.up));
    }

    private void EndRide()
    {
        RemoveRider();
        phase = RoundPhase.Choosing;
        placedThisRound = false;
        FocusFront();
        mapCamera.transform.SetPositionAndRotation(viewCenter + cameraOffset, buildCameraRotation);
        offers.Clear();
        var pool = new List<(Piece, PrefabOption)>();
        Piece guaranteedPlatform = Random.value < 0.5f ? Piece.Grass : Piece.Ramp;
        offers.Add((guaranteedPlatform, null));
        foreach (Piece piece in System.Enum.GetValues(typeof(Piece)))
        {
            if (piece != guaranteedPlatform)
                pool.Add((piece, null));
        }
        foreach (PrefabOption option in customPrefabs)
        {
            if (option != null && option.prefab != null)
                pool.Add((Piece.Barrel, option));
        }
        while (offers.Count < 4)
        {
            int index = Random.Range(0, pool.Count);
            offers.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }

    private void DrawRoundUI()
    {
        if (phase == RoundPhase.Choosing)
        {
            float width = Mathf.Min(520f, Screen.width - 20f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 24f, width, 196f), GUI.skin.box);
            GUILayout.Label("Choose one piece for Round " + (round + 1), menuHeading);
            foreach (var offer in offers)
            {
                string name = offer.custom == null
                    ? (offer.piece == Piece.Grass ? "Platform" : offer.piece.ToString())
                    : string.IsNullOrWhiteSpace(offer.custom.displayName) ? offer.custom.prefab.name : offer.custom.displayName;
                Texture thumbnail = offer.custom == null ? icons[(int)offer.piece] : offer.custom.thumbnail;
                if (!GUILayout.Button(new GUIContent(name, thumbnail), GUILayout.Height(35f)))
                    continue;
                if (offer.custom == null)
                    Choose(offer.piece);
                else
                    ChoosePrefab(offer.custom);
                phase = RoundPhase.Placing;
            }
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(8f, 8f, MenuWidth - 16f, phase == RoundPhase.Riding ? 132f : 180f), GUI.skin.box);
        GUILayout.Label("Round " + round, menuHeading);
        if (phase == RoundPhase.Riding)
        {
            GUILayout.Label("Time Space as you spin. Hold to charge, then release. WASD steers in the air.");
            if (GUILayout.Button("End Round"))
                EndRide();
        }
        else if (placedThisRound)
        {
            if (GUILayout.Button("Start Next Round"))
            {
                round++;
                BeginRide();
            }
        }
        else
        {
            GUILayout.Label(IsSection ? "Click the green section at the front." : "Click an empty tile.");
            GUILayout.Label("WASD moves the build camera.");
            if (IsWall)
                GUILayout.Label("R rotates the wall.");
            if (GUILayout.Button("Choose a Different Piece"))
            {
                preview.SetActive(false);
                phase = RoundPhase.Choosing;
            }
        }
        GUILayout.EndArea();
    }

    private void DrawChoice(Piece piece)
    {
        float width = (MenuWidth - 34f) / 3f;
        GUILayout.BeginVertical(GUILayout.Width(width));
        if (GUILayout.Button(icons[(int)piece], GUILayout.Height(54f), GUILayout.Width(width)))
            Choose(piece);
        string label = piece == Piece.Grass ? "Platform" : piece.ToString();
        GUILayout.Label(selectedPrefab == null && piece == selectedPiece ? "> " + label : label);
        GUILayout.EndVertical();
    }

    private void DrawPrefabChoice(PrefabOption option)
    {
        string label = string.IsNullOrWhiteSpace(option.displayName) ? option.prefab.name : option.displayName;
        if (selectedPrefab == option)
            label = "> " + label;

        var content = new GUIContent(label, option.thumbnail);
        if (!GUILayout.Button(content, GUILayout.Height(option.thumbnail == null ? 28f : 52f)))
            return;

        ChoosePrefab(option);
    }

    private void ChoosePrefab(PrefabOption option)
    {
        selectedPrefab = option;
        wallQuarterTurns = 0;
        var ghost = new GameObject("Prefab Preview");
        CopyPreviewGeometry(option.prefab.transform, ghost.transform);
        SitOnSurface(ghost);
        SetPreview(ghost);
    }

    private void Choose(Piece piece)
    {
        selectedPrefab = null;
        wallQuarterTurns = 0;
        selectedPiece = piece;
        SetPreview(IsSection ? CreateSection(piece, previewMaterial, false) : CreateObstacle(piece, previewMaterial));
    }

    private void SetPreview(GameObject newPreview)
    {
        if (preview != null)
        {
            preview.SetActive(false);
            Destroy(preview);
        }

        preview = newPreview;
        preview.name = "Placement Preview";
        preview.transform.SetParent(transform);
        preview.SetActive(false);
    }

    private void PlacePiece()
    {
        if (IsSection)
        {
            AppendSection(selectedPiece);
            FocusFront();
        }
        else if (selectedPrefab != null)
        {
            PlacePrefab(selectedPrefab.prefab);
            obstacles.Add(selectedCell);
        }
        else
        {
            GameObject instance = CreateObstacle(selectedPiece, modelMaterial);
            instance.transform.SetParent(pieces);
            instance.layer = ObstacleLayer;
            Bounds bounds = GetBounds(instance);
            BoxCollider collider = instance.AddComponent<BoxCollider>();
            collider.center = bounds.center - instance.transform.position;
            collider.size = bounds.size;
            instance.transform.SetPositionAndRotation(hoverPosition, hoverRotation);
            obstacles.Add(selectedCell);
        }

        placedThisRound = true;
    }

    private void FocusFront()
    {
        viewCenter = new Vector3(courseColumn * CellSize, nextHeight, (nextRow - 1) * CellSize);
    }

    private void AppendSection(Piece piece)
    {
        GameObject section = CreateSection(piece, modelMaterial, true);
        section.transform.SetParent(pieces);
        section.transform.position = CellPosition(new Vector2Int(courseColumn, nextRow), nextHeight);
        nextRow += SectionRows;
        if (piece == Piece.Ramp)
            nextHeight += RampRise;
    }

    private GameObject CreateSection(Piece piece, Material material, bool solid)
    {
        bool rising = piece == Piece.Ramp;
        var section = new GameObject(rising ? "Ramp Section" : "Grass Platform Section");
        for (int row = 0; row < SectionRows; row++)
        {
            Piece tilePiece = rising && row == SectionRows / 2 ? Piece.Ramp : Piece.Grass;
            float height = rising && row > SectionRows / 2 ? RampRise : 0f;
            for (int column = -SideColumn; column <= SideColumn; column++)
            {
                GameObject tile = CreateTile(tilePiece, material, solid);
                tile.transform.SetParent(section.transform, false);
                tile.transform.localPosition = new Vector3(column * CellSize, height, row * CellSize);
                if (solid)
                {
                    tileHeights.Add(new Vector2Int(courseColumn + column, nextRow + row), nextHeight + height);
                    RegisterTrackTile(tile);
                }
            }
        }

        return section;
    }

    private void RegisterTrackTile(GameObject tile)
    {
        if (!tile.TryGetComponent<PogoSurface>(out _))
            tile.AddComponent<PogoSurface>();
        foreach (Collider collider in tile.GetComponentsInChildren<Collider>())
            trackColliders.Add(collider);
    }

    private GameObject CreateTile(Piece piece, Material material, bool solid)
    {
        var root = new GameObject(piece + " Tile");
        bool isRamp = piece == Piece.Ramp;
        GameObject model = Instantiate(models[(int)piece], root.transform);
        if (isRamp)
        {
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale *= CellSize / 2f;
        }
        else
            model.transform.localScale *= CellSize;

        ApplyMaterial(model, material);
        Bounds bounds = GetBounds(model);
        float surface = isRamp ? bounds.min.y + RampLowEdge : bounds.max.y;
        model.transform.localPosition -= new Vector3(bounds.center.x, surface, bounds.center.z);

        if (solid && isRamp)
        {
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
        else if (solid)
        {
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(CellSize, bounds.size.y, CellSize);
            collider.center = Vector3.down * bounds.extents.y;
        }

        return root;
    }

    private GameObject CreateObstacle(Piece piece, Material material)
    {
        var root = new GameObject(piece.ToString());
        GameObject model = Instantiate(models[(int)piece], root.transform);
        ApplyMaterial(model, material);
        Bounds bounds = GetBounds(model);
        if (piece == Piece.Wall)
        {
            model.transform.localScale = Vector3.Scale(model.transform.localScale,
                new Vector3(1.8f / bounds.size.x, 1.45f / bounds.size.y, 0.38f / bounds.size.z));
        }
        else
        {
            model.transform.localScale *= 1.35f / Mathf.Max(bounds.size.x, bounds.size.z);
        }
        bounds = GetBounds(model);
        model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return root;
    }

    private void PlacePrefab(GameObject prefab)
    {
        var anchor = new GameObject(prefab.name);
        anchor.SetActive(false);
        Instantiate(prefab, anchor.transform);
        Bounds bounds = SitOnSurface(anchor);
        if (anchor.GetComponentInChildren<Collider>(true) == null && bounds.size.sqrMagnitude > 0f)
        {
            BoxCollider collider = anchor.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        anchor.transform.SetParent(pieces, false);
        anchor.transform.SetPositionAndRotation(hoverPosition, hoverRotation);
        anchor.SetActive(true);
    }

    private void CopyPreviewGeometry(Transform source, Transform parent)
    {
        if (!source.gameObject.activeSelf)
            return;

        var copy = new GameObject(source.name);
        copy.transform.SetParent(parent, false);
        copy.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
        copy.transform.localScale = source.localScale;

        Mesh mesh = null;
        if (source.TryGetComponent(out MeshRenderer renderer) && renderer.enabled &&
            source.TryGetComponent(out MeshFilter filter))
            mesh = filter.sharedMesh;
        else if (source.TryGetComponent(out SkinnedMeshRenderer skinned) && skinned.enabled)
            mesh = skinned.sharedMesh;

        if (mesh != null)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = previewMaterial;
            copy.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        foreach (Transform child in source)
            CopyPreviewGeometry(child, copy.transform);
    }

    private static Bounds SitOnSurface(GameObject anchor)
    {
        Renderer[] renderers = anchor.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds();

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);

        Vector3 offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        foreach (Transform child in anchor.transform)
            child.localPosition += offset;
        bounds.center += offset;
        return bounds;
    }

    private static void ApplyMaterial(GameObject model, Material material)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = material;
    }

    private static Vector3 CellPosition(Vector2Int cell, float height)
    {
        return new Vector3(cell.x * CellSize, height, cell.y * CellSize);
    }

    private static Bounds GetBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private void OnDestroy()
    {
        Destroy(modelMaterial);
        Destroy(menuSkin);
        Destroy(previewMaterial);
    }
}