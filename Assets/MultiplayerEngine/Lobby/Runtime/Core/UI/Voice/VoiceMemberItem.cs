using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Ignitives.MultiplayerEngine
{
    public class VoiceMemberItem : MonoBehaviour
    {
        [SerializeField] private Image playerIcon;
        [SerializeField] private Button muteButton;
        [SerializeField] private Image muteIcon;

        [SerializeField] private TMP_Text playerName;
        [SerializeField] private Slider sliderVolume;

        public string PlayerId { get; private set; }

        private bool IsMuted;   

        private void Awake()
        {
            sliderVolume.onValueChanged.AddListener(OnVolumeChanged);
            muteButton.onClick.AddListener(() =>
            {
                if (IsMuted)
                {
                    VoiceManager.Instance.UnmuteMember(PlayerId);
                }
                else
                {
                    VoiceManager.Instance.MuteMember(PlayerId);
                }
            });
        }

        private void OnVolumeChanged(float volume)
        {
            VoiceManager.Instance.SetMemberVolume((int)volume, PlayerId);
        }

        public void Initialize(string playerName, string playerId, Sprite playerIcon)
        {
            this.playerIcon.sprite = playerIcon;
            this.playerName.text = playerName;
            this.PlayerId = playerId;
        }

        public void SetMutedState(bool isMuted)
        {
            IsMuted = isMuted;
            if (muteIcon != null)
            {
                muteIcon.gameObject.SetActive(isMuted);
            }
        }

        public void SetVolume(int volume)
        {
            sliderVolume.maxValue = 100;
            sliderVolume.minValue = 0;
            sliderVolume.value = volume;
        }
    }
}
