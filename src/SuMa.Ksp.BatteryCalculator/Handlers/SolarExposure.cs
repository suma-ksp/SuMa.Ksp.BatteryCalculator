using UnityEngine;

namespace SuMa.Ksp.BatteryCalculator.Handlers
{
    /// <summary>
    /// The best sun exposure of a panel made of several flat cells pointing different ways, such as a curved
    /// panel: the share of its cells' combined output when the Sun stands straight above their average
    /// direction, which is where a symmetric array does best.
    /// </summary>
    internal static class SolarExposure
    {
        /// <summary>
        /// Between 0 and 1, from each transform's forward axis; 1 when there are no transforms to go on.
        /// </summary>
        public static double Best(Transform[] cells)
        {
            if (cells == null || cells.Length == 0)
            {
                return 1.0;
            }

            var sum = Vector3.zero;
            foreach (var cell in cells)
            {
                if (cell != null)
                {
                    sum += cell.forward;
                }
            }

            if (sum.sqrMagnitude < 1e-6f)
            {
                return 1.0;
            }

            var sunDirection = sum.normalized;
            var exposure = 0.0;
            foreach (var cell in cells)
            {
                if (cell != null)
                {
                    exposure += Mathf.Clamp01(Vector3.Dot(cell.forward, sunDirection));
                }
            }

            return exposure / cells.Length;
        }
    }
}
