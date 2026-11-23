using System.Collections.Generic;
using UnityEngine;

public class BarnBackdrop : MonoBehaviour
{
    private Material nearMaterial;
    private Material farMaterial;
    private Renderer waterRenderer;
    private MaterialPropertyBlock rippleProperties;
    private Vector3 splashPosition;
    private float splashTime = float.NegativeInfinity;
    private bool rippleVisible;

    public float SurfaceHeight => detailWater != null ? detailWater.position.y + WaterHeight : WaterHeight;
    private const float WaterHeight = -3.6f;
    private const float IslandRepeat = 150f;
    private readonly List<(Transform group, float startZ)> islands = new List<(Transform, float)>();
    private Camera followCamera;
    [SerializeField] private Transform detailWater;
    [SerializeField] private Transform horizonWater;
    [SerializeField] private Transform woodedIslands;
    private Mesh waterMesh;
    private Mesh horizonMesh;

    public void Build(Material atlasMaterial, Camera camera)
    {
        followCamera = camera;
        var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            cameraData.requiresDepthTexture = true;
            var profile = Resources.Load<UnityEngine.Rendering.VolumeProfile>("BarnPieces/Scenery/Barn Finish Lighting");
            if (profile != null)
            {
                var volumeObject = new GameObject("Barn Color and Bloom");
                volumeObject.transform.SetParent(transform, false);
                var volume = volumeObject.AddComponent<UnityEngine.Rendering.Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
                cameraData.renderPostProcessing = true;
            }
        }
        if (detailWater != null && horizonWater != null && woodedIslands != null)
        {
            waterRenderer = detailWater.GetComponent<Renderer>();
            int islandIndex = 0;
            foreach (Transform island in woodedIslands)
            {
                islands.Add((island, island.position.z));
                DecorateIsland(island, atlasMaterial, islandIndex++);
            }
            BuildArchipelago(woodedIslands, atlasMaterial);
            return;
        }

        var scenery = new GameObject("Water and Wooded Islands");
        scenery.transform.SetParent(transform, false);
        BuildWater(scenery.transform);

        BuildArchipelago(scenery.transform, atlasMaterial);
    }

    private void BuildArchipelago(Transform parent, Material material)
    {
        GameObject terrain = Resources.Load<GameObject>("BarnPieces/Scenery/block-grass-large");
        GameObject pine = Resources.Load<GameObject>("BarnPieces/Scenery/tree-pine");
        GameObject oak = Resources.Load<GameObject>("BarnPieces/Scenery/tree");
        if (!terrain || !pine || !oak)
            return;

        var random = new System.Random(822);
        var locations = new[]
        {
            new Vector3(-13f, 3.2f, 28f), new Vector3(14f, 3.7f, 34f),
            new Vector3(-29f, 5.3f, 67f), new Vector3(36f, 6.6f, 78f),
            new Vector3(-49f, 7.8f, 102f), new Vector3(47f, 7.2f, 105f),
            new Vector3(-19f, 4.5f, 119f), new Vector3(20f, 5.3f, 135f)
        };

        foreach (Vector3 location in locations)
        {
            int index = islands.Count;
            var group = new GameObject("Lagoon Island " + (index + 1)).transform;
            group.SetParent(parent, false);
            group.position = new Vector3(location.x, SurfaceHeight - 0.48f, location.z);
            islands.Add((group, location.z));

            GameObject shore = AddModel(terrain, group, group.position, location.y, random.Next(360), material);
            Bounds baseBounds = shore.GetComponentInChildren<Renderer>().bounds;
            if (location.y >= 5f)
            {
                Vector3 hill = baseBounds.center + new Vector3(baseBounds.extents.x * 0.17f, 0f, baseBounds.extents.z * 0.12f);
                hill.y = baseBounds.max.y - 0.75f;
                GameObject upperGrass = AddModel(terrain, group, hill, location.y * 0.49f, random.Next(360), material);
                Bounds hillBounds = upperGrass.GetComponentInChildren<Renderer>().bounds;
                AddGrove(group, hillBounds, pine, oak, material, random, 5, 0.7f);
            }

            AddGrove(group, baseBounds, pine, oak, material, random, location.y >= 5f ? 10 : 5, 0.88f);
            DecorateIsland(group, material, index);
        }
    }

    private static void AddGrove(Transform group, Bounds terrain, GameObject pine, GameObject oak,
                                 Material material, System.Random random, int count, float ring)
    {
        float radius = Mathf.Min(terrain.extents.x, terrain.extents.z) * ring;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + (float)random.NextDouble() * 0.45f) * Mathf.PI * 2f / count;
            float distance = radius * (0.62f + (float)random.NextDouble() * 0.34f);
            Vector3 point = terrain.center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            point.y = terrain.max.y - 0.12f;
            float scale = 0.7f + (float)random.NextDouble() * 0.75f;
            AddModel(i % 3 == 0 ? oak : pine, group, point, scale, random.Next(360), material);
        }
    }

    private void DecorateIsland(Transform island, Material atlasMaterial, int index)
    {
        if (island.childCount == 0)
            return;

        Renderer terrain = island.GetChild(0).GetComponentInChildren<Renderer>();
        if (terrain == null)
            return;

        var rocks = Resources.Load<GameObject>("BarnPieces/Scenery/rocks");
        var flowers = Resources.Load<GameObject>("BarnPieces/Scenery/flowers");
        var mushrooms = Resources.Load<GameObject>("BarnPieces/Scenery/mushrooms");
        var pine = Resources.Load<GameObject>("BarnPieces/Scenery/tree-pine-small");
        if (rocks == null || flowers == null || mushrooms == null || pine == null)
            return;

        Bounds bounds = terrain.bounds;
        var detail = new GameObject("Island Details").transform;
        detail.SetParent(island, false);
        var random = new System.Random(527 + index);
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        for (int i = 0; i < 13; i++)
        {
            float angle = (i + random.Next(0, 90) / 100f) * Mathf.PI * 2f / 13f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float distance = radius * (i < 3 ? 1.03f : 0.70f + (float)random.NextDouble() * 0.14f);
            Vector3 point = bounds.center + direction * distance;
            point.y = i < 3 ? SurfaceHeight - 0.12f : bounds.max.y - 0.075f;
            GameObject model = i < 3 ? rocks : i < 7 ? flowers : i < 11 ? mushrooms : pine;
            float scale = i < 3 ? 1.8f : 0.75f + (float)random.NextDouble() * 0.56f;
            AddModel(model, detail, point, scale, random.Next(360), atlasMaterial);
        }
    }

    private void LateUpdate()
    {
        if (followCamera == null)
            return;

        UpdateRipple();
        Vector3 center = followCamera.transform.position;
        const float detailStep = 24f;
        detailWater.position = new Vector3(Mathf.Round(center.x / detailStep) * detailStep, detailWater.position.y,
                                           Mathf.Round(center.z / detailStep) * detailStep);
        horizonWater.position = new Vector3(center.x, horizonWater.position.y, center.z);

        foreach (var island in islands)
        {
            Vector3 position = island.group.position;
            float behindCamera = center.z - 28f;
            position.z = behindCamera + Mathf.Repeat(island.startZ - behindCamera, IslandRepeat);
            island.group.position = position;
        }
    }

    public void Splash(Vector3 position)
    {
        splashPosition = position;
        splashTime = Time.time;
        rippleVisible = true;
        UpdateRipple();
    }

    private void UpdateRipple()
    {
        if (!rippleVisible || waterRenderer == null)
            return;

        rippleProperties ??= new MaterialPropertyBlock();
        float age = Time.time - splashTime;
        rippleVisible = age < 1.35f;
        waterRenderer.GetPropertyBlock(rippleProperties);
        rippleProperties.SetVector("_Splash", new Vector4(splashPosition.x, splashPosition.z, age, rippleVisible ? 1f : 0f));
        waterRenderer.SetPropertyBlock(rippleProperties);
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
        var water = new GameObject("Lagoon Water");
        detailWater = water.transform;
        detailWater.SetParent(parent, false);
        waterMesh = new Mesh { name = "Barn Smooth Water" };
        BuildSurface(waterMesh);
        water.AddComponent<MeshFilter>().sharedMesh = waterMesh;
        var shader = Shader.Find("Pogo Barn/Animated Water") ?? Shader.Find("Universal Render Pipeline/Unlit");
        nearMaterial = new Material(shader);
        farMaterial = new Material(shader);
        farMaterial.SetFloat("_WaveHeight", 0f);
        farMaterial.SetFloat("_Detail", 0f);
        waterRenderer = water.AddComponent<MeshRenderer>();
        waterRenderer.sharedMaterial = nearMaterial;

        var horizon = new GameObject("Ocean Horizon");
        horizonWater = horizon.transform;
        horizonWater.SetParent(parent, false);
        horizonWater.localPosition = Vector3.up * (WaterHeight - 0.25f);
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
        horizon.AddComponent<MeshRenderer>().sharedMaterial = farMaterial;
    }

    public static void BuildSurface(Mesh mesh)
    {
        const int segments = 96;
        const float spacing = 3f;
        int stride = segments + 1;
        var vertices = new Vector3[stride * stride];
        var triangles = new int[segments * segments * 6];
        int index = 0;
        for (int z = 0; z < stride; z++)
        {
            for (int x = 0; x < stride; x++)
            {
                int corner = z * stride + x;
                vertices[corner] = new Vector3((x - segments * 0.5f) * spacing, WaterHeight,
                                               (z - segments * 0.5f) * spacing);
                if (x == segments || z == segments)
                    continue;
                triangles[index++] = corner;
                triangles[index++] = corner + stride;
                triangles[index++] = corner + 1;
                triangles[index++] = corner + 1;
                triangles[index++] = corner + stride;
                triangles[index++] = corner + stride + 1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.up * WaterHeight, new Vector3(segments * spacing, 0.6f, segments * spacing));
    }

    private void OnDestroy()
    {
        Destroy(nearMaterial);
        Destroy(farMaterial);
        Destroy(waterMesh);
        Destroy(horizonMesh);
    }
}
