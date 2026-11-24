using System;
using UnityEngine;
using UnityEngine.Rendering;

public class CharacterSelectionUI : MonoBehaviour
{
    private const string PreferenceKey = "Barn.SelectedCharacter";
    private const string ModelPath = "PlayerVisuals/CharacterModel";
    private CharacterDatabase database;
    private GameObject riderPrefab;
    private Func<Transform, RenderTexture> takePhoto;
    private int browseIndex;
    private bool isOpen;
    private bool photoPending;
    private RenderTexture photo;

    private int ChoiceCount => (database != null ? database.CharacterCount : 0) + 1;
    private CharacterData BrowsedCharacter => browseIndex == 0 ? null : database.GetCharacter(browseIndex - 1);

    public void Initialize(CharacterDatabase roster, GameObject player, Func<Transform, RenderTexture> photographer)
    {
        database = roster;
        riderPrefab = player;
        takePhoto = photographer;
    }

    public void Open()
    {
        CharacterData saved = GetSavedCharacter();
        browseIndex = saved != null ? database.characters.IndexOf(saved) + 1 : 0;
        isOpen = true;
        photoPending = true;
    }

    private void LateUpdate()
    {
        if (!isOpen || !photoPending) return;
        photoPending = false;
        ReleasePhoto();
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

        Transform original = riderPrefab.transform.Find(ModelPath);
        if (original == null || original.childCount == 0) return;
        GameObject preview = new GameObject("Character Preview");
        try
        {
            Transform defaultAnimal = Instantiate(original.GetChild(0), preview.transform, false);
            SetAnimal(defaultAnimal, BrowsedCharacter);
            photo = takePhoto(preview.transform);
        }
        finally
        {
            preview.SetActive(false);
            Destroy(preview);
        }
    }

    public bool Draw(GUIStyle button, GUIStyle heading)
    {
        float width = Mathf.Min(460f, Screen.width - 24f);
        float height = Mathf.Min(420f, Screen.height - 24f);
        float photoHeight = Mathf.Max(38f, height - 179f);
        var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
        GUI.Box(panel, GUIContent.none);
        var title = new GUIStyle(heading) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        var centered = new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(panel.x, panel.y + 12, width, 38), "Character Selection", title);

        var photoRect = new Rect(panel.x + 18, panel.y + 54, width - 36, photoHeight);
        Color originalColor = GUI.color;
        GUI.color = new Color(.13f, .14f, .17f);
        GUI.DrawTexture(photoRect, Texture2D.whiteTexture);
        GUI.color = originalColor;
        if (photo != null) GUI.DrawTexture(photoRect, photo, ScaleMode.ScaleToFit);

        CharacterData character = BrowsedCharacter;
        string label = character != null ? character.characterName : "Pig";
        float navigationY = photoRect.yMax + 9f;
        if (GUI.Button(new Rect(panel.x + 18, navigationY, 45, 34), "<", button)) Browse(-1);
        if (GUI.Button(new Rect(panel.xMax - 63, navigationY, 45, 34), ">", button)) Browse(1);
        GUI.Label(new Rect(panel.x + 67, navigationY, width - 134, 34),
                  label + "   " + (browseIndex + 1) + "/" + ChoiceCount, centered);

        float actionWidth = (width - 46) * .5f;
        if (GUI.Button(new Rect(panel.x + 18, navigationY + 52, actionWidth, 39), "Back", button))
        {
            Close();
            return true;
        }
        if (GUI.Button(new Rect(panel.xMax - actionWidth - 18, navigationY + 52, actionWidth, 39), "Select", button))
        {
            Confirm();
            return true;
        }
        return false;
    }

    private void Browse(int direction)
    {
        browseIndex = (browseIndex + direction + ChoiceCount) % ChoiceCount;
        photoPending = true;
    }

    private void Confirm()
    {
        CharacterData character = BrowsedCharacter;
        if (character != null && character.characterPrefab != null)
            PlayerPrefs.SetString(PreferenceKey, character.name);
        else
            PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        Close();
    }

    private void Close()
    {
        isOpen = false;
        ReleasePhoto();
    }

    public void ApplyToRider(GameObject rider)
    {
        CharacterData character = GetSavedCharacter();
        if (character == null) return;
        Transform holder = rider.transform.Find(ModelPath);
        if (holder != null && holder.childCount > 0) SetAnimal(holder.GetChild(0), character);
    }

    private CharacterData GetSavedCharacter()
    {
        string saved = PlayerPrefs.GetString(PreferenceKey);
        return database == null || string.IsNullOrEmpty(saved)
            ? null
            : database.characters.Find(character => character != null && character.name == saved);
    }

    private static void SetAnimal(Transform defaultAnimal, CharacterData character)
    {
        if (character == null || character.characterPrefab == null) return;

        var animal = Instantiate(character.characterPrefab, defaultAnimal.parent, false);
        animal.transform.SetLocalPositionAndRotation(defaultAnimal.localPosition, defaultAnimal.localRotation);
        animal.transform.localScale = defaultAnimal.localScale;

        Renderer original = defaultAnimal.GetComponentInChildren<Renderer>();
        if (original != null)
        {
            foreach (Renderer renderer in animal.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = original.sharedMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        defaultAnimal.gameObject.SetActive(false);
        Destroy(defaultAnimal.gameObject);
    }

    private void ReleasePhoto()
    {
        if (photo == null) return;
        photo.Release();
        Destroy(photo);
        photo = null;
    }

    private void OnDestroy() => ReleasePhoto();
}

