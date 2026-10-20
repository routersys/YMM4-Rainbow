using System.Numerics;

namespace Rainbow.Tests;

internal static class MieScattering
{
    public static (double Perpendicular, double Parallel)[] Intensities(double index, double sizeParameter, IReadOnlyList<double> anglesRadians)
    {
        var x = sizeParameter;
        var terms = (int)Math.Floor(x + 4.0 * Math.Cbrt(x) + 2.0);
        var start = (int)Math.Max(terms, index * x) + 15;
        var logarithmic = new double[start + 1];
        for (var n = start; n >= 1; n--)
            logarithmic[n - 1] = n / (index * x) - 1.0 / (logarithmic[n] + n / (index * x));

        var a = new Complex[terms + 1];
        var b = new Complex[terms + 1];
        var psiPrevious = Math.Cos(x);
        var psiCurrent = Math.Sin(x);
        var chiPrevious = -Math.Sin(x);
        var chiCurrent = Math.Cos(x);
        var xiCurrent = new Complex(psiCurrent, -chiCurrent);
        for (var n = 1; n <= terms; n++)
        {
            var psi = (2.0 * n - 1.0) / x * psiCurrent - psiPrevious;
            var chi = (2.0 * n - 1.0) / x * chiCurrent - chiPrevious;
            var xi = new Complex(psi, -chi);
            var d = logarithmic[n];
            var numeratorA = (d / index + n / x) * psi - psiCurrent;
            var denominatorA = (d / index + n / x) * xi - xiCurrent;
            a[n] = numeratorA / denominatorA;
            var numeratorB = (d * index + n / x) * psi - psiCurrent;
            var denominatorB = (d * index + n / x) * xi - xiCurrent;
            b[n] = numeratorB / denominatorB;
            psiPrevious = psiCurrent;
            psiCurrent = psi;
            chiPrevious = chiCurrent;
            chiCurrent = chi;
            xiCurrent = xi;
        }

        var results = new (double, double)[anglesRadians.Count];
        for (var index2 = 0; index2 < anglesRadians.Count; index2++)
        {
            var mu = Math.Cos(anglesRadians[index2]);
            var pi0 = 0.0;
            var pi1 = 1.0;
            Complex s1 = Complex.Zero;
            Complex s2 = Complex.Zero;
            for (var n = 1; n <= terms; n++)
            {
                var tau = n * mu * pi1 - (n + 1.0) * pi0;
                var factor = (2.0 * n + 1.0) / (n * (n + 1.0));
                s1 += factor * (a[n] * pi1 + b[n] * tau);
                s2 += factor * (a[n] * tau + b[n] * pi1);
                var next = ((2.0 * n + 1.0) * mu * pi1 - (n + 1.0) * pi0) / n;
                pi0 = pi1;
                pi1 = next;
            }

            results[index2] = (s1.Magnitude * s1.Magnitude, s2.Magnitude * s2.Magnitude);
        }

        return results;
    }

    public static double[] Unpolarized(double index, double sizeParameter, IReadOnlyList<double> anglesRadians)
    {
        var intensities = Intensities(index, sizeParameter, anglesRadians);
        return intensities.Select(pair => 0.5 * (pair.Perpendicular + pair.Parallel) / (sizeParameter * sizeParameter)).ToArray();
    }
}
