namespace Rainbow;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/Rainbow;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}
