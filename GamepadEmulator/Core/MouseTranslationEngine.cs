using System;
using System.Numerics;

namespace GamepadEmulator
{
    public class MouseTranslationEngine
    {
        // Sensibilidade independente por eixo
        public float SensitivityX { get; set; } = 15.0f;
        public float SensitivityY { get; set; } = 15.0f;

        // Setter de conveniência — define X e Y ao mesmo tempo
        public float Sensitivity { set { SensitivityX = value; SensitivityY = value; } }

        public float YAxisRatio { get; set; } = 1.0f;
        public float PowerCurve { get; set; } = 0.65f;
        public int AntiDeadzone { get; set; } = 6500;
        public float SmoothingFactor { get; set; } = 0.5f;

        private Vector2 _smoothedDelta = Vector2.Zero;
        private Vector2 _overflowBuffer = Vector2.Zero;

        private const float MAX_AXIS_VALUE = 32767.0f;
        private const float MIN_AXIS_VALUE = -32768.0f;

        public (short x, short y) Translate(int deltaX, int deltaY)
        {
            // 1. Inverter eixo Y (mouse cima = Y negativo → analógico cima = Y positivo)
            Vector2 rawInput = new Vector2(deltaX, -deltaY);

            // 2. Filtro temporal EMA (Steady Aim)
            _smoothedDelta = Vector2.Lerp(_smoothedDelta, rawInput, SmoothingFactor);

            if (_smoothedDelta.LengthSquared() < 0.01f && _overflowBuffer.LengthSquared() < 0.01f)
            {
                _smoothedDelta = Vector2.Zero;
                _overflowBuffer = Vector2.Zero;
                return (0, 0);
            }

            // 3. Sensibilidade por eixo
            float scaledX = _smoothedDelta.X * SensitivityX;
            float scaledY = _smoothedDelta.Y * SensitivityY * YAxisRatio;

            // 4. Integração do buffer de overflow (Turn Speed Bleed-off)
            scaledX += _overflowBuffer.X;
            scaledY += _overflowBuffer.Y;
            _overflowBuffer = Vector2.Zero;

            Vector2 processedVector = new Vector2(scaledX, scaledY);
            float length = processedVector.Length();

            if (length < 0.01f)
                return (0, 0);

            Vector2 direction = processedVector / length;

            // 5. Curva balística (Power Curve)
            float baseNormalization = 20.0f;
            float normalizedLength = length / baseNormalization;
            float curvedLength = (float)Math.Pow(normalizedLength, PowerCurve) * baseNormalization;

            // 6. Anti-Deadzone radial — empurra o movimento para fora da zona morta do jogo
            float finalLength = curvedLength > 0.05f ? AntiDeadzone + (curvedLength * 200.0f) : 0;

            Vector2 finalVector = direction * finalLength;

            // 7. Limite do analógico com bleed-off
            short outX, outY;

            if (finalVector.Length() > MAX_AXIS_VALUE)
            {
                Vector2 maxedOut = direction * MAX_AXIS_VALUE;
                outX = (short)Math.Clamp(maxedOut.X, MIN_AXIS_VALUE, MAX_AXIS_VALUE);
                outY = (short)Math.Clamp(maxedOut.Y, MIN_AXIS_VALUE, MAX_AXIS_VALUE);

                float sentRatio = MAX_AXIS_VALUE / finalVector.Length();
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
