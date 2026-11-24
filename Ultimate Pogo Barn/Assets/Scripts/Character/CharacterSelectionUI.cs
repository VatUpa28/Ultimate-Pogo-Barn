
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Character Database")]
    [SerializeField] private CharacterDatabase characterDatabase;

    [Header("Preview")]
    [SerializeField] private Transform previewSpawnPoint;

    [Header("UI References")]
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button selectButton;

    private int currentCharacterIndex = 0;
    private GameObject currentPreview;

    private void Start()
    {
        previousButton.onClick.AddListener(PreviousCharacter);
        nextButton.onClick.AddListener(NextCharacter);
        selectButton.onClick.AddListener(SelectCharacter);

        UpdatePreview();
    }

    private void PreviousCharacter()
    {
        currentCharacterIndex--;

        if (currentCharacterIndex < 0)
            currentCharacterIndex = characterDatabase.CharacterCount - 1;

        UpdatePreview();
    }

    private void NextCharacter()
    {
        currentCharacterIndex++;

        if (currentCharacterIndex >= characterDatabase.CharacterCount)
            currentCharacterIndex = 0;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (characterDatabase == null ||
            characterDatabase.CharacterCount == 0)
            return;

        CharacterData character =
            characterDatabase.GetCharacter(currentCharacterIndex);

        if (character == null)
            return;

        if (currentPreview != null)
            Destroy(currentPreview);

        currentPreview = Instantiate(
            character.characterPrefab,
            previewSpawnPoint.position,
            previewSpawnPoint.rotation,
            previewSpawnPoint
        );

        characterNameText.text = character.characterName;
    }

    private void SelectCharacter()
    {
        CharacterData selectedCharacter =
            characterDatabase.GetCharacter(currentCharacterIndex);

        Debug.Log("Selected Character: " +
                  selectedCharacter.characterName);

        // Player model replacement will be implemented next.
    }
}
