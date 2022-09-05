using System;
using UnityEngine;

namespace RUtil.Curve
{
    public static partial class RKCurves
    {
        /// <summary>
        /// Wrapper of Vector3[] for Bezier control-points. c_{i,j} can be accessed by the indexer [i,j].
        /// </summary>
        public class BezierControls
        {
            /// <summary>
            /// The max number of segments.
            /// </summary>
            public int SegmentMax { get => segmentMax; private set => segmentMax = Mathf.Max(value, 1); }
            int segmentMax;

            /// <summary>
            /// The number of valid segments. 
            /// </summary>
            public int SegmentCount { get => segmentCount; set => segmentCount = Mathf.Clamp(value, 1, SegmentMax); }
            int segmentCount;

            /// <summary>
            /// The number of valid points.
            /// </summary>
            public int PointCount => 2 * SegmentCount + 1;

            /// <summary>
            /// Raw bezier control-points, in order of c_{0,0}, c_{0,1}, c_{1,0}, ..., c_{n-1,0}, c_{n-1,1}, c_{n-1,2}.
            /// </summary>
            public ControlPoint[] Points => points;
            ControlPoint[] points;

            /// <summary>
            /// Same as this.Points[i].
            /// </summary>
            public ControlPoint this[int i]
            {
                get => points[i];
                set => points[i] = value;
            }

            /// <summary>
            /// Get c_{i,j}.
            /// </summary>
            public ControlPoint this[int i, int j]
            {
                get => points[2 * i + j];
                set => points[2 * i + j] = value;
            }

            public void SetWeight(int i, float weight)
            {
                points[2 * i + 1].TargetWeight = weight;
            }

            public (ControlPoint c0, ControlPoint c1, ControlPoint c2) GetSegment(int i)
            {
                return (this[i, 0], this[i, 1], this[i, 2]);
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="n">The number of user's control-points</param>
            public BezierControls(int max, int segmentCount = 1)
            {
                SegmentMax = max;
                SegmentCount = segmentCount;
                points = new ControlPoint[2 * SegmentMax + 1];

                for (int i = 0; i < points.Length; i++)
                    points[i].TargetWeight = 1;
            }

            internal void Resize(int max)
            {
                int prev = PointCount;
                SegmentMax = max;
                SegmentCount = Mathf.Min(SegmentCount, SegmentMax);
                Array.Resize(ref points, 2 * SegmentMax + 1);
                for (int i = prev; i < points.Length; i++)
                    points[i].TargetWeight = 1;
            }
        }
    }
}
