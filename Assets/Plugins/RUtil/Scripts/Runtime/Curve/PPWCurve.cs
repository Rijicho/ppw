using UnityEngine;
using System;

namespace RUtil.Curve
{

    public static partial class PPWCurve
    {
        public enum BlendingScheme
        {
            Trigonometric,
            Hyperbolic_Default,
            Hyperbolic_Lame,
            Hyperbolic_Extended,
        }

        public static BlendingScheme Scheme { get; set; } = BlendingScheme.Hyperbolic_Extended;
        public static BezierCurve.CalcQMethod CalcQMethod { get; set; } = BezierCurve.CalcQMethod.Newton;
        public static BezierCurve.CalcQNewtonInit CalcQNewtonInit { get; set; } = BezierCurve.CalcQNewtonInit.Heulistic1;


        public static float PsiInfinity { get; set; } = 2;


        public static void CalcAll(CurveData data)
        {
            CalcRange(data, 0, data.ValidCPCount);
        }

        public static void CalcRange(CurveData data, int begin, int count)
        {
            if(data.ValidCPCount == 0)
            {
                return;
            }
            if(data.ValidCPCount == 1)
            {
                data.Plots[0] = data.ControlPoints[0];
                return;
            }
            if (data.ValidCPCount == 2)
            {
                data.Plots[0] = data.ControlPoints[0];
                data.Plots[1] = data.ControlPoints[1];
                return;
            }


            var plotPerSegment = data.PlotStepPerSegment;

            if (data.IsClosed)
            {
                var segCount = data.ValidCPCount;
                var ucCount = data.ValidCPCount;

                for(int i= begin; i< begin + count; i++)
                {
                    var p = data.ControlPoints[(i + ucCount - 1) % ucCount];
                    var q = data.ControlPoints[i];
                    var r = data.ControlPoints[(i + 1) % segCount];
                    var (ci1, ti) = BezierCurve.CalcQFromMaxCurvaturePoint(p,q,r, q.w, CalcQMethod, CalcQNewtonInit);

                    for(int j = 0; j < plotPerSegment; j++)
                    {
                        var t = ti * j / plotPerSegment;
                        data.Polygons[i][j] = BezierCurve.PlotRationalBezier(p, ci1, r, q.w, t);
                    }
                    for (int j=0; j <= plotPerSegment; j++) // Interpolation function表示用にt=1まで繋げておく
                    {
                        var t = ti + (1 - ti) * j / plotPerSegment;
                        data.Polygons[i][plotPerSegment + j] = BezierCurve.PlotRationalBezier(p, ci1, r, q.w, t);
                    }
                }

                for(int i=begin; i<begin+count; i++)
                {
                    //P[i]～P[i+1]区間
                    for(int j=0; j<plotPerSegment; j++)
                    {
                        var t = (float)j / plotPerSegment;

                        var (h1, h2) = GetBlendCoefficients(t, data.Phis[i], data.Psis[i]);

                        data.Plots[plotPerSegment * i + j]
                            = h1 * data.Polygons[i][plotPerSegment + j]
                            + h2 * data.Polygons[(i + 1) % segCount][j];
                    }
                }
            }
            else
            {
                var segCount = data.ValidCPCount-1;
                var ucCount = data.ValidCPCount;

                for (int i = begin; i < begin + count; i++)
                {
                    if (i == 0)
                    {
                        var q = data.ControlPoints[0];
                        var r = data.ControlPoints[1];
                        for(int j=0; j<=plotPerSegment; j++)// Interpolation function表示用にt=1まで繋げておく
                        {
                            data.Polygons[i][plotPerSegment + j] = Vector3.Lerp(q, r, (float)j / plotPerSegment);
                        }
                    }
                    else if (i == ucCount - 1)
                    {
                        var p = data.ControlPoints[ucCount-2];
                        var q = data.ControlPoints[ucCount-1];
                        for (int j = 0; j <= plotPerSegment; j++)// Interpolation function表示用にt=1まで繋げておく
                        {
                            data.Polygons[i][j] = Vector3.Lerp(p, q, (float)j / plotPerSegment);
                        }
                    }
                    else
                    {
                        var p = data.ControlPoints[(i + ucCount - 1) % ucCount];
                        var q = data.ControlPoints[i];
                        var r = data.ControlPoints[(i + 1) % ucCount];
                        var (ci1, ti) = BezierCurve.CalcQFromMaxCurvaturePoint(p, q, r, q.w, CalcQMethod, CalcQNewtonInit);

                        for (int j = 0; j < plotPerSegment; j++)
                        {
                            var t = ti * j / plotPerSegment;
                            data.Polygons[i][j] = BezierCurve.PlotRationalBezier(p, ci1, r, q.w, t);
                        }
                        for (int j = 0; j <= plotPerSegment; j++)// Interpolation function表示用にt=1まで繋げておく
                        {
                            var t = ti + (1 - ti) * j / plotPerSegment;
                            data.Polygons[i][plotPerSegment + j] = BezierCurve.PlotRationalBezier(p, ci1, r, q.w, t);
                        }
                    }
                }


                for (int i = begin; i < begin + count && i<segCount; i++)
                {
                    //P[i]～P[i+1]区間
                    for (int j = 0; j < plotPerSegment; j++)
                    {
                        var t = (float)j / plotPerSegment;

                        var (h1, h2) = GetBlendCoefficients(t, data.Phis[i], data.Psis[i]);

                        if(Scheme != BlendingScheme.Hyperbolic_Extended)
                        {
                            if (i == 0)
                                (h1, h2) = (0, 1);
                            if (i == segCount - 1)
                                (h1, h2) = (1, 0);
                        }


                        data.Plots[plotPerSegment * i + j]
                            = h1 * data.Polygons[i][plotPerSegment + j]
                            + h2 * data.Polygons[i + 1][j];
                    }
                }
                data.Plots[data.ValidPlotLength - 1] = data.ControlPoints[data.ValidCPCount - 1];
            }
        }

        static (float b1, float b2) GetBlendCoefficients(float t, float phi, float psi)
        {
            float b1 = 0;
            float b2 = 0;
            switch (Scheme)
            {
                case BlendingScheme.Trigonometric:
                    {
                        var theta = t * Mathf.PI / 2;
                        var ct = Mathf.Cos(theta);
                        b1 = ct * ct;
                        b2 = 1 - b1;

                        break;
                    }
                case BlendingScheme.Hyperbolic_Default:
                    {
                        var ephi = Mathf.Pow(2.718281828f, phi);
                        var sigma = 2 / (1 + ephi);
                        var delta = -0.5f * Math.Log(ephi - Math.Sqrt(ephi + 1) * Math.Sqrt(ephi - 1));
                        var t2 = t / (Mathf.Pow(2.718281828f, -psi) * (1 - t) + t);
                        var t3 = delta * (2 * t2 - 1);
                        var ht = Math.Tanh(t3) - sigma * t3;
                        var hA = Math.Tanh(delta) - sigma * delta;
                        b1 = (float)(1 - ht / hA) / 2;
                        b2 = 1 - b1;
                        break;
                    }
                case BlendingScheme.Hyperbolic_Lame:
                    {
                        var ephi = Mathf.Pow(2.718281828f, phi);
                        var sigma = 2 / (1 + ephi);
                        var delta = -0.5f * Math.Log(ephi - Math.Sqrt(ephi + 1) * Math.Sqrt(ephi - 1));
                        var t2 = Mathf.Pow(1 - Mathf.Pow(1 - t, psi), 1 / psi);
                        var t3 = delta * (2 * t2 - 1);
                        var ht = Math.Tanh(t3) - sigma * t3;
                        var hA = Math.Tanh(delta) - sigma * delta;
                        b1 = (float)(1 - ht / hA) / 2;
                        b2 = 1 - b1;
                        break;
                    }
                case BlendingScheme.Hyperbolic_Extended:
                    {
                        var ephi = Mathf.Pow(2.718281828f, phi);
                        var sigma = 2 / (1 + ephi);
                        var delta = -0.5f * Math.Log(ephi - Math.Sqrt(ephi + 1) * Math.Sqrt(ephi - 1));
                        var t2 = psi <= -PsiInfinity ? 0
                            : psi >= PsiInfinity ? 1
                            : t / (Mathf.Pow(2.718281828f, -psi) * (1 - t) + t);
                        var t3 = delta * (2 * t2 - 1);
                        var ht = Math.Tanh(t3) - sigma * t3;
                        var hA = Math.Tanh(delta) - sigma * delta;
                        b1 = (float)(1 - ht / hA) / 2;
                        b2 = 1 - b1;
                        break;
                    }
            }
            return (b1, b2);
        } 
    }
}
