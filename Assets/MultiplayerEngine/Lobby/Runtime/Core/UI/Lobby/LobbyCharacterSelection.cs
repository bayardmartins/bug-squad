using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages character selection in the lobby using a grid of clickable character buttons.
    /// </summary>
    public class LobbyCharacterSelection : MonoBehaviour
    {
        [Header("Character Display")]
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private Transform characterSpawnPoint;

        [Header("Stats Display")]
        [SerializeField] private Slider stealthSlider;
        [SerializeField] private Slider mobilitySlider;

        [Header("Grid Selection")]
        [Tooltip("Parent container for character selection buttons (e.g., GridLayoutGroup)")]
        [SerializeField] private Transform characterGridContainer;
        [Tooltip("Prefab for character selection button (must have Button and Image components)")]
        [SerializeField] private GameObject characterButtonPrefab;

        [Header("Selection Colors")]
        [SerializeField] private Color selectedColor = Color.green;
        [SerializeField] private Color normalColor = Color.white;

        private GameObject currentCharacterInstance;
        private List<CharacterData> characterData;
        private CharacterData selectedCharacter;
        private readonly List<Button> spawnedButtons = new();

        private void Awake()
        {
            // Subscribe to lobby events
            LobbyManagerBase.OnLobbyJoined += _ => OnLobbyEntered();
            LobbyManagerBase.OnLobbyCreated += _ => OnLobbyEntered();
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            LobbyManagerBase.OnLobbyJoined -= _ => OnLobbyEntered();
            LobbyManagerBase.OnLobbyCreated -= _ => OnLobbyEntered();
        }

        public void Initialize()
        {
            // Can be called manually if needed, but Awake handles event subscription
        }

        private void OnLobbyEntered()
        {
            SpawnCharacterButtons();
            if (characterData != null && characterData.Count > 0)
            {
                SelectCharacter(0);
            }
        }

        private void SpawnCharacterButtons()
        {
            // Clear existing buttons
            foreach (var btn in spawnedButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            spawnedButtons.Clear();

            var profileService = ServiceLocator.Get<IProfileService>();
            if (profileService == null) return;
            characterData = profileService.CharacterData;

            if (characterData == null || characterButtonPrefab == null || characterGridContainer == null) return;

            for (int i = 0; i < characterData.Count; i++)
            {
                var data = characterData[i];
                if (data == null) continue;

                var buttonObj = Instantiate(characterButtonPrefab, characterGridContainer);
                var button = buttonObj.GetComponent<Button>();
                var image = buttonObj.GetComponent<Image>();

                if (image != null && data.CharacterIcon != null)
                {
                    image.sprite = data.CharacterIcon;
                    image.color = normalColor;
                }

                if (button != null)
                {
                    int index = i; // Capture for closure
                    button.onClick.AddListener(() => SelectCharacter(index));
                    spawnedButtons.Add(button);
                }
            }
        }

        private async void SelectCharacter(int index)
        {
            if (characterData == null || index < 0 || index >= characterData.Count) return;

            selectedCharacter = characterData[index];

            // Update visual selection
            UpdateButtonSelection(index);

            // Update character name
            if (characterNameText != null)
                characterNameText.text = selectedCharacter.CharacterName;

            // Update stats
            if (stealthSlider != null) stealthSlider.value = selectedCharacter.Stealth;
            if (mobilitySlider != null) mobilitySlider.value = selectedCharacter.Mobility;

            // Spawn character model
            if (currentCharacterInstance != null)
            {
                Destroy(currentCharacterInstance);
            }

            if (selectedCharacter.CharacterLobbyPrefab != null && characterSpawnPoint != null)
            {
                currentCharacterInstance = Instantiate(selectedCharacter.CharacterLobbyPrefab, characterSpawnPoint.position, characterSpawnPoint.rotation);
            }

            // Update lobby
            var lobbyService = ServiceLocator.Get<ILobbyService>();
            if (lobbyService != null)
            {
                await lobbyService.UpdateCharacter(selectedCharacter.CharacterId);
            }
        }

        private void UpdateButtonSelection(int selectedIndex)
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                var image = spawnedButtons[i]?.GetComponent<Image>();
                if (image != null)
                {
                    image.color = (i == selectedIndex) ? selectedColor : normalColor;
                }
            }
        }
    }
}
