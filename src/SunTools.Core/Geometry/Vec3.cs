using System;

namespace SunTools.Core.Geometry;

/// <summary>Immutable 3D vector: metres in world space, normalised units in a window frame.</summary>
public readonly struct Vec3
{
    public Vec3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double k) => new Vec3(a.X * k, a.Y * k, a.Z * k);
    public static Vec3 operator *(double k, Vec3 a) => a * k;
    public static Vec3 operator /(Vec3 a, double k) => new Vec3(a.X / k, a.Y / k, a.Z / k);

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;

    public Vec3 Cross(Vec3 b) => new Vec3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

    public double Length => Math.Sqrt(Dot(this));

    public bool IsFinite => IsFiniteNumber(X) && IsFiniteNumber(Y) && IsFiniteNumber(Z);

    /// <summary>Unit vector with the same direction.</summary>
    /// <exception cref="InvalidOperationException">The vector is zero or not finite.</exception>
    public Vec3 Normalized()
    {
        double length = Length;
        if (!(length > 0) || double.IsInfinity(length))
            throw new InvalidOperationException("Cannot normalise a zero or non-finite vector.");
        return this / length;
    }

    public override string ToString() => $"({X}, {Y}, {Z})";

    internal static bool IsFiniteNumber(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
}
