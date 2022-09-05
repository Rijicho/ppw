using System.Collections.Generic;
using System;
using UnityEngine;

namespace RUtil.Curve
{
    /// <summary>
    /// Implementation of κ-Curves system in 2D space (x-y)
    /// </summary>
    public static class KCurves2D
    {
        /*  
            Mainly implemented based on this paper:

            Zhipei Yan, Stephen Schiller, Gregg Wilensky, Nathan Carr, and Scott Schaefer. 2017. 
            K-curves: interpolation at local maximum curvature. 
            ACM Trans. Graph. 36, 4, Article 129 (July 2017), 7 pages. 
            DOI:https://doi.org/10.1145/3072959.3073692
        */

        /// <summary>
        /// Wrapper of Vector2[] for Bezier-control-points. c_{i,j} can be accessed by indexer [i,j].
        /// </summary>
        public class BezierControls
        {
            /// <summary>
            /// Raw bezier control-points, in order of c_{0,0}, c_{0,1}, c_{1,0}, ..., c_{n-1,0}, c_{n-1,1}, c_{n-1,2}.
            /// </summary>
            public Vector2[] Points { get; private set; }

            /// <summary>
            /// The number of bezier curves. 
            /// </summary>
            public int SegmentCount { get; private set; }

            /// <summary>
            /// Same as this.Points[i].
            /// </summary>
            public Vector2 this[int i]
            {
                get => Points[i];
                set => Points[i] = value;
            }

            /// <summary>
            /// Get c_{i,j}.
            /// </summary>
            public Vector2 this[int i, int j]
            {
                get => Points[2 * i + j];
                set => Points[2 * i + j] = value;
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="n">The number of user's control-points</param>
            public BezierControls(int n)
            {
                SegmentCount = n < 3 ? 1 : n;
                Points = new Vector2[2 * SegmentCount + 1];
            }
        }

        /// <summary>
        /// Memory space for bezier-control-points calculation. 
        /// Please renew the instance when the number of user-control-points has changed.
        /// </summary>
        public sealed class CalcSpace
        {
            /// <summary>
            /// The number of user-control-points
            /// </summary>
            public int N { get; private set; }
            /// <summary>
            /// Memory for λi
            /// </summary>
            internal float[] L { get; private set; }
            /// <summary>
            /// Memory for Bezier-control-points (output)
            /// </summary>
            internal BezierControls C { get; private set; }
            /// <summary>
            /// Memory for ti
            /// </summary>
            internal double[] T { get; private set; }
            /// <summary>
            /// Memory for matrix for simultaneous equation of Step4 
            /// </summary>
            internal double[] A { get; private set; }

            /// <summary>
            /// Allocator
            /// </summary>
            /// <param name="n">The number of user-control-points</param>
            public CalcSpace(int n)
            {
                N = n;
                L = new float[n];
                C = new BezierControls(n);
                T = new double[n];
                A = new double[(n+2) * 3];
            }
            public BezierControls Result => C;
        }

        /// <summary>
        /// Memory space for plotting calculation.
        /// Please renew the instance when the number of user's control-points, loop option or step-per-segment has changed.
        /// </summary>
        public sealed class PlotSpace
        {
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
            public Vector2[] Result { get; private set; }

            /// <summary>
            /// Allocator
            /// </summary>
            /// <param name="n">The number of user's control-points</param>
            /// <param name="stepPerSegment">The number of points used to plot one bezier curve</param>
            /// <param name="isLoop">The curve is closed or not</param>
            public PlotSpace(int n, int stepPerSegment, bool isLoop)
            {
                SegmentCount = n;
                StepPerSegment = stepPerSegment;
                if (n < 3)
                    Result = new Vector2[stepPerSegment + 1];
                else
                    Result = new Vector2[(isLoop ? n : (n - 2)) * stepPerSegment + 1];
            }
        }


        struct ExtendedPlayerControls
        {
            Vector2 top;
            Vector2[] ps;
            Vector2 bottom;

            public Vector2 this[int i]
            {
                get => i == 0 ? top : i <= ps.Length ? ps[i - 1] : bottom;
                set
                {
                    if (i == 0) top = value;
                    else if (i <= ps.Length) ps[i - 1] = value;
                    else bottom = value;
                }
            }

            public ExtendedPlayerControls(Vector2[] ps, BezierControls cs)
            {
                top = cs[cs.SegmentCount-1,1];
                this.ps = ps;
                bottom = cs[0,1];
            }
        }
        struct ExtendedBezierControls
        {
            Vector2 top;
            Vector2[] cs;
            Vector2 bottom;

            public Vector2 this[int i]
            {
                get => i == 0 ? top : i <= cs.Length / 2 ? cs[i * 2 - 1] : bottom;
                set
                {
                    if (i == 0) top = value;
                    else if (i <= cs.Length / 2) cs[i * 2 - 1] = value;
                    else bottom = value;
                }
            }

            public ExtendedBezierControls(BezierControls cs)
            {
                top = cs[cs.SegmentCount - 1, 1];
                this.cs = cs.Points;
                bottom = cs[0, 1];
            }
        }

        //Solve Ax=b. A is tridiagonal nxn matrix (with corners). For each i, only x[i,1] is used as buffer. 
        static void SolveTridiagonalEquation(double[] A, ExtendedBezierControls x, ExtendedPlayerControls b)
        {
            var n = A.Length / 3 - 2;

            /* A=LU */
            for (int i=0, i3=0; i < n + 1; i++, i3 += 3)
            {
                A[i3 + 3] /= A[i3 + 1];                 //l21  := a21/a11
                A[i3 + 4] -= A[i3 + 3] * A[i3 + 2];     //a'11 := a22-l21u12
            }
            
            /* Ly=b */            
            x[0] = b[0];                    //対角要素は全て1なので、最上行はそのまま            
            for (var i = 1; i < n + 1; i++) //対角要素の左隣の要素を対応するx（計算済み）にかけて引く
            {
                x[i] = b[i] - (float)A[i * 3] * x[i - 1];
            }

            /* Ux=y */
            x[n + 1] /= (float)A[(n + 1) * 3 + 1];              //最下行はただ割るだけ
            for (int i = n, i3 = n * 3; i >= 0; i--, i3 -= 3)   //対角要素の右隣の要素を対応するx（計算済み）にかけて引いて割る
            {
                x[i] = (x[i] - (float)A[i3 + 2] * x[i + 1]) / (float)A[i3 + 1];
            }
        }



        //Return a real root of ax^3+bx^2+cx+d in [0,1] by Cardano's formula
        static double SolveCubicEquation(double a, double b, double c, double d)
        {
            //負の値に対応した３乗根
            double Cbrt(double x) => Math.Sign(x) * Math.Pow(Math.Abs(x), 1.0 / 3);

            var A = b / a;
            var B = c / a;
            var C = d / a;
            var p = (B - A * A / 3) / 3;
            var q = (2.0 / 27 * A * A * A - A * B / 3 + C) / 2;
            var D = q * q + p * p * p;
            var Ad3 = A / 3;

            if (Math.Abs(D) < 1.0E-12)
            {
                var ret = Cbrt(q) - Ad3;
                return Math.Min(Math.Max(ret, 0), 1);
            }
            else if (D > 0)
            {
                var sqrtD = Math.Sqrt(D);
                var u = Cbrt(-q + sqrtD);
                var v = Cbrt(-q - sqrtD);
                var ret = u + v - Ad3;
                if (0 <= ret && ret <= 1)
                    return ret;

                throw new Exception($"Invalid solution: {ret}");
            }
            else //D < 0
            {
                var tmp = 2 * Math.Sqrt(-p);
                var arg = Math.Atan2(Math.Sqrt(-D), -q) / 3;
                var pi2d3 = 2 * Math.PI / 3;
                var X1mAd3 = tmp * Math.Cos(arg) - Ad3;
                if (0 <= X1mAd3 && X1mAd3 <= 1) return X1mAd3;

                var X2mAd3 = tmp * Math.Cos(arg + pi2d3) - Ad3;
                if (0 <= X2mAd3 && X2mAd3 <= 1) return X2mAd3;

                var X3mAd3 = tmp * Math.Cos(arg + pi2d3 + pi2d3) - Ad3;
                if (0 <= X3mAd3 && X3mAd3 <= 1) return X3mAd3;

                throw new Exception($"Invalid solution: {X1mAd3}, {X2mAd3}, {X3mAd3}");
            }
        }

        //Initialization
        static void Step0(Vector2[] ps, BezierControls cs, float[] lambdas, double[] A, bool isLoop)
        {
            var n = ps.Length;

            //全てのλを0.5で初期化
            for (var i = 0; i < n; i++)
                lambdas[i] = 0.5f;

            //ループしない場合、最初と最後から２番目を0,1に変更（最後はそもそも使わない）
            if (!isLoop)
            {
                lambdas[0] = 0;
                lambdas[n - 2] = 1;
                //lambdas[n - 1] = undefined;
            }

            //中央のベジェ制御点を全てユーザ制御点で初期化
            for (var i = 0; i < n; i++)
                cs[i, 1] = ps[i];

            //他のベジェ制御点を初期化
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                cs[next, 0] = cs[i, 2] = (1 - lambdas[i]) * cs[i, 1] + lambdas[i] * cs[next, 1];
            }

            //行列の端の値は固定
            A[0] = 0;
            A[1] = 1;
            A[2] = 0;
            A[A.Length - 1] = 0;
            A[A.Length - 2] = 1;
            A[A.Length - 3] = 0;
            if (!isLoop)
            {
                //非ループの場合はさらにもう一行ずつ固定
                A[3] = 0;
                A[4] = 1;
                A[5] = 0;
                A[A.Length - 4] = 0;
                A[A.Length - 5] = 1;
                A[A.Length - 6] = 0;
            }
        }
        //Calculate λi
        static void Step1(BezierControls cs, float[] lambdas, bool isLoop)
        {
            //三角形の面積を求める関数
            float TriArea(Vector2 p1, Vector2 p2, Vector2 p3)
            {
                p1 -= p3; p2 -= p3;
                return Mathf.Abs(p1.x * p2.y - p2.x * p1.y) / 2f;
            }

            var n = lambdas.Length;
            int begin = isLoop ? 0 : 1;
            int end = isLoop ? n : n - 2;
            for (var i = begin; i < end; i++)
            {
                var next = (i + 1) % n;
                var c = cs.Points;
                var t1 = TriArea(c[i*2], c[i*2+1], c[next*2+1]);
                var t2 = TriArea(c[i*2+1], c[next*2+1], c[next*2+ 2]);
                if (Mathf.Abs(t1 - t2) < 0.00001f)
                    lambdas[i] = 0.5f;
                else
                    lambdas[i] = (t1 - Mathf.Sqrt(t1 * t2)) / (t1 - t2);    
            }
        }
        //Update bezier-control-points 0,2
        static void Step2(BezierControls cs, float[] lambdas)
        {
            var n = lambdas.Length;
            for (var i = 0; i < n - 1; i++)
            {
                cs[i + 1, 0] = (1 - lambdas[i]) * cs[i, 1] + lambdas[i] * cs[i + 1, 1];
            }
            cs[0, 0] = cs[n - 1, 2] = (1 - lambdas[n - 1]) * cs[n - 1, 1] + lambdas[n - 1] * cs[0, 1];
        }
        //Calculate maximum curvature points
        static void Step3(Vector2[] ps, BezierControls cs, double[] ts)
        {
            for (int i = 0; i < ts.Length; i++)
            {
                //セグメントが潰れている場合は不定解なので0.5とする
                if(cs[i,0] == cs[i, 2]) { ts[i] = 0.5; continue; }
                //セグメントの端にユーザ制御点がある場合は図形的に自明
                if(ps[i] == cs[i, 0]) { ts[i] = 0; continue; }
                if(ps[i] == cs[i, 2]) { ts[i] = 1; continue; }

                var c2 = cs[i, 2] - cs[i, 0];   // != 0
                var p = ps[i] - cs[i, 0];       // != 0

                double a = c2.sqrMagnitude;             // != 0
                double b = -3 * Vector2.Dot(c2, p);     
                double c = Vector2.Dot(2 * p + c2, p);  
                double d = -p.sqrMagnitude;             // != 0

                ts[i] = SolveCubicEquation(a, b, c, d);
            }
        }
        //Update bezier-control-points 1
        static void Step4(Vector2[] ps, BezierControls cs, float[] lambdas, double[] ts, double[] A, bool isLoop)
        {
            var n = ps.Length;

            //係数行列Aを構成（端の部分はStep0で初期化済）
            {
                for (int i = isLoop ? 0 : 1; i < (isLoop ? n : (n-1)); i++)
                {
                    var ofs = (i+1) * 3;
                    var next = (i + 1) % n;
                    var prev = (i - 1 + n) % n;

                    //ランクが下がってしまう場合微調整
                    if (ts[i] == 1 && ts[next] == 0 || !isLoop && i == n - 2 && ts[i] == 1)
                        ts[i] = 0.99999f;
                    if (!isLoop && i == 1 && ts[i] == 0)
                        ts[i] = 0.00001f;

                    var tmp = (1 - ts[i]) * (1 - ts[i]);
                    A[ofs] = (1 - lambdas[prev]) * tmp;
                    A[ofs + 1] = lambdas[prev] * tmp + (2 - (1 + lambdas[i]) * ts[i]) * ts[i];
                    A[ofs + 2] = lambdas[i] * ts[i] * ts[i];
                }
            }

            //入出力ベクトルを拡張
            var extendedPs = new ExtendedPlayerControls(ps,cs);
            var extendedCs = new ExtendedBezierControls(cs);

            //連立方程式を解く
            SolveTridiagonalEquation(A, extendedCs, extendedPs);
        }



        /// <summary>
        /// Calculate bezier control-points from user's control-points.
        /// </summary>
        /// <param name="points">User's control points</param>
        /// <param name="space">Calculation space. The results will be stored at [space.BezierPoints].</param>
        /// <param name="iteration">Iteration count</param>
        public static BezierControls CalcBezierControls(Vector2[] points, CalcSpace space, int iteration, bool isLoop)
        {
            if (points.Length != space.N)
            {
                throw new ArgumentException($"The length of {nameof(points)} must equals to {nameof(space)}.{nameof(space.N)}.");
            }
            if (points.Length == 0)
            {
                for (int i = 0; i < 3; i++)
                    space.C.Points[i] = Vector2.zero;
                return space.C;
            }
            if (points.Length == 1)
            {
                for (int i = 0; i < 3; i++)
                    space.C.Points[i] = points[0];
                return space.C;
            }
            if (points.Length == 2)
            {
                space.C.Points[0] = points[0];
                space.C.Points[1] = (points[0] + points[1]) / 2;
                space.C.Points[2] = points[1];
                return space.C;
            }
            Step0(points, space.C, space.L, space.A, isLoop);
            for (int i = 0; i < iteration; i++)
            {
                if (i < 3 || i < iteration / 2)
                    Step1(space.C, space.L, isLoop);
                Step2(space.C, space.L);
                Step3(points, space.C, space.T);
                Step4(points, space.C, space.L, space.T, space.A, isLoop);
            }
            Step2(space.C, space.L);
            return space.C;
        }

        /// <summary>
        /// Calculate a point from bezier-control-points and parameter t.
        /// </summary>
        public static Vector2 CalcPlotSingle(Vector2 c0, Vector2 c1, Vector2 c2, float t)
        {
            var s = 1 - t;
            return s * (s * c0 + t * c1) + t * (s * c1 + t * c2);
            //return s * s * c0 + 2 * s * t * c1 + t * t * c2;
        }

        /// <summary>
        /// Calculate points for plotting κ-Curves from user's control points.
        /// </summary>
        /// <param name="points">User's control points</param>
        /// <param name="calcSpace">Calculation space. The results will be stored at [space.Plot].</param>
        /// <param name="iteration">Iteration count</param>
        /// <param name="stepPerSegment">More step, more detailed curve.</param>
        public static Vector2[] CalcPlots(Vector2[] points, CalcSpace calcSpace, PlotSpace plotSpace, int iteration, int stepPerSegment, bool isLoop)
        {
            //ベジェ制御点を計算
            var cs = CalcBezierControls(points, calcSpace, iteration, isLoop);

            //各セグメントについて、指定されたステップ数で分割した点を計算
            return CalcPlots(cs, plotSpace, stepPerSegment, isLoop);
        }

        public static Vector2[] CalcPlots(BezierControls cs, PlotSpace space, int stepPerSegment, bool isLoop)
        {
            //各セグメントについて、指定されたステップ数で分割した点を計算
            int offset, k;
            int segCnt = isLoop || cs.SegmentCount < 3 ? cs.SegmentCount : cs.SegmentCount - 2;
            for (k = 0; k < segCnt; k++)
            {
                offset = k * stepPerSegment;
                var nextk = (k + 1) % cs.SegmentCount;
                for (var i = 0; i < stepPerSegment; i++)
                {
                    space.Result[offset + i] = CalcPlotSingle(cs[nextk, 0], cs[nextk, 1], cs[nextk, 2], i / (float)stepPerSegment);
                }
            }
            var last = isLoop || cs.SegmentCount < 3 ? 0 : k;
            space.Result[space.Result.Length - 1] = CalcPlotSingle(cs[last, 0], cs[last, 1], cs[last, 2], 1);
            return space.Result;

        }

        /// <summary>
        /// Judge if the point x is on the bezier curves generated by cs.
        /// </summary>
        /// <param name="cs">Bezier-control-points</param>
        /// <param name="x">Point to be judged</param>
        /// <param name="range">Torelance of distance from the nearest curve</param>
        /// <param name="distance">[Out] the distance between the point and the nearest curve</param>
        public static bool IsOnBezierCurve(Vector2[,] cs, Vector2 x, float range, out float distance)
        {
            distance = -1f;
            var n = cs.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                if (IsOnBezierCurve(cs[i, 0], cs[i, 1], cs[i, 2], x, range, out var tmp))
                {
                    if (distance < 0 || tmp < distance)
                        distance = tmp;
                }
            }
            return distance >= 0;
        }

        /// <summary>
        /// Judge if the point x is on the bezier curve generated by cs.
        /// </summary>
        /// <param name="c0">Bezier-control-points 0</param>
        /// <param name="c1">Bezier-control-points 1</param>
        /// <param name="c2">Bezier-control-points 2</param>
        /// <param name="x">Point to be judged</param>
        /// <param name="range">Torelance of distance from the curve</param>
        /// <param name="distance">[Out] the distance between the point and the curve</param>
        public static bool IsOnBezierCurve(Vector2 c0, Vector2 c1, Vector2 c2, Vector2 x, float range, out float distance)
        {
            var ts = new List<(float, int)>();
            for (int k = 0; k < 2; k++)
            {
                var a = c0[k];
                var b = c1[k];
                var c = c2[k];
                var d = Mathf.Sqrt((a - b) * (a - b) - (a - 2 * b + c) * (a - x[k]));
                var tmp0 = (a - b + d) / (a - 2 * b + c);
                var tmp1 = (a - b - d) / (a - 2 * b + c);
                if (0 <= tmp0 && tmp0 <= 1)
                    ts.Add((tmp0, 1 - k));
                if (0 <= tmp1 && tmp1 <= 1)
                    ts.Add((tmp1, 1 - k));
            }

            distance = -1f;

            foreach (var (t, s) in ts)
            {
                var other = (1 - t) * (1 - t) * c0[s] + 2 * (1 - t) * t * c1[s] + t * t * c2[s];

                var dist = Mathf.Abs(other - x[s]);
                if (dist < range && (distance < 0 || dist < distance))
                {
                    distance = dist;
                }
            }

            return distance >= 0;
        }

        /// <summary>
        /// Separate the plotted curve into same-length segments. 
        /// </summary>
        /// <param name="curve">Existing plotted curve</param>
        /// <param name="separated">Buffer for the results</param>
        /// <param name="segmentSize">Length of one segment</param>
        public static void SameLengthPlots(Vector2[] curve, List<Vector2> separated, float segmentSize)
        {
            separated.Clear();
            if (curve.Length == 0)
            {
                separated.Clear();
                return;
            }
            if (curve.Length == 1)
            {
                separated.Add(curve[0]);
                return;
            }

            float ovf = 0;
            for (int i = 0; i < curve.Length - 1; i++)
            {
                var distance = (curve[i + 1] - curve[i]).magnitude;
                if (distance <= ovf)
                {
                    ovf -= distance;
                    continue;
                }
                int n = (int)Mathf.Floor((distance - ovf) / segmentSize);
                var dir = (curve[i + 1] - curve[i]) / distance;
                var start = curve[i] + dir * ovf;

                separated.Add(start);
                for (int j = 1; j <= n; j++)
                    separated.Add(start + (segmentSize * j) * dir);
                ovf = ovf + segmentSize * (n + 1) - distance;
            }
            separated.Add(curve[curve.Length - 1]);
        }

        /// <summary>
        /// Returns the whole length of given plotted curve.
        /// </summary>
        public static float CurveLength(Vector2[] curve)
        {
            var ret = 0f;
            for (int i = 0; i < curve.Length - 1; i++)
            {
                ret += (curve[i + 1] - curve[i]).magnitude;
            }
            return ret;
        }


    }
}

