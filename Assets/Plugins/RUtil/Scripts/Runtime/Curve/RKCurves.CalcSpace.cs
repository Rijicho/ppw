using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

namespace RUtil.Curve
{
    public static partial class RKCurves
    {
        /// <summary>
        /// Memory space for main calculation. 
        /// </summary>
        public sealed class CalcSpace
        {
            /// <summary>
            /// The max number of user's control-points
            /// </summary>
            public int NMax { get; private set; }
            /// <summary>
            /// The number of valid user's control-points
            /// </summary>
            public int N { get => n; set => n = Mathf.Clamp(value, 0, NMax); }
            int n;

            internal ControlPoint[] P => p;
            ControlPoint[] p;

            /// <summary>
            /// Memory for λi
            /// </summary>
            internal float[] L => l;
            float[] l;
            /// <summary>
            /// Memory for Bezier-control-points (output)
            /// </summary>
            internal BezierControls C { get; private set; }
            /// <summary>
            /// Memory for ti
            /// </summary>
            internal double[] T => t;
            double[] t;
            /// <summary>
            /// Memory for matrix for simultaneous equation of Step4 
            /// </summary>
            internal double[] A => a;
            double[] a;
            /// <summary>
            /// The result of calculation
            /// </summary>
            public BezierControls Result => C;
            /// <summary>
            /// Allocator
            /// </summary>
            /// <param name="max">The number of user's control-points</param>
            public CalcSpace(int max)
            {
                NMax = Mathf.Max(1, max);
                p = new ControlPoint[NMax];
                l = new float[NMax];
                C = new BezierControls(NMax);
                t = new double[NMax];
                a = new double[(NMax + 2) * 3];
            }

            internal void Init(IEnumerable<ControlPoint> ps)
            {
                var len = ps.Count();

                if (len > NMax)
                {
                    int n = 2;
                    while (n < len)
                    {
                        n *= 2;
                    }
                    Resize(n);
                }

                n = len;

                int i = 0;
                foreach (var point in ps)
                {
                    p[i] = point;
                    C.SetWeight(i++, point.TargetWeight);
                }

                C.SegmentCount = N < 3 ? 1 : N;
            }

            public void Resize(int max)
            {
                var prev = NMax;
                NMax = Mathf.Max(1, max);
                Array.Resize(ref p, NMax);
                Array.Resize(ref l, NMax);
                C.Resize(NMax);
                Array.Resize(ref t, NMax);
                Array.Resize(ref a, (NMax + 2) * 3);
#if KCONICS_DEBUG
                Debug.Log($"Resize CalcSpace {prev} to {NMax}");
#endif
            }
        }
    }
}
