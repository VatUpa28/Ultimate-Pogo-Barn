using System.Collections.Generic;
using UnityEngine;

public class BarnBackdrop : MonoBehaviour
{
    private readonly List<Material> waterMaterials = new List<Material>();
    private const float WaterHeight = -3.6f;
    private const float IslandRepeat = 96f;
    private readonly List<(Transform group, float startZ)> islands = new List<(Transform, float)>();
    private Camera followCamera;
    private Transform detailWater;
    private Transform horizonWater;
    private Mesh waterMesh;
    private Mesh horizonMesh;

    public void Build(Material atlasMaterial, Camera camera)
    {
        followCamera = camera;
        var scenery = new GameObject("Water and Wooded Islands");
        scenery.transform.SetParent(transform, false);
        BuildWater(scenery.transform);

        GameObject islandTile = Resources.Load<GameObject>("BarnPieces/Scenery/block-grass-large");
        GameObject pine = Resources.Load<GameObject>("BarnPieces/Scenery/tree-pine");
        GameObject oak = Resources.Load<GameObject>("BarnPieces/Scenery/tree");
        var random = new System.Random(803);
        foreach (Vector3 position in new[]
        {
            new Vector3(-18f, -3.9f, 15f), new Vector3(19f, -3.9f, 22f),
            new Vector3(-32f, -3.9f, 43f), new Vector3(32f, -3.9f, 49f)
        })
        {
            var group = new GameObject("Wooded Island").transform;
            group.SetParent(scenery.transform);
            group.position = position;
            islands.Add((group, position.z));
            GameObject island = AddModel(islandTile, group, position, 5f, random.Next(360), atlasMaterial);
            Bounds bounds = island.GetComponentInChildren<Renderer>().bounds;
            for (int i = 0; i < 11; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 0.7f + (float)random.NextDouble() * 3.25f;
                Vector3 treePosition = new Vector3(bounds.center.x + Mathf.Cos(angle) * radius,
                                                   bounds.max.y - 0.12f,
                                                   bounds.center.z + Mathf.Sin(angle) * radius);
                AddModel(i % 3 == 0 ? oak : pine, group, treePosition,
                         0.8f + (float)random.NextDouble() * 0.9f, random.Next(360), atlasMaterial);
            }
        }
    }

    private void LateUpdate()
    {
        if (followCamera == null)
            return;

        Vector3 center = followCamera.transform.position;
        const float detailStep = 24f;
        detailWater.position = new Vector3(Mathf.Round(center.x / detailStep) * detailStep, 0f,
                                           Mathf.Round(center.z / detailStep) * detailStep);
        horizonWater.position = new Vector3(center.x, WaterHeight - 0.25f, center.z);

        foreach (var island in islands)
        {
            Vector3 position = island.group.position;
            float behindCamera = center.z - 28f;
            position.z = behindCamera + Mathf.Repeat(island.startZ - behindCamera, IslandRepeat);
            island.group.position = position;
        }
    }

    private static GameObject AddModel(GameObject asset, Transform parent, Vector3 position,
                                       float scale, float angle, Material material)
    {
        GameObject model = Instantiate(asset, position, Quaternion.Euler(0f, angle, 0f), parent);
        model.transform.localScale *= scale;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = material;
        return model;
    }

    private void BuildWater(Transform parent)
    {
        const int size = 48;
        const float spacing = 6f;
        var vertices = new List<Vector3>();
        var facets = new List<int>[] { new List<int>(), new List<int>(), new List<int>() };
        var random = new System.Random(100);
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector3 a = WaterPoint(x, z, size, spacing);
                Vector3 b = WaterPoint(x + 1, z, size, spacing);
                Vector3 c = WaterPoint(x, z + 1, size, spacing);
                Vector3 d = WaterPoint(x + 1, z + 1, size, spacing);
                bool outer = x < 5 || z < 5 || x >= size - 5 || z >= size - 5;
                AddFacet(a, c, b, vertices, facets[outer ? 1 : random.Next(facets.Length)]);
                AddFacet(b, c, d, vertices, facets[outer ? 1 : random.Next(facets.Length)]);
            }
        }

        var water = new GameObject("Faceted Water");
        detailWater = water.transform;
        detailWater.SetParent(parent, false);
        waterMesh = new Mesh { name = "Barn Water Facets" };
        waterMesh.SetVertices(vertices);
        waterMesh.subMeshCount = facets.Length;
        for (int i = 0; i < facets.Length; i++)
            waterMesh.SetTriangles(facets[i], i);
        waterMesh.RecalculateNormals();
        water.AddComponent<MeshFilter>().sharedMesh = waterMesh;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        waterMaterials.Add(new Material(shader) { color = new Color(0.29f, 0.67f, 0.82f) });
        waterMaterials.Add(new Material(shader) { color = new Color(0.33f, 0.72f, 0.85f) });
        waterMaterials.Add(new Material(shader) { color = new Color(0.36f, 0.75f, 0.87f) });
        water.AddComponent<MeshRenderer>().sharedMaterials = waterMaterials.ToArray();

        var horizon = new GameObject("Ocean Horizon");
        horizonWater = horizon.transform;
        horizonWater.SetParent(parent, false);
        float extent = followCamera.farClipPlane * 2f;
        horizonMesh = new Mesh { name = "Barn Ocean Horizon" };
        horizonMesh.vertices = new[]
        {
            new Vector3(-extent, 0f, -extent), new Vector3(-extent, 0f, extent),
            new Vector3(extent, 0f, -extent), new Vector3(extent, 0f, extent)
        };
        horizonMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        horizonMesh.RecalculateNormals();
        horizon.AddComponent<MeshFilter>().sharedMesh = horizonMesh;
        horizon.AddComponent<MeshRenderer>().sharedMaterial = waterMaterials[1];
    }

    private static Vector3 WaterPoint(int x, int z, int size, float spacing)
    {
        float wave = Mathf.Sin(x * 1.57f + z * 0.74f) * 0.2f;
        float stagger = z % 2 == 0 ? 0f : spacing * 0.5f;
        return new Vector3((x - size / 2) * spacing + stagger, WaterHeight + wave, (z - size / 2) * spacing);
    }

    private static void AddFacet(Vector3 a, Vector3 b, Vector3 c,
                                 List<Vector3> vertices, List<int> indices)
    {
        indices.Add(vertices.Count);
        indices.Add(vertices.Count + 1);
        indices.Add(vertices.Count + 2);
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
    }

    private void OnDestroy()
    {
        foreach (Material material in waterMaterials)
            Destroy(material);
        Destroy(waterMesh);
        Destroy(horizonMesh);
    }
}
