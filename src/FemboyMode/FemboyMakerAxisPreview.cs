using System;
using KKAPI.Maker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FemboyMode
{
    // Native Unity UI canvas with real EventSystem-driven drag handles.
    // The original IMGUI overlay did not own input or participate in the
    // Maker UI hierarchy; this one renders above the Maker Canvas and takes
    // pointer events only on the six small axis handles.
    internal sealed class FemboyMakerAxisPreview
    {
        private static readonly Color[] AxisColors = {
            new Color(0.95f, 0.28f, 0.32f, 1f),
            new Color(0.29f, 0.88f, 0.42f, 1f),
            new Color(0.30f, 0.63f, 1f, 1f)
        };
        private static readonly Vector3[] Directions = {
            Vector3.right, Vector3.up, Vector3.forward
        };
        private static readonly string[] AxisNames = { "X", "Y", "Z" };
        private static readonly string[] ModeNames = { "缩放", "位移", "旋转" };
        private RectTransform root;
        private RectTransform[,] lines = new RectTransform[2, 3];
        private RectTransform[,] handles = new RectTransform[2, 3];
        private Text[] headings = new Text[2];
        private Text warning;
        private Vector2[,] handleScreen = new Vector2[2, 3];
        private Vector2[] centerScreen = new Vector2[2];
        private bool[] active = new bool[2];
        private bool[] missing = new bool[2];
        private int[] modes;
        private FemboyCharacterController controller;
        private Action<int, int, int, float> onValueChanged;
        private int dragPart = -1, dragAxis, dragMode;
        private FemboyCharacterController dragController;
        private Vector2 dragStart, dragDirection;
        private float dragStartValue;

        private sealed class AxisHandle : MonoBehaviour,
            IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
        {
            internal FemboyMakerAxisPreview Owner;
            internal int Part, Axis;

            public void OnPointerDown(PointerEventData data)
            {
                if (data.button == PointerEventData.InputButton.Left)
                    Owner.BeginDrag(Part, Axis, data.position);
            }

            public void OnDrag(PointerEventData data)
            {
                Owner.UpdateDrag(data.position);
            }

            public void OnEndDrag(PointerEventData data) { Owner.EndDrag(); }
            public void OnPointerUp(PointerEventData data) { Owner.EndDrag(); }
        }

        private static RectTransform AddGraphic(Transform parent, string name,
            Vector2 size, Color color, bool raycast)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            RawImage image = go.GetComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static Text AddText(Transform parent, string name, Vector2 size, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void EnsureUi()
        {
            if (root != null) return;
            // The dedicated ScreenSpaceOverlay canvas uses the native UI
            // event system rather than GUI.DrawTexture and raw Input polling.
            GameObject go = new GameObject("FemboyMode.TransformGizmo",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root = (RectTransform)go.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2500;
            go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            for (int part = 0; part < 2; part++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    lines[part, axis] = AddGraphic(root, "AxisLine-" + part + "-" + axis,
                        new Vector2(1f, 3f), AxisColors[axis], false);
                }
                headings[part] = AddText(root, "AxisHeading-" + part, new Vector2(190f, 26f), 15);
                headings[part].alignment = TextAnchor.MiddleLeft;
                headings[part].text = part == 0 ? "Penis" : "Balls";
                for (int axis = 0; axis < 3; axis++)
                {
                    RectTransform handle = AddGraphic(root, "AxisHandle-" + part + "-" + axis,
                        new Vector2(24f, 24f), AxisColors[axis], true);
                    AxisHandle drag = handle.gameObject.AddComponent<AxisHandle>();
                    drag.Owner = this;
                    drag.Part = part;
                    drag.Axis = axis;
                    Text label = AddText(handle, "AxisLabel", new Vector2(24f, 24f), 14);
                    label.text = AxisNames[axis];
                    handles[part, axis] = handle;
                }
            }
            warning = AddText(root, "MissingBoneNotice", new Vector2(640f, 30f), 15);
            warning.alignment = TextAnchor.MiddleLeft;
            ((RectTransform)warning.transform).anchoredPosition = new Vector2(30f - Screen.width / 2f,
                Screen.height / 2f - 52f);
            warning.color = new Color(1f, .83f, .30f, 1f);
            go.SetActive(false);
        }

        private static Camera FindCamera()
        {
            Camera main = Camera.main;
            if (main != null && main.enabled && main.targetTexture == null) return main;
            Camera best = null;
            foreach (Camera camera in Camera.allCameras)
                if (camera != null && camera.enabled && camera.targetTexture == null &&
                    (best == null || camera.depth > best.depth)) best = camera;
            return best;
        }

        internal void Tick(FemboyCharacterController current, bool[] visible,
            int[] currentModes, Action<int, int, int, float> changed)
        {
            if (!MakerAPI.InsideAndLoaded)
            {
                Dispose();
                return;
            }
            controller = current;
            modes = currentModes;
            onValueChanged = changed;
            if (controller == null || visible == null || modes == null ||
                !MakerAPI.IsInterfaceVisible())
            {
                HideAll();
                return;
            }
            EnsureUi();
            Camera camera = FindCamera();
            int activeCount = 0;
            string missingDescription = string.Empty;
            for (int part = 0; part < 2; part++)
            {
                active[part] = false;
                missing[part] = false;
                if (part >= visible.Length || part >= modes.Length || !visible[part]) continue;
                Transform bone = controller.GetGeometryBone(part);
                if (bone == null)
                {
                    missing[part] = true;
                    missingDescription += (part == 0 ? "Penis" : "Balls") + " target bone not found. ";
                    continue;
                }
                if (camera == null) continue;
                Vector3 center = camera.WorldToScreenPoint(bone.position);
                if (center.z <= .0001f) continue;
                Vector2 centerPx = new Vector2(center.x, center.y);
                centerScreen[part] = centerPx;
                Vector2 centerUi = centerPx - new Vector2(Screen.width * .5f, Screen.height * .5f);
                Quaternion rotation = modes[part] == 1 && bone.parent != null
                    ? bone.parent.rotation : bone.rotation;
                headings[part].text = (part == 0 ? "Penis" : "Balls") + " / " +
                    ModeNames[Mathf.Clamp(modes[part], 0, 2)];
                ((RectTransform)headings[part].transform).anchoredPosition = centerUi + new Vector2(80f, -20f);
                headings[part].gameObject.SetActive(true);
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 projected = camera.WorldToScreenPoint(
                        bone.position + rotation * Directions[axis] * .065f);
                    Vector2 delta = new Vector2(projected.x - center.x, projected.y - center.y);
                    if (delta.sqrMagnitude < 64f)
                        delta = axis == 0 ? new Vector2(1f, .35f) :
                            axis == 1 ? new Vector2(-.35f, 1f) : new Vector2(.7f, -.7f);
                    Vector2 endPx = centerPx + delta.normalized * 74f;
                    handleScreen[part, axis] = endPx;
                    Vector2 endUi = endPx - new Vector2(Screen.width * .5f, Screen.height * .5f);
                    handles[part, axis].anchoredPosition = endUi;
                    Vector2 segment = endUi - centerUi;
                    RectTransform line = lines[part, axis];
                    line.anchoredPosition = (endUi + centerUi) * .5f;
                    line.sizeDelta = new Vector2(segment.magnitude, 4f);
                    line.localEulerAngles = new Vector3(0f, 0f,
                        Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                    line.gameObject.SetActive(true);
                    handles[part, axis].gameObject.SetActive(true);
                }
                active[part] = true;
                activeCount++;
            }
            for (int part = 0; part < 2; part++)
                if (!active[part])
                {
                    headings[part].gameObject.SetActive(false);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        lines[part, axis].gameObject.SetActive(false);
                        handles[part, axis].gameObject.SetActive(false);
                    }
                }
            if (dragPart >= 0 && (controller != dragController ||
                dragPart >= modes.Length || modes[dragPart] != dragMode || !active[dragPart]))
                EndDrag();
            warning.text = missingDescription;
            warning.gameObject.SetActive(missingDescription.Length != 0);
            root.gameObject.SetActive(activeCount > 0 || missingDescription.Length > 0);
            // Some Maker variants do not supply an EventSystem. Keep handles
            // functional in that environment without drawing an IMGUI overlay.
            if (EventSystem.current == null && root.gameObject.activeSelf)
                FallbackInput();
        }

        private void FallbackInput()
        {
            Vector2 position = Input.mousePosition;
            if (Input.GetMouseButtonUp(0)) EndDrag();
            if (dragPart >= 0)
            {
                if (Input.GetMouseButton(0)) UpdateDrag(position);
                return;
            }
            if (!Input.GetMouseButtonDown(0)) return;
            for (int part = 0; part < 2; part++)
            {
                if (!active[part]) continue;
                for (int axis = 0; axis < 3; axis++)
                {
                    if ((position - handleScreen[part, axis]).sqrMagnitude <= 14f * 14f)
                    {
                        BeginDrag(part, axis, position);
                        return;
                    }
                }
            }
        }

        private void BeginDrag(int part, int axis, Vector2 position)
        {
            if (part < 0 || part >= active.Length || axis < 0 || axis >= 3 ||
                !active[part] || controller == null || modes == null) return;
            dragPart = part;
            dragAxis = axis;
            dragMode = modes[part];
            dragController = controller;
            dragStart = position;
            dragStartValue = controller.GetGeometryValue(part, dragMode, axis);
            dragDirection = (handleScreen[part, axis] - centerScreen[part]).normalized;
        }

        private void UpdateDrag(Vector2 position)
        {
            if (dragPart < 0 || controller != dragController ||
                !active[dragPart] || modes == null || modes[dragPart] != dragMode)
            {
                EndDrag();
                return;
            }
            float pixels = Vector2.Dot(position - dragStart, dragDirection);
            float desired = FemboyMakerAxisMath.DragValue(dragStartValue, pixels, dragMode,
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            dragController.SetGeometryValue(dragPart, dragMode, dragAxis, desired);
            if (onValueChanged != null)
                onValueChanged(dragPart, dragMode, dragAxis,
                    dragController.GetGeometryValue(dragPart, dragMode, dragAxis));
        }

        private void EndDrag()
        {
            dragPart = -1;
            dragController = null;
        }

        private void HideAll()
        {
            EndDrag();
            if (root != null) root.gameObject.SetActive(false);
        }

        internal void Dispose()
        {
            EndDrag();
            controller = null;
            onValueChanged = null;
            active[0] = active[1] = false;
            missing[0] = missing[1] = false;
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
        }
    }
}
