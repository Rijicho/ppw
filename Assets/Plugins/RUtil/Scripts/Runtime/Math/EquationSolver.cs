using System;
using UnityEngine;

namespace RUtil.Mathematics
{
    public static class MathUtil
    {
        public static float Cbrt(float value) => Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), 1f / 3);
        public static double Cbrt(double value) => Math.Sign(value) * Math.Pow(Math.Abs(value), 1.0 / 3);

        public static bool Approx(float a, float b, float epsilon) => Mathf.Abs(a - b) < epsilon;
        public static bool Approx(double a, double b, double epsilon) => Math.Abs(a - b) < epsilon;
    }

    public static class EquationSolver
    {

        public static (DComplex x1, DComplex x2, DComplex x3, DComplex x4) SolveQuarticEquation(double a, double b, double c, double d, double e)
        {
            b /= a * 4;
            c /= a;
            d /= a;
            e /= a;

            //y^4 + py^2 + qy + r = 0,  x=y-b
            var p = c - 6 * b * b;
            var q = d + b * (8 * b * b - 2 * c);
            var r = e + b * (b * (-3 * b * b + c) - d);

            //resolvent: x^3 + fx^2 + gx + h = 0
            var f = p / 2;
            var g = (p * p - 4 * r) / 16;
            var h = -q * q / 64;

            //判別式
            /*
            var rD = -4 * g * g * g
                + g * g * f * f
                - 4 * h * f * f * f
                + 18 * f * g * h
                - 27 * h * h;
            */

#if KCONICS_DEBUG
            if(double.IsNaN(a2) || double.IsNaN(a1) || double.IsNaN(a0))
            {
                Debug.Log((a, b, c, d, e, f, g, h));
            }
#endif            
            //Solve resolvent: x^3+fx^2+gx+h=0
            var (y1, y2, y3) = SolveCubicEquation(1, f, g, h);


            var s = DComplex.Sqrt(y2);
            var t = DComplex.Sqrt(y3);
            var u = -q / (8 * s * t);

            var x1 = s + t + u - b;
            var x2 = s - t - u - b;
            var x3 = -s + t - u - b;
            var x4 = -s - t + u - b;

            return (x1, x2, x3, x4);
        }


        public static (double x1, DComplex x2, DComplex x3) SolveCubicEquation(double a, double b, double c, double d)
        {
            const double r3 = 1.73205080756887729352744634150587237;

            b /= a;
            c /= a;
            d /= a;

            b /= 3;

            var p = c / 3 - b * b;
            var q = b * b * b - (b * c - d) / 2;
            var D = q * q + p * p * p; // 判別式

            if (D == 0)
            {
                var ret = MathUtil.Cbrt(q) - b;
                var x0 = -2 * ret;
                var x1 = ret;
                var x2 = ret;

                return (x0, x1, x2);
            }
            else if (D > 0)
            {
                var rD = Math.Sqrt(D);
                var u = MathUtil.Cbrt(-q + rD);
                var v = MathUtil.Cbrt(-q - rD);
                var x0 = u + v - b;
                var x1 = new DComplex(-u - v, r3 * (u - v)) / 2 - b;
                var x2 = new DComplex(-u - v, -r3 * (u - v)) / 2 - b;
                return (x0, x1, x2);
            }
            else //D < 0
            {
                var tmp = 2 * Math.Sqrt(-p);
                var arg = Math.Atan2(Math.Sqrt(-D), -q) / 3;
                var theta = 2 * Math.PI / 3;
                var x0 = tmp * Math.Cos(arg - theta) - b;
                var x1 = tmp * Math.Cos(arg + theta) - b;
                var x2 = tmp * Math.Cos(arg) - b;
                return (x0, x1, x2);
            }
        }


        public static double SolveCubicEquation1Real(double a, double b, double c, double d)
        {
            const double r3 = 1.73205080756887729352744634150587237;

            b /= a;
            c /= a;
            d /= a;

            b /= 3;

            var p = c / 3 - b * b;
            var q = b * b * b - (b * c - d) / 2;
            var D = q * q + p * p * p; // 判別式

            if (MathUtil.Approx(D,0,1.0E-12))
            {
                return -2 * (MathUtil.Cbrt(q) - b);
            }
            else if (D > 0)
            {
                var rD = Math.Sqrt(D);
                var u = MathUtil.Cbrt(-q + rD);
                var v = MathUtil.Cbrt(-q - rD);
                return u + v - b;
            }
            else //D < 0
            {
                var tmp = 2 * Math.Sqrt(-p);
                var arg = Math.Atan2(Math.Sqrt(-D), -q) / 3;
                var theta = 2 * Math.PI / 3;
                return tmp * Math.Cos(arg - theta) - b;
            }
        }

        public static double SolveCubicEquationRealIn01(double a, double b, double c, double d)
        {
            b /= a * 3;
            c /= a;
            d /= a;

            var p = c / 3 - b * b;
            var q = b * b * b - (b * c - d) / 2;
            var D = q * q + p * p * p;

            if (MathUtil.Approx(D, 0, 1.0E-12))
            {
                var ret = MathUtil.Cbrt(q) - b;

                if (ret >= 0)
                    return Math.Min(ret, 1);
                else
                    return Math.Min(ret * -2,1);
            }
            else if (D > 0)
            {
                var sqrtD = Math.Sqrt(D);
                var u = MathUtil.Cbrt(-q + sqrtD);
                var v = MathUtil.Cbrt(-q - sqrtD);
                var ret = u + v - b;

                return  ret < 0 ? 0 : ret > 1 ? 1 : ret;
            }
            else //D < 0
            {
                var tmp = 2 * Math.Sqrt(-p);
                var arg = Math.Atan2(Math.Sqrt(-D), -q) / 3;
                const double pi2d3 = 2 * Math.PI / 3;
                var ret1 = tmp * Math.Cos(arg) - b;
                if (0 <= ret1 && ret1 <= 1) return ret1;

                var ret2 = tmp * Math.Cos(arg + pi2d3) - b;
                if (0 <= ret2 && ret2 <= 1) return ret2;

                var ret3 = tmp * Math.Cos(arg - pi2d3) - b;
                if (0 <= ret3 && ret3 <= 1) return ret3;

                throw new Exception($"Invalid solution: {ret1}, {ret2}, {ret3}");
            }
        }
    }
}


