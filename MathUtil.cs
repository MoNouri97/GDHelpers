using System;

namespace GDHelpers
{
    public static class MathUtil
    {
        /// <summary>
        /// Maps a value from one range to another range.
        /// </summary>
        /// <param name="value">The value to map</param>
        /// <param name="fromMin">The minimum value of the source range</param>
        /// <param name="fromMax">The maximum value of the source range</param>
        /// <param name="toMin">The minimum value of the target range</param>
        /// <param name="toMax">The maximum value of the target range</param>
        /// <returns>The mapped value in the target range</returns>
        /// <exception cref="ArgumentException">Thrown when source range min equals max</exception>
        public static double Map(
            double value,
            double fromMin,
            double fromMax,
            double toMin,
            double toMax
        )
        {
            if (Math.Abs(fromMax - fromMin) < double.Epsilon)
                throw new ArgumentException("Source range cannot have zero width", nameof(fromMax));

            var slope = (toMax - toMin) / (fromMax - fromMin);
            return toMin + slope * (value - fromMin);
        }

        /// <summary>
        /// Maps a value from one range to another range with clamping to ensure the output stays within bounds.
        /// </summary>
        /// <param name="value">The value to map</param>
        /// <param name="fromMin">The minimum value of the source range</param>
        /// <param name="fromMax">The maximum value of the source range</param>
        /// <param name="toMin">The minimum value of the target range</param>
        /// <param name="toMax">The maximum value of the target range</param>
        /// <returns>The mapped value, clamped to the target range</returns>
        /// <exception cref="ArgumentException">Thrown when source range min equals max</exception>
        public static double MapClamped(
            double value,
            double fromMin,
            double fromMax,
            double toMin,
            double toMax
        )
        {
            var mapped = Map(value, fromMin, fromMax, toMin, toMax);
            return Math.Min(Math.Max(mapped, Math.Min(toMin, toMax)), Math.Max(toMin, toMax));
        }

        /// <summary>
        /// provides smooth, natural-feeling movement that's truly frame rate independent
        /// </summary>
        public static float FpsIndependentSpeed(this double delta, float speed)
        {
            var lerpFactor = 1.0f - Math.Exp(-speed * delta);
            return (float)lerpFactor;
        }

        public static float Round2Decimal(float value)
        {
            float rounded = (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return rounded;
        }

        public static string Round2String(float value)
        {
            return Round2Decimal(value).ToString("0.##");
        }

        private static readonly string[] LargeSuffixes = ["", "K", "M", "B", "T", "Qa", "Qi"];

        public static string FormatLargeNumber(this float value)
        {
            return FormatLargeNumber((double)value);
        }

        public static string FormatLargeNumber(this int value)
        {
            return FormatLargeNumber((double)value);
        }

        public static string FormatLargeNumber(this double value)
        {
            if (value < 1000)
                return value.ToString("0.##");

            int i = 0;
            double scaled = value;

            while (scaled >= 1000 && i < LargeSuffixes.Length - 1)
            {
                scaled /= 1000;
                i++;
            }

            return scaled.ToString("0.##") + LargeSuffixes[i];
        }

        public static double RoundToFormatPrecision(double value)
        {
            if (value < 1000)
                return Math.Round(value, 2, MidpointRounding.AwayFromZero);

            int i = 0;
            double scaled = value;

            while (scaled >= 1000 && i < LargeSuffixes.Length - 1)
            {
                scaled /= 1000;
                i++;
            }

            scaled = Math.Round(scaled, 2, MidpointRounding.AwayFromZero);
            return scaled * Math.Pow(1000, i);
        }
    }
}
