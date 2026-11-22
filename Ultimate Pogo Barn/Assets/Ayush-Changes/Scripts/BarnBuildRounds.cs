using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class BarnBuildRounds : MonoBehaviour
{
    private const float CellSize = 2f;
    private const float PanelWidth = 240f;
    private const float RampLowEdge = 0.24f;
    private const float RampRise = 0.54f;
    private const int ObstacleLayer = 2; // Ignore Raycast
    private static readonly Vector3 CameraOffset = new Vector3(0f, 7.5f, -17f);
    private static readonly string[] ModelNames =
    {
        "block-grass", "block-grass-large-slope", "barrel", "crate", "poles", "trap-spikes", "brick"
    };
    private const int SectionRows = 3;
    private const int SideColumn = 1;

    private enum Piece { Grass, Ramp, Barrel, Crate, Posts, Spikes, Wall }

    private readonly Dictionary<Vector2Int, float> tileHeights = new Dictionary<Vector2Int, float>();
    private readonly HashSet<Vector2Int> obstacles = new HashSet<Vector2Int>();
    private readonly GameObject[] models = new GameObject[ModelNames.Length];
    private readonly Texture2D[] icons = new Texture2D[ModelNames.Length];

    private Camera mapCamera;
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
    private int nextRow = -1;
    private float nextHeight;
    private Vector3 hoverPosition;
    private Quaternion hoverRotation = Quaternion.identity;
    private bool validPlacement;

    private bool IsSection => selectedPiece <= Piece.Ramp;

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

        modelMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        modelMaterial.mainTexture = Resources.Load<Texture2D>("BarnPieces/colormap");
        previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        pieces = new GameObject("Map Pieces").transform;
        pieces.SetParent(transform);

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

        AppendSection(Piece.Grass);

        gameObject.AddComponent<BarnBackdrop>().Build(modelMaterial, mapCamera);
        Choose(Piece.Grass);
    }

    private void Update()
    {
        if (mapCamera == null || preview == null)
            return;

        float menuFraction = MenuWidth / Screen.width;
        mapCamera.rect = new Rect(menuFraction, 0f, 1f - menuFraction, 1f);
        MoveCamera();

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

    private void UpdatePlacement(Ray ray)
    {
        if (IsSection)
        {
            hoverPosition = CellPosition(new Vector2Int(0, nextRow), nextHeight);
            hoverRotation = Quaternion.identity;
            Vector3 center = hoverPosition + new Vector3(0f, 0.35f, CellSize);
            Vector3 size = new Vector3(CellSize * 3f, 0.8f, CellSize * SectionRows);
            validPlacement = new Bounds(center, size).IntersectRay(ray);
            return;
        }

        bool hitTrack = Physics.Raycast(ray, out RaycastHit hit, 300f);
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
        if (Physics.Raycast(aboveCenter, Vector3.down, out RaycastHit surface, 3f))
        {
            hoverPosition = surface.point;
            hoverRotation = Quaternion.FromToRotation(Vector3.up, surface.normal);
            validPlacement = hit.normal.y > 0.5f && !obstacles.Contains(selectedCell);
        }
    }

    private void MoveCamera()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector3 direction = Vector3.zero;
            if (keyboard.leftArrowKey.isPressed) direction += Vector3.left;
            if (keyboard.rightArrowKey.isPressed) direction += Vector3.right;
            if (keyboard.upArrowKey.isPressed) direction += Vector3.forward;
            if (keyboard.downArrowKey.isPressed) direction += Vector3.back;
            viewCenter += direction.normalized * (7f * Time.unscaledDeltaTime);
        }

        float blend = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        mapCamera.transform.position = Vector3.Lerp(mapCamera.transform.position, viewCenter + CameraOffset, blend);
    }

    private void OnGUI()
    {
        if (mapCamera == null)
            return;

        GUILayout.BeginArea(new Rect(0f, 0f, MenuWidth, Screen.height), GUI.skin.box);
        menuScroll = GUILayout.BeginScrollView(menuScroll);
        GUILayout.Label("Build Round " + round);
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

    private void DrawChoice(Piece piece)
    {
        float width = (MenuWidth - 34f) / 3f;
        GUILayout.BeginVertical(GUILayout.Width(width));
        if (GUILayout.Button(icons[(int)piece], GUILayout.Height(54f), GUILayout.Width(width)))
            Choose(piece);
        string label = piece == Piece.Grass ? "Platform" : piece.ToString();
        GUILayout.Label(piece == selectedPiece ? "> " + label : label);
        GUILayout.EndVertical();
    }

    private void Choose(Piece piece)
    {
        if (preview != null)
        {
            preview.SetActive(false);
            Destroy(preview);
        }

        selectedPiece = piece;
        preview = IsSection ? CreateSection(piece, previewMaterial, false) : CreateObstacle(piece, previewMaterial);
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
        viewCenter = new Vector3(0f, nextHeight, (nextRow - 1) * CellSize);
    }

    private void AppendSection(Piece piece)
    {
        GameObject section = CreateSection(piece, modelMaterial, true);
        section.transform.SetParent(pieces);
        section.transform.position = CellPosition(new Vector2Int(0, nextRow), nextHeight);
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
            Piece tilePiece = rising && row == 1 ? Piece.Ramp : Piece.Grass;
            float height = rising && row == 2 ? RampRise : 0f;
            for (int column = -SideColumn; column <= SideColumn; column++)
            {
                GameObject tile = CreateTile(tilePiece, material, solid);
                tile.transform.SetParent(section.transform, false);
                tile.transform.localPosition = new Vector3(column * CellSize, height, row * CellSize);
                if (solid)
                    tileHeights.Add(new Vector2Int(column, nextRow + row), nextHeight + height);
            }
        }

        return section;
    }

    private GameObject CreateTile(Piece piece, Material material, bool solid)
    {
        var root = new GameObject(piece + " Tile");
        bool isRamp = piece == Piece.Ramp;
        GameObject model = Instantiate(models[(int)piece], root.transform);
        if (isRamp)
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
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
        Destroy(previewMaterial);
    }
}
