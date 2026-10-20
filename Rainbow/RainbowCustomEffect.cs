using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace Rainbow;

internal sealed class RainbowCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    internal enum PropertyIndex
    {
        Amount = 0,
        AntisolarX,
        AntisolarY,
        AntisolarZ,
        HalfFieldTangent,
    }

    public float Amount { set => SetValue((int)PropertyIndex.Amount, value); }
    public float AntisolarX { set => SetValue((int)PropertyIndex.AntisolarX, value); }
    public float AntisolarY { set => SetValue((int)PropertyIndex.AntisolarY, value); }
    public float AntisolarZ { set => SetValue((int)PropertyIndex.AntisolarZ, value); }
    public float HalfFieldTangent { set => SetValue((int)PropertyIndex.HalfFieldTangent, value); }

    private const float DefaultHalfFieldTangent = 1f;

    [CustomEffect(2)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;
        private RawRect _tableRect = new(0, 0, RainbowSettings.TableWidth, 1);

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Amount)]
        public float Amount { get => _cb.Amount; set { _cb.Amount = Sanitize(value, 0f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AntisolarX)]
        public float AntisolarX { get => _cb.AntisolarX; set { _cb.AntisolarX = Sanitize(value, -1f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AntisolarY)]
        public float AntisolarY { get => _cb.AntisolarY; set { _cb.AntisolarY = Sanitize(value, -1f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AntisolarZ)]
        public float AntisolarZ { get => _cb.AntisolarZ; set { _cb.AntisolarZ = Sanitize(value, -1f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.HalfFieldTangent)]
        public float HalfFieldTangent { get => _cb.HalfFieldTangent; set { _cb.HalfFieldTangent = float.IsNaN(value) ? DefaultHalfFieldTangent : Math.Clamp(value, RainbowSettings.MinimumHalfFieldTangent, RainbowSettings.MaximumHalfFieldTangent); UpdateConstants(); } }

        public EffectImpl() : base(ShaderResourceUri.Get("Rainbow"))
        {
            _cb.AntisolarZ = 1f;
            _cb.HalfFieldTangent = DefaultHalfFieldTangent;
            _cb.TableStart = (float)RainbowSettings.TableStartDegrees;
            _cb.TableSpan = (float)(RainbowSettings.TableEndDegrees - RainbowSettings.TableStartDegrees);
            _cb.TableWidth = RainbowSettings.TableWidth;
        }

        private static float Sanitize(float value, float minimum, float maximum)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, minimum, maximum);

        protected override void UpdateConstants()
        {
            if (drawInformation is null)
                return;

            try
            {
                drawInformation.SetPixelShaderConstantBuffer(_cb);
            }
            catch (Exception exception)
            {
                RainbowTelemetry.Report(exception);
                throw;
            }
        }

        public override void MapInputRectsToOutputRect(
            RawRect[] inputRects,
            RawRect[] inputOpaqueSubRects,
            out RawRect outputRect,
            out RawRect outputOpaqueSubRect)
        {
            inputRect = ClampInputRect(inputRects[0]);
            if (inputRects.Length > 1)
                _tableRect = inputRects[1];
            outputRect = inputRect;
            outputOpaqueSubRect = default;
            if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                return;

            _cb.InputBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
            UpdateConstants();
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            if (inputRects.Length > 0)
                inputRects[0] = outputRect;
            if (inputRects.Length > 1)
                inputRects[1] = _tableRect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public Vector4 InputBounds;
            public float AntisolarX;
            public float AntisolarY;
            public float AntisolarZ;
            public float HalfFieldTangent;
            public float Amount;
            public float TableStart;
            public float TableSpan;
            public float TableWidth;
        }
    }
}
