using UnityEngine;

public partial class BarnBuildRounds
{
    private Material dungeonMaterial;
    private Material sawMaterial;
    private Mesh sawMesh;

    private Material DungeonMaterial()
    {
        if (dungeonMaterial != null) return dungeonMaterial;
        dungeonMaterial = new Material(modelMaterial);
        dungeonMaterial.mainTexture = Resources.Load<Texture2D>("BarnCourse/Dungeon/Textures/colormap");
        return dungeonMaterial;
    }

    private bool HitsHazard(Vector3 position)
    {
        var cell = new Vector2Int(Mathf.RoundToInt(position.x / CellSize), Mathf.RoundToInt(position.z / CellSize));
        if (!hazardCells.Contains(cell) || !tileHeights.TryGetValue(cell, out float height)) return false;
        Vector3 offset = position - CellPosition(cell, height);
        return offset.y >= 0.15f && offset.y <= 1.45f && offset.x * offset.x + offset.z * offset.z < .78f;
    }

    private GameObject CreateSaw(Material material, bool forPhoto)
    {
        var root = new GameObject("Saw");
        var blade = new GameObject("Spinning Blade").transform;
        blade.SetParent(root.transform, false);
        blade.localPosition = Vector3.up * 0.65f;
        blade.gameObject.AddComponent<MeshFilter>().sharedMesh = SawMesh();
        var renderer = blade.gameObject.AddComponent<MeshRenderer>();
        if (material == modelMaterial)
        {
            if (!sawMaterial)
            {
                sawMaterial = new Material(previewMaterial) { color = new Color(.67f,.80f,.91f) };
                sawMaterial.SetFloat("_Gloss", .73f);
            }
            renderer.sharedMaterial = sawMaterial;
            if (!forPhoto) sawBlades.Add(blade);
        }
        else renderer.sharedMaterial = material;

        GameObject shield = Resources.Load<GameObject>("BarnCourse/Dungeon/shield-round");
        if (shield && material == modelMaterial)
        {
            var hub = Instantiate(shield, blade);
            Bounds bounds = GetBounds(hub);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            hub.transform.localScale *= .36f / Mathf.Max(.01f, size);
            foreach (Renderer side in hub.GetComponentsInChildren<Renderer>())
                side.sharedMaterial = DungeonMaterial();
            hub.transform.position += blade.position - GetBounds(hub).center + Vector3.forward * .09f;
        }
        return root;
    }

    private Mesh SawMesh()
    {
        if (sawMesh) return sawMesh;
        const int edge = 42;
        var vertices = new Vector3[edge * 2 + 2];
        var indices = new int[edge * 12];
        vertices[edge * 2] = new Vector3(0, 0, -.06f);
        vertices[edge * 2 + 1] = new Vector3(0, 0, .06f);
        for (int i = 0; i < edge; i++)
        {
            float angle = i * Mathf.PI * 2f / edge;
            float radius = i % 3 == 0 ? .66f : .49f;
            Vector3 point = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -.06f);
            vertices[i] = point;
            vertices[i + edge] = point + Vector3.forward * .12f;
            int next = (i + 1) % edge;
            int t = i * 12;
            indices[t] = edge * 2;
            indices[t + 1] = next;
            indices[t + 2] = i;
            indices[t + 3] = edge * 2 + 1;
            indices[t + 4] = i + edge;
            indices[t + 5] = next + edge;
            indices[t + 6] = i;
            indices[t + 7] = next;
            indices[t + 8] = next + edge;
            indices[t + 9] = i;
            indices[t + 10] = next + edge;
            indices[t + 11] = i + edge;
        }
        sawMesh = new Mesh { name = "Toothed Saw", vertices = vertices, triangles = indices, uv = new Vector2[vertices.Length] };
        sawMesh.RecalculateNormals();
        return sawMesh;
    }
}
