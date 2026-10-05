using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Support
{
    /// <summary>
    /// The mod's log lines, all under one prefix so they can be found in KSP.log.
    /// </summary>
    public static class Log
    {
        private const string Prefix = "[SuMa.BatteryCalculator] ";

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Warning(string message)
        {
            Debug.LogWarning(Prefix + message);
        }
    }
}
