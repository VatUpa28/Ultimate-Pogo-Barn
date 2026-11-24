
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Ultimate Pogo Barn/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Information")]
    public string characterName;

    [TextArea(2, 4)]
    public string description;

    [Header("Character Prefab")]
    public GameObject characterPrefab;
}
