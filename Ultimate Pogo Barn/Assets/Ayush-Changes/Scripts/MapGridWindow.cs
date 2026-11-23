#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapGridWindow : EditorWindow
{
    private const int GridRadius = 3;
    private const string PiecesRootName = "Map Grid Pieces";

    private enum Piece { Platform, Block, Barrel }

    private Piece piece;
    private GameObject prefab;
    private float cellSize = 2f;
    private Vector3 origin;
    private Vector2Int cell;

    [MenuItem("Ultimate Pogo Barn/Map Grid")]
    private static void Open()
    {
        GetWindow<MapGridWindow>("Map Grid");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawSceneGrid;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneGrid;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        Scene scene = SceneManager.GetActiveScene();
        EditorGUILayout.LabelField("Scene", scene.name);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                                           !scene.IsValid() || !scene.isLoaded))
        {
            origin = EditorGUILayout.Vector3Field("Grid origin", origin);
            if (GUILayout.Button("Use Selected Object as Origin") && Selection.activeTransform != null)
                origin = Selection.activeTransform.position;

            cellSize = Mathf.Max(0.25f, EditorGUILayout.FloatField("Cell size", cellSize));
            piece = (Piece)EditorGUILayout.EnumPopup("Shape", piece);
            prefab = (GameObject)EditorGUILayout.ObjectField("Prefab Override", prefab, typeof(GameObject), false);
            EditorGUILayout.LabelField("Select a cell, then place a piece.");

            Transform root = FindPiecesRoot(scene);
            for (int z = GridRadius; z >= -GridRadius; z--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int x = -GridRadius; x <= GridRadius; x++)
                    {
                        var candidate = new Vector2Int(x, z);
                        bool occupied = FindPiece(root, CellPosition(candidate)) != null;
                        string label = occupied ? "X" : "+";
                        if (candidate == cell)
                            label = "[" + label + "]";

                        if (GUILayout.Button(label))
                            cell = candidate;
                    }
                }
            }

            EditorGUILayout.LabelField("Selected cell", $"X {cell.x}, Z {cell.y}");
            Vector3 position = CellPosition(cell);
            Transform existingPiece = FindPiece(root, position);

            using (new EditorGUI.DisabledScope(existingPiece != null))
            {
                if (GUILayout.Button("Place Piece"))
                    PlacePiece(scene, root, position);
            }

            using (new EditorGUI.DisabledScope(existingPiece == null))
            {
                if (GUILayout.Button("Remove Piece"))
                    Undo.DestroyObjectImmediate(existingPiece.gameObject);
            }
        }

        if (GUI.changed || Event.current.type == EventType.Used)
            SceneView.RepaintAll();
    }

    private Vector3 CellPosition(Vector2Int coordinates)
    {
        return origin + new Vector3(coordinates.x * cellSize, 0f, coordinates.y * cellSize);
    }

    private static Transform FindPiecesRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == PiecesRootName)
                return root.transform;
        }

        return null;
    }

    private static Transform FindPiece(Transform root, Vector3 position)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if ((child.position - position).sqrMagnitude < 0.0001f)
                return child;
        }

        return null;
    }

    private void PlacePiece(Scene scene, Transform root, Vector3 position)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place Map Piece");

        if (root == null)
        {
            var container = new GameObject(PiecesRootName);
            Undo.RegisterCreatedObjectUndo(container, "Create Map Pieces Root");
            root = container.transform;
        }

        var anchor = new GameObject(prefab != null ? prefab.name : piece.ToString());
        Undo.RegisterCreatedObjectUndo(anchor, "Create Map Piece");
        Undo.SetTransformParent(anchor.transform, root, "Parent Map Piece");
        Undo.RecordObject(anchor.transform, "Position Map Piece");
        anchor.transform.position = position;

        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Create Piece Prefab");
        }
        else
        {
            CreatePlaceholder(anchor.transform);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = anchor;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Repaint();
        SceneView.RepaintAll();
    }

    private void CreatePlaceholder(Transform anchor)
    {
        float width = cellSize * 0.9f;
        float height = cellSize * 0.5f;
        if (piece == Piece.Platform)
            height = cellSize * 0.15f;
        if (piece == Piece.Barrel)
            width = cellSize * 0.5f;

        PrimitiveType shape = piece == Piece.Barrel ? PrimitiveType.Cylinder : PrimitiveType.Cube;
        GameObject placeholder = GameObject.CreatePrimitive(shape);
        Undo.RegisterCreatedObjectUndo(placeholder, "Create Placeholder");
        Undo.SetTransformParent(placeholder.transform, anchor, "Parent Placeholder");
        Undo.RecordObject(placeholder.transform, "Fit Placeholder");
        placeholder.transform.localPosition = Vector3.up * (height * 0.5f);
        placeholder.transform.localScale = new Vector3(width, piece == Piece.Barrel ? height * 0.5f : height, width);
    }

    private void DrawSceneGrid(SceneView view)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        using (new Handles.DrawingScope(new Color(0.1f, 0.8f, 1f, 0.8f)))
        {
            float edge = (GridRadius + 0.5f) * cellSize;
            for (int i = -GridRadius; i <= GridRadius + 1; i++)
            {
                float line = (i - 0.5f) * cellSize;
                Handles.DrawLine(origin + new Vector3(line, 0f, -edge), origin + new Vector3(line, 0f, edge));
                Handles.DrawLine(origin + new Vector3(-edge, 0f, line), origin + new Vector3(edge, 0f, line));
            }
        }

        using (new Handles.DrawingScope(Color.yellow))
        {
            Vector3 center = CellPosition(cell);
            Handles.DrawWireCube(center, new Vector3(cellSize, 0.04f, cellSize));
        }
    }
}
#endif
