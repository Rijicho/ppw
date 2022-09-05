using System;
using UnityEngine;

namespace RUtil.Mathematics
{
    public struct Complex : IEquatable<Complex>
    {
        public float a;
        public float b;
        public Complex(float a, float b) => (this.a, this.b) = (a, b);
        public (float r, float theta) Euler => (Mathf.Sqrt(a * a + b * b), Mathf.Atan2(b, a));
        public Complex Conj => new Complex(a, -b);
        public float sqrMagnitude => a * a + b * b;
        public float magnitude => Mathf.Sqrt(a * a + b * b);

        public static implicit operator Complex(float src) => new Complex(src, 0);
        public static Complex operator +(Complex lhs, Complex rhs) => new Complex(lhs.a + rhs.a, lhs.b + rhs.b);
        public static Complex operator -(Complex lhs, Complex rhs) => new Complex(lhs.a - rhs.a, lhs.b - rhs.b);
        public static Complex operator *(Complex lhs, float rhs) => new Complex(lhs.a * rhs, lhs.b * rhs);
        public static Complex operator *(Complex lhs, Complex rhs) => new Complex(lhs.a * rhs.a - lhs.b * rhs.b, lhs.a * rhs.b + lhs.b * rhs.a);
        public static Complex operator /(Complex lhs, float rhs) => new Complex(lhs.a / rhs, lhs.b / rhs);
        public static Complex operator /(Complex lhs, Complex rhs) => lhs * rhs.Conj / rhs.sqrMagnitude;
        public static bool operator ==(Complex lhs, Complex rhs)
        {
            return lhs.a == rhs.a && lhs.b == rhs.b;
        }
        public static bool operator !=(Complex lhs, Complex rhs)
        {
            return lhs.a != rhs.a || lhs.b != rhs.b;
        }
        public bool Equals(Complex other) => a == other.a && b == other.b;
        public override bool Equals(object obj) => obj is Complex v ? Equals(v) : false;
        public override int GetHashCode() => a.GetHashCode() ^ b.GetHashCode();
        public override string ToString()
        {
            if (b < 0)
                return $"{a}{b}i";
            else
                return $"{a}+{b}i";
        }

        public static Complex Sqrt(Complex value)
        {
            if (value.b == 0)
            {
                if (value.a == 0)
                    return 0;
                if (value.a > 0)
                    return Mathf.Sqrt(value.a);

                return new Complex(0, Mathf.Sqrt(-value.a));
            }
            else
            {
                var m = value.magnitude;
                if (value.b > 0)
                {
                    return new Complex(Mathf.Sqrt((value.a + m) / 2), Mathf.Sqrt((-value.a + m) / 2));
                }
                else
                {
                    return new Complex(Mathf.Sqrt((value.a + m) / 2), -Mathf.Sqrt((-value.a + m) / 2));
                }
            }
        }
    }

    public struct DComplex : IEquatable<DComplex>
    {
        public double a;
        public double b;
        public DComplex(double a, double b) => (this.a, this.b) = (a, b);
        public (double r, double theta) Euler => (Math.Sqrt(a * a + b * b), Math.Atan2(b, a));
        public DComplex Conj => new DComplex(a, -b);
        public double sqrMagnitude => a * a + b * b;
        public double magnitude => Math.Sqrt(a * a + b * b);

        public static implicit operator DComplex(double src) => new DComplex(src, 0);
        public static DComplex operator +(DComplex lhs, DComplex rhs) => new DComplex(lhs.a + rhs.a, lhs.b + rhs.b);
        public static DComplex operator -(DComplex lhs, DComplex rhs) => new DComplex(lhs.a - rhs.a, lhs.b - rhs.b);
        public static DComplex operator -(DComplex value) => new DComplex(-value.a, -value.b);
        public static DComplex operator *(DComplex lhs, double rhs) => new DComplex(lhs.a * rhs, lhs.b * rhs);
        public static DComplex operator *(DComplex lhs, DComplex rhs) => new DComplex(lhs.a * rhs.a - lhs.b * rhs.b, lhs.a * rhs.b + lhs.b * rhs.a);
        public static DComplex operator /(DComplex lhs, double rhs) => new DComplex(lhs.a / rhs, lhs.b / rhs);
        public static DComplex operator /(DComplex lhs, DComplex rhs) => lhs * rhs.Conj / rhs.sqrMagnitude;
        public static bool operator ==(DComplex lhs, DComplex rhs)
        {
            return lhs.a == rhs.a && lhs.b == rhs.b;
        }
        public static bool operator !=(DComplex lhs, DComplex rhs)
        {
            return lhs.a != rhs.a || lhs.b != rhs.b;
        }
        public bool Equals(DComplex other) => a == other.a && b == other.b;
        public override bool Equals(object obj) => obj is DComplex v ? Equals(v) : false;
        public override int GetHashCode() => a.GetHashCode() ^ b.GetHashCode();
        public override string ToString()
        {
            if (b < 0)
                return $"{a}{b}i";
            else
                return $"{a}+{b}i";
        }

        public static DComplex Sqrt(DComplex value)
        {
            if (value.b == 0)
            {
                if (value.a == 0)
                    return 0;
                if (value.a > 0)
                    return Math.Sqrt(value.a);

                return new DComplex(0, Math.Sqrt(-value.a));
            }
            else
            {
                var m = value.magnitude;
                if (value.b > 0)
                {
                    return new DComplex(Math.Sqrt((value.a + m) / 2), Math.Sqrt((-value.a + m) / 2));
                }
                else
                {
                    return new DComplex(Math.Sqrt((value.a + m) / 2), -Math.Sqrt((-value.a + m) / 2));
                }
            }
        }

        public bool IsReal => Math.Abs(b) <= Mathf.Epsilon;
    }
}


