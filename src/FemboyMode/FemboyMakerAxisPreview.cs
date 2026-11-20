using System;
using System.Collections.Generic;
using KKAPI.Maker;
using UnityEngine.EventSystems;
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
        private readonly Dictionary<Collider, HitAxis> handles = new Dictionary<Collider, HitAxis>();
        private int dragPart=-1,dragAxis,dragMode;
        private Vector2 startMouse,axisPixels;
        private float startValue,axisPixelLength;
        private FemboyCharacterController dragController;
        private Action<int,int,int,float> onValueChanged;
        private sealed class HitAxis { internal int Part,Axis; }

        internal void Tick(FemboyCharacterController controller, bool[] visible,
            int[] modes, Action<int,int,int,float> onChanged)
        {
            onValueChanged=onChanged;
            if (!MakerAPI.InsideAndLoaded || controller==null || visible==null || modes==null)
            {
                dragPart=-1; HideAll(); return;
            }
            for (int part=0;part<2;part++)
            {
                if (!visible[part])
                {
                    if (roots[part]!=null) roots[part].SetActive(false);
                    if (dragPart==part) dragPart=-1;
                    continue;
                }
                Transform bone=controller.GetGeometryBone(part);
                if (bone==null)
                {
                    if (roots[part]!=null) roots[part].SetActive(false);
                    if (dragPart==part) dragPart=-1;
                    continue;
                }
                if (roots[part]==null) CreateGuide(part);
                if (roots[part]==null) continue;
                roots[part].SetActive(true);
                roots[part].transform.position=bone.position;
                roots[part].transform.rotation=modes[part]==1 && bone.parent!=null
                    ? bone.parent.rotation : bone.rotation;
            }
            if (dragPart>=0 && (!visible[dragPart] || dragController!=controller ||
                                 dragMode!=modes[dragPart])) dragPart=-1;
            HandleInput(controller,visible,modes);
        }

        private static Camera GetMakerCamera()
        {
            if (Camera.main!=null && Camera.main.enabled) return Camera.main;
            Camera best=null;
            foreach(Camera cam in Camera.allCameras)
                if (cam.enabled && cam.targetTexture==null && (best==null || cam.depth>best.depth))
                    best=cam;
            return best;
        }

        private void HandleInput(FemboyCharacterController controller,bool[] visible,int[] modes)
        {
            if (dragPart>=0)
            {
                if (!Input.GetMouseButton(0) || dragController==null) { dragPart=-1; return; }
                Vector2 delta=(Vector2)Input.mousePosition-startMouse;
                Vector2 unit=axisPixels.sqrMagnitude>.0001f?axisPixels.normalized:Vector2.right;
                float change;
                if (dragMode==2)
                    change=Vector2.Dot(delta,new Vector2(-unit.y,unit.x))*.65f;
                else if (dragMode==0)
                    change=Vector2.Dot(delta,unit)*.0075f;
                else
                    change=Vector2.Dot(delta,unit)*.065f/Mathf.Max(axisPixelLength,8f);
                dragController.SetGeometryValue(dragPart,dragMode,dragAxis,startValue+change);
                if(onValueChanged!=null)
                    onValueChanged(dragPart,dragMode,dragAxis,
                        dragController.GetGeometryValue(dragPart,dragMode,dragAxis));
                return;
            }
            if (!Input.GetMouseButtonDown(0) ||
                (EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject()))
                return;
            Camera cam=GetMakerCamera();
            if (cam==null)return;
            RaycastHit[] hits=Physics.RaycastAll(
                cam.ScreenPointToRay(Input.mousePosition),100f);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(RaycastHit hit in hits)
            {
                HitAxis selected;
                if(hit.collider==null || !handles.TryGetValue(hit.collider,out selected) ||
                   !visible[selected.Part] || roots[selected.Part]==null ||
                   !roots[selected.Part].activeInHierarchy)continue;
                dragPart=selected.Part;dragAxis=selected.Axis;dragMode=modes[dragPart];
                dragController=controller;
                startMouse=(Vector2)Input.mousePosition;
                startValue=controller.GetGeometryValue(dragPart,dragMode,dragAxis);
                GameObject root=roots[dragPart];
                Vector3 origin=root.transform.position;
                Vector3 direction=root.transform.TransformDirection(Directions[dragAxis]);
                Vector3 a=cam.WorldToScreenPoint(origin);
                Vector3 b=cam.WorldToScreenPoint(origin+direction*.065f);
                axisPixels=new Vector2(b.x-a.x,b.y-a.y);
                axisPixelLength=axisPixels.magnitude;
                break;
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
                GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = "Drag"+line.name;
                tip.hideFlags = HideFlags.DontSave;
                tip.transform.SetParent(root.transform,false);
                tip.transform.localPosition = Directions[axis]*.065f;
                tip.transform.localScale = Vector3.one*.009f;
                Renderer sphere=tip.GetComponent<Renderer>();
                if (sphere!=null) sphere.sharedMaterial=materials[axis];
                Collider collider=tip.GetComponent<Collider>();
                if (collider!=null) handles.Add(collider,new HitAxis {Part=part,Axis=axis});
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
            dragPart=-1;
            handles.Clear();
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
