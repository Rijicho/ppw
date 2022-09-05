using RUtil.Mathematics;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace RUtil.Curve
{
    /// <summary>
    /// Implementation of rational κ-Curves 
    /// </summary>
    public static partial class RKCurves
    {
        public static BezierCurve.CalcQMethod CalcQMethod { get; set; } = BezierCurve.CalcQMethod.Newton;
        public static BezierCurve.CalcQNewtonInit CalcQNewtonInitMethod { get; set; } = BezierCurve.CalcQNewtonInit.Heulistic1;

        public static float InfinityWeight { get; set; } = 1000;
        public static double NewtonEpsilon { get; set; } = 0.001;

        public static int DefaultSize = 8;
        public static int DefaultStepPerSegment = 16;

        static CalcSpace defaultCSpace;
        static PlotSpace defaultPSpace;

        //Solve Ax=b. A is tridiagonal nxn matrix (with corners). For each i, only x[i,1] is used as buffer. 
        static void SolveTridiagonalEquation(int n, double[] A, ExtendedBezierControls x, ExtendedPlayerControls b)
        {
            /* A=LU */
            for (int i = 0, i3 = 0; i < n + 1; i++, i3 += 3)
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

        //Initialization
        static void Step0(CalcSpace space, bool isLoop)
        {
            var n = space.N;
            var ps = space.P;
            var cs = space.C;
            var lambdas = space.L;
            var A = space.A;

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
            {
                cs[i, 1] = ps[i];
            }

            //他のベジェ制御点を初期化
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                cs[next, 0] = cs[i, 2] = (1 - lambdas[i]) * cs[i, 1] + lambdas[i] * cs[next, 1];
            }

            //行列の端の値は固定
            var alen = (n + 2) * 3;
            A[0] = 0;
            A[1] = 1;
            A[2] = 0;
            A[alen - 1] = 0;
            A[alen - 2] = 1;
            A[alen - 3] = 0;
            if (!isLoop)
            {
                //非ループの場合はさらにもう一行ずつ固定
                A[3] = 0;
                A[4] = 1;
                A[5] = 0;
                A[alen - 4] = 0;
                A[alen - 5] = 1;
                A[alen - 6] = 0;
            }
        }
        //Calculate λi
        static void Step1(CalcSpace space, bool isLoop)
        {
            var cs = space.C;
            var ls = space.L;
            var n = space.N;

            //三角形の面積を求める関数
            float TriArea(Vector3 p1, Vector3 p2, Vector3 p3)
            {
                p1 -= p3; p2 -= p3;
                return Vector3.Cross(p1, p2).magnitude / 2f;
            }

            int begin = isLoop ? 0 : 1;
            int end = isLoop ? n : n - 2;
            for (var i = begin; i < end; i++)
            {
                var next = (i + 1) % n;
                var w = space.C[i, 1].ModifiedWeight;
                var nextw = space.C[next, 1].ModifiedWeight;
                var c = cs.Points;
                var t1 = nextw * nextw * TriArea(c[i * 2].Position, c[i * 2 + 1].Position, c[next * 2 + 1].Position);
                var t2 = w * w * TriArea(c[i * 2 + 1].Position, c[next * 2 + 1].Position, c[next * 2 + 2].Position);
                if (Mathf.Abs(t1 - t2) < 0.00001f)
                    ls[i] = 0.5f;
                else
                    ls[i] = (t1 - Mathf.Sqrt(t1 * t2)) / (t1 - t2);
            }
        }
        //Update bezier-control-points 0,2
        static void Step2(CalcSpace space)
        {
            var n = space.N;
            var cs = space.C;
            var ls = space.L;

            for (var i = 0; i < n - 1; i++)
            {
                cs[i + 1, 0] = new ControlPoint((1 - ls[i]) * cs[i, 1].Position + ls[i] * cs[i + 1, 1].Position);
            }
            cs[0, 0] = cs[n - 1, 2] = new ControlPoint((1 - ls[n - 1]) * cs[n - 1, 1].Position + ls[n - 1] * cs[0, 1].Position);
        }


        public static (double a, double b) SolveCurvatureRequirement(Vector3 P, Vector3 L, Vector3 R, double w, float approxError=0.000001f)
        {
            var p = P - L;
            var r = R - L;
            var smp = p.sqrMagnitude;
            var smr = r.sqrMagnitude;
            var mp = Mathf.Sqrt(smp);
            var mr = Mathf.Sqrt(smr);

            var w2 = w * w;
            var M = (P + R) / 2;
            var c2 = (double)(L - M).sqrMagnitude / (P - M).sqrMagnitude;
            var b = (double)Vector3.Cross(P - L, M - L).magnitude / (P - M).sqrMagnitude;
            var a2 = c2 - b * b;
            if (a2 < 0) a2 = 0;
            var a = Math.Sqrt(a2);

            if (Mathf.Abs(mp)<approxError)
            {
                return (1, 1);
            }
            if (Mathf.Abs(mr)<approxError)
            {
                return (1, -1);
            }
            if (Mathf.Abs(mp-mr)<approxError || a2==0)
            {
                return (1 / (1 + w), 0);
            }

            var sigma = Math.Sign((R - L).sqrMagnitude - (P - L).sqrMagnitude);

            if (Mathf.Abs((float)b)<approxError)
            {
                if (a < 1)
                {
                    var rho = sigma * (P - M).magnitude / (L - M).magnitude;
                    var beta = sigma / Math.Sqrt(1 - w2 * (1 - rho * rho));
                    var alpha = (beta * (beta + rho)) / (1 + rho * beta);
                    return (alpha, beta);
                }
                else
                {
                    var phi = (P - M).magnitude / (L - M).magnitude;
                    var k = 1 / (1 + w * Math.Sqrt(1 - phi * phi));
                    return (k, k * sigma * phi);
                }
            }


            {
                var A = a2 * w2 + c2 * c2;
                var B = -2 * a * (w2 + c2 + c2 * w2);
                var C = w2 * (2 * a2 + (c2 + 1) * (c2 + 1)) + a2 - c2 * c2;
                var D = -2 * a * (w2 - c2 + c2 * w2);
                var E = a2 * (w2 - 1);
                var (phi1, phi2, phi3, phi4) = EquationSolver.SolveQuarticEquation(A, B, C, D, E);

                double phi = phi1.a;

                //４実解の場合、phi2とphi3が重解かつ求める解
                //float e = 0.000001f; //小さくしすぎるとNaN発生、大きくしすぎると別の解へ飛ぶ。ちゃんとやるには４次の判別式が必要そう
                float e = 0.000001f;
                if (Mathf.Abs((float)phi2.b) <= e && Mathf.Abs((float)phi3.b) <= e)
                {
                    phi = phi2.a;
                }

                //phi1>1の場合、phi4が解っぽい？（主にw<<1で発生）
                if (phi > 1)
                {
                    phi = Math.Abs(phi4.a);
                }

                //ここでまだphiが範囲外なら４虚解。w>>1でPRの中央付近で発生、よって0とする。４次方程式の各係数が数万だったりする。桁あふれ？
                if (Math.Abs(phi) > 1)
                {
                    phi = 0;
                }

                var k = 1 / (1 + w * Math.Sqrt(1 - phi * phi));
                return (k, k * sigma * phi);
            }
        }



        //Calculate maximum curvature points
        static void Step3(CalcSpace space)
        {
            var n = space.N;
            var ucs = space.P;
            var bcs = space.C;
            var ts = space.T;

            bool checkShortAxis = true;
            bool modifyWeightsForShortAxis = false;

            for (int i = 0; i < n; i++)
            {
                var (P, L, R) = (bcs[i, 0], ucs[i], bcs[i, 2]);


                //セグメントが潰れている場合は不定解なので0.5とする
                if (P == R) { ts[i] = 0.5f; continue; }
                //セグメントの端にユーザ制御点がある場合は図形的に自明
                if (L == P) { ts[i] = 0; continue; }
                if (L == R) { ts[i] = 1; continue; }


                var w = bcs[i,1].TargetWeight;

                if (w >= InfinityWeight)
                {
                    ts[i] = 0.5f;
                    continue;
                }

                if(Mathf.Approximately(w, 1))
                {
                    var c2 = R - P;   // != 0
                    var u = ucs[i] - P;       // != 0

                    double a = c2.sqrMagnitude;             // != 0
                    double b = -3 * Vector3.Dot(c2, u);
                    double c = Vector3.Dot(2 * u + c2, u);
                    double d = -u.sqrMagnitude;             // != 0

                    ts[i] = EquationSolver.SolveCubicEquationRealIn01(a, b, c, d);
                }
                else
                {
                    var (p, r) = (P - L, R - L);
                    var (mp, mr) = ((double)p.magnitude, (double)r.magnitude);
                    var pr = (double)Vector3.Dot(p, r);


                    if (checkShortAxis && modifyWeightsForShortAxis)
                    {
                        var axisCheck = (float)(pr / mp / mr + w);
                        var isShortAxis = axisCheck <= 0;

                        if (isShortAxis) //短軸の頂点の場合
                        {
                            w = (float)(-pr / mp / mr);
                            bcs[i, 1] = new ControlPoint(bcs[i, 1].Position, bcs[i, 1].TargetWeight, w);
                            ucs[i] = new ControlPoint(ucs[i].Position, ucs[i].TargetWeight, w);
                        }
                    }



                    var (_,ti) = BezierCurve.CalcQFromMaxCurvaturePoint(P, L, R, w, CalcQMethod, CalcQNewtonInitMethod);


                    ti = ti < 0 ? 0 : ti > 1 ? 1 : ti;
                    
                    if (checkShortAxis && !modifyWeightsForShortAxis)
                    {
                        var axisCheck = (float)(pr / mp / mr + w);
                        var isShortAxis = axisCheck <= 0;

                        if (isShortAxis) //短軸の頂点の場合
                        {
                            var gamma = (float)(mp / (mp + mr));
                            if (Mathf.Approximately(gamma, 0.5f))
                            {
                                ti = 0.5f;
                            }
                            else
                            {
                                var altT = (- gamma + Mathf.Sqrt(gamma - gamma * gamma)) / (1 - 2 * gamma);
                                var lerp = Mathf.Pow(Mathf.Clamp01(axisCheck / (w - 1)), 0.5f);

                                ti = Mathf.Lerp(ti, altT, lerp);
                            }
                        }
                    }
                    
                    ts[i] = ti;
                }

            }
        }
        //Update bezier-control-points 1
        static void Step4(CalcSpace space, bool isLoop)
        {
            var n = space.N;
            var ps = space.P;
            var cs = space.C;
            var lambdas = space.L;
            var ts = space.T;
            var A = space.A;

            //係数行列Aを構成（端の部分はStep0で初期化済）
            {
                for (int i = isLoop ? 0 : 1; i < (isLoop ? n : (n - 1)); i++)
                {
                    var w = cs[i,1].ModifiedWeight;
                    var ofs = (i + 1) * 3;
                    var next = (i + 1) % n;
                    var prev = (i - 1 + n) % n;

                    //ランクが下がってしまう場合微調整
                    if (ts[i] == 1 && ts[next] == 0 || !isLoop && i == n - 2 && ts[i] == 1)
                        ts[i] = 0.99999f;
                    if (!isLoop && i == 1 && ts[i] == 0)
                        ts[i] = 0.00001f;

                    var tmp = (1 - ts[i]) * (1 - ts[i]);
                    var weight = w == 1 ? 1 : (tmp + 2 * w * ts[i] * (1 - ts[i]) + ts[i] * ts[i]);                    
                    A[ofs] = (1 - lambdas[prev]) * tmp / weight;
                    A[ofs + 1] = (lambdas[prev] * tmp + (2 * w - (2 * w - 1 + lambdas[i]) * ts[i]) * ts[i]) / weight;
                    A[ofs + 2] = lambdas[i] * ts[i] * ts[i] / weight;
                }
            }

            //入出力ベクトルを拡張
            var extendedPs = new ExtendedPlayerControls(ps, cs);
            var extendedCs = new ExtendedBezierControls(cs);

            //連立方程式を解く
            SolveTridiagonalEquation(n, A, extendedCs, extendedPs);

        }

        /// <summary>
        /// Calculate bezier control-points from user's control-points. CalcSpace space must be already initialized with inputs.
        /// </summary>
        /// <param name="space">Memory for calculation initialized with inputs</param>
        /// <param name="iteration">Iteration count</param>
        /// <param name="isLoop">Close curve or not</param>
        internal static BezierControls CalcBezierControlsInternal(CalcSpace space, int iteration, bool isLoop)
        {
            if (space.N == 0)
            {
                for (int i = 0; i < 3; i++)
                    space.C[i] = ControlPoint.zero;
                return space.C;
            }
            if (space.N == 1)
            {
                for (int i = 0; i < 3; i++)
                    space.C[i] = space.P[0];
                return space.C;
            }
            if (space.N == 2)
            {
                space.C[0] = space.P[0];
                space.C[1] = (space.P[0] + space.P[1]) / 2;
                space.C[2] = space.P[1];
                return space.C;
            }


            Step0(space, isLoop);
            for (int i = 0; i < iteration; i++)
            {
                if (i < 3 || i < iteration / 2)
                    Step1(space, isLoop);
                Step2(space);
                Step3(space);
                Step4(space, isLoop);
            }
            Step2(space);

            return space.Result;
        }

        /// <summary>
        /// Calculate bezier control-points from user's control-points.
        /// </summary>
        /// <param name="points">User's control points</param>
        /// <param name="weights">Weights of each control point</param>
        /// <param name="globalWeight">Global weights of the curve</param>
        /// <param name="iteration">Iteration count</param>
        /// <param name="isLoop">Close curve or not</param>
        /// <param name="space">The memory for calculation</param>
        /// <returns></returns>
        public static BezierControls CalcBezierControls(IEnumerable<ControlPoint> points, int iteration, bool isLoop, CalcSpace space = default)
        {
            space = space ?? defaultCSpace ?? (defaultCSpace = new CalcSpace(DefaultSize));
            space.Init(points);
            var result = CalcBezierControlsInternal(space, iteration, isLoop);

            var ret = new BezierControls(result.SegmentCount, result.SegmentCount);
            Array.Copy(result.Points, 0, ret.Points, 0, ret.PointCount);
            return ret;
        }

        public static void CalcBezierControls(IEnumerable<ControlPoint> points, int iteration, bool isLoop, BezierControls output, CalcSpace space = default)
        {
            space = space ?? defaultCSpace ?? (defaultCSpace = new CalcSpace(DefaultSize));
            space.Init(points);
            var result = CalcBezierControlsInternal(space, iteration, isLoop);

            if (output.SegmentMax < result.SegmentCount)
                output.Resize(result.SegmentCount);
            output.SegmentCount = result.SegmentCount;

            Array.Copy(result.Points, 0, output.Points, 0, result.PointCount);
        }


        /// <summary>
        /// Calculate a point from bezier-control-points and parameter t.
        /// </summary>
        public static Vector3 CalcPlotSingle(Vector3 c0, Vector3 c1, Vector3 c2, float t, float w = 1)
        {
            var s = 1 - t;
            if (w == 1)
            {
                return s * (s * c0 + t * c1) + t * (s * c1 + t * c2);
            }
            else
            {
                return (s * s * c0 + 2 * w * s * t * c1 + t * t * c2) / (s * s + 2 * w * s * t + t * t);
            }
        }

        /// <summary>
        /// Calculate points for plotting rational bezier curves.
        /// </summary>
        /// <param name="space">The memory for plot</param>
        /// <param name="cs">Bezier control points</param>
        /// <param name="stepPerSegment">More step, more detailed curve.</param>
        /// <param name="isLoop">Close curve or not</param>
        /// <returns>The number of valid points</returns>
        internal static int CalcPlotsInternal(PlotSpace space, BezierControls cs, int stepPerSegment, bool isLoop)
        {

            //各セグメントについて、指定されたステップ数で分割した点を計算
            int k, idx = 0;
            int segCnt = isLoop || cs.SegmentCount < 3 ? cs.SegmentCount : cs.SegmentCount - 2;
            for (k = 0; k < segCnt; k++)
            {
                var nextk = (k + 1) % cs.SegmentCount;
                for (var i = 0; i < stepPerSegment; i++)
                {
                    space.Result[idx++] = CalcPlotSingle(cs[nextk, 0].Position, cs[nextk, 1].Position, cs[nextk, 2].Position, i / (float)stepPerSegment, cs[nextk, 1].ModifiedWeight);
                }
            }
            var last = isLoop || cs.SegmentCount < 3 ? 0 : k;
            space.Result[idx++] = CalcPlotSingle(cs[last, 0].Position, cs[last, 1].Position, cs[last, 2].Position, 1, cs[last,1].ModifiedWeight);
            return idx;
        }

        /// <summary>
        /// Calculate points for plotting rational bezier curves.
        /// </summary>
        /// <param name="cs">Bezier control points</param>
        /// <param name="isLoop">Close curve or not</param>
        /// <param name="stepPerSegment">More step, more detailed curve.</param>
        /// <param name="space">The memory for plot</param>
        /// <returns>Plotted points</returns>
        public static Vector3[] CalcPlots(BezierControls cs, bool isLoop, int stepPerSegment = default, PlotSpace space = default)
        {
            space = space ?? defaultPSpace ?? (defaultPSpace = new PlotSpace(DefaultSize, DefaultStepPerSegment));
            if (stepPerSegment <= 0) 
                stepPerSegment = DefaultStepPerSegment;

            if (space.SegmentMax < cs.SegmentCount || space.StepPerSegment < stepPerSegment)
            {
                int n = 2;
                while (n < cs.SegmentCount)
                {
                    n *= 2;
                }
                space.Resize(n, stepPerSegment);
            }

            var count = CalcPlotsInternal(space, cs, stepPerSegment, isLoop);
            var ret = new Vector3[count];
            Array.Copy(space.Result, 0, ret, 0, count);
            return ret;
        }

        /// <summary>
        /// Calculate points for plotting rational bezier curves, output to given array.
        /// </summary>
        /// <param name="cs">Bezier control points</param>
        /// <param name="isLoop">Close curve or not</param>
        /// <param name="stepPerSegment">More step, more detailed curve.</param>
        /// <param name="space">The memory for plot</param>
        /// <returns>The number of valid points</returns>
        public static int CalcPlots(BezierControls cs, bool isLoop, ref Vector3[] output, int stepPerSegment = default, PlotSpace space = default)
        {
            space = space ?? defaultPSpace ?? (defaultPSpace = new PlotSpace(DefaultSize, DefaultStepPerSegment));
            if (stepPerSegment <= 0)
                stepPerSegment = DefaultStepPerSegment;

            if (space.SegmentMax < cs.SegmentCount || space.StepPerSegment < stepPerSegment)
            {
                int n = 2;
                while (n < cs.SegmentCount)
                {
                    n *= 2;
                }
                space.Resize(n, stepPerSegment);
            }
            var count = CalcPlotsInternal(space, cs, stepPerSegment, isLoop);
            if (output.Length < count)
                Array.Resize(ref output, count);
            Array.Copy(space.Result, 0, output, 0, count);
            return count;
        }



        public static float CalcCurvature(Vector3 c0, Vector3 c1, Vector3 c2, float w, float t)
        {
            //三角形の面積を求める関数
            float TriArea(Vector2 p1, Vector2 p2, Vector2 p3)
            {
                p1 -= p3; p2 -= p3;
                return (p1.x * p2.y - p2.x * p1.y) / 2f;
            }
            var s = 1 - t;
            var a = s * s + 2 * w * t * s + t * t;
            var b = (w * (c1 - c0) * s * s + (c2 - c0) * t * s + w * (c2 - c1) * t * t).magnitude;
            var ab = a / b;
            return w * TriArea(c0, c1, c2) * ab * ab * ab;
        }
    }
}
