using System;

namespace GeneratorBebnaKrzywkowego.Core
{
    public struct Wektor3
    {
        public double X;
        public double Y;
        public double Z;

        public Wektor3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Dlugosc => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Wektor3 Znormalizowany()
        {
            double d = Dlugosc;
            if (d < 1e-14)
                throw new InvalidOperationException("Nie można normalizować wektora zerowego.");
            return this / d;
        }

        public static double IloczynSkalarny(Wektor3 a, Wektor3 b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Wektor3 IloczynWektorowy(Wektor3 a, Wektor3 b) =>
            new Wektor3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);

        public static Wektor3 operator +(Wektor3 a, Wektor3 b) =>
            new Wektor3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Wektor3 operator -(Wektor3 a, Wektor3 b) =>
            new Wektor3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Wektor3 operator -(Wektor3 a) =>
            new Wektor3(-a.X, -a.Y, -a.Z);

        public static Wektor3 operator *(Wektor3 a, double s) =>
            new Wektor3(a.X * s, a.Y * s, a.Z * s);

        public static Wektor3 operator *(double s, Wektor3 a) => a * s;

        public static Wektor3 operator /(Wektor3 a, double s) =>
            new Wektor3(a.X / s, a.Y / s, a.Z / s);

        public override string ToString() =>
            $"({X:0.######}, {Y:0.######}, {Z:0.######})";
    }

    public static class Jednostki
    {
        public const double MmNaM = 0.001;
        public const double StopnieNaRadiany = Math.PI / 180.0;

        public static double MmDoM(double mm) => mm * MmNaM;
        public static double StopnieDoRadianow(double stopnie) => stopnie * StopnieNaRadiany;
    }
}
