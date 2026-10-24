#nullable enable

using System;
using MinesServer.Networking.Client.Packets.Actions;
using MinesServer.Networking.Client.Packets.GUI;
using UnityEngine.UIElements;

namespace Kern.UI.HUD.Player.View;

public sealed class PlayerHUDPopups
{
    private readonly Action<SuicidePacket> _sendSuicide;
    private readonly Action<OpenHelpClickPacket> _sendHelpRequest;
    private readonly Action<OpenBuildingsClickPacket> _sendBuildingsRequest;
    private VisualElement? _respawnPopup;

    public PlayerHUDPopups(
        Action<SuicidePacket> sendSuicide,
        Action<OpenHelpClickPacket> sendHelpRequest,
        Action<OpenBuildingsClickPacket> sendBuildingsRequest)
    {
        _sendSuicide = sendSuicide;
        _sendHelpRequest = sendHelpRequest;
        _sendBuildingsRequest = sendBuildingsRequest;
    }

    public void Initialize(VisualElement root)
    {
        _respawnPopup = root.Q<VisualElement>("RespawnPopup") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnPopup is missing from PlayerHUD.uxml.");
        Button respawnConfirm = root.Q<Button>("RespawnConfirmButton") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnConfirmButton is missing from PlayerHUD.uxml.");
        respawnConfirm.clicked += () =>
        {
            _sendSuicide(new SuicidePacket());
            _respawnPopup.style.display = DisplayStyle.None;
        };
        Button respawnCancel = root.Q<Button>("RespawnCancelButton") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnCancelButton is missing from PlayerHUD.uxml.");
        respawnCancel.clicked += () => _respawnPopup.style.display = DisplayStyle.None;
        Button respawnButton = root.Q<Button>("RespawnButton") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnButton is missing from PlayerHUD.uxml.");
        respawnButton.clicked += () => _respawnPopup.style.display = DisplayStyle.Flex;

        // «Мои здания» и FAQ — серверные страницы: кнопки шлют OpenBuildingsClickPacket (26)
        // и OpenHelpClickPacket (18); сервер отвечает окном (Session.BuildingsHandler →
        // Player.MyBuildingsWindow, Session.HelpHandler → Settings.FAQWindow) через
        // ServerWindowPresenter. Локальные попапы из UXML удалены.
        Button buildingsButton = root.Q<Button>("BuildingsButton") ??
            throw new InvalidOperationException("[PlayerHUD] BuildingsButton is missing from PlayerHUD.uxml.");
        buildingsButton.clicked += () => _sendBuildingsRequest(new OpenBuildingsClickPacket());

        Button faqButton = root.Q<Button>("FaqButton") ??
            throw new InvalidOperationException("[PlayerHUD] FaqButton is missing from PlayerHUD.uxml.");
        faqButton.clicked += () => _sendHelpRequest(new OpenHelpClickPacket());
    }
}
