using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class BarnBuildRounds
{
    private const int PiecePhotoLayer = 31;
    private static readonly Color PiecePhotoBackground = new Color(.13f, .14f, .17f);
    private readonly Dictionary<(Piece, PrefabOption), RenderTexture> piecePhotos =
        new Dictionary<(Piece, PrefabOption), RenderTexture>();
    private Camera pieceCamera;

    private Texture GetPiecePhoto(Piece piece, PrefabOption option)
    {
        if (option != null && option.thumbnail != null)
            return option.thumbnail;
        if (piecePhotos.TryGetValue((piece, option), out RenderTexture photo) && photo != null)
            return photo;
        if (option != null)
            return null;
        if ((int)piece < icons.Length)
            return icons[(int)piece];
        return System.Array.IndexOf(Blueprints, piece) >= 0 ? icons[(int)Piece.Grass] : null;
    }

    private void PreparePiecePhotos()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            return;

        foreach (var offer in offers)
        {
            if ((offer.custom != null && offer.custom.thumbnail != null) || piecePhotos.ContainsKey(offer))
                continue;
            piecePhotos.Add(offer, PhotographPiece(offer.piece, offer.custom));
            break;
        }
    }

    private RenderTexture PhotographPiece(Piece piece, PrefabOption option)
    {
        GameObject model;
        if (option != null)
        {
            if (IsPlatformPrefab(option.prefab))
                model = CreateCustomPlatform(option.prefab, false, true);
            else
            {
                model = new GameObject("Custom Piece Photo");
                CopyPreviewGeometry(option.prefab.transform, model.transform, true);
            }
        }
        else
        {
            model = System.Array.IndexOf(Blueprints, piece) >= 0
                ? CreateSection(piece, modelMaterial, false)
                : CreateObstacle(piece, modelMaterial, true);
        }

        try
        {
            if (model.GetComponentInChildren<Renderer>() == null)
                return null;

            if (pieceCamera == null)
            {
                var cameraObject = new GameObject("Piece Photo Camera");
                cameraObject.transform.SetParent(transform, false);
                pieceCamera = cameraObject.AddComponent<Camera>();
                pieceCamera.enabled = false;
                pieceCamera.orthographic = true;
                pieceCamera.clearFlags = CameraClearFlags.SolidColor;
                pieceCamera.backgroundColor = PiecePhotoBackground;
                pieceCamera.cullingMask = 1 << PiecePhotoLayer;
                pieceCamera.farClipPlane = 120f;
                pieceCamera.allowHDR = false;
                UniversalAdditionalCameraData data = pieceCamera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
            }

            model.transform.position += Vector3.up * 2048f;
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = PiecePhotoLayer;

            Bounds bounds = GetBounds(model);
            const int width = 384;
            const int height = 272;
            Quaternion angle = Quaternion.LookRotation(new Vector3(-.92f, -.84f, 1.28f));
            pieceCamera.aspect = (float)width / height;
            pieceCamera.transform.SetPositionAndRotation(bounds.center - angle * Vector3.forward * 48f, angle);
            Vector3 right = pieceCamera.transform.right;
            Vector3 up = pieceCamera.transform.up;
            Vector3 extents = bounds.extents;
            float halfWidth = Vector3.Dot(extents, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)));
            float halfHeight = Vector3.Dot(extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)));
            pieceCamera.orthographicSize = Mathf.Max(.25f, Mathf.Max(halfHeight, halfWidth / pieceCamera.aspect) * 1.15f);

            var photo = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Piece Photo - " + (option?.prefab.name ?? piece.ToString()),
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = photo };
            if (!RenderPipeline.SupportsRenderRequest(pieceCamera, request))
            {
                Destroy(photo);
                return null;
            }

            RenderPipeline.SubmitRenderRequest(pieceCamera, request);
            return photo;
        }
        finally
        {
            model.SetActive(false);
            Destroy(model);
        }
    }

    private void DisposePiecePhotos()
    {
        foreach (RenderTexture photo in piecePhotos.Values)
        {
            if (photo == null) continue;
            photo.Release();
            Destroy(photo);
        }
        piecePhotos.Clear();
    }
}
