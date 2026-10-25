#nullable enable

using System;
using System.Collections.Generic;
using Kern.Core;
using Kern.Core.Interfaces;
using Kern.Core.Localization;
using Kern.Game.Managers;
using Kern.Networking;
using MinesServer.Networking.Client.Packets.Chat;
using MinesServer.Networking.Server.Packets.Chat;
using MinesServer.Networking.Server.Packets.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

namespace Kern.UI
{
    public class GlobalChatUI : MonoBehaviour, ILocalizableUI
    {
        [Inject] private UIDocument _doc = null!;
        [Inject] private INetworkService _networkService = null!;
        [Inject] private IInputBlocker _inputBlocker = null!;
        [Inject] private UIInputManager _uiInput = null!;
        [Inject] private ILocalizationService _loc = null!;
        [Inject] private IAsyncOperationSupervisor _operations = null!;

        private ChatEventGateway _chatEvents = null!;
        private ChatViewElements? _view;
        private ChatColorController? _colorController;
        private ChatBlinkController? _blink;
        private bool _isOpen;
        private bool _initialized;
        private readonly GlobalChatMessagePresenter _messages = new();
        private readonly ChatMuteTracker _muteTracker = new();
        private bool _lastMutedState;
        private bool _hasCachedMuteState;
        // Активный чат: тег маршрутизации (SendChatMessagePacket/история) и
        // серверное имя для заголовка. Дефолт — глобальный канал (FED).
        private string _activeTag = ProjectRuntimeContracts.Chat.GlobalChannelTag;
        private string? _activeChatName;
        // Список чатов: сервер объявляет каналы на входе (pSenders.SendChatList)
        // и присылает сводный список по кнопке «Чаты». Если список не пришёл ни
        // разу — доверяем контракту (FED). Попап перерисовывается только по
        // явному запросу (_chatListRequested), входное объявление его не открывает.
        private readonly HashSet<string> _knownChatTags = new(StringComparer.OrdinalIgnoreCase);
        private bool _chatListRequested;

        protected void Start()
        {
            // Школа (одна дорога): зарегистрированные вьюхи инжектятся при
            // сборке scope (фаза Awake), панель UIDocument создаётся в OnEnable —
            // к Start и зависимости, и панель гарантированы. Один вызов, без
            // ретраев из Update. Серверный конфиг приходит по сети — событие
            // OnInitialized ниже.
            TryInitialize();
        }

        [Inject]
        private void Construct(ChatEventGateway chatEvents)
        {
            _chatEvents = chatEvents;
            _chatEvents.MessageReceived += AddMessage;
            _chatEvents.HistoryReceived += AddHistory;
            _chatEvents.MuteReceived += ApplyMute;
            _chatEvents.ChatListReceived += ApplyChatList;
            if (_chatEvents.LastChatList is ChatListPacket lastChatList)
            {
                ApplyChatList(lastChatList);
            }
        }

        private void TryInitialize()
        {
            if (_initialized)
            {
                return;
            }

            // [Inject]-метод гарантирует зависимости и панель UIDocument к
            // моменту вызова; null здесь — дефект проводки, а не гонка.
            // Молчаливый пропуск оставил бы чат вечно нерабочим без ошибки.
            if (_doc == null || _doc.rootVisualElement == null || _networkService == null ||
                _inputBlocker == null || _operations == null)
            {
                throw new InvalidOperationException(
                    "[GlobalChatUI] Required injection missing: " +
                    $"{(_doc == null ? "UIDocument" : _networkService == null ? "INetworkService" : _inputBlocker == null ? "IInputBlocker" : "UIDocument root")}. " +
                    "GlobalChatUI must be registered in the Game scope before Start.");
            }

            _initialized = true;
            ILocalizationService loc = _loc!;

            // Реестр применяет текст сразу и на каждой смене языка — подписка
            // вручную не нужна и запрещена линтером.
            loc.RegisterLocalizable(this);
            CreateUI();
            _blink = new ChatBlinkController(_operations, destroyCancellationToken, () => _view?.Blinker);
            if (_view?.Panel != null)
            {
                _view.Panel.style.display = DisplayStyle.None;
            }

            ApplyChatConfig();

            if (Application.isPlaying)
            {
                try
                {
                    _networkService.Send(new QueryChatHistoryPacket(_activeTag, 0));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GlobalChatUI] Не удалось запросить историю чата: {ex}");
                }
            }
        }

        public void ApplyLocalizedText()
        {
            UILocalizer.AssertLocalizationServiceAvailable(_loc, nameof(GlobalChatUI));
            if (_view?.Tree == null || _loc == null)
            {
                return;
            }

            UILocalizer.Apply(_view.Tree, _loc);
            UILocalizer.AssertLocalized(_view.Tree, _loc);
            UpdateChannelHeader();
        }

        protected void OnDestroy()
        {
            if (_uiInput != null)
            {
                _uiInput.IsChatFocused = false;
            }

            if (_loc != null)
            {
                _loc.UnregisterLocalizable(this);
            }

            if (_chatEvents != null)
            {
                _chatEvents.MessageReceived -= AddMessage;
                _chatEvents.HistoryReceived -= AddHistory;
                _chatEvents.MuteReceived -= ApplyMute;
                _chatEvents.ChatListReceived -= ApplyChatList;
            }

            _blink?.Dispose();
            _blink = null;
            _view?.Tree.RemoveFromHierarchy();
            _messages.Detach();
            _view = null;
            _colorController = null;
            _hasCachedMuteState = false;
        }

        private void ApplyChatConfig()
        {
            if (_view?.InputField != null)
            {
                _view.InputField.maxLength = ProjectRuntimeContracts.Chat.MaximumGlobalChatLength;
            }
        }

        protected void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (Keyboard.current == null)
            {
                return;
            }

            RefreshMuteState();

            bool inputBlocked = _inputBlocker != null && _inputBlocker.IsInputBlocked;

            // T не открывает этот канал: он принадлежит локальному чату
            // (FloatingChatManager → LocalChatInput). Глобальное окно открывает
            // TAB - кнопки чата в HUD больше нет.
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                // При заблокированном вводе (поверх открыты окна/инвентарь) чат
                // не открываем, но закрыть уже открытый TAB-ом можно всегда.
                if (!_isOpen || !inputBlocked)
                {
                    Toggle();
                }

                return;
            }

            if (!_isOpen)
            {
                return;
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                // Input blocking includes chat focus, but Enter must remain
                // available to the chat that currently owns keyboard focus.
                if (!inputBlocked || _uiInput.IsChatFocused)
                {
                    OnSendClicked();
                }

                return;
            }

            // Escape этот чат не закрывает: TAB — единственная клавиша
            // открытия/закрытия. Пока поле в фокусе, ESC ничего не делает:
            // меню паузы не открывается, потому что ввод занят чатом
            // (IsChatFocused проверяется в PauseMenu.Update).
        }

        private void CreateUI()
        {
            var uiUxml = Resources.Load<VisualTreeAsset>(
                ProjectRuntimeContracts.ResourcePaths.GlobalChatUxml);
            if (uiUxml == null)
            {
                return;
            }

            VisualElement tree = uiUxml.CloneTree();
            UILocalizer.Apply(tree, _loc);
            _view = new ChatViewElements(tree, _loc);
            _messages.Attach(_view.ScrollView);

            if (_doc != null && _view.Panel != null)
            {
                _doc.rootVisualElement.Add(tree);
            }

            if (_view.InputField != null)
            {
                _view.InputField.selectAllOnFocus = false;
                _view.InputField.selectAllOnMouseUp = false;
                _view.InputField.RegisterCallback<FocusEvent>(_ =>
                {
                    _blink?.StartBlink();
                    _uiInput.IsChatFocused = true;
                });
                _view.InputField.RegisterCallback<BlurEvent>(_ =>
                {
                    _blink?.StopBlink();
                    _uiInput.IsChatFocused = false;
                });
                _view.InputField.RegisterValueChangedCallback(_ => _blink?.NotifyInput());
            }

            _view.BindActions(
                OnSendClicked,
                OpenChatList,
                CloseChatListPopup);

            _colorController = new ChatColorController(_networkService, _view.ColorButton, _view.ColorGrid);
            _messages.Attach(
                _view.ScrollView,
                OnMessageClicked);
            UpdateChannelHeader();
        }

        private void OnSendClicked()
        {
            if (_view?.InputField == null || _muteTracker.IsMuted)
            {
                return;
            }

            if (!CanSendToActiveChannel)
            {
                return;
            }

            string text = _view.InputField.value.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int chatMaxLen = ProjectRuntimeContracts.Chat.MaximumGlobalChatLength;
            if (text.Length > chatMaxLen)
            {
                text = text.Substring(0, chatMaxLen);
            }

            try
            {
                _networkService.Send(new SendChatMessagePacket(
                    _activeTag,
                    text));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GlobalChatUI] Не удалось отправить сообщение в чат: {ex}");
            }

            _view.InputField.value = string.Empty;
            _view.InputField.Focus();
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public void Show()
        {
            _isOpen = true;
            if (_view?.Panel != null)
            {
                _view.Panel.style.display = DisplayStyle.Flex;
            }

            _view?.InputField?.Focus();
        }

        public void Hide()
        {
            _isOpen = false;
            if (_view?.Panel != null)
            {
                _view.Panel.style.display = DisplayStyle.None;
            }

            if (_view?.InputField != null)
            {
                _view.InputField.value = string.Empty;
                _view.InputField.Blur();
            }
        }

        public void AddMessage(ChatMessagePacket msg)
        {
            if (_view?.ScrollView == null)
            {
                return;
            }

            AppendMessage(ChatMessageFormatter.FormatGlobal(msg, DateTime.Now), msg.PlayerId);
        }

        // Живые сообщения приходят только участникам текущего чата (роутинг по
        // CurrentChat на сервере) — тег учитывать не требуется. История (E5-C)
        // отдаётся по тегу: чужая лента отбрасывается.

        private void AppendMessage(string formattedMessage, int playerId = 0)
        {
            _messages.Add(formattedMessage, playerId);
        }

        private void AddHistory(ChatMessageListPacket packet)
        {
            if (!string.Equals(packet.Tag, _activeTag, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[GlobalChatUI] Ignoring history for inactive chat '{packet.Tag}'.");
                return;
            }

            foreach (ChatMessagePacket message in packet.Messages)
            {
                AppendMessage(ChatMessageFormatter.FormatGlobal(message, DateTime.Now), message.PlayerId);
            }
        }

        private void UpdateChannelHeader()
        {
            ChatChannelPresenter.UpdatePresentation(
                _view?.ChatHeader,
                _view?.GlobalChannelButton,
                _loc,
                _activeChatName);
        }

        // Кнопка «Чаты»: запрос сводного списка (легаси Cmen → ChatListPacket).
        // Отказ сети не роняет UI — если список уже приходил, открываем его.

        private void OpenChatList()
        {
            _chatListRequested = true;
            ChatListPacket? cached = _chatEvents.LastChatList;
            try
            {
                _networkService.Send(new OpenChatListPacket());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GlobalChatUI] Не удалось запросить список чатов: {ex}");
                if (cached is ChatListPacket snapshot)
                {
                    RenderChatList(snapshot);
                }
            }
        }

        private void CloseChatListPopup()
        {
            _chatListRequested = false;
            if (_view?.ChatListPopup != null)
            {
                _view.ChatListPopup.style.display = DisplayStyle.None;
            }
        }

        // Отправка разрешена в объявленный сервером чат; если список не приходил
        // ни разу — доверяем дефолтному тегу контракта (FED).

        private bool CanSendToActiveChannel => _knownChatTags.Count == 0 ||
            _knownChatTags.Contains(_activeTag);

        // Сервер штатно объявляет каналы на входе (48 с описаниями) и пересылает
        // сводный список по запросу «Чаты» (48 с последними сообщениями). Оба
        // пути обновляют реестр тегов; попап перерисовывается только по запросу.

        private void ApplyChatList(ChatListPacket packet)
        {
            _knownChatTags.Clear();
            foreach (var (tag, _, _) in packet.Chats)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    _knownChatTags.Add(tag);
                }
            }

            RefreshChatControls(_muteTracker.IsMuted);
            if (_chatListRequested)
            {
                RenderChatList(packet);
            }
        }

        private void RenderChatList(ChatListPacket packet)
        {
            if (_view?.ChatListScroll == null || _view.ChatListPopup == null)
            {
                return;
            }

            ScrollView scroll = _view.ChatListScroll;
            scroll.Clear();
            foreach (var (tag, name, lastMessage) in packet.Chats)
            {
                string chatName = string.IsNullOrEmpty(name) ? tag : name;
                string preview = string.IsNullOrEmpty(lastMessage.Message)
                    ? chatName
                    : lastMessage.Message;
                var row = new Button { text = string.Empty };
                row.AddToClassList("gchat-chat-row");
                var nameLabel = new Label(ChatMessageFormatter.PlainText(chatName));
                nameLabel.AddToClassList("gchat-chat-row-name");
                var previewLabel = new Label(ChatMessageFormatter.PlainText(preview));
                previewLabel.AddToClassList("gchat-chat-row-preview");
                row.Add(nameLabel);
                row.Add(previewLabel);
                string chosenTag = tag;
                string chosenName = chatName;
                row.clicked += () => ChooseChat(chosenTag, chosenName);
                scroll.Add(row);
            }

            _view.ChatListPopup.style.display = DisplayStyle.Flex;
        }

        // Выбор из списка: тег становится активным, лента очищается, сервер
        // переключает CurrentChat и присылает последнюю страницу (ProvideChatHistory).
        // Сервер шлёт тегом выбранного чата — история чужого тега игнорируется.

        private void ChooseChat(string tag, string chatName)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            _activeTag = tag;
            _activeChatName = chatName;
            _chatListRequested = false;
            if (_view?.ChatListPopup != null)
            {
                _view.ChatListPopup.style.display = DisplayStyle.None;
            }
            _messages.Clear();
            UpdateChannelHeader();
            RefreshChatControls(_muteTracker.IsMuted);
            try
            {
                _networkService.Send(new ChooseChatPacket(tag));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GlobalChatUI] Не удалось переключить чат: {ex}");
            }
        }

        // Клик по строке сообщения (легаси Cpri): окно отправителя открывает
        // сервер. Системные строки (playerId == 0) не кликабельны.

        private void OnMessageClicked(int playerId)
        {
            if (playerId <= 0)
            {
                return;
            }

            try
            {
                _networkService.Send(new ChatMessageClickPacket(_activeTag, playerId));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GlobalChatUI] Не удалось открыть окно отправителя: {ex}");
            }
        }

        public void ApplyMute(ChatMutePacket packet)
        {
            _muteTracker.ApplyMute(packet, _loc, out string statusMessage, out string notificationMessage);
            _view?.SetMuteStatus(statusMessage);
            RefreshMuteState();
            AddSystemMessage(notificationMessage);
        }

        private void RefreshMuteState()
        {
            if (_muteTracker.CheckExpiration())
            {
                _view?.SetMuteStatus(string.Empty);
            }

            bool muted = _muteTracker.IsMuted;

            // Кэш переживал вид, который он и защищал.
            //
            // RefreshMuteState вызывается из Update, то есть и тогда, когда чат
            // закрыт и _view равен null: первый же такой вызов запоминал
            // «немой = нет» и глушил все последующие. Дальше чат открывался,
            // элементы создавались заново — и SetEnabled к ним не применялся уже
            // никогда, потому что состояние немоты с тех пор не менялось. Поле
            // ввода оставалось в том виде, в каком его собрал Uxml.
            //
            // Сбрасывается вместе с видом: сравнивать состояние можно только с
            // тем, кому его в самом деле выставили.
            if (_view == null)
            {
                _hasCachedMuteState = false;
                return;
            }

            bool muteChanged = !_hasCachedMuteState || _lastMutedState != muted;
            _lastMutedState = muted;
            _hasCachedMuteState = true;
            if (muteChanged)
            {
                RefreshChatControls(muted);
            }
        }

        private void RefreshChatControls(bool muted)
        {
            if (_view == null)
            {
                return;
            }

            bool activeAvailable = CanSendToActiveChannel;
            _view.InputField?.SetEnabled(!muted);
            _view.SendButton?.SetEnabled(!muted && activeAvailable);
            _view.ColorButton?.SetEnabled(!muted && activeAvailable);
            _view.GlobalChannelButton?.SetEnabled(true);
        }

        private void AddSystemMessage(string message) => AppendMessage(message);
    }
}
