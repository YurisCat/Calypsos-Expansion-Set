using CalypsoExpansionSet.Idols;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using User;
using _200_Dev.Entities.Player.Behaviours.Camera;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Visuals;

internal enum IdolScreenStyle { Mosaic, Outline, Fisheye }

// A single held item owns the local final-frame pass. HUD is included; native
// gameplay/menu readiness gates the entire pass, leaving menus untouched.
internal class HeldScreenEffect : HeldIdolEffect
{
    private static HeldScreenEffect? _active;
    private readonly ulong _configId;
    private readonly IdolScreenStyle _style;
    private HeldIdolContext _context;
    private RenderTexture? _source;
    private Material? _material;
    private CommandBuffer? _commands;
    private bool _faulted, _logged;
    private int _frame = -1;

    internal HeldScreenEffect(ulong configId, IdolScreenStyle style)
    {
        _configId = configId;
        _style = style;
    }

    public override void Start(HeldIdolContext context)
    {
        Dispose();
        _context = context;
        _faulted = _logged = false;
    }

    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (Ready()) _active = this;
        else if (_active == this) _active = null;
    }

    private Camera? Pov()
    {
        var source = SetFovShakeData.Instance;
        var manager = CameraManager.Instance;
        return source != null && source._ready && source._cam != null && source._cam.isActiveAndEnabled
            && source._cam.targetTexture == null && source._playerPerlinShake == _context.Holder.playerPerlinShake
            && manager != null && (manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                || manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH)
            ? source._cam : null;
    }

    private bool Ready() => !_faulted && !Testing.TestRoomAppearanceView.IsInspecting
        && LocalHeldGuard.Ready(_context, _configId) && Pov() != null;

    internal static void Render(Il2CppSystem.Collections.Generic.List<Camera> cameras)
    {
        var effect = _active;
        if (effect == null) return;
        try
        {
            if (!effect.Ready() || effect._frame == Time.frameCount || cameras == null) return;
            var pov = effect.Pov();
            if (pov == null || !cameras.Contains(pov)) return;
            effect._frame = Time.frameCount;
            effect.Draw();
        }
        catch (Exception error)
        {
            effect._faulted = true;
            effect.Release();
            if (_active == effect) _active = null;
            CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Screen effect disabled until released: {error}");
        }
    }

    private void Draw()
    {
        var width = Screen.width;
        var height = Screen.height;
        if (width < 1 || height < 1 || width > 16384 || height > 16384) return;
        if (_material == null)
        {
            var shader = ScreenEffectResources.Shader;
            if (shader == null) throw new InvalidOperationException("Screen-effect shader lifetime ended.");
            _material = new Material(shader) { name = "Calypso held screen effect", hideFlags = HideFlags.DontSave };
            _material.SetFloat("_Mode", (float)_style);
        }
        if (_source == null || _source.width != width || _source.height != height)
        {
            ReleaseTexture();
            _source = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Calypso final frame source", hideFlags = HideFlags.DontSave,
                filterMode = _style == IdolScreenStyle.Mosaic ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!_source.Create()) throw new InvalidOperationException("Cannot allocate screen-effect render texture.");
        }
        var pixelSize = Math.Max(1f, height / 90f);
        _material.SetVector("_PixelGrid", new Vector4(width / pixelSize, height / pixelSize, 0, 0));
        _material.SetFloat("_Strength", _style == IdolScreenStyle.Fisheye ? 1.6f : Math.Max(1f, height / 1080f));
        var previous = RenderTexture.active;
        try
        {
            _commands ??= new CommandBuffer { name = "Calypso final-frame style" };
            _commands.Clear();
            var screen = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            var source = new RenderTargetIdentifier(_source);
            _commands.Blit(screen, source);
            // Both calls are retained in this game's actual metadata. Avoid the
            // restored ScreenCapture/material-Graphics.Blit bindings.
            _commands.Blit_Identifier(ref source, ref screen, _material, 0, Vector2.one, Vector2.zero, -1, -1);
            Graphics.ExecuteCommandBuffer(_commands);
        }
        finally { RenderTexture.active = previous; }
        if (!_logged && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
        {
            _logged = true;
            CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Screen idol] {_style}: {width}x{height}, world/HUD pass active.");
        }
    }

    private void ReleaseTexture()
    {
        if (_source == null) return;
        _source.Release();
        UObject.Destroy(_source);
        _source = null;
    }

    private void Release()
    {
        ReleaseTexture();
        if (_material != null) UObject.Destroy(_material);
        _material = null;
        _commands?.Dispose();
        _commands = null;
        _frame = -1;
    }

    public override void Dispose()
    {
        if (_active == this) _active = null;
        Release();
        _context = default;
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "EndContextRendering")]
internal static class IdolScreenFramePatch
{
    [HarmonyPostfix]
    private static void Postfix(Il2CppSystem.Collections.Generic.List<Camera> cameras) => HeldScreenEffect.Render(cameras);
}
