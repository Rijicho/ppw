using UnityEngine;
using System;
using RUtil.Mathematics;

namespace RUtil.Curve
{
    public static class BezierCurve
    {
        public static Vector3 PlotBezier(Vector3 p, Vector3 q, Vector3 r, float t)
        {
            return (1 - t) * (1 - t) * p + 2 * t * (1 - t) * q + t * t * r;
        }

        public static Vector3 PlotRationalBezier(Vector3 p, Vector3 q, Vector3 r, float w, float t)
        {
            if (w == 1)
                return PlotBezier(p, q, r, t);

            return ((1 - t) * (1 - t) * p + 2 * w * t * (1 - t) * q + t * t * r)
                            / ((1 - t) * (1 - t) + 2 * w * t * (1 - t) + t * t);
        }


        public enum CalcQMethod
        {
            Analytical_Yan,
            Analytical_Rijicho,
            Approximated,
            Newton,
        }

        public enum CalcQNewtonInit
        {
            Heulistic1,
            Heulistic2,
            Heulistic3,
            Slope1,
            Slope2,
        }

        public static (Vector3 Q, float t) CalcQFromMaxCurvaturePoint(Vector3 P, Vector3 L, Vector3 R, float w, CalcQMethod solver, float optimalEpsilon = 0.0001f)
        {
            return CalcQFromMaxCurvaturePoint(P, L, R, w, solver, CalcQNewtonInit.Heulistic1, optimalEpsilon);
        }


        public static (Vector3 Q, float t) CalcQFromMaxCurvaturePoint(Vector3 P, Vector3 L, Vector3 R, float w, CalcQNewtonInit newtonInit, float optimalEpsilon = 0.0001f)
        {
            return CalcQFromMaxCurvaturePoint(P, L, R, w, CalcQMethod.Newton, newtonInit, optimalEpsilon);
        }

        public static (Vector3 Q, float t) CalcQFromMaxCurvaturePoint(Vector3 P, Vector3 L, Vector3 R, float w, CalcQMethod solver, CalcQNewtonInit newtonInit, float optimalEpsilon = 0.0001f)
        {
            //セグメントが潰れている場合は不定解なので0.5とする。
            //セグメントの端にユーザ制御点がある場合も特別扱い。
            if (MathUtil.Approx((P - R).sqrMagnitude, 0, optimalEpsilon)) return (P, 0.5f);
            if (MathUtil.Approx((L - P).sqrMagnitude, 0, optimalEpsilon)) return (L, 0);
            if (MathUtil.Approx((L - R).sqrMagnitude, 0, optimalEpsilon)) return (L, 1);



            var p = P - L;
            var r = R - L;
            var (mp, mr) = ((double)p.magnitude, (double)r.magnitude);
            var pr = (double)Vector3.Dot(p, r);
            var (smp, smr) = ((double)p.sqrMagnitude, (double)r.sqrMagnitude);
            var (smppr, smpmr) = ((double)(p + r).sqrMagnitude, (double)(p - r).sqrMagnitude);
            var ww = w * w;

            (Vector3 q, float t) AB2QT(double _alpha, double _beta)
            {
                var l0 = (float)(_alpha + _beta) / 2;
                var l2 = (float)(_alpha - _beta) / 2;
                var l1 = 1 - (float)_alpha;
                var _q = (L - l0 * P - l2 * R) / l1;
                var sqrt = l0 * l2 < 0 ? 0 : Math.Sqrt(l0 * l2);
                var _t = (float)((2 * w * l2 + l1) / (2 * w * (_alpha + 2 * sqrt)));
                return (_q, _t);
            }

            //特殊解（ΛがPRの垂直二等分線上）
            if (MathUtil.Approx((float)smp, (float)smr, optimalEpsilon))
            {
                return AB2QT(1 / (1 + w), 0);
            }

            //特殊解（ΛがPR上）
            if (MathUtil.Approx(Mathf.Abs((float)pr), (float)mp * (float)mr, optimalEpsilon))
            {
                var M = (P + R) / 2;
                var sign = Math.Sign(mr - mp);
                var k = sign * (P - M).magnitude / (L - M).magnitude;
                if (pr < 0)
                {
                    var beta = sign / Math.Sqrt(1 - ww * (1 - k * k));
                    var alpha = (beta * (beta + k)) / (1 + k * beta);
                    return AB2QT(alpha, beta);
                }
                else
                {
                    var dby = 1 + w * Math.Sqrt(1 - k * k);
                    return AB2QT(1 / dby, k / dby);
                }
            }


            switch (solver)
            {
                case CalcQMethod.Analytical_Yan:
                    {
                        var A = Vector3.Dot(P - L, P - L);
                        var B = Vector3.Dot(P - L, R - L);
                        var C = Vector3.Dot(R - L, R - L);
                        var a = C * (w - 1) + A * (1 - w);
                        var b = A * (4 * w - 3) - 2 * B + C;
                        var c = 3 * (A * (1 - 2 * w) + B);
                        var d = A * (4 * w - 1) - B;
                        var e = -w * A;
                        float ti;
                        if (MathUtil.Approx(a, 0, optimalEpsilon))
                        {
                            ti = (float)EquationSolver.SolveCubicEquationRealIn01(b, c, d, e);
                        }
                        else
                        {
                            var (t1, t2, t3, t4) = EquationSolver.SolveQuarticEquation(a, b, c, d, e);
                            if (t1.IsReal && 0 <= t1.a && t1.a <= 1) ti = (float)t1.a;
                            else if (t2.IsReal && 0 <= t2.a && t2.a <= 1) ti = (float)t2.a;
                            else if (t3.IsReal && 0 <= t3.a && t3.a <= 1) ti = (float)t3.a;
                            else if (t4.IsReal && 0 <= t4.a && t4.a <= 1) ti = (float)t4.a;
                            else { ti = 0.5f; Debug.LogError($"{t1},   {t2},   {t3},   {t4}\na={a}, b={b}, c={c}, d={d}, e={e}"); }
                        }
                        Vector3 qi = (((1 - ti) * (1 - ti) + 2 * (1 - ti) * ti * w + ti * ti) * L - (1 - ti) * (1 - ti) * P - ti * ti * R) / (2 * (1 - ti) * ti * w);
                        return (qi, ti);
                    }
                case CalcQMethod.Analytical_Rijicho:
                    {
                        var (alpha, beta) = InitializeNewtonForCalcQAnalytical(P, L, R, w, mp, mr);
                        return AB2QT(alpha, beta);
                    }
                case CalcQMethod.Approximated:
                    {
                        var (alpha, beta) = ApproximateAlphaBeta(mp, mr, smp, smr, smppr, smpmr, w);
                        return AB2QT(alpha, beta);
                    }
                case CalcQMethod.Newton:
                    {
                        var (alpha, beta, isOptimal) = InitializeNewtonForCalcQ(newtonInit, P, L, R, w, mp, mr, smp, smr, smppr, smpmr, pr, false);

                        if (!isOptimal)
                        {

                            double f(double _a, double _b) => (1 - ww) * _a * _a - 2 * _a + 1 + ww * _b * _b;
                            double g(double _a, double _b) =>
                                smppr * _b * _b * _b
                                - (smp - smr) * (1 - 2 * _a) * _b * _b
                                + (smpmr * _a - 2 * (smp + smr)) * _a * _b
                                - (smp - smr) * _a * _a;
                            double fda(double _a) => 2 * (1 - ww) * _a - 2;
                            double fdb(double _b) => 2 * ww * _b;
                            double gda(double _a, double _b) => 2 * (smp - smr) * _b * _b + 2 * _b * smpmr * _a - 2 * _b * (smp + smr) - 2 * (smp - smr) * _a;
                            double gdb(double _a, double _b) => 3 * smppr * _b * _b - 2 * (smp - smr) * (1 - 2 * _a) * _b + (smpmr * _a - 2 * (smp + smr)) * _a;


                            for (int j = 0; j < 300; j++)
                            {
                                var rf = f(alpha, beta);
                                var rg = g(alpha, beta);
                                if (Math.Abs(rf) < RKCurves.NewtonEpsilon && Math.Abs(rg) < RKCurves.NewtonEpsilon)
                                    break;
                                var rfda = fda(alpha);
                                var rfdb = fdb(beta);
                                var rgda = gda(alpha, beta);
                                var rgdb = gdb(alpha, beta);
                                var dby = rfda * rgdb - rfdb * rgda;
                                alpha -= (rgdb * rf - rfdb * rg) / dby;
                                beta -= (-rgda * rf + rfda * rg) / dby;
                            }
                        }
                        return AB2QT(alpha, beta);
                    }
                default:
                    return default;
            }
        }

        static (double a, double b) ApproximateAlphaBeta(double mp, double mr, double smp, double smr,double smppr, double smpmr, double w)
        {
            var smpmsmr = smp - smr;
            var smppsmr = smp + smr;
            var ww = w * w;

            //g's slope on O
            double m0 = -(mp - mr) / (mp + mr);

            //intersection X1(a1,b1) between g's tangent on O and f
            double a1 = 1 / (1 + w * Math.Sqrt(1 - m0 * m0));

            //f's slope on X1
            var m1inv = m0 * a1 * ww / (1 - (1 - ww) * a1);
            var gamma = a1 * (1 - m0 * m1inv);
            //tangent line: a=m1inv*b+gamma

            var a = smppr + 2 * m1inv * smpmsmr + m1inv * m1inv * smpmr;
            var b = smpmsmr * (2 * gamma - m1inv * m1inv - 1) + 2 * m1inv * (gamma * smpmr - smppsmr);
            var c = gamma * (gamma * smpmr - 2 * (smppsmr + m1inv * smpmsmr));
            var d = -smpmsmr * gamma * gamma;

            var b2 = EquationSolver.SolveCubicEquation1Real(a, b, c, d);
            var a2 = m1inv * b2 + gamma;

            var m2 = 2 * (smpmsmr * (a2 - b2 * b2) + b2 * (smppsmr - smpmr * a2))
                / (3 * smppr * b2 * b2 - 2 * smpmsmr * (1 - 2 * a2) * b2 + (smpmr * a2 - 2 * smppsmr) * a2);

            var A3 = 1 - ww + ww * m2 * m2;
            var mamb = m2 * a2 - b2;
            var B3 = ww * m2 * mamb + 1;
            var insqrt = B3 * B3 - A3 * (ww * mamb * mamb + 1);
            insqrt = Math.Max(0, insqrt);
            var a3 = (B3 - Math.Sqrt(insqrt)) / A3;
            var b3 = m2 * (a3 - a2) + b2;
            return (a3, b3);
        }

        public static (double a, double b, bool isOptimal) InitializeNewtonForCalcQ(CalcQNewtonInit initializationMethod,
            Vector3 P, Vector3 L, Vector3 R, double w,
            bool checkOptimal = true)
        {
            var (p, r) = (P - L, R - L);
            var (mp, mr) = (p.magnitude, r.magnitude);
            var (smp, smr) = (p.sqrMagnitude, r.sqrMagnitude);
            var (smppr, smpmr) = ((p + r).sqrMagnitude, (p - r).sqrMagnitude);
            var pr = Vector3.Dot(p, r);
            return InitializeNewtonForCalcQ(initializationMethod, p, L, r, w, mp, mr, smp, smr, smppr, smpmr, pr, checkOptimal);
        }


        public static (double a, double b, bool isOptimal) InitializeNewtonForCalcQ(CalcQNewtonInit initializationMethod, 
            Vector3 P, Vector3 L, Vector3 R, double w, double mp, double mr, double smp, double smr, double smppr, double smpmr, double pr, 
            bool checkOptimal = true)
        {
            //var (p, r) = (P - L, R - L);

            if (checkOptimal)
            {
                if (Mathf.Approximately((float)smp, 0))
                {
                    return (0, 0, true);
                }
                if (Mathf.Approximately((float)smr, 0))
                {
                    return (0, 0, true);
                }

                var ww = w * w;
                if (Mathf.Approximately((float)smp, (float)smr))
                {
                    return (1 / (1 + w), 0, true);
                }

                if (Mathf.Approximately(Mathf.Abs((float)pr), (float)mp * (float)mr))
                {
                    var M = (P + R) / 2;
                    var sign = Math.Sign(mr - mp);
                    var k = sign * (P - M).magnitude / (L - M).magnitude;
                    if (pr < 0)
                    {
                        var beta = sign / Math.Sqrt(1 - ww * (1 - k * k));
                        var alpha = (beta * (beta + k)) / (1 + k * beta);
                        return (alpha, beta, true);
                    }
                    else
                    {
                        var dby = 1 + w * Math.Sqrt(1 - k * k);
                        return (1 / dby, k / dby, true);
                    }
                }
            }
            //return (1 / (1 + w), 0, false);

            double reta = 0;
            double retb = 0;
            bool isOptimal = false;

            switch (initializationMethod)
            {
                case CalcQNewtonInit.Heulistic1:
                    (reta, retb) = GetInitialValueInternalH1();
                    break;
                case CalcQNewtonInit.Heulistic2:
                    (reta, retb) = GetInitialValueInternalH2();
                    break;
                case CalcQNewtonInit.Heulistic3:
                    (reta, retb) = GetInitialValueInternalH3();
                    break;
                case CalcQNewtonInit.Slope1:
                    (reta, retb) = GetInitialValueInternalS1();
                    break;
                case CalcQNewtonInit.Slope2:
                    (reta, retb) = GetInitialValueInternalS2();
                    break;
            }
            return (reta, retb, isOptimal);



            (double a, double b) GetInitialValueInternalH1()
            {
                if (w <= 1)
                {
                    var nowa = 1 / (1 + w);
                    var tmpb = (smp - smr) * (1 - 2 * nowa) / 3 / smppr;
                    var nowb = tmpb < -nowa ? -nowa : tmpb > nowa ? nowa : tmpb;
                    return (nowa, nowb);
                }
                else
                {
                    var ww = w * w;
                    var chord = 2 * mr / (mp + mr) - 1;
                    var nowb = 2 / (1 + Math.Pow(2.718281828f, -2 * chord / w)) - 1;
                    var nowa = 1 / (1 - ww) + Math.Sqrt(ww / (1 - ww) / (1 - ww) - ww * nowb * nowb / (1 - ww));
                    return (nowa, nowb);
                }
            }

            (double a, double b) GetInitialValueInternalH2()
            {
                double ia, ib;

                double OASlope = -(smp - smr) / smppr;
                Vector2 B;
                if (Math.Abs(OASlope) < 1)
                {
                    var Bdb = (1 + w * Math.Sqrt(1 - OASlope * OASlope));
                    B = new Vector2((float)(1 / Bdb), (float)(OASlope / Bdb));
                }
                else
                {
                    B = new Vector2(1, Math.Sign(OASlope));
                }

                double Aslope = -2 * (smp - smr) / smppr - 4 * pr * (smp - smr) / ((smp - smr) * (smp - smr) - smppr * smppr);
                double Oslope = -(mp - mr) * (mp - mr) / (smp - smr);
                var Cdb = 1 + w * Math.Sqrt(1 - Oslope * Oslope);
                var C = new Vector2((float)(1 / Cdb), (float)(Oslope / Cdb));

                var D = (B + C) / 2;

                if (w >= 1)
                {
                    (ia, ib) = (C.x, C.y);
                }
                else
                {
                    if ((mp < mr && (pr + smp) > 0f
                        || mp > mr && (pr + smr) > 0f))
                    {
                        if (Math.Abs(Aslope) > 5f)
                        {
                            (ia, ib) = (C.x, C.y);
                        }
                        else
                        {
                            (ia, ib) = (D.x, D.y);
                        }
                    }
                    else
                    {
                        if (Math.Abs(OASlope) > Math.Abs(Oslope))
                        {
                            (ia, ib) = (C.x, C.y);
                        }
                        else
                        {
                            (ia, ib) = (D.x, D.y);
                        }
                    }
                }
                return (ia, ib);
            }

            (double a, double b) GetInitialValueInternalS1()
            {
                var ww = w * w;

                var smpmsmr = smp - smr;
                var smppsmr = smp + smr;

                //g's slope on O
                double m = -(mp - mr) * (mp - mr) / (smp - smr);
                if (Math.Abs(m) > 1)
                    m = Math.Sign(m);
                //intersection C between g's tangent on O and f
                double x0 = 1 / (1 + w * Math.Sqrt(1 - m * m));

                //f's slope on C
                var m2 = -m * x0 * ww / ((1 - ww) * x0 - 1);
                //tangent line: x - fcs y - fcst=0
                var t2 = x0 * (1 - m * m2);

                var a = smppr + 2 * m2 * smpmsmr + m2 * m2 * smpmr;
                var b = smpmsmr * (-1 + 2 * t2 - m2 * m2) + 2 * m2 * (t2 * smpmr - smppsmr);
                var c = t2 * (t2 * smpmr - 2 * (smppsmr + m2 * smpmsmr));
                var d = -smpmsmr * t2 * t2;

                var (y3, _, _) = EquationSolver.SolveCubicEquation(a, b, c, d);
                var x3 = m2 * y3 + t2;
                return (x3, y3);
            }

            (double a, double b) GetInitialValueInternalS2()
            {
                return ApproximateAlphaBeta(mp, mr, smp, smr, smppr, smpmr, w);
            }


            (double a, double b) GetInitialValueInternalH3()
            {
                var ww = w * w;

                var smpmsmr = smp - smr;
                var smppsmr = smp + smr;

                //g's slope on O
                double m = -(mp - mr) * (mp - mr) / (smp - smr);

                var nowa = 1 / (1 + w);
                var nowb = m * nowa;
                return (nowa, nowb);
            }
        }


        public static (double a, double b) InitializeNewtonForCalcQAnalytical(Vector3 P, Vector3 L, Vector3 R, float w, double mp, double mr)
        {
            var w2 = w * w;
            var M = (P + R) / 2;
            var c2 = (double)(L - M).sqrMagnitude / (P - M).sqrMagnitude;
            var b = (double)Vector3.Cross(P - L, M - L).magnitude / (P - M).sqrMagnitude;
            var a2 = c2 - b * b;
            if (a2 < 0) a2 = 0;
            var a = Math.Sqrt(a2);

            if (Mathf.Approximately((float)mp, 0))
            {
                return (1, 1);
            }
            if (Mathf.Approximately((float)mr, 0))
            {
                return (1, -1);
            }
            if (Mathf.Approximately((float)mp, (float)mr) || a2 == 0)
            {
                return (1 / (1 + w), 0);
            }

            var sigma = Math.Sign((R - L).sqrMagnitude - (P - L).sqrMagnitude);

            if (Mathf.Approximately((float)b, 0))
            {
                if (Math.Sqrt(a2) < 1)
                {
                    var rho = sigma * (P - M).magnitude / (L - M).magnitude;
                    var _beta = sigma / Math.Sqrt(1 - w2 * (1 - rho * rho));
                    var _alpha = (_beta * (_beta + rho)) / (1 + rho * _beta);
                    return (_alpha, _beta);
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
                float e = 0.000001f; //小さくしすぎるとNaN発生、大きくしすぎると別の解へ飛ぶ。ちゃんとやるには４次の判別式が必要そう
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

    }
}
