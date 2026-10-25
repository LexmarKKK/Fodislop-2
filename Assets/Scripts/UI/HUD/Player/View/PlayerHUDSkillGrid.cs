#nullable enable

using System.Collections.Generic;
using MinesServer.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI.HUD.Player.View;

// Сетка умений PlayerHUD: иконка умения, под ней полоса опыта с процентами
// (SkillProgressBar), над иконкой - "up" при готовности апгрейда
// (xp >= 100%), прыгающее вверх-вниз для привлечения внимания. Место под
// "up" резервируется отступом рамки, сама надпись растянута ровно по ширине
// иконки, поэтому всегда отцентрирована по умению.
public sealed class PlayerHUDSkillGrid
{
    private const int SKILL_GRID_COLS = 4;
    private const string ReadyText = "up";
    private const long BounceIntervalMs = 40L;

    private readonly Dictionary<SkillType, (Label arrow, SkillProgressBar bar)> _skillIcons = new();
    private readonly Dictionary<SkillType, IVisualElementScheduledItem> _bounceSchedules = new();

    private VisualElement? _skillContainer;
    private VisualElement? _currentSkillRow;
    private int _skillCountInRow;

    public void Initialize(VisualElement skillContainer)
    {
        _skillContainer = skillContainer;
        _currentSkillRow = null;
        _skillCountInRow = 0;
        _skillIcons.Clear();
    }

    public void UpdateSkillProgress(SkillType skill, long current, long max)
    {
        if (!_skillIcons.TryGetValue(skill, out var icon))
        {
            icon = CreateSkillIcon(skill);
        }

        // Сырой процент БЕЗ Clamp01: перелив сверх 100% (current > max)
        // доезжает до полосы - заливка краснеет, проценты показывают
        // сверхнорму ("120%"). Длину заливки клампит сам SkillProgressBar.
        float progress = max > 0 ? current / (float)max : 0f;
        bool ready = progress >= 1f;

        // Полоса плавно догоняет целевое заполнение - заливка движется,
        // пока умение прокачивается.
        icon.bar.SetTarget(progress);

        icon.arrow.text = ready ? ReadyText : string.Empty;
        if (ready)
        {
            StartBounce(skill, icon.arrow);
        }
        else
        {
            StopBounce(skill, icon.arrow);
        }
    }

    public void ClearSchedules()
    {
        foreach (var schedule in _bounceSchedules.Values)
        {
            schedule.Pause();
        }

        _bounceSchedules.Clear();

        foreach (var (_, bar) in _skillIcons.Values)
        {
            bar.PauseAnimation();
        }
    }

    // "up" прыгает вверх-вниз (0..-4px), как в старом клиенте.
    private void StartBounce(SkillType skill, Label arrow)
    {
        if (_bounceSchedules.ContainsKey(skill))
        {
            return;
        }

        float elapsed = 0f;
        IVisualElementScheduledItem schedule = arrow.schedule.Execute(() =>
        {
            elapsed += BounceIntervalMs / 1000f;
            float y = -Mathf.PingPong(elapsed * 10f, 4f);
            arrow.style.translate = new Translate(0, y);
        }).Every(BounceIntervalMs);
        _bounceSchedules[skill] = schedule;
    }

    private void StopBounce(SkillType skill, Label arrow)
    {
        if (_bounceSchedules.TryGetValue(skill, out var existing))
        {
            existing.Pause();
            _bounceSchedules.Remove(skill);
        }

        arrow.style.translate = new Translate(0, 0);
    }

    private void EnsureSkillRow()
    {
        if (_currentSkillRow != null && _skillCountInRow < SKILL_GRID_COLS)
        {
            return;
        }

        _currentSkillRow = new VisualElement();
        _currentSkillRow.AddToClassList("hud-skill-row");
        _skillContainer?.Add(_currentSkillRow);
        _skillCountInRow = 0;
    }

    private (Label arrow, SkillProgressBar bar) CreateSkillIcon(SkillType skill)
    {
        EnsureSkillRow();

        // Ячейка - колонка без подложки: рамка с иконкой, под ней полоса
        // опыта. Верхний отступ рамки резервирует место под "up"; надпись
        // привязана к рамке и растянута по ширине иконки, поэтому
        // центрирована ровно по умению.
        var cell = new VisualElement();
        cell.AddToClassList("hud-skill-icon");

        // Рамка вокруг иконки - точка привязки "up".
        var iconFrame = new VisualElement();
        iconFrame.AddToClassList("hud-skill-icon-frame");

        var iconImage = new Image();
        iconImage.AddToClassList("hud-skill-icon-image");

        Texture2D? tex = Resources.Load<Texture2D>($"Skills/{skill}");
        if (tex != null)
        {
            RuntimeTextureFactory.ApplySampling(
                tex,
                FilterMode.Point,
                TextureWrapMode.Clamp);
            iconImage.image = tex;
        }

        iconFrame.Add(iconImage);

        // "up" при готовности апгрейда: абсолютный слой НАД иконкой, растянутый
        // ровно на ширину рамки (left/right: 0), поэтому middle-center центрирует
        // текст точно по умению - без зависимости от stretch и измерения шрифта.
        // Прыжок вверх-вниз уводит надпись в зарезервированную зону над иконкой.
        var arrow = new Label(ReadyText);
        arrow.AddToClassList("hud-skill-arrow");
        iconFrame.Add(arrow);

        cell.Add(iconFrame);

        // Полоса опыта с процентами - ПОД иконкой, центрируется ячейкой.
        var bar = new SkillProgressBar();
        cell.Add(bar);

        _currentSkillRow?.Add(cell);
        _skillCountInRow++;

        var result = (arrow, bar);
        _skillIcons[skill] = result;
        return result;
    }
}
