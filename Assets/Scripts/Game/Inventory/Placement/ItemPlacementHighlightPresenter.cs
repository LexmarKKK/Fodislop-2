#nullable enable

using System;
using Kern.Core.Interfaces;
using Kern.Core.Lifecycle;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Kern.Game.Inventory;

/// <summary>
/// Создаёт мировое превью установки предметов и связывает его с моделью
/// инвентаря. Компонента нет в сцене, поэтому живёт через ISceneObjectFactory.
/// </summary>
public sealed class ItemPlacementHighlightPresenter : IInitializable, IDisposable
{
    private readonly ISceneObjectFactory _factory;
    private readonly IInventoryModel _model;
    private readonly ILocalPlayerState _localPlayer;
    private readonly IMapDataProvider _map;
    private ItemPlacementHighlightView? _view;

    [Inject]
    public ItemPlacementHighlightPresenter(
        ISceneObjectFactory factory,
        IInventoryModel model,
        ILocalPlayerState localPlayer,
        IMapDataProvider map)
    {
        _factory = factory;
        _model = model;
        _localPlayer = localPlayer;
        _map = map;
    }

    public void Initialize()
    {
        _view = _factory.Create<ItemPlacementHighlightView>("ItemPlacementHighlight");
        _view.Bind(_model, _localPlayer, _map);
    }

    public void Dispose()
    {
        if (_view != null)
        {
            UnityEngine.Object.Destroy(_view.gameObject);
            _view = null;
        }
    }
}
