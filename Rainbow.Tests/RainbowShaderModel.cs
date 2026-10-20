using System.Numerics;

namespace Rainbow.Tests;

internal static class RainbowShaderModel
{
    const float DegreesPerRadian = 57.29577951308232f;

    static float ToLinear(float encoded)
        => encoded <= 0.04045f ? encoded / 12.92f : MathF.Pow((encoded + 0.055f) / 1.055f, 2.4f);

    static float ToEncoded(float linear)
        => linear <= 0.0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;

    static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    public static Bgra Shade(Bgra source, int x, int y, int width, int height, Vector3 antisolar, float halfFieldTangent, Half[] table, float amount)
    {
        if (amount <= 0f || source.Alpha == 0)
            return source;

        var focal = 0.5f * width / halfFieldTangent;
        var direction = Vector3.Normalize(new Vector3(x + 0.5f - width * 0.5f, y + 0.5f - height * 0.5f, focal));
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(direction, antisolar), -1f, 1f)) * DegreesPerRadian;
        var position = (angle - (float)RainbowSettings.TableStartDegrees) / (float)(RainbowSettings.TableEndDegrees - RainbowSettings.TableStartDegrees);
        if (position < 0f || position > 1f)
            return source;

        var (red, green, blue, gain) = RainbowTable.Sample(table, angle);
        var alpha = source.Alpha / 255f;
        var gainWithAmount = 1f + (gain - 1f) * amount;
        byte Channel(byte premultiplied, float add)
        {
            var straight = Saturate(premultiplied / 255f / alpha);
            var linear = Saturate(ToLinear(straight) * gainWithAmount + add * amount);
            return (byte)Math.Clamp((int)MathF.Round(ToEncoded(linear) * alpha * 255f), 0, 255);
        }

        return new Bgra(Channel(source.Blue, blue), Channel(source.Green, green), Channel(source.Red, red), source.Alpha);
    }
}
