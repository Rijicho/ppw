using System;
using UnityEngine;

namespace RUtil.Curve
{
    public static partial class RKCurves
    {
        /// <summary>
        /// Memory space for plotting calculation.
        /// </summary>
        public sealed class PlotSpace
        {
            public int SegmentMax { get; private set; }
            /// <summary>
            /// The number of bezier curves
            /// </summary>
            public int SegmentCount { get; private set; }
            /// <summary>
            /// The number of points used to plot one bezier curve 
            /// </summary>
            public int StepPerSegment { get; private set; }
            /// <summary>
            /// Plotted points
            /// </summary>
            public Vector3[] Result => result;
            Vector3[] result;

            /// <summary>
            /// Allocator
            /// </summary>
            /// <param name="max">The number of user's control-points</param>
            /// <param name="stepPerSegment">The number of points used to plot one bezier curve</param>
            /// <param name="isLoop">The curve is closed or not</param>
            public PlotSpace(int max, int stepPerSegment)
            {
                SegmentMax = max;
                StepPerSegment = stepPerSegment;
                if (max < 3)
                    result = new Vector3[stepPerSegment + 1];
                else
                    result = new Vector3[max * stepPerSegment + 1];
            }

            public void Resize(int max, int stepPerSegment)
            {
                SegmentMax = max;
                StepPerSegment = stepPerSegment;
                if (max < 3)
                    Array.Resize(ref result, stepPerSegment + 1);
                else
                    Array.Resize(ref result, max * stepPerSegment + 1);

            }
        }
    }
}
