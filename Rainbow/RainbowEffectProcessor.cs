using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using Vortice.DCommon;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace Rainbow;

internal sealed class RainbowEffectProcessor(IGraphicsDevicesAndContext devices, RainbowEffect item) : VideoEffectProcessorBase(devices)
{
    private readonly IGraphicsDevicesAndContext _devices = devices;
    private readonly RainbowEffect _item = item;
    private RainbowCustomEffect? _effect;
    private ID2D1Bitmap1? _table;
    private RainbowPhysicalTable? _physical;
    private bool _isFirst = true;
    private PhysicalKey _physicalKey;
    private TableKey _tableKey;
    private ConstantKey _constantKey;

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        try
        {
            return UpdateCore(effectDescription);
        }
        catch (Exception exception)
        {
            RainbowTelemetry.Report(exception);
            throw;
        }
    }

    private DrawDescription UpdateCore(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _effect is null || _table is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;
        var amount = (float)(_item.Amount.GetValue(frame, length, fps) / 100.0);
        if (_isFirst || _constantKey.Amount != amount)
            _effect.Amount = amount;

        if (amount <= 0f)
        {
            _constantKey = _constantKey with { Amount = amount };
            _isFirst = false;
            return effectDescription.DrawDescription;
        }

        var radius = Math.Clamp(_item.DropRadius.GetValue(frame, length, fps), RainbowSettings.MinimumRadiusMillimeters, RainbowSettings.MaximumRadiusMillimeters);
        var sunDiameter = Math.Clamp(_item.SunDiameter.GetValue(frame, length, fps), 0.0, RainbowSettings.MaximumSunDiameterDegrees);
        var physicalKey = new PhysicalKey(radius, sunDiameter);
        var tableKey = new TableKey(
            physicalKey,
            Math.Clamp(_item.SecondaryBow.GetValue(frame, length, fps) / 100.0, 0.0, 1.0),
            Math.Clamp(_item.DarkBand.GetValue(frame, length, fps) / 100.0, 0.0, 1.0),
            Math.Clamp(_item.Saturation.GetValue(frame, length, fps) / 100.0, 0.0, 2.0));
        if (_isFirst || _physical is null || _physicalKey != physicalKey)
        {
            _physical = RainbowPhysicalTable.Build(radius, sunDiameter);
            _physicalKey = physicalKey;
        }

        if (_isFirst || _tableKey != tableKey)
        {
            WriteTable(RainbowTable.Compose(_physical, tableKey.Secondary, tableKey.Darkness, tableKey.Saturation));
            _tableKey = tableKey;
        }

        var antisolar = RainbowSettings.GetAntisolarDirection(
            _item.SunElevation.GetValue(frame, length, fps),
            _item.AntisolarDirection.GetValue(frame, length, fps),
            _item.CameraPitch.GetValue(frame, length, fps));
        var halfFieldTangent = RainbowSettings.GetHalfFieldOfViewTangent(_item.FieldOfView.GetValue(frame, length, fps));
        var constantKey = new ConstantKey(amount, antisolar.X, antisolar.Y, antisolar.Z, halfFieldTangent);
        if (_isFirst || _constantKey.AntisolarX != constantKey.AntisolarX)
            _effect.AntisolarX = constantKey.AntisolarX;
        if (_isFirst || _constantKey.AntisolarY != constantKey.AntisolarY)
            _effect.AntisolarY = constantKey.AntisolarY;
        if (_isFirst || _constantKey.AntisolarZ != constantKey.AntisolarZ)
            _effect.AntisolarZ = constantKey.AntisolarZ;
        if (_isFirst || _constantKey.HalfFieldTangent != constantKey.HalfFieldTangent)
            _effect.HalfFieldTangent = constantKey.HalfFieldTangent;

        _constantKey = constantKey;
        _isFirst = false;
        return effectDescription.DrawDescription;
    }

    private void WriteTable(Half[] texels)
    {
        var handle = GCHandle.Alloc(texels, GCHandleType.Pinned);
        try
        {
            _table!.CopyFromMemory(handle.AddrOfPinnedObject(), RainbowSettings.TableWidth * RainbowSettings.TexelBytes);
        }
        finally
        {
            handle.Free();
        }
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var effect = new RainbowCustomEffect(devices);
        if (!effect.IsEnabled)
        {
            effect.Dispose();
            return null;
        }

        _effect = effect;
        disposer.Collect(effect);
        var properties = new BitmapProperties1(new PixelFormat(Format.R16G16B16A16_Float, Vortice.DCommon.AlphaMode.Premultiplied), 96f, 96f, BitmapOptions.None);
        var empty = new Half[RainbowSettings.TableWidth * RainbowSettings.TexelChannels];
        var handle = GCHandle.Alloc(empty, GCHandleType.Pinned);
        try
        {
            _table = ((ID2D1DeviceContext1)devices.DeviceContext).CreateBitmap(
                new Vortice.Mathematics.SizeI(RainbowSettings.TableWidth, 1),
                handle.AddrOfPinnedObject(),
                RainbowSettings.TableWidth * RainbowSettings.TexelBytes,
                properties);
        }
        finally
        {
            handle.Free();
        }

        disposer.Collect(_table);
        _effect.SetInput(1, _table, true);
        var output = effect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _effect?.SetInput(0, input, true);
    }

    protected override void ClearEffectChain()
    {
        try
        {
            _effect?.SetInput(0, null, true);
            _isFirst = true;
        }
        catch (Exception exception)
        {
            RainbowTelemetry.Report(exception);
            throw;
        }
    }

    private readonly record struct PhysicalKey(double Radius, double SunDiameter);

    private readonly record struct TableKey(PhysicalKey Physical, double Secondary, double Darkness, double Saturation);

    private readonly record struct ConstantKey(float Amount, float AntisolarX, float AntisolarY, float AntisolarZ, float HalfFieldTangent);
}
