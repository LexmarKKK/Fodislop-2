#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kern.Effects;

/// <summary>Deterministic run of one authored effect: fixed-step simulation, no Unity randomness.</summary>
public sealed class EffectInstance : IDisposable
{
    public const float SimulationStepSeconds = 1f / 60f;
    private const int MaximumParticles = 4096;

    private readonly EffectDefinition _definition;
    private readonly EmitterState[] _emitters;
    private readonly List<EffectParticle> _particles = new();
    private readonly Vector2 _origin;
    private EffectRandom _random;
    private float _elapsedSeconds;
    private float _accumulatorSeconds;
    private bool _completed;
    private bool _disposed;

    public EffectInstance(EffectDefinition definition, Vector2 origin, int seed)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (definition.Emitters == null || definition.Emitters.Length == 0)
        {
            throw new ArgumentException($"Effect '{definition.Id}' has no emitters.", nameof(definition));
        }

        _origin = origin;
        _random = new EffectRandom(seed);
        _emitters = new EmitterState[definition.Emitters.Length];
        for (int index = 0; index < _emitters.Length; index++)
        {
            _emitters[index] = new EmitterState(definition.Emitters[index]);
        }
    }

    public string EffectId => _definition.Id;

    public bool IsComplete => _completed;

    public int ParticleCount => _particles.Count;

    public void Update(float deltaSeconds, IEffectSpriteSink sink)
    {
        if (sink == null)
        {
            throw new ArgumentNullException(nameof(sink));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EffectInstance));
        }

        if (_completed || !(deltaSeconds > 0f) || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
        {
            return;
        }

        _accumulatorSeconds += deltaSeconds;
        while (_accumulatorSeconds >= SimulationStepSeconds)
        {
            _accumulatorSeconds -= SimulationStepSeconds;
            Step(sink);
            if (_completed)
            {
                _accumulatorSeconds = 0f;
                return;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _completed = true;
        _particles.Clear();
    }

    private void Step(IEffectSpriteSink sink)
    {
        _elapsedSeconds += SimulationStepSeconds;
        bool emitting = true;
        if (_definition.Loop)
        {
            if (_elapsedSeconds >= _definition.DurationSeconds)
            {
                _elapsedSeconds -= _definition.DurationSeconds;
                foreach (EmitterState emitter in _emitters)
                {
                    emitter.Reset();
                }
            }
        }
        else if (_elapsedSeconds >= _definition.DurationSeconds)
        {
            emitting = false;
        }

        if (emitting)
        {
            Emit();
        }

        Simulate();
        Draw(sink);
        if (!_definition.Loop && !emitting && _particles.Count == 0)
        {
            _completed = true;
        }
    }

    private void Emit()
    {
        for (int emitterIndex = 0; emitterIndex < _emitters.Length; emitterIndex++)
        {
            EmitterState state = _emitters[emitterIndex];
            EffectEmitterDefinition emitter = state.Definition;
            if (state.BurstRemaining > 0)
            {
                int burst = state.BurstRemaining;
                state.BurstRemaining = 0;
                for (int index = 0; index < burst && _particles.Count < MaximumParticles; index++)
                {
                    SpawnParticle(emitterIndex, emitter);
                }
            }

            state.Accrual += emitter.RatePerSecond * SimulationStepSeconds;
            while (state.Accrual >= 1f && _particles.Count < MaximumParticles)
            {
                state.Accrual -= 1f;
                SpawnParticle(emitterIndex, emitter);
            }
        }
    }

    private void SpawnParticle(int emitterIndex, EffectEmitterDefinition emitter)
    {
        Vector2 offset;
        switch (emitter.Shape)
        {
            case EffectEmitterShape.Circle:
                float angleRadians = _random.NextUnit() * Mathf.PI * 2f;
                offset = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * emitter.ShapeExtents.x;
                break;
            case EffectEmitterShape.Box:
                offset = new Vector2(
                    _random.Range(new Vector2(-emitter.ShapeExtents.x, emitter.ShapeExtents.x)),
                    _random.Range(new Vector2(-emitter.ShapeExtents.y, emitter.ShapeExtents.y)));
                break;
            default:
                offset = Vector2.zero;
                break;
        }

        float direction = _random.Range(emitter.DirectionDegrees) * Mathf.Deg2Rad;
        float speed = _random.Range(emitter.SpeedUnitsPerSecond);
        _particles.Add(new EffectParticle
        {
            Position = _origin + offset,
            Velocity = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * speed,
            AgeSeconds = 0f,
            LifetimeSeconds = Mathf.Max(SimulationStepSeconds, _random.Range(emitter.LifetimeSeconds)),
            StartSizeUnits = _random.Range(emitter.StartSizeRange),
            EndSizeUnits = _random.Range(emitter.EndSizeRange),
            EmitterIndex = emitterIndex,
            FrameOffset = _random.RangeInt(emitter.FrameCount),
        });
    }

    private void Simulate()
    {
        for (int index = _particles.Count - 1; index >= 0; index--)
        {
            EffectParticle particle = _particles[index];
            particle.AgeSeconds += SimulationStepSeconds;
            if (particle.AgeSeconds >= particle.LifetimeSeconds)
            {
                _particles.RemoveAt(index);
                continue;
            }

            EffectEmitterDefinition emitter = _emitters[particle.EmitterIndex].Definition;
            particle.Velocity += new Vector2(0f, emitter.GravityUnitsPerSecondSquared) * SimulationStepSeconds;
            particle.Velocity *= Mathf.Max(0f, 1f - (emitter.DragPerSecond * SimulationStepSeconds));
            particle.Position += particle.Velocity * SimulationStepSeconds;
            _particles[index] = particle;
        }
    }

    private void Draw(IEffectSpriteSink sink)
    {
        foreach (EffectParticle particle in _particles)
        {
            EffectEmitterDefinition emitter = _emitters[particle.EmitterIndex].Definition;
            float progress = Mathf.Clamp01(particle.AgeSeconds / particle.LifetimeSeconds);
            float size = Mathf.Lerp(particle.StartSizeUnits, particle.EndSizeUnits, progress);
            Color color = Color.Lerp(emitter.StartColor, emitter.EndColor, progress);
            int frame = 0;
            if (emitter.FrameCount > 1 && emitter.FramesPerSecond > 0f)
            {
                int advanced = particle.FrameOffset +
                    Mathf.FloorToInt(particle.AgeSeconds * emitter.FramesPerSecond);
                frame = advanced % emitter.FrameCount;
            }

            sink.Draw(new EffectSpriteDraw(
                emitter.SpriteSheet,
                emitter.SpriteSheetColumns,
                emitter.SpriteSheetRows,
                frame,
                particle.Position,
                size,
                color,
                emitter.SortingOrder));
        }
    }

    private sealed class EmitterState
    {
        public readonly EffectEmitterDefinition Definition;
        public float Accrual;
        public int BurstRemaining;

        public EmitterState(EffectEmitterDefinition definition)
        {
            Definition = definition;
            BurstRemaining = definition.BurstCount;
        }

        public void Reset()
        {
            Accrual = 0f;
            BurstRemaining = Definition.BurstCount;
        }
    }
}
