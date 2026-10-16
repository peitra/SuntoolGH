using System;
using SunTools.Core.Geometry;

namespace SunTools.Core.Tests.Randomized;

/// <summary>Rotation about a unit axis (Rodrigues) followed by a translation.</summary>
internal readonly struct RigidTransform
{
    private readonly Vec3 _axis;
    private readonly double _angle;
    private readonly Vec3 _translation;

    public RigidTransform(Vec3 axis, double angle, Vec3 translation)
    {
        _axis = axis.Normalized();
        _angle = angle;
        _translation = translation;
    }

    public Vec3 Rotate(Vec3 v)
    {
        double c = Math.Cos(_angle), s = Math.Sin(_angle);
        return v * c + _axis.Cross(v) * s + _axis * (_axis.Dot(v) * (1 - c));
    }

    public Vec3 Apply(Vec3 p) => Rotate(p) + _translation;

    public static RigidTransform Random(System.Random rng, double maxTranslation)
    {
        Vec3 axis;
        do
        {
            axis = new Vec3(2 * rng.NextDouble() - 1, 2 * rng.NextDouble() - 1, 2 * rng.NextDouble() - 1);
        } while (axis.Length < 0.1);
        double T() => (2 * rng.NextDouble() - 1) * maxTranslation;
        return new RigidTransform(axis, 2 * Math.PI * rng.NextDouble(), new Vec3(T(), T(), T()));
    }
}
