using System;
using ChaCustom;
using CustomUtility;
using KKAPI.Maker;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// The REAL Koikatsu Maker CustomGuideObject, not a re-drawn Canvas,
    /// RawImage, IMGUI or a private imitation of the 3D axis sprites.
    ///
    /// Clone the *native guide object* from CustomControl.cmpGuid[0]. Its
    /// CustomGuideMove / CustomGuideRotation scripts and game model meshes
    /// receive the native pointer events. The clone is not placed in
    /// cmpGuid[]: CvsAccessory therefore never observes its drag state and
    /// cannot write these bone edits into the selected accessory slot.
    /// Only FemboyCharacterController.SetGeometryValue() is ever written.
    ///
    /// The original Maker guide has move and rotation geometry (no native
    /// scale mode). The scale mode intentionally uses its native X/Y/Z move
    /// handles but interprets the displacement as scale values; it never
    /// substitutes self-drawn shapes.
    /// </summary>
    internal sealed class FemboyMakerAxisPreview
    {
        private GameObject root;
        private CustomGuideObject guide;
        private FemboyCharacterController actor, dragActor;
        private Action<int, int, int, float> notify;
        private bool warnedMissingSource, dragging, fallbackDragging;
        private int fallbackAxis, fallbackPart, fallbackMode;
        private float fallbackStartValue;
        private Vector2 fallbackStartScreen, fallbackScreenAxis;
        private FemboyCharacterController fallbackActor;
        private int part = -1, mode = -1;
        private Vector3 anchorWorld, anchorWorldEuler, baseLocalEuler;
        private Quaternion parentWorldRotation;
        private readonly float[] baseValues = new float[3];

        private static CustomGuideObject OriginalGuide()
        {
            CustomBase maker = CustomBase.Instance;
            if (maker == null || maker.customCtrl == null ||
                maker.customCtrl.cmpGuid == null ||
                maker.customCtrl.cmpGuid.Length == 0)
                return null;
            return maker.customCtrl.cmpGuid[0];
        }

        private void EnsureNativeGuide()
        {
            if (root != null) return;
            CustomGuideObject source = OriginalGuide();
            if (source == null)
            {
                if (!warnedMissingSource)
                {
                    warnedMissingSource = true;
                    Debug.LogWarning("Femboy Mode: native Maker CustomGuideObject not yet initialized; " +
                        "not substituting a custom Canvas axis.");
                }
                return;
            }

            GameObject created = null;
            try
            {
                created = UnityEngine.Object.Instantiate(source.gameObject);
                created.name = "FemboyMode.NativeMakerGuide";
                CustomGuideObject instance = created.GetComponent<CustomGuideObject>();
                if (instance == null) throw new InvalidOperationException(
                    "Cloned Maker guide has no CustomGuideObject component.");

                // An amount is a reference-type nested object; never alias
                // the original cmpGuid amount, even on Unity versions whose
                // cloning behaviour does not deep-copy ordinary C# objects.
                instance.amount = new CustomGuideObject.Amount();
                instance.scaleAxis = source.scaleAxis;
                instance.speedMove = source.speedMove;
                foreach (CustomGuideBase axis in created.GetComponentsInChildren<CustomGuideBase>(true))
                {
                    if (axis == null) continue;
                    axis.guideObject = instance;
                    axis.draw = true;
                    axis.enabled = true;
                    axis.gameObject.SetActive(true);
                }
                created.SetActive(false);
                root = created;
                guide = instance;
            }
            catch (Exception ex)
            {
                if (created != null) UnityEngine.Object.Destroy(created);
                Debug.LogWarning("Femboy Mode: cannot clone native Maker guide safely: " + ex);
                root = null;
                guide = null;
            }
        }

        internal void Tick(FemboyCharacterController current, bool[] visible,
            int[] modes, Action<int, int, int, float> changed)
        {
            if (!MakerAPI.InsideAndLoaded)
            {
                Dispose();
                return;
            }
            notify = changed;
            actor = current;
            if (current == null || visible == null || modes == null ||
                !MakerAPI.IsInterfaceVisible())
            {
                HideAll();
                return;
            }
            int target = -1;
            for (int i = 0; i < 2 && i < visible.Length && i < modes.Length; i++)
                if (visible[i]) { target = i; break; }
            if (target < 0)
            {
                HideAll();
                return;
            }
            Transform bone = current.GetGeometryBone(target);
            if (bone == null)
            {
                HideAll();
                return;
            }
            EnsureNativeGuide();
            if (guide == null) return;
            int targetMode = Mathf.Clamp(modes[target], 0, 2);
            if (target != part || targetMode != mode || dragActor != null && dragActor != current)
            {
                fallbackDragging = false;
                dragging = false;
                dragActor = null;
                part = target;
                mode = targetMode;
                // Koikatsu Maker supports 0 = position, 1 = rotation.
                // For scale, use native position handles with scale semantics.
                guide.SetMode((byte)(targetMode == 2 ? 1 : 0));
                guide.isDrag = false;
            }

            if (root != null && !root.activeSelf) root.SetActive(true);
            bool nativeDrag = guide.isDrag;
            if (!nativeDrag)
            {
                dragging = false;
                dragActor = null;
                // The original Maker axes use PhysicsRaycaster on some
                // installations. When the EventSystem does not deliver its
                // pointer events, accept drag on the *actual cloned native
                // axis collider*, not a fabricated screen-space handle.
                if (RunNativeColliderFallback(current, target, targetMode, bone))
                    return;
                guide.amount.position = bone.position;
                guide.amount.rotation = bone.eulerAngles;
                return;
            }
            fallbackDragging = false;
            if (!dragging)
            {
                dragging = true;
                dragActor = current;
                anchorWorld = bone.position;
                anchorWorldEuler = bone.eulerAngles;
                parentWorldRotation = bone.parent == null ?
                    Quaternion.identity : bone.parent.rotation;
                baseLocalEuler = bone.localEulerAngles;
                for (int i = 0; i < 3; i++)
                    baseValues[i] = current.GetGeometryValue(target, targetMode, i);
            }
            if (dragActor != current) { HideAll(); return; }

            Vector3 localMove = Quaternion.Inverse(parentWorldRotation) *
                (guide.amount.position - anchorWorld);
            Quaternion localGuideRot = Quaternion.Inverse(parentWorldRotation) *
                Quaternion.Euler(guide.amount.rotation);
            Vector3 localAngles = localGuideRot.eulerAngles;
            bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool coarse = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            float precision = fine ? .2f : coarse ? 3f : 1f;
            for (int i = 0; i < 3; i++)
            {
                float value;
                if (targetMode == 2)
                {
                    float degrees = Mathf.DeltaAngle(baseLocalEuler[i], localAngles[i]);
                    value = FemboyNativeAxisMath.Rotated(baseValues[i], degrees, precision);
                }
                else
                {
                    value = FemboyNativeAxisMath.Moved(baseValues[i],
                        localMove[i], targetMode == 0, precision);
                }
                float before = current.GetGeometryValue(target, targetMode, i);
                if (Mathf.Abs(before - value) <= .00001f) continue;
                current.SetGeometryValue(target, targetMode, i, value);
                if (notify != null)
                    notify(target, targetMode, i, current.GetGeometryValue(target, targetMode, i));
            }
        }

        private static Camera FindCamera()
        {
            if (Camera.main != null && Camera.main.enabled &&
                Camera.main.targetTexture == null) return Camera.main;
            Camera chosen = null;
            foreach (Camera camera in Camera.allCameras)
                if (camera != null && camera.enabled && camera.targetTexture == null &&
                    (chosen == null || camera.depth > chosen.depth))
                    chosen = camera;
            return chosen;
        }

        private bool RunNativeColliderFallback(FemboyCharacterController current,
            int target, int targetMode, Transform bone)
        {
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0) ||
                fallbackActor != null && fallbackActor != current)
                fallbackDragging = false;

            if (fallbackDragging)
            {
                if (fallbackActor != current || fallbackPart != target ||
                    fallbackMode != targetMode) { fallbackDragging = false; return false; }
                Vector2 mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                float projected = Vector2.Dot(mouse - fallbackStartScreen, fallbackScreenAxis);
                float desired = FemboyMakerAxisMath.DragValue(fallbackStartValue, projected,
                    targetMode,
                    Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                    Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
                float previous = current.GetGeometryValue(target, targetMode, fallbackAxis);
                if (Mathf.Abs(previous - desired) > .00001f)
                {
                    current.SetGeometryValue(target, targetMode, fallbackAxis, desired);
                    if (notify != null)
                        notify(target, targetMode, fallbackAxis,
                            current.GetGeometryValue(target, targetMode, fallbackAxis));
                }
                guide.amount.position = bone.position;
                guide.amount.rotation = bone.eulerAngles;
                return true;
            }

            if (!Input.GetMouseButtonDown(0) || root == null) return false;
            Camera camera = FindCamera();
            if (camera == null) return false;
            RaycastHit[] hits = Physics.RaycastAll(
                camera.ScreenPointToRay(Input.mousePosition), 1000f, ~0,
                QueryTriggerInteraction.Collide);
            float closest = float.PositiveInfinity;
            int selectedAxis = -1;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || !hit.collider.transform.IsChildOf(root.transform) ||
                    hit.distance >= closest) continue;
                int axis = -1;
                if (targetMode == 2)
                {
                    CustomGuideRotation ring =
                        hit.collider.GetComponentInParent<CustomGuideRotation>();
                    if (ring != null) axis = (int)ring.axis;
                }
                else
                {
                    CustomGuideMove arrow =
                        hit.collider.GetComponentInParent<CustomGuideMove>();
                    if (arrow != null) axis = (int)arrow.axis;
                }
                if (axis < 0 || axis > 2) continue;
                closest = hit.distance;
                selectedAxis = axis;
            }
            if (selectedAxis < 0) return false;
            Vector3 localAxis = selectedAxis == 0 ? Vector3.right :
                selectedAxis == 1 ? Vector3.up : Vector3.forward;
            Quaternion basis = targetMode == 1 && bone.parent != null ?
                bone.parent.rotation : bone.rotation;
            Vector3 center = camera.WorldToScreenPoint(bone.position);
            Vector3 endpoint = camera.WorldToScreenPoint(
                bone.position + basis * localAxis * .065f);
            Vector2 direction = new Vector2(endpoint.x - center.x, endpoint.y - center.y);
            if (targetMode == 2) direction = new Vector2(-direction.y, direction.x);
            if (direction.sqrMagnitude < 16f)
                direction = selectedAxis == 0 ? Vector2.right :
                    selectedAxis == 1 ? Vector2.up : new Vector2(1f, 1f);
            fallbackDragging = true;
            fallbackActor = current;
            fallbackPart = target;
            fallbackMode = targetMode;
            fallbackAxis = selectedAxis;
            fallbackStartScreen = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            fallbackScreenAxis = direction.normalized;
            fallbackStartValue = current.GetGeometryValue(target, targetMode, selectedAxis);
            return true;
        }

        private void HideAll()
        {
            fallbackDragging = false;
            fallbackActor = null;
            dragging = false;
            dragActor = null;
            if (root != null) root.SetActive(false);
            if (guide != null) guide.isDrag = false;
        }

        internal void Dispose()
        {
            HideAll();
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            guide = null;
            actor = null;
            notify = null;
            part = mode = -1;
            warnedMissingSource = false;
        }
    }
}
