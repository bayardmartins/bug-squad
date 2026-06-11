using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Holds character-related data for multiplayer functionality.
    /// </summary>

    [CreateAssetMenu(fileName = "CharacterData", menuName = "Ignitives/MultiplayerEngine/Character Data", order = 1)]
    public class CharacterData : ScriptableObject
    {
        [SerializeField] private string characterName;
        [SerializeField] private string characterId;
        [SerializeField] private Sprite characterIcon;
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private GameObject characterLobbyPrefab;
        [Range(0f, 1f)] [SerializeField] private float stealth;
        [Range(0f, 1f)] [SerializeField] private float mobility;

        public string CharacterName => characterName;
        public Sprite CharacterIcon => characterIcon;
        public string CharacterId => characterId;
        public GameObject CharacterPrefab => characterPrefab;
        public GameObject CharacterLobbyPrefab => characterLobbyPrefab;
        public float Stealth => stealth;
        public float Mobility => mobility;
    }
}
