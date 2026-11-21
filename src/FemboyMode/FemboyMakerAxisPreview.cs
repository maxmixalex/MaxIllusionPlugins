using System;
using KKAPI.Maker;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FemboyMode
{
    /// <summary>
    /// Maker-only screen-space transform gizmo. Unlike the v0.5.1 collider/LineRenderer
    /// implementation, it stays visible when the target bone is inside the body,
    /// and it never changes vanilla accessory slots or attaches colliders to the rig.
    /// The three axes always represent the selected scale/move/rotate mode.
    /// </summary>
    internal sealed class FemboyMakerAxisPreview
    {
        private static readonly Color[] Colors =
        {
            new Color(1f, .24f, .27f, 1f),
            new Color(.27f, 1f, .34f, 1f),
            new Color(.30f, .64f, 1f, 1f)
        };
        private static readonly string[] AxisNames = { "X", "Y", "Z" };
        private static readonly string[] ModeNames = { "Scale", "Move", "Rotate" };
        private static readonly Vector3[] Directions =
        {
            Vector3.right, Vector3.up, Vector3.forward
        };
        private readonly Vector2[,] ends = new Vector2[2, 3];
        private readonly Vector2[] centers = new Vector2[2];
        private readonly bool[] active = new bool[2];
        private readonly bool[] missingBone = new bool[2];
        private int[] currentModes;
        private FemboyCharacterController currentController;
        private Action<int, int, int, float> onValueChanged;
        private int dragPart = -1, dragAxis, dragMode;
        private Vector2 startMouse, dragDirection;
        private float startValue;
        private FemboyCharacterController dragController;

        internal void Tick(FemboyCharacterController controller, bool[] visible,
            int[] modes, Action<int, int, int, float> onChanged)
        {
            currentController = controller;
            currentModes = modes;
            onValueChanged = onChanged;
            Camera cam = GetMakerCamera();
            for (int part = 0; part < 2; part++)
            {
                active[part] = false;
                missingBone[part] = false;
                if (!MakerAPI.InsideAndLoaded || controller == null || visible == null ||
                    modes == null || !visible[part])
                    continue;
                Transform bone = controller.GetGeometryBone(part);
                if (bone == null) { missingBone[part] = true; continue; }
                if (cam == null) continue;
                Vector3 pixel = cam.WorldToScreenPoint(bone.position);
                if (pixel.z <= .0001f) continue;
                Vector2 origin = new Vector2(pixel.x, Screen.height - pixel.y);
                // Use the correct transform space for the move mode.
                Quaternion rotation = modes[part] == 1 && bone.parent != null
                    ? bone.parent.rotation : bone.rotation;
                centers[part] = origin;
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 projected = cam.WorldToScreenPoint(
                        bone.position + rotation * Directions[axis] * .065f);
                    Vector2 delta = new Vector2(projected.x - pixel.x, pixel.y - projected.y);
                    // A world-space axis viewed end-on must remain selectable.
                    if (delta.sqrMagnitude < 64f)
                        delta = axis == 0 ? new Vector2(1f, -.35f) :
                                axis == 1 ? new Vector2(-.35f, -1f) :
                                new Vector2(.7f, .7f);
                    ends[part, axis] = origin + delta.normalized * 72f;
                }
                active[part] = true;
            }

            if (dragPart >= 0 &&
                (!MakerAPI.InsideAndLoaded || controller != dragController ||
                 currentModes == null || dragMode != currentModes[dragPart] ||
                 !active[dragPart]))
                dragPart = -1;
            HandleMouse();
        }

        private static Camera GetMakerCamera()
        {
            if (Camera.main != null && Camera.main.enabled &&
                Camera.main.targetTexture == null) return Camera.main;
            Camera best = null;
            foreach (Camera cam in Camera.allCameras)
                if (cam.enabled && cam.targetTexture == null &&
                    (best == null || cam.depth > best.depth)) best = cam;
            return best;
        }

        private void HandleMouse()
        {
            if (currentController == null || currentModes == null ||
                !MakerAPI.InsideAndLoaded) { dragPart = -1; return; }
            Vector2 mouse = new Vector2(Input.mousePosition.x,
                Screen.height - Input.mousePosition.y);
            if (dragPart >= 0)
            {
                if (!Input.GetMouseButton(0)) { dragPart = -1; return; }
                Vector2 displacement = mouse - startMouse;
                float distance = Vector2.Dot(displacement, dragDirection);
                float value = startValue + (dragMode == 0 ? distance * .016f :
                    dragMode == 1 ? distance * .0015f : distance * .85f);
                dragController.SetGeometryValue(dragPart, dragMode, dragAxis, value);
                if (onValueChanged != null)
                    onValueChanged(dragPart, dragMode, dragAxis,
                        dragController.GetGeometryValue(dragPart, dragMode, dragAxis));
                return;
            }
            if (!Input.GetMouseButtonDown(0) ||
                (EventSystem.current != null &&
                 EventSystem.current.IsPointerOverGameObject())) return;

            float bestDistance = 20f * 20f;
            int selectedPart = -1, selectedAxis = -1;
            for (int part = 0; part < 2; part++)
            {
                if (!active[part]) continue;
                for (int axis = 0; axis < 3; axis++)
                {
                    float d = (mouse - ends[part, axis]).sqrMagnitude;
                    if (d < bestDistance)
                    { bestDistance = d; selectedPart = part; selectedAxis = axis; }
                }
            }
            if (selectedPart < 0) return;
            dragPart = selectedPart;
            dragAxis = selectedAxis;
            dragMode = currentModes[dragPart];
            dragController = currentController;
            startMouse = mouse;
            startValue = currentController.GetGeometryValue(dragPart, dragMode, dragAxis);
            dragDirection = (ends[dragPart, dragAxis] - centers[dragPart]).normalized;
        }

        internal void DrawOverlay()
        {
            if (!MakerAPI.InsideAndLoaded || currentModes == null) return;
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.depth = -10000;
            try
            {
                for (int part = 0; part < 2; part++)
                {
                    if (active[part])
                    {
                        GUI.color = Color.white;
                        string heading = (part == 0 ? "Penis" : "Balls") +
                            " / " + ModeNames[Mathf.Clamp(currentModes[part], 0, 2)];
                        GUI.Label(new Rect(centers[part].x + 9f,
                            centers[part].y + 12f, 160f, 23f), heading);
                        for (int axis = 0; axis < 3; axis++)
                        {
                            DrawLine(centers[part], ends[part, axis], Colors[axis], 4f);
                            GUI.color = Colors[axis];
                            GUI.DrawTexture(new Rect(ends[part, axis].x - 9f,
                                ends[part, axis].y - 9f, 18f, 18f), Texture2D.whiteTexture);
                            GUI.color = Color.white;
                            GUI.Label(new Rect(ends[part, axis].x - 4f,
                                ends[part, axis].y - 11f, 25f, 24f), AxisNames[axis]);
                        }
                    }
                    else if (missingBone[part])
                    {
                        GUI.color = new Color(1f, .85f, .35f, 1f);
                        GUI.Label(new Rect(20f, 75f + part * 24f, 540f, 24f),
                            (part == 0 ? "Penis" : "Balls") +
                            " axis: target bone missing (check UncensorSelector & Maker bone config)");
                    }
                }
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.depth = previousDepth;
            }
        }

        private static void DrawLine(Vector2 begin, Vector2 end, Color color, float width)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Vector2 delta = end - begin;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, begin);
            GUI.DrawTexture(new Rect(begin.x, begin.y - width * .5f,
                delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
        }

        internal void Dispose()
        {
            dragPart = -1;
            dragController = null;
            currentController = null;
            active[0] = active[1] = false;
        }
    }
}
