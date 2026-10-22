using System;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Resultado de uma iteração de processamento do trackpad em um frame.
    /// </summary>
    public struct TrackpadOutput
    {
        public float MouseDx;
        public float MouseDy;
        public int WheelDelta;
        public List<InputAction> Actions;
        public bool TriggerHaptic;
        public TrackpadHapticIntensity HapticIntensity;
    }

    /// <summary>
    /// Motor matemático puro para processamento dos trackpads do Steam Deck.
    /// Totalmente desacoplado de hardware físico, chamadas Win32 e UI: opera exclusivamente
    /// sobre coordenadas numéricas, estados lógicos e deltas de tempo (dt).
    /// </summary>
    public sealed class TrackpadProcessor
    {
        private enum DPadQuadrant
        {
            None,
            Up,
            Down,
            Left,
            Right
        }

        private const float DeadzoneThreshold = 28f;
        private const float MinFlickSpeed = 40f;
        private const float TrackballHapticStep = 28f;
        private const float ScrollStep = 650f;

        public bool IsRightPad { get; }

        private bool _wasTouched;
        private bool _isFirstTouch = true;
        private short _lastRawX;
        private short _lastRawY;

        private float _smoothDx;
        private float _smoothDy;

        private float _velocityX;
        private float _velocityY;
        private float _hapticDistanceAccumulator;
        private float _scrollAccumulator;
        private DPadQuadrant _prevQuadrant = DPadQuadrant.None;

        public TrackpadProcessor(bool isRightPad)
        {
            IsRightPad = isRightPad;
        }

        /// <summary>
        /// Processa um frame de entrada do trackpad e retorna as ações de mouse, scroll e botões a serem aplicadas.
        /// </summary>
        public TrackpadOutput Process(
            bool isTouch,
            bool isPress,
            short rawX,
            short rawY,
            TrackpadConfig config,
            float dt)
        {
            var output = new TrackpadOutput
            {
                MouseDx = 0f,
                MouseDy = 0f,
                WheelDelta = 0,
                Actions = new List<InputAction>(),
                TriggerHaptic = false,
                HapticIntensity = config?.HapticIntensity ?? TrackpadHapticIntensity.Off
            };

            if (config == null || config.Mode == TrackpadMode.Disabled)
            {
                _wasTouched = false;
                _isFirstTouch = true;
                _velocityX = 0f;
                _velocityY = 0f;
                return output;
            }

            if (dt <= 0.0001f) dt = 0.008f; // Fallback defensivo para 120Hz

            // 1. Processamento específico do modo funcional
            switch (config.Mode)
            {
                case TrackpadMode.MouseTrackball:
                    ProcessMouseTrackball(isTouch, rawX, rawY, config, dt, ref output);
                    break;

                case TrackpadMode.Scrollwheel:
                    ProcessScrollwheel(isTouch, rawX, rawY, config, ref output);
                    break;

                case TrackpadMode.DPad:
                    ProcessDPad(isTouch, rawX, rawY, config, ref output);
                    break;

                case TrackpadMode.SingleButton:
                    ProcessSingleButton(isTouch, isPress, config, ref output);
                    break;
            }

            // 2. Dupla Ação Universal (Touch e/ou Press)
            // Se PressActions estiver configurado em qualquer modo (ex: clique mecânico no MouseTrackball),
            // dispara ao afundar o botão físico.
            if (isPress && config.PressActions != null && config.PressActions.Count > 0)
            {
                output.Actions.AddRange(config.PressActions);
            }

            // Se TouchActions estiver configurado em modos analógicos (ex: tocar ativa mira/giro), dispara ao encostar.
            if (isTouch && config.Mode != TrackpadMode.SingleButton && config.TouchActions != null && config.TouchActions.Count > 0)
            {
                output.Actions.AddRange(config.TouchActions);
            }

            _wasTouched = isTouch;
            return output;
        }

        private void ProcessMouseTrackball(
            bool isTouch,
            short rawX,
            short rawY,
            TrackpadConfig config,
            float dt,
            ref TrackpadOutput output)
        {
            if (isTouch)
            {
                // Se o usuário tocou novamente, interrompe imediatamente qualquer inércia remanescente
                _velocityX = 0f;
                _velocityY = 0f;

                if (!_wasTouched || _isFirstTouch)
                {
                    _lastRawX = rawX;
                    _lastRawY = rawY;
                    _smoothDx = 0f;
                    _smoothDy = 0f;
                    _isFirstTouch = false;
                    return;
                }

                float rawDeltaX = rawX - _lastRawX;
                float rawDeltaY = rawY - _lastRawY;
                _lastRawX = rawX;
                _lastRawY = rawY;

                float deltaDist = (float)Math.Sqrt(rawDeltaX * rawDeltaX + rawDeltaY * rawDeltaY);

                // 1. Filtro Anti-Jitter com Zona Morta Suave (Soft-knee Deadband):
                // Elimina ruídos elétricos/capacitivos em repouso sem degrau de descontinuidade ("jump/pulando").
                if (deltaDist <= DeadzoneThreshold)
                {
                    _smoothDx *= 0.5f;
                    _smoothDy *= 0.5f;
                    if (Math.Abs(_smoothDx) < 0.05f) _smoothDx = 0f;
                    if (Math.Abs(_smoothDy) < 0.05f) _smoothDy = 0f;
                    return;
                }

                float scale = (deltaDist - DeadzoneThreshold) / deltaDist;
                float cleanDx = rawDeltaX * scale;
                float cleanDy = rawDeltaY * scale;

                // 2. Fator de sensibilidade calibrado para ergonomia natural no Steam Deck
                float sens = Math.Max(1, config.Sensitivity);
                float sensFactor = (sens / 50f) * 0.0055f;

                // 3. Curva de aceleração natural: linear (1.0x) em baixa/média velocidade para precisão cirúrgica;
                // curva suave limitada a no máximo 1.65x durante varreduras rápidas.
                float speed = deltaDist / dt;
                float accel = 1f;
                if (speed > 25000f)
                {
                    float excess = (speed - 25000f) / 100000f;
                    accel = 1f + Math.Min(0.65f, excess * 0.65f);
                }

                float targetDx = cleanDx * sensFactor * accel;
                float targetDy = -cleanDy * sensFactor * accel; // Deck Y positivo é para cima; tela Windows Y positivo é para baixo.

                // 4. Filtro Passa-Baixa Dinâmico (EMA - Exponential Moving Average):
                // Remove trepidações ("tremendo") em micro-ajustes enquanto preserva resposta imediata em flicks rápidos.
                float alpha = Math.Min(0.85f, Math.Max(0.45f, speed / 35000f));
                _smoothDx = (_smoothDx * (1f - alpha)) + (targetDx * alpha);
                _smoothDy = (_smoothDy * (1f - alpha)) + (targetDy * alpha);

                output.MouseDx = _smoothDx;
                output.MouseDy = _smoothDy;

                // Atualiza velocidade instantânea filtrada para a física da trackball
                float instVx = output.MouseDx / dt;
                float instVy = output.MouseDy / dt;
                _velocityX = (_velocityX * 0.35f) + (instVx * 0.65f);
                _velocityY = (_velocityY * 0.35f) + (instVy * 0.65f);

                // Feedback háptico proporcional à distância percorrida
                if (config.HapticIntensity != TrackpadHapticIntensity.Off)
                {
                    _hapticDistanceAccumulator += (float)Math.Sqrt(output.MouseDx * output.MouseDx + output.MouseDy * output.MouseDy);
                    if (_hapticDistanceAccumulator >= TrackballHapticStep)
                    {
                        _hapticDistanceAccumulator -= TrackballHapticStep;
                        output.TriggerHaptic = true;
                    }
                }
            }
            else
            {
                _isFirstTouch = true;
                _smoothDx = 0f;
                _smoothDy = 0f;

                // Usuário soltou o dedo: verifica se havia velocidade de flick
                if (_wasTouched)
                {
                    float liftSpeed = (float)Math.Sqrt(_velocityX * _velocityX + _velocityY * _velocityY);
                    if (liftSpeed < MinFlickSpeed)
                    {
                        _velocityX = 0f;
                        _velocityY = 0f;
                    }
                }

                // Inércia física da trackball desacelerando com atrito natural
                if (_velocityX != 0f || _velocityY != 0f)
                {
                    output.MouseDx = _velocityX * dt;
                    output.MouseDy = _velocityY * dt;

                    float frictionCoeff = 3.0f + (config.Friction / 100f) * 8.0f;
                    float decay = Math.Max(0f, 1f - frictionCoeff * dt);
                    _velocityX *= decay;
                    _velocityY *= decay;

                    if (Math.Abs(_velocityX) < 1.0f && Math.Abs(_velocityY) < 1.0f)
                    {
                        _velocityX = 0f;
                        _velocityY = 0f;
                    }

                    // Ticks hápticos sutis durante o rolamento por inércia
                    if (config.HapticIntensity != TrackpadHapticIntensity.Off)
                    {
                        _hapticDistanceAccumulator += (float)Math.Sqrt(output.MouseDx * output.MouseDx + output.MouseDy * output.MouseDy);
                        if (_hapticDistanceAccumulator >= (TrackballHapticStep * 1.25f))
                        {
                            _hapticDistanceAccumulator -= (TrackballHapticStep * 1.25f);
                            output.TriggerHaptic = true;
                        }
                    }
                }
            }
        }

        private void ProcessScrollwheel(
            bool isTouch,
            short rawX,
            short rawY,
            TrackpadConfig config,
            ref TrackpadOutput output)
        {
            if (isTouch)
            {
                if (!_wasTouched || _isFirstTouch)
                {
                    _lastRawY = rawY;
                    _isFirstTouch = false;
                    _scrollAccumulator = 0f;
                    return;
                }

                float diffY = rawY - _lastRawY;
                _lastRawY = rawY;

                // Filtro de ruído mínimo para scroll
                if (Math.Abs(diffY) > 8f)
                {
                    float sens = Math.Max(1, config.Sensitivity);
                    _scrollAccumulator += diffY * (sens / 50f);

                    if (_scrollAccumulator >= ScrollStep)
                    {
                        int notches = (int)(_scrollAccumulator / ScrollStep);
                        _scrollAccumulator -= notches * ScrollStep;

                        // Rolagem natural invertida padrão: deslizar para cima (diffY > 0) rola para baixo (wheelDelta < 0).
                        output.WheelDelta = (config.InvertScroll ? -1 : 1) * notches * 120;
                        output.TriggerHaptic = config.HapticIntensity != TrackpadHapticIntensity.Off;
                    }
                    else if (_scrollAccumulator <= -ScrollStep)
                    {
                        int notches = (int)(-_scrollAccumulator / ScrollStep);
                        _scrollAccumulator += notches * ScrollStep;

                        output.WheelDelta = (config.InvertScroll ? 1 : -1) * notches * 120;
                        output.TriggerHaptic = config.HapticIntensity != TrackpadHapticIntensity.Off;
                    }
                }
            }
            else
            {
                _isFirstTouch = true;
                _scrollAccumulator = 0f;
            }
        }

        private void ProcessDPad(
            bool isTouch,
            short rawX,
            short rawY,
            TrackpadConfig config,
            ref TrackpadOutput output)
        {
            if (!isTouch)
            {
                _prevQuadrant = DPadQuadrant.None;
                return;
            }

            float normX = rawX / 32768f;
            float normY = rawY / 32768f;
            float radialDist = (float)Math.Sqrt(normX * normX + normY * normY);

            float deadzone = (config.DeadzonePercent > 0 ? config.DeadzonePercent : 15) / 100f;
            if (radialDist < deadzone)
            {
                // Polegar na zona morta central de descanso
                _prevQuadrant = DPadQuadrant.None;
                return;
            }

            double deg = Math.Atan2(normY, normX) * (180.0 / Math.PI);
            DPadQuadrant currentQuadrant;

            if (deg >= -45.0 && deg <= 45.0)
            {
                currentQuadrant = DPadQuadrant.Right;
                if (config.Right != null) output.Actions.AddRange(config.Right);
            }
            else if (deg > 45.0 && deg <= 135.0)
            {
                currentQuadrant = DPadQuadrant.Up;
                if (config.Up != null) output.Actions.AddRange(config.Up);
            }
            else if (deg > 135.0 || deg <= -135.0)
            {
                currentQuadrant = DPadQuadrant.Left;
                if (config.Left != null) output.Actions.AddRange(config.Left);
            }
            else
            {
                currentQuadrant = DPadQuadrant.Down;
                if (config.Down != null) output.Actions.AddRange(config.Down);
            }

            // Pulso háptico na transição de quadrante direcional
            if (currentQuadrant != _prevQuadrant)
            {
                if (config.HapticIntensity != TrackpadHapticIntensity.Off)
                {
                    output.TriggerHaptic = true;
                }
                _prevQuadrant = currentQuadrant;
            }
        }

        private static void ProcessSingleButton(
            bool isTouch,
            bool isPress,
            TrackpadConfig config,
            ref TrackpadOutput output)
        {
            if (isTouch && config.TouchActions != null && config.TouchActions.Count > 0)
            {
                output.Actions.AddRange(config.TouchActions);
            }
            if (isPress && config.PressActions != null && config.PressActions.Count > 0)
            {
                output.Actions.AddRange(config.PressActions);
            }
        }

        /// <summary>
        /// Reseta estados transitórios de tracking, inércia e acumuladores.
        /// </summary>
        public void Reset()
        {
            _wasTouched = false;
            _isFirstTouch = true;
            _lastRawX = 0;
            _lastRawY = 0;
            _smoothDx = 0f;
            _smoothDy = 0f;
            _velocityX = 0f;
            _velocityY = 0f;
            _hapticDistanceAccumulator = 0f;
            _scrollAccumulator = 0f;
            _prevQuadrant = DPadQuadrant.None;
        }
    }
}
