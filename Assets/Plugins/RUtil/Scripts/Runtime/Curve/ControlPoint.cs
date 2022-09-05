using System;
using UnityEngine;

namespace RUtil.Curve
{
    [Serializable]
    public struct ControlPoint : IEquatable<ControlPoint>
    {
        public Vector3 Position;
        public float TargetWeight;
        public float ModifiedWeight;

        public float x => Position.x;
        public float y => Position.y;
        public float z => Position.z;
        public float w => TargetWeight;

        public ControlPoint(Vector3 pos, float targetWeight, float modifiedWeight)
        {
            Position = pos;
            TargetWeight = targetWeight;
            ModifiedWeight = modifiedWeight;
        }

        public ControlPoint(float x, float y, float z = 0, float weight = 1)
        {
            Position = new Vector3(x, y, z);
            TargetWeight = weight;
            ModifiedWeight = TargetWeight;
        }

        public ControlPoint(Vector2 pos, float weight = 1)
        {
            Position = pos;
            TargetWeight = weight;
            ModifiedWeight = TargetWeight;
        }

        public ControlPoint(Vector3 pos, float weight = 1)
        {
            Position = pos;
            TargetWeight = weight;
            ModifiedWeight = TargetWeight;
        }

        public ControlPoint(Vector4 pos)
        {
            Position = new Vector3(pos.x, pos.y, pos.z);
            TargetWeight = pos.w;
            ModifiedWeight = TargetWeight;
        }

        public float this[int i] => 0 <= i && i < 3 ? Position[i] : TargetWeight;


        public static ControlPoint operator +(ControlPoint lhs, ControlPoint rhs)
            => new ControlPoint(lhs.Position + rhs.Position, lhs.TargetWeight, lhs.ModifiedWeight);
        public static ControlPoint operator -(ControlPoint lhs, ControlPoint rhs)
            => new ControlPoint(lhs.Position - rhs.Position, lhs.TargetWeight, lhs.ModifiedWeight);
        public static ControlPoint operator -(ControlPoint lhs)
            => new ControlPoint(-lhs.Position, lhs.TargetWeight, lhs.ModifiedWeight);
        public static ControlPoint operator *(float lhs, ControlPoint rhs)
            => new ControlPoint(lhs * rhs.Position, rhs.TargetWeight, rhs.ModifiedWeight);
        public static ControlPoint operator *(ControlPoint lhs, float rhs)
            => new ControlPoint(lhs.Position * rhs, lhs.TargetWeight, lhs.ModifiedWeight);
        public static ControlPoint operator /(ControlPoint lhs, float rhs)
            => new ControlPoint(lhs.Position / rhs, lhs.TargetWeight, lhs.ModifiedWeight);

        public static implicit operator ControlPoint(Vector3 pos) => new ControlPoint(pos);
        public static implicit operator Vector3(ControlPoint pos) => pos.Position;

        public static ControlPoint zero => new ControlPoint(Vector3.zero);
        public static ControlPoint one => new ControlPoint(Vector3.one);

        public ControlPoint normalized => new ControlPoint(Position.normalized, TargetWeight);
        public float magnitude => Position.magnitude;
        public float sqrMagnitude => Position.sqrMagnitude;

        public void SetPosition(Vector3 pos) => Position = pos;
        public void SetWeight(float weight) => TargetWeight = weight;

        public static bool operator ==(ControlPoint lhs, ControlPoint rhs) => lhs.Equals(rhs);
        public static bool operator !=(ControlPoint lhs, ControlPoint rhs) => !lhs.Equals(rhs);
        public override int GetHashCode() => Position.GetHashCode() ^ TargetWeight.GetHashCode();
        public override bool Equals(object obj) => obj is ControlPoint cp ? Equals(cp) : false;
        public bool Equals(ControlPoint other) => Position == other.Position && TargetWeight == other.TargetWeight;
        public override string ToString() => $"({Position.x:#.####}, {Position.y:#.####}, {Position.z:#.####}; {TargetWeight:#.####})";
    }
}
