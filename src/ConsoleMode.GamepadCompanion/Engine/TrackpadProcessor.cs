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

    /*
     * ========================================================================================================
     * GUIA RÁPIDO DE CALIBRAÇÃO ERGONÔMICA (FINE-TUNING DOS TRACKPADS)
     * ========================================================================================================
     *
     * TABELA DE REFERÊNCIA:
     * +-------------------------------+---------------+----------------------------------------------------------+
     * | PARÂMETRO                     | VALOR PADRÃO  | EFEITO PRÁTICO AO AUMENTAR / DIMINUIR                    |
     * +-------------------------------+---------------+----------------------------------------------------------+
     * | DeadzoneThreshold             | 10f           | [+] Filtra mais tremor, mas exige empurrão inicial maior |
     * |                               |               | [-] Mira começa com menor toque, mas pode tremer parado  |
     * | BaseSensitivityFactor         | 0.0058f       | [+] Cursor viaja mais pixels na tela com mesmo movimento |
     * |                               |               | [-] Cursor fica mais lento e exige mais passadas         |
     * | LowSpeedAlpha                 | 0.40f         | [+] Mais direto/instantâneo em miras lentas              |
     * |                               |               | [-] Mais suave/pesado (estilo estabilizador de caneta)   |
     * | HighSpeedAlphaMax             | 0.85f         | [+] Resposta 1:1 sem atraso em gestos rápidos e flicks   |
     * |                               |               | [-] Sensação de arrasto ou atraso em movimentos rápidos  |
     * | LowSpeedThreshold             | 15000f        | [+] Estende a zona de mira fina para movimentos médios   |
     * |                               |               | [-] Entra na zona rápida mais cedo                       |
     * | AccelStartSpeed               | 25000f        | [+] Precisa de movimento mais brusco para acelerar       |
     * |                               |               | [-] Acelera o cursor com movimentos mais moderados       |
     * | MaxAccelGain                  | 1.65f         | [+] Flicks rápidos jogam o cursor bem mais longe         |
     * |                               |               | [-] Movimento mais linear e previsível em alta velocidade|
     * | ClickPressurePrepThreshold    | 8500          | [+] Amortece a mira só no finalzinho do aperto           |
     * |                               |               | [-] Amortece a mira mais cedo ao encostar com peso       |
     * | ClickStabilizationDamping     | 0.15f         | [+] Permite mais movimento enquanto clica                |
     * |                               |               | [-] Trava o cursor como uma rocha durante o clique       |
     * | MinStrokeDistanceForInertia   | 700f          | [+] Exige varredura longa no pad para a trackball rolar  |
     * |                               |               | [-] Movimentos curtos já armam a inércia                 |
     * | MinFlickSpeed                 | 180f          | [+] Exige soltar o dedo muito rápido para inércia        |
     * |                               |               | [-] Qualquer soltura moderada continua rolando           |
     * | ScrollStep                    | 1600f         | [+] Rolagem mais lenta e dentes de catraca espaçados     |
     * |                               |               | [-] Rolagem mais rápida, passa mais linhas por passada   |
     * | TrackballHapticStep           | 16f           | [+] Menos cliques hápticos por rotação da trackball      |
     * |                               |               | [-] Mais vibrações táteis contínuas na mão               |
     * +-------------------------------+---------------+----------------------------------------------------------+
     *
     * EXEMPLOS DE CENÁRIOS COMUNS:
     * - "Quero que a rolagem fique mais rápida":
     *     Diminua ScrollStep (ex: de 1600f para 1200f).
     * - "Ao clicar, o cursor ainda mexe um pouquinho":
     *     Diminua ClickStabilizationDamping (ex: de 0.15f para 0.08f) ou ClickPressurePrepThreshold (ex: para 7500).
     * - "Quero dar flick de ponta a ponta na tela em um golpe só":
     *     Aumente MaxAccelGain (ex: de 1.65f para 2.0f) ou diminua AccelStartSpeed (ex: para 20000f).
     * - "A inércia ainda está escapando em movimentos pequenos":
     *     Aumente MinStrokeDistanceForInertia (ex: de 700f para 900f).
     * - "Sinto que o cursor demora a começar a mover na mira fina":
     *     Diminua DeadzoneThreshold (ex: de 10f para 8f).
     * ========================================================================================================
     */

    /// <summary>
    /// Central de parâmetros e calibração fina (Fine-Tuning) para a física dos trackpads.
    /// Reúne todas as variáveis globais de ajuste ergonômico no início do arquivo para rápida iteração.
    /// </summary>
    public static class TrackpadTuning
    {
        // ==========================================
        // 1. ZONA MORTA & SENSIBILIDADE BASE
        // ==========================================
        /// <summary>Limiar de zona morta para filtrar micro-ruído capacitivo com o dedo parado.</summary>
        public static float DeadzoneThreshold = 10f;

        /// <summary>Fator de escala de sensibilidade para conversão de deltas do hardware em pixels de tela.</summary>
        public static float BaseSensitivityFactor = 0.0058f;

        // ==========================================
        // 2. FILTRO PASSA-BAIXA DINÂMICO (EMA)
        // ==========================================
        /// <summary>Coeficiente alfa em baixíssima velocidade (mira fina em abas, botões e caixas de texto).</summary>
        public static float LowSpeedAlpha = 0.40f;

        /// <summary>Coeficiente alfa máximo em movimentos rápidos/flicks (alta responsividade sem lag perceptível).</summary>
        public static float HighSpeedAlphaMax = 0.85f;

        /// <summary>Velocidade até a qual o alfa de baixa velocidade é interpolado linearmente.</summary>
        public static float LowSpeedThreshold = 15000f;

        // ==========================================
        // 3. CURVA DE ACELERAÇÃO
        // ==========================================
        /// <summary>Velocidade a partir da qual a aceleração começa a aumentar progressivamente.</summary>
        public static float AccelStartSpeed = 25000f;

        /// <summary>Ganho máximo multiplicador da curva de aceleração.</summary>
        public static float MaxAccelGain = 1.65f;

        // ==========================================
        // 4. ESTABILIZAÇÃO DE CLIQUE (ANTI-SQUASH / MAC-OS & STEAM-OS)
        // ==========================================
        /// <summary>Limiar de pressão analógica preparatória onde o amortecimento de mira começa a atuar.</summary>
        public static short ClickPressurePrepThreshold = 8500;

        /// <summary>Fator de atenuação do movimento durante o clique (0.15f amortiza 85% do tremor da polpa do polegar).</summary>
        public static float ClickStabilizationDamping = 0.15f;

        // ==========================================
        // 5. INÉRCIA DA TRACKBALL (GESTOS DE VARREDURA)
        // ==========================================
        /// <summary>Distância cumulativa mínima de trajeto no pad necessária para armar a inércia.</summary>
        public static float MinStrokeDistanceForInertia = 700f;

        /// <summary>Velocidade mínima de decolagem do dedo (px/s) para a trackball continuar girando.</summary>
        public static float MinFlickSpeed = 180f;

        // ==========================================
        // 6. ROLAGEM & HÁPTICOS
        // ==========================================
        /// <summary>Deslocamento necessário no pad esquerdo para emitir 1 notch de rolagem de roda do mouse.</summary>
        public static float ScrollStep = 1600f;

        /// <summary>Distância percorrida pelo mouse para emitir 1 micro-pulso tátil de rotação da trackball.</summary>
        public static float TrackballHapticStep = 16f;
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

        public bool IsRightPad { get; }

        private bool _wasTouched;
        private bool _isFirstTouch = true;
        private bool _wasEffectivePress;
        private short _lastRawX;
        private short _lastRawY;

        private float _smoothDx;
        private float _smoothDy;

        private float _strokeDistanceAccumulator;
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
        /// Processa um frame de entrada do trackpad (compatibilidade sem sensor de pressão analógica).
        /// </summary>
        public TrackpadOutput Process(
            bool isTouch,
            bool isPress,
            short rawX,
            short rawY,
            TrackpadConfig config,
            float dt)
        {
            return Process(isTouch, isPress, rawX, rawY, 0, config, dt);
        }

        /// <summary>
        /// Processa um frame de entrada do trackpad com coordenadas brutas, pressão analógica e deltas de tempo.
        /// Suporta disparo de clique por limiar de pressão analógica configurável (estilo Force Touch)
        /// com histerese e pulso háptico na borda de subida.
        /// </summary>
        public TrackpadOutput Process(
            bool isTouch,
            bool isPress,
            short rawX,
            short rawY,
            short rawPressure,
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
                _wasEffectivePress = false;
                _velocityX = 0f;
                _velocityY = 0f;
                return output;
            }

            if (dt <= 0.0001f) dt = 0.008f; // Fallback defensivo para 120Hz

            // Avaliação do clique efetivo (Chave mecânica física OU Pressão analógica com histerese)
            int pressureThreshold = config.ClickPressureThreshold;
            bool isEffectivePress = isPress;

            if (isTouch && pressureThreshold > 0)
            {
                if (_wasEffectivePress)
                {
                    // Mantém pressionado até cair 1500 unidades abaixo do limiar (evita trepidação)
                    isEffectivePress = isPress || (rawPressure >= Math.Max(1000, pressureThreshold - 1500));
                }
                else
                {
                    isEffectivePress = isPress || (rawPressure >= pressureThreshold);
                }
            }
            else if (!isTouch)
            {
                isEffectivePress = false;
            }

            // Pulso háptico tátil de clique instantâneo na borda de subida (ao acionar o clique)
            if (isEffectivePress && !_wasEffectivePress)
            {
                if (config.HapticIntensity != TrackpadHapticIntensity.Off)
                {
                    output.TriggerHaptic = true;
                }
            }
            _wasEffectivePress = isEffectivePress;

            // 1. Processamento específico do modo funcional
            switch (config.Mode)
            {
                case TrackpadMode.MouseTrackball:
                    ProcessMouseTrackball(isTouch, isEffectivePress, rawPressure, rawX, rawY, config, dt, ref output);
                    break;

                case TrackpadMode.Scrollwheel:
                    ProcessScrollwheel(isTouch, rawX, rawY, config, ref output);
                    break;

                case TrackpadMode.DPad:
                    ProcessDPad(isTouch, rawX, rawY, config, ref output);
                    break;

                case TrackpadMode.SingleButton:
                    ProcessSingleButton(isTouch, isEffectivePress, config, ref output);
                    break;
            }

            // 2. Dupla Ação Universal (Touch e/ou Press)
            // Se PressActions estiver configurado em qualquer modo (ex: clique no MouseTrackball),
            // dispara quando o clique efetivo estiver ativo.
            if (isEffectivePress && config.PressActions != null && config.PressActions.Count > 0)
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
            bool isEffectivePress,
            short rawPressure,
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
                    _strokeDistanceAccumulator = 0f;
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
                if (deltaDist <= TrackpadTuning.DeadzoneThreshold)
                {
                    _smoothDx *= 0.50f;
                    _smoothDy *= 0.50f;
                    if (Math.Abs(_smoothDx) < 0.05f) _smoothDx = 0f;
                    if (Math.Abs(_smoothDy) < 0.05f) _smoothDy = 0f;
                    return;
                }

                _strokeDistanceAccumulator += deltaDist;

                float scale = (deltaDist - TrackpadTuning.DeadzoneThreshold) / deltaDist;
                float cleanDx = rawDeltaX * scale;
                float cleanDy = rawDeltaY * scale;

                // 2. Estabilização de Mira Anti-Squash (Padrão SteamOS / macOS):
                // Durante a aplicação de força para o clique analógico ou acionamento mecânico,
                // a polpa do polegar deforma contra o sensor capacitivo gerando um salto espúrio de baricentro.
                // Atenuamos severamente o movimento para que o cursor trave com precisão sobre o botão/aba que o jogador quer clicar.
                bool isClickingOrPrepping = isEffectivePress || (rawPressure >= TrackpadTuning.ClickPressurePrepThreshold);
                if (isClickingOrPrepping)
                {
                    cleanDx *= TrackpadTuning.ClickStabilizationDamping;
                    cleanDy *= TrackpadTuning.ClickStabilizationDamping;
                    _strokeDistanceAccumulator = 0f; // Evita armar inércia ao soltar após o clique
                    _velocityX = 0f;
                    _velocityY = 0f;
                }

                // 3. Fator de sensibilidade calibrado para ergonomia natural no Steam Deck
                float sens = Math.Max(1, config.Sensitivity);
                float sensFactor = (sens / 50f) * TrackpadTuning.BaseSensitivityFactor;

                // 4. Curva de aceleração natural: linear (1.0x) em baixa/média velocidade para precisão cirúrgica;
                // curva suave limitada a no máximo MaxAccelGain durante varreduras rápidas.
                float speed = deltaDist / dt;
                float accel = 1f;
                if (speed > TrackpadTuning.AccelStartSpeed)
                {
                    float excess = (speed - TrackpadTuning.AccelStartSpeed) / 100000f;
                    accel = 1f + Math.Min(TrackpadTuning.MaxAccelGain - 1f, excess * (TrackpadTuning.MaxAccelGain - 1f));
                }

                float targetDx = cleanDx * sensFactor * accel;
                float targetDy = -cleanDy * sensFactor * accel; // Deck Y positivo é para cima; tela Windows Y positivo é para baixo.

                // 5. Filtro Passa-Baixa Dinâmico (EMA - Exponential Moving Average):
                // Calibração de baixa velocidade a 0.40 para controle suave e preciso sobre abas e botões pequenos,
                // escalando progressivamente até 0.85 em varreduras e flicks rápidos.
                float alpha;
                if (speed < TrackpadTuning.LowSpeedThreshold)
                {
                    alpha = TrackpadTuning.LowSpeedAlpha + (speed / TrackpadTuning.LowSpeedThreshold) * 0.20f;
                }
                else
                {
                    alpha = Math.Min(TrackpadTuning.HighSpeedAlphaMax, 0.60f + ((speed - TrackpadTuning.LowSpeedThreshold) / 30000f) * 0.25f);
                }

                _smoothDx = (_smoothDx * (1f - alpha)) + (targetDx * alpha);
                _smoothDy = (_smoothDy * (1f - alpha)) + (targetDy * alpha);

                output.MouseDx = _smoothDx;
                output.MouseDy = _smoothDy;

                // Atualiza velocidade instantânea filtrada para a física da trackball (apenas se não estiver clicando)
                if (!isClickingOrPrepping)
                {
                    float instVx = output.MouseDx / dt;
                    float instVy = output.MouseDy / dt;
                    _velocityX = (_velocityX * 0.35f) + (instVx * 0.65f);
                    _velocityY = (_velocityY * 0.35f) + (instVy * 0.65f);
                }
                else
                {
                    _velocityX = 0f;
                    _velocityY = 0f;
                }

                // Feedback háptico proporcional à distância percorrida
                if (config.HapticIntensity != TrackpadHapticIntensity.Off)
                {
                    _hapticDistanceAccumulator += (float)Math.Sqrt(output.MouseDx * output.MouseDx + output.MouseDy * output.MouseDy);
                    if (_hapticDistanceAccumulator >= TrackpadTuning.TrackballHapticStep)
                    {
                        _hapticDistanceAccumulator -= TrackpadTuning.TrackballHapticStep;
                        output.TriggerHaptic = true;
                    }
                }
            }
            else
            {
                _isFirstTouch = true;
                _smoothDx = 0f;
                _smoothDy = 0f;

                // Usuário soltou o dedo: verifica se havia velocidade de flick E trajeto de gesto suficiente
                if (_wasTouched)
                {
                    float liftSpeed = (float)Math.Sqrt(_velocityX * _velocityX + _velocityY * _velocityY);
                    if (liftSpeed < TrackpadTuning.MinFlickSpeed || _strokeDistanceAccumulator < TrackpadTuning.MinStrokeDistanceForInertia)
                    {
                        _velocityX = 0f;
                        _velocityY = 0f;
                    }
                }
                _strokeDistanceAccumulator = 0f;

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
                        if (_hapticDistanceAccumulator >= (TrackpadTuning.TrackballHapticStep * 1.25f))
                        {
                            _hapticDistanceAccumulator -= (TrackpadTuning.TrackballHapticStep * 1.25f);
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

                    if (_scrollAccumulator >= TrackpadTuning.ScrollStep)
                    {
                        int notches = (int)(_scrollAccumulator / TrackpadTuning.ScrollStep);
                        _scrollAccumulator -= notches * TrackpadTuning.ScrollStep;

                        // Rolagem natural invertida padrão: deslizar para cima (diffY > 0) rola para baixo (wheelDelta < 0).
                        output.WheelDelta = (config.InvertScroll ? -1 : 1) * notches * 120;
                        output.TriggerHaptic = config.HapticIntensity != TrackpadHapticIntensity.Off;
                    }
                    else if (_scrollAccumulator <= -TrackpadTuning.ScrollStep)
                    {
                        int notches = (int)(-_scrollAccumulator / TrackpadTuning.ScrollStep);
                        _scrollAccumulator += notches * TrackpadTuning.ScrollStep;

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
            _wasEffectivePress = false;
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
