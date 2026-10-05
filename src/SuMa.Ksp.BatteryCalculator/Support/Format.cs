using System;
using System.Globalization;

namespace SuMa.Ksp.BatteryCalculator.Support
{
    /// <summary>
    /// How numbers are written in the window: one place, so every figure reads the same way.
    /// </summary>
    internal static class Format
    {
        /// <summary>
        /// A distance in metres, as kilometres once it is large enough to need them.
        /// </summary>
        public static string Distance(double metres)
        {
            if (Math.Abs(metres) >= 10000.0)
            {
                return (metres / 1000.0).ToString("N0", CultureInfo.InvariantCulture) + " km";
            }

            return metres.ToString("N0", CultureInfo.InvariantCulture) + " m";
        }

        /// <summary>
        /// An amount of electric charge.
        /// </summary>
        public static string Energy(double electricCharge)
        {
            return electricCharge.ToString(Math.Abs(electricCharge) >= 100.0 ? "N0" : "N1", CultureInfo.InvariantCulture) + " EC";
        }

        /// <summary>
        /// A rate of electric charge per second, with enough decimals that a probe core's trickle does not
        /// read as zero.
        /// </summary>
        public static string Rate(double electricChargePerSecond)
        {
            var magnitude = Math.Abs(electricChargePerSecond);
            string format;
            if (magnitude >= 100.0)
            {
                format = "N1";
            }
            else if (magnitude >= 0.1 || magnitude == 0.0)
            {
                format = "N2";
            }
            else
            {
                format = "N4";
            }

            return electricChargePerSecond.ToString(format, CultureInfo.InvariantCulture) + " EC/s";
        }

        /// <summary>
        /// A duration, in the game's own calendar so a Kerbin day is six hours unless the player has chosen
        /// Earth time.
        /// </summary>
        public static string Time(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds))
            {
                return "forever";
            }

            if (seconds < 1.0)
            {
                return "0s";
            }

            return KSPUtil.PrintTime(seconds, 3, false);
        }
    }
}
