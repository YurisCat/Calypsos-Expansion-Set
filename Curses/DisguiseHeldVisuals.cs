using CalypsoExpansionSet.Visuals;
using Entities.Player;
using Entities.Player.Behaviours;
using Items;
using UnityEngine;
using _200_Dev.EnvironmentElements.Player;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Curses;

// Native equipment keeps its owner, physics and hold pose. Only its visible
// geometry follows the matching anchor on the disguised character.
internal sealed class DisguiseHeldVisuals : IDisposable
{
    private sealed record Attachment(Transform Source, VisualRigCopy Copy, Renderer[] Renderers, Renderer?[] Copies,
        bool[] PriorOff, bool[] PriorEnabled)
    {
        internal int Hidden;
    }

    private readonly Item? _held;
    private readonly FlashlightTool? _flashlight;
    private readonly Renderer? _flashlightRenderer;
    private readonly Transform? _heldRoot, _flashlightRoot;
    private readonly Dictionary<IntPtr, Transform> _anchors = new();
    private readonly List<Attachment> _attachments = new();
    private readonly HashSet<IntPtr> _renderers = new();
    private readonly Transform? _flashlightMesh;
    private readonly Transform? _flashlightMount;
    private Vector3 _meshPosition, _meshScale;
    private Quaternion _meshRotation;
    private bool _meshMoved;
    private IntPtr _reportedParent;
    private bool _reportedMount;

    internal DisguiseHeldVisuals(PlayerEntity player, PlayerRigConfigWorld? donor, VisualRigCopy body)
    {
        _held = player.InventoryNew?.InHand;
        _flashlight = player.FlashLight;
        _flashlightRenderer = _flashlight != null ? _flashlight.selfRenderer : null;
        _heldRoot = Root(_held); _flashlightRoot = Root(_flashlight);
        var rig = player.PlayerWorldRigController;
        var source = rig != null ? rig.RigAnchor : null;
        var target = donor != null ? donor.RigAnchor : null;
        if (source == null || target == null) return;
        try
        {
            Bind(source.item1HRhAnchor, target.item1HRhAnchor, body);
            Bind(source.item1HLhAnchor, target.item1HLhAnchor, body);
            Bind(source.item2HAnchor, target.item2HAnchor, body);
            Bind(source.flashLightAnchor, target.flashLightAnchor, body);
            Bind(source.flashLightSecondaryAnchor, target.flashLightSecondaryAnchor, body);
            Bind(source.headAnchor, target.headAnchor, body);
            // Destroyed Unity components still have managed wrappers during a rig swap.
            var mesh = _flashlightRenderer != null ? _flashlightRenderer.transform : null;
            // The native flashlight has one leaf containing only its static mesh.
            // Move that mesh during rendering instead of creating a second renderer.
            // The Item root, light and collider remain in the native hierarchy.
            if (mesh != null && mesh.parent == _flashlightRoot && mesh.childCount == 0
                && mesh.GetComponent<MeshRenderer>() != null && mesh.GetComponent<MeshFilter>() != null
                && mesh.GetComponents<Component>().All(component => component.TryCast<Transform>() != null
                    || component.TryCast<MeshFilter>() != null || component.TryCast<MeshRenderer>() != null))
            {
                _flashlightMesh = mesh;
                _flashlightMount = new GameObject("Calypso flashlight visual mount") { hideFlags = HideFlags.DontSave }.transform;
                _flashlightMount.SetParent(body.Root, false);
                _flashlightMount.gameObject.SetActive(false);
                _renderers.Add(_flashlightRenderer!.Pointer);
            }
            if (_flashlight != null && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
                CalypsoExpansionSetPlugin.Instance.Log.LogInfo(_flashlightMesh != null
                    ? "[Disguise flashlight] Single native renderer prepared; resolving its disguise mount when drawing."
                    : "[Disguise flashlight] Unsupported visual structure; keeping native mount without a flashlight copy.");
            // An unsupported flashlight keeps its native mount. Never fall back
            // to a second renderer, including when inventory references the same tool.
            // A carried player keeps the native ragdoll and its real appearance.
            if (_heldRoot != _flashlightRoot && (_held == null || _held.TryCast<DeadPlayerTool>() == null))
                Add(_heldRoot, body);
        }
        catch { Dispose(); throw; }
    }

    private static Transform? Root(Item? item) => item == null ? null
        : item.SoItem?.useRootTransformWhenReparenting == true ? item.TransformRoot : item.transform;

    private void Bind(Transform? source, Transform? target, VisualRigCopy body)
    {
        if (source == null || target == null) return;
        var copy = body.CopiedTransform(target);
        if (copy != null) _anchors[source.Pointer] = copy;
    }

    private void Add(Transform? source, VisualRigCopy body)
    {
        if (source == null || _attachments.Any(attachment => attachment.Source == source)) return;
        var renderers = source.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.TryCast<MeshRenderer>() != null || renderer.TryCast<SkinnedMeshRenderer>() != null).ToArray();
        if (renderers.Length == 0) return;
        var copy = new VisualRigCopy(source, Vector3.one, hideSource: false);
        try
        {
            copy.Root.SetParent(body.Root, false);
            copy.Root.gameObject.SetActive(false);
            renderers = renderers.Where(renderer => copy.CopiedRenderer(renderer) != null).ToArray();
            var copies = renderers.Select(copy.CopiedRenderer).ToArray();
            _attachments.Add(new Attachment(source, copy, renderers, copies, new bool[renderers.Length], new bool[renderers.Length]));
            foreach (var renderer in renderers) _renderers.Add(renderer.Pointer);
        }
        catch { copy.Dispose(); throw; }
    }

    internal bool Owns(Renderer renderer) => _renderers.Contains(renderer.Pointer);

    internal bool Matches(PlayerEntity player)
    {
        var flashlight = player.FlashLight;
        var renderer = flashlight != null ? flashlight.selfRenderer : null;
        if (player.InventoryNew?.InHand != _held || flashlight != _flashlight
            || renderer != _flashlightRenderer
            || Root(_held) != _heldRoot || Root(_flashlight) != _flashlightRoot) return false;
        if (_flashlightMesh is not null && (_flashlightMesh == null || _flashlightMount == null
            || renderer == null || renderer.transform != _flashlightMesh)) return false;
        foreach (var attachment in _attachments) if (!attachment.Copy.Alive) return false;
        return true;
    }

    internal void Render()
    {
        Restore();
        RenderFlashlight();
        foreach (var attachment in _attachments)
        {
            var source = attachment.Source;
            var parent = source != null ? source.parent : null;
            if (!attachment.Copy.Alive || parent == null || !_anchors.TryGetValue(parent.Pointer, out var anchor)
                || anchor == null || !source!.gameObject.activeInHierarchy)
            {
                attachment.Copy.Root.gameObject.SetActive(false);
                continue;
            }
            attachment.Copy.SyncPose();
            var root = attachment.Copy.Root;
            if (root.parent != anchor) root.SetParent(anchor, false);
            root.localPosition = source.localPosition;
            root.localRotation = source.localRotation;
            root.localScale = source.localScale;
            for (var index = 0; index < attachment.Renderers.Length; index++)
            {
                var renderer = attachment.Renderers[index];
                if (renderer == null) continue;
                var copy = attachment.Copies[index];
                if (copy == null) continue;
                copy.enabled = renderer.enabled && !renderer.forceRenderingOff;
                attachment.PriorOff[index] = renderer.forceRenderingOff;
                attachment.PriorEnabled[index] = renderer.enabled;
                attachment.Hidden = index + 1;
                // Keep the native mesh out of renderer lists as well as suppressing drawing.
                // GameObject activation (e.g. flashlight on/off) remains owned by the game.
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
            }
        }
    }

    private void RenderFlashlight()
    {
        if (_flashlightMesh == null || _flashlightMount == null || _flashlightRoot == null) return;
        var parent = _flashlightRoot.parent;
        var matched = parent != null && _anchors.TryGetValue(parent.Pointer, out var anchor) && anchor != null;
        ReportMount(parent, matched);
        if (!matched
            || !_flashlightRoot.gameObject.activeInHierarchy || _flashlightMesh.parent != _flashlightRoot) return;
        _flashlightMount.SetParent(_anchors[parent!.Pointer], false);
        _flashlightMount.localPosition = _flashlightRoot.localPosition;
        _flashlightMount.localRotation = _flashlightRoot.localRotation;
        _flashlightMount.localScale = _flashlightRoot.localScale;
        _meshPosition = _flashlightMesh.localPosition;
        _meshRotation = _flashlightMesh.localRotation;
        _meshScale = _flashlightMesh.localScale;
        _meshMoved = true;
        _flashlightMount.gameObject.SetActive(true);
        _flashlightMesh.SetParent(_flashlightMount, false);
        _flashlightMesh.localPosition = _meshPosition;
        _flashlightMesh.localRotation = _meshRotation;
        _flashlightMesh.localScale = _meshScale;
    }

    private void ReportMount(Transform? parent, bool matched)
    {
        var pointer = parent != null ? parent.Pointer : IntPtr.Zero;
        if (_reportedMount && pointer == _reportedParent) return;
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled != true) return;
        _reportedMount = true; _reportedParent = pointer;
        CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Disguise flashlight] Native parent={(parent != null ? parent.name : "none")}; "
            + (matched ? $"mapped to disguise anchor={_anchors[pointer].name}, native position={parent!.position}, disguise position={_anchors[pointer].position}."
                : "no matching disguise anchor; keeping native position."));
    }

    internal void Restore()
    {
        if (_meshMoved)
        {
            if (_flashlightMesh != null && _flashlightRoot != null && _flashlightMesh.parent == _flashlightMount)
            {
                _flashlightMesh.SetParent(_flashlightRoot, false);
                _flashlightMesh.localPosition = _meshPosition;
                _flashlightMesh.localRotation = _meshRotation;
                _flashlightMesh.localScale = _meshScale;
            }
            _meshMoved = false;
        }
        if (_flashlightMount != null) _flashlightMount.gameObject.SetActive(false);
        foreach (var attachment in _attachments)
        {
            for (var index = 0; index < attachment.Hidden; index++)
            {
                var renderer = attachment.Renderers[index];
                if (renderer == null) continue;
                if (renderer.forceRenderingOff) renderer.forceRenderingOff = attachment.PriorOff[index];
                if (!renderer.enabled) renderer.enabled = attachment.PriorEnabled[index];
            }
            attachment.Hidden = 0;
        }
    }

    internal void Hide()
    {
        Restore();
        foreach (var attachment in _attachments)
            if (attachment.Copy.Alive) attachment.Copy.Root.gameObject.SetActive(false);
    }

    public void Dispose()
    {
        Restore();
        foreach (var attachment in _attachments) attachment.Copy.Dispose();
        if (_flashlightMount != null) UObject.Destroy(_flashlightMount.gameObject);
        _attachments.Clear(); _anchors.Clear(); _renderers.Clear();
    }
}
