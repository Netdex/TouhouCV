using System;
using System.Drawing;

namespace TouhouCV
{
    public struct Vec2
    {
        public static Vec2 ZERO = new Vec2(0, 0);

        public double X { get; private set; }
        public double Y { get; private set; }

        public Vec2(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public Vec2(Vec2 vec)
        {
            this.X = vec.X;
            this.Y = vec.Y;
        }

        public double Length()
        {
            return Math.Sqrt(LengthSq());
        }

        public double LengthSq()
        {
            return X * X + Y * Y;
        }

        public static Vec2 operator +(Vec2 a, Vec2 b)
        {
            a.X += b.X;
            a.Y += b.Y;
            return a;
        }

        public static Vec2 operator -(Vec2 a, Vec2 b)
        {
            a.X -= b.X;
            a.Y -= b.Y;
            return a;
        }

        public static Vec2 operator *(Vec2 a, double b)
        {
            a.X *= b;
            a.Y *= b;
            return a;
        }

        public static Vec2 operator *(double b, Vec2 a)
        {
            a.X *= b;
            a.Y *= b;
            return a;
        }

        public static Vec2 operator /(Vec2 a, double b)
        {
            a.X /= b;
            a.Y /= b;
            return a;
        }

        public static Vec2 operator /(double b, Vec2 a)
        {
            a.X /= b;
            a.Y /= b;
            return a;
        }

        public static bool operator ==(Vec2 a, Vec2 b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Vec2 a, Vec2 b)
        {
            return !a.Equals(b);
        }

        public bool Equals(Vec2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Vec2 && Equals((Vec2)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static double Dot(Vec2 a, Vec2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        public static double Cross(Vec2 a, Vec2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        public static Vec2 Cross(Vec2 a, double s)
        {
            a.X *= -s;
            a.Y *= s;
            return a;
        }

        public static Vec2 Cross(double s, Vec2 a)
        {
            a.X *= s;
            a.Y *= -s;
            return a;
        }

        public static Vec2 CalculateForce(Vec2 a, Vec2 b, double coulomb)
        {
            Vec2 acc = a - b;
            acc /= acc.Length();
            acc *= coulomb / (DistanceSq(a, b));
            return acc;
        }

        public static double CalculateForce(double dist, double coulomb)
        {
            return coulomb / (dist * dist);
        }

        public static double DistanceSq(Vec2 a, Vec2 b)
        {
            return (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        }
        public static double Distance(Vec2 a, Vec2 b)
        {
            return Math.Sqrt(DistanceSq(a, b));
        }
        public override string ToString()
        {
            return $"<{X}, {Y}>";
        }
    }
}
