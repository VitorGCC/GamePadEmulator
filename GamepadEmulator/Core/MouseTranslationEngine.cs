using System;
using System.Numerics;

namespace GamepadEmulator
{
    public class MouseTranslationEngine
    {
        // Configurações
        public float Sensitivity { get; set; } = 15.0f;
        public float YAxisRatio { get; set; } = 1.0f;     // Se precisar de mais sensi vertical
        public float PowerCurve { get; set; } = 0.65f;    // 1.0 é linear. Menor = mais aceleração na arrancada
        public int AntiDeadzone { get; set; } = 6500;     // ~20% (Valor base para CoD)
        
        // Steady Aim (Filtros Temporais)
        public float SmoothingFactor { get; set; } = 0.5f; // Lerp de suavização básica
        
        // Estado Interno
        private Vector2 _smoothedDelta = Vector2.Zero;
        private Vector2 _overflowBuffer = Vector2.Zero;
        
        // Constantes
        private const float MAX_AXIS_VALUE = 32767.0f;
        private const float MIN_AXIS_VALUE = -32768.0f;

        public (short x, short y) Translate(int deltaX, int deltaY)
        {
            // 1. Inverter Eixo Y para mapeamento do controle (mouse para cima = Y negativo, analógico para cima = Y positivo)
            Vector2 rawInput = new Vector2(deltaX, -deltaY);

            // 2. Filtro Temporal (Steady Aim / EMA Smoothing)
            _smoothedDelta = Vector2.Lerp(_smoothedDelta, rawInput, SmoothingFactor);

            // Cortar ruído extremo (anti-jitter) se o buffer estiver zerado
            if (_smoothedDelta.LengthSquared() < 0.01f && _overflowBuffer.LengthSquared() < 0.01f)
            {
                _smoothedDelta = Vector2.Zero;
                _overflowBuffer = Vector2.Zero;
                return (0, 0);
            }

            // 3. Sensibilidade Base
            float scaledX = _smoothedDelta.X * Sensitivity;
            float scaledY = _smoothedDelta.Y * Sensitivity * YAxisRatio;

            // 4. Integração do Buffer de Flicks (Turn Speed Bleed-off)
            scaledX += _overflowBuffer.X;
            scaledY += _overflowBuffer.Y;
            _overflowBuffer = Vector2.Zero;

            Vector2 processedVector = new Vector2(scaledX, scaledY);
            float length = processedVector.Length();

            if (length < 0.01f)
                return (0, 0);

            Vector2 direction = processedVector / length; // Normalize

            // 5. Curva Balística (Ballistic Curve)
            // Aplica a potência ao comprimento para alterar a resposta.
            // length é calibrado empiricamente (dividir por um valor base para a curva não estourar)
            float baseNormalization = 50.0f; 
            float normalizedLength = length / baseNormalization;
            float curvedLength = (float)Math.Pow(normalizedLength, PowerCurve) * baseNormalization;

            // 6. Anti-Deadzone Radial
            // Empurra o início do movimento para fora da Deadzone do jogo.
            float finalLength = curvedLength > 0.05f ? AntiDeadzone + (curvedLength * 50.0f) : 0; // Multiplicador de escala

            Vector2 finalVector = direction * finalLength;

            // 7. Gerenciamento do Limite da Engine do Jogo (Turn Speed Cap)
            short outX, outY;
            
            if (finalVector.Length() > MAX_AXIS_VALUE)
            {
                // Flick rápido superou o limite do analógico
                // Retornar força máxima
                Vector2 maxedOut = direction * MAX_AXIS_VALUE;
                outX = (short)Math.Clamp(maxedOut.X, MIN_AXIS_VALUE, MAX_AXIS_VALUE);
                outY = (short)Math.Clamp(maxedOut.Y, MIN_AXIS_VALUE, MAX_AXIS_VALUE);

                // Sangria Suave (Bleed-off): guarda o excesso real não utilizado
                float sentRatio = MAX_AXIS_VALUE / finalVector.Length();
                // O overflow guardado deve ser na mesma proporção do scaledVector original, não do output pós-curva
                _overflowBuffer = processedVector * (1.0f - sentRatio);
            }
            else
            {
                outX = (short)Math.Clamp(finalVector.X, MIN_AXIS_VALUE, MAX_AXIS_VALUE);
                outY = (short)Math.Clamp(finalVector.Y, MIN_AXIS_VALUE, MAX_AXIS_VALUE);
            }

            return (outX, outY);
        }

        public void Reset()
        {
            _smoothedDelta = Vector2.Zero;
            _overflowBuffer = Vector2.Zero;
        }
    }
}
