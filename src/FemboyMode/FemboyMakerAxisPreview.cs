using KKAPI.Maker;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Maker-only noninteractive axis preview. Vanilla cmpGuid is bound to
    /// accessory slots; using it directly would move the selected accessory.
    /// This independent visual preview never changes accessory state.
    /// </summary>
    internal sealed class FemboyMakerAxisPreview
    {
        private readonly GameObject[] roots = new GameObject[2];
        private readonly Material[] materials = new Material[3];
        private static readonly Color[] Colors =
        {
            new Color(1f, .20f, .22f, 1f),
            new Color(.20f, .92f, .28f, 1f),
            new Color(.22f, .55f, 1f, 1f)
        };
        private static readonly Vector3[] Directions =
        {
            Vector3.right, Vector3.up, Vector3.forward
        };
        private bool warned;

        internal void Tick(FemboyCharacterController controller, bool[] visible)
        {
            if (!MakerAPI.InsideAndLoaded || controller == null || visible == null)
            {
                HideAll();
                return;
            }
            for (int part = 0; part < 2; part++)
            {
                if (!visible[part])
                {
                    if (roots[part] != null) roots[part].SetActive(false);
                    continue;
                }
                Transform target = controller.GetGeometryBone(part);
                if (target == null)
                {
                    if (roots[part] != null) roots[part].SetActive(false);
                    continue;
                }
                if (roots[part] == null) CreateGuide(part);
                if (roots[part] == null) continue;
                roots[part].SetActive(true);
                roots[part].transform.position = target.position;
                roots[part].transform.rotation = target.rotation;
            }
        }

        private void CreateGuide(int part)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                if (!warned && FemboyModePlugin.Log != null)
                    FemboyModePlugin.Log.LogWarning("No compatible shader for Maker axis preview.");
                warned = true;
                return;
            }
            GameObject root = new GameObject(part == 0
                ? "FemboyMaker_Penis_Axis" : "FemboyMaker_Balls_Axis");
            root.hideFlags = HideFlags.DontSave;
            for (int axis = 0; axis < 3; axis++)
            {
                if (materials[axis] == null)
                {
                    materials[axis] = new Material(shader);
                    materials[axis].color = Colors[axis];
                }
                GameObject line = new GameObject(axis == 0 ? "X" : axis == 1 ? "Y" : "Z");
                line.hideFlags = HideFlags.DontSave;
                line.transform.SetParent(root.transform, false);
                LineRenderer r = line.AddComponent<LineRenderer>();
                r.useWorldSpace = false;
                r.material = materials[axis];
                r.positionCount = 2;
                r.SetPosition(0, Vector3.zero);
                r.SetPosition(1, Directions[axis] * .065f);
                r.startWidth = .0025f;
                r.endWidth = .0011f;
                r.receiveShadows = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            roots[part] = root;
        }

        private void HideAll()
        {
            foreach (GameObject root in roots)
                if (root != null && root.activeSelf) root.SetActive(false);
        }

        internal void Dispose()
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null) UnityEngine.Object.Destroy(roots[i]);
                roots[i] = null;
            }
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null) UnityEngine.Object.Destroy(materials[i]);
                materials[i] = null;
            }
        }
    }
}
