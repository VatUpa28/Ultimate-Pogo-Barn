
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Ultimate Pogo Barn/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    [Header("Available Characters")]
    public List<CharacterData> characters = new List<CharacterData>();

    public int CharacterCount => characters.Count;

    public CharacterData GetCharacter(int index)
    {
        if (characters.Count == 0)
            return null;

        index = (index % characters.Count + characters.Count) % characters.Count;

        return characters[index];
    }
}
