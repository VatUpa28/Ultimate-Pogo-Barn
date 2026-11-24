using System.Collections.Generic;
using UnityEngine;

public partial class BarnBuildRounds
{
    private const string CustomPieceResources = "BarnCourse/CustomPieces";
    private const string PlatformTag = "BarnPlatform";
    private const string ObstacleTag = "BarnObstacle";

    private static bool IsPlatformPrefab(GameObject prefab)
        => prefab != null && prefab.tag == PlatformTag;

    private void LoadTaggedBuildPrefabs()
    {
        var registered = new HashSet<GameObject>();
        foreach (PrefabOption option in customPrefabs)
            if (option != null && option.prefab) registered.Add(option.prefab);

        foreach (GameObject prefab in Resources.LoadAll<GameObject>(CustomPieceResources))
        {
            if ((prefab.tag == PlatformTag || prefab.tag == ObstacleTag) &&
                prefab.GetComponentInChildren<Renderer>() != null && registered.Add(prefab))
                customPrefabs.Add(new PrefabOption { prefab = prefab });
        }
    }

    private void AppendCustomPlatform(GameObject prefab)
    {
        GameObject section = CreateCustomPlatform(prefab, true, true);
        section.transform.SetParent(pieces, false);
        section.transform.position = CellPosition(new Vector2Int(courseColumn, nextRow), nextHeight);
        for (int row = 0; row < SectionRows; row++)
            for (int column = -SideColumn; column <= SideColumn; column++)
                tileHeights.Add(new Vector2Int(courseColumn + column, nextRow + row), nextHeight);
        nextRow += SectionRows;
        section.SetActive(true);
        RegisterTrackTile(section);
    }

    private GameObject CreateCustomPlatform(GameObject prefab, bool solid, bool keepMaterials)
    {
        var section = new GameObject(prefab.name);
        var geometry = new GameObject("Platform Geometry").transform;
        geometry.SetParent(section.transform, false);
        if (solid)
        {
            section.SetActive(false);
            Instantiate(prefab, geometry);
        }
        else
        {
            CopyPreviewGeometry(prefab.transform, geometry, keepMaterials);
        }

        Bounds bounds = GetBounds(geometry.gameObject, true);
        if (bounds.size == Vector3.zero) return section;

        float width = (SideColumn * 2 + 1) * CellSize;
        float length = SectionRows * CellSize;
        geometry.localScale = new Vector3(width / Mathf.Max(.01f, bounds.size.x), 1f,
                                           length / Mathf.Max(.01f, bounds.size.z));
        bounds = GetBounds(geometry.gameObject, true);

        Vector3 deckCenter = Vector3.forward * (CellSize * (SectionRows - 1) * .5f);
        Vector3 correction = deckCenter - new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        geometry.localPosition += correction;

        if (solid)
        {
            bool hasSolidCollider = false;
            foreach (Collider collider in geometry.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                collider.gameObject.layer = 0;
                hasSolidCollider = true;
            }
            if (!hasSolidCollider)
            {
                var floor = section.AddComponent<BoxCollider>();
                float depth = Mathf.Max(bounds.size.y, .1f);
                floor.center = deckCenter - Vector3.up * (depth * .5f);
                floor.size = new Vector3(width, depth, length);
            }
        }

        return section;
    }
}
