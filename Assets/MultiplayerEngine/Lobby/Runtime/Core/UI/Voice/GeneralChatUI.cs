using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Simple chat UI with auto-hide behavior.
    /// - Input section always visible
    /// - Chat window auto-shows for 2 sec when new message arrives
    /// - Manual button opens full chat with scroll history
    /// </summary>
    public class GeneralChatUI : MonoBehaviour
    {
        [Header("Chat Display")]
        [Tooltip("Container where chat messages are spawned.")]
        [SerializeField] private RectTransform messageHolder;

        [Tooltip("Prefab for individual chat message items.")]
        [SerializeField] private ChatMessageItem messageItem;

        [Tooltip("ScrollRect for the chat view.")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Scrollbar to hide in auto mode.")]
        [SerializeField] private Scrollbar scrollbar;

        [Tooltip("CanvasGroup for fading the chat window.")]
        [SerializeField] private CanvasGroup chatCanvasGroup;

        [Header("UI Controls")]
        [Tooltip("Button to manually show/hide full chat with scroll.")]
        [SerializeField] private Button showHideChatButton;

        [Tooltip("Input field for typing messages.")]
        [SerializeField] private TMP_InputField inputField;

        [Tooltip("Button to send message.")]
        [SerializeField] private Button sendButton;

        [Header("Settings")]
        [Tooltip("Time in seconds before auto-hiding chat.")]
        [SerializeField] private float autoHideDelay = 2f;

        [Tooltip("Duration of fade animation.")]
        [SerializeField] private float fadeDuration = 0.3f;

        [Tooltip("Maximum messages to keep in history.")]
        [SerializeField] private int maxMessages = 100;

        // State
        private readonly List<ChatMessageItem> messages = new();
        private Coroutine autoHideCoroutine;
        private Coroutine fadeCoroutine;
        private bool isManuallyOpened = false;  // True when user clicked show button
        private bool isChatVisible = false;

        /// <summary>
        /// Initialize the chat UI.
        /// </summary>
        public void Start()
        {
            SetupListeners();
            SubscribeToEvents();
            HideChatInstant();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #region Setup

        private void SetupListeners()
        {
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClicked);

            if (inputField != null)
                inputField.onSubmit.AddListener(OnInputSubmit);

            if (showHideChatButton != null)
                showHideChatButton.onClick.AddListener(OnShowHideClicked);

            UpdateSendButtonState();
        }

        private void SubscribeToEvents()
        {
            VoiceManager.OnMessageReceived += OnMessageReceived;
            VoiceManager.ChannelJoined += OnChannelJoined;
            VoiceManager.ChannelLeft += OnChannelLeft;
        }

        private void UnsubscribeFromEvents()
        {
            VoiceManager.OnMessageReceived -= OnMessageReceived;
            VoiceManager.ChannelJoined -= OnChannelJoined;
            VoiceManager.ChannelLeft -= OnChannelLeft;
        }

        #endregion

        #region Button Handlers

        private void OnSendClicked()
        {
            SendMessage();
        }

        private void OnInputSubmit(string text)
        {
            SendMessage();
        }

        /// <summary>
        /// Manual show/hide button clicked.
        /// </summary>
        private void OnShowHideClicked()
        {
            if (isManuallyOpened)
            {
                // Close chat
                isManuallyOpened = false;
                HideChat();
            }
            else
            {
                // Open chat with scroll visible
                isManuallyOpened = true;
                CancelAutoHide();
                ShowChat(showScrollbar: true);
            }
        }

        #endregion

        #region Sending Messages

        private void SendMessage()
        {
            if (inputField == null || string.IsNullOrWhiteSpace(inputField.text))
                return;

            string text = inputField.text.Trim();

            // Send via VoiceManager
            VoiceManager.Instance?.SendTextMessage(text);

            // Add local message immediately
            string localName = GetLocalPlayerName();
            AddMessage(localName, text, true);

            // Clear and refocus input
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        private void UpdateSendButtonState()
        {
            if (sendButton != null && inputField != null)
                sendButton.interactable = !string.IsNullOrWhiteSpace(inputField.text);
        }

        private string GetLocalPlayerName()
        {
            var profileService = ServiceLocator.Get<IProfileService>();
            if (profileService?.LocalPlayerStats != null)
                return profileService.LocalPlayerStats.DisplayName;
            return "You";
        }

        #endregion

        #region Receiving Messages

        private void OnMessageReceived(string senderId, string message)
        {
            // Skip own messages (already added locally)
            string localId = ServiceLocator.Get<IProfileService>()?.LocalPlayerStats?.PlayerId;
            if (!string.IsNullOrEmpty(localId) && senderId == localId)
                return;

            string senderName = GetSenderDisplayName(senderId);
            AddMessage(senderName, message, false);
        }

        private string GetSenderDisplayName(string senderId)
        {
            // Check lobby players
            var lobbyService = ServiceLocator.Get<ILobbyService>();
            if (lobbyService?.CurrentLobbyData?.Players != null)
            {
                foreach (var player in lobbyService.CurrentLobbyData.Players)
                {
                    if (player.PlayerId == senderId && !string.IsNullOrEmpty(player.PlayerName))
                        return player.PlayerName;
                }
            }

            // Fallback to truncated ID
            return senderId.Length > 12 ? senderId.Substring(0, 12) + "..." : senderId;
        }

        #endregion

        #region Adding Messages

        /// <summary>
        /// Add a message to the chat.
        /// </summary>
        public void AddMessage(string senderName, string message, bool isLocal)
        {
            if (messageItem == null || messageHolder == null)
                return;

            // Remove oldest if at capacity
            while (messages.Count >= maxMessages && messages.Count > 0)
            {
                var oldest = messages[0];
                messages.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest.gameObject);
            }

            // Create new message
            var newMsg = Instantiate(messageItem, messageHolder);
            newMsg.SetMessage(senderName, message, isLocal);
            messages.Add(newMsg);

            // Scroll to bottom
            ScrollToBottom();

            // Show chat briefly if not manually opened
            if (!isManuallyOpened)
            {
                ShowChat(showScrollbar: false);
                StartAutoHide();
            }
        }

        public void AddSystemMessage(string message)
        {
            AddMessage("System", message, false);
        }

        private void ScrollToBottom()
        {
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        #endregion

        #region Show/Hide Chat

        private void ShowChat(bool showScrollbar)
        {
            // Only show scrollbar when manually opened
            if (scrollbar != null)
                scrollbar.gameObject.SetActive(showScrollbar);

            if (!isChatVisible)
            {
                isChatVisible = true;
                FadeTo(1f, allowInteraction: showScrollbar);
            }
        }

        private void HideChat()
        {
            CancelAutoHide();
            isChatVisible = false;

            if (scrollbar != null)
                scrollbar.gameObject.SetActive(false);

            FadeTo(0f);
        }

        private void HideChatInstant()
        {
            isChatVisible = false;
            isManuallyOpened = false;

            if (chatCanvasGroup != null)
            {
                chatCanvasGroup.alpha = 0f;
                chatCanvasGroup.interactable = false;
                chatCanvasGroup.blocksRaycasts = false;
            }

            if (scrollbar != null)
                scrollbar.gameObject.SetActive(false);
        }

        #endregion

        #region Auto Hide

        private void StartAutoHide()
        {
            CancelAutoHide();
            autoHideCoroutine = StartCoroutine(AutoHideRoutine());
        }

        private void CancelAutoHide()
        {
            if (autoHideCoroutine != null)
            {
                StopCoroutine(autoHideCoroutine);
                autoHideCoroutine = null;
            }
        }

        private IEnumerator AutoHideRoutine()
        {
            yield return new WaitForSeconds(autoHideDelay);

            if (!isManuallyOpened)
                HideChat();
        }

        #endregion

        #region Fade Animation

        private void FadeTo(float targetAlpha, bool allowInteraction = false)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, allowInteraction));
        }

        private IEnumerator FadeRoutine(float targetAlpha, bool allowInteraction)
        {
            if (chatCanvasGroup == null) yield break;

            float startAlpha = chatCanvasGroup.alpha;
            float elapsed = 0f;

            // Only enable interaction if manually opened (allowInteraction = true)
            chatCanvasGroup.interactable = allowInteraction;
            chatCanvasGroup.blocksRaycasts = allowInteraction;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                chatCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                yield return null;
            }

            chatCanvasGroup.alpha = targetAlpha;
        }

        #endregion

        #region Channel Events

        private void OnChannelJoined(string channelId)
        {
            if (inputField != null)
                inputField.interactable = true;

            AddSystemMessage("Connected to chat.");
        }

        private void OnChannelLeft()
        {
            if (inputField != null)
                inputField.interactable = false;

            AddSystemMessage("Disconnected from chat.");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Clear all chat messages.
        /// </summary>
        public void ClearChat()
        {
            foreach (var msg in messages)
            {
                if (msg != null)
                    Destroy(msg.gameObject);
            }
            messages.Clear();
        }

        #endregion
    }
}
