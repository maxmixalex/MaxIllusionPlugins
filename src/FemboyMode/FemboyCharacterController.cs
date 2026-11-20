using System;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Card-specific marker and independent male-genital bone offsets.
    /// Keep old BodyBoneScaleX/Y/Z keys for cards made with 0.4.11.
    /// </summary>
    public sealed partial class FemboyCharacterController : CharaCustomFunctionController
    {
        private const string FlagKey = "IsFemboy";
        private const string GeometryXKey = "BodyBoneScaleX";
        private const string GeometryYKey = "BodyBoneScaleY";
        private const string GeometryZKey = "BodyBoneScaleZ";
        private static readonly string[] Axis = { "X", "Y", "Z" };
        private bool isFemboy;
        public bool IsInitialized { get; private set; }
        private float geometryScaleX = 1f, geometryScaleY = 1f, geometryScaleZ = 1f;
        private readonly float[] penisMove = new float[3];
        private readonly float[] penisRot = new float[3];
        private readonly float[] ballsScale = { 1f, 1f, 1f };
        private readonly float[] ballsMove = new float[3];
        private readonly float[] ballsRot = new float[3];

        public float GeometryScaleX
        {
            get { return geometryScaleX; }
            set { geometryScaleX = FemboyRules.SafeBoneScale(value); ApplyMaleGeometry(); }
        }
        public float GeometryScaleY
        {
            get { return geometryScaleY; }
            set { geometryScaleY = FemboyRules.SafeBoneScale(value); ApplyMaleGeometry(); }
        }
        public float GeometryScaleZ
        {
            get { return geometryScaleZ; }
            set { geometryScaleZ = FemboyRules.SafeBoneScale(value); ApplyMaleGeometry(); }
        }

        /// <summary>
        /// part: 0 penis, 1 balls; mode: 0 scale, 1 position, 2 rotation; axis: 0..2.
        /// Position is a local parent-bone offset in Unity units, rotation is local degrees.
        /// </summary>
        public float GetGeometryValue(int part, int mode, int axis)
        {
            if (axis < 0 || axis > 2 || part < 0 || part > 1 || mode < 0 || mode > 2)
                return mode == 0 ? 1f : 0f;
            if (part == 0 && mode == 0)
                return axis == 0 ? geometryScaleX : axis == 1 ? geometryScaleY : geometryScaleZ;
            float[] values = ValuesFor(part, mode);
            return values == null ? (mode == 0 ? 1f : 0f) : values[axis];
        }

        public void SetGeometryValue(int part, int mode, int axis, float value)
        {
            if (axis < 0 || axis > 2 || part < 0 || part > 1 || mode < 0 || mode > 2)
                return;
            value = SafeGeometryValue(mode, value);
            if (part == 0 && mode == 0)
            {
                if (axis == 0) geometryScaleX = value;
                else if (axis == 1) geometryScaleY = value;
                else geometryScaleZ = value;
            }
            else
            {
                float[] values = ValuesFor(part, mode);
                if (values == null) return;
                values[axis] = value;
            }
            ApplyMaleGeometry();
        }

        private float[] ValuesFor(int part, int mode)
        {
            if (part == 0) return mode == 1 ? penisMove : mode == 2 ? penisRot : null;
            return mode == 0 ? ballsScale : mode == 1 ? ballsMove : ballsRot;
        }

        private static float SafeGeometryValue(int mode, float value)
        {
            return mode == 0 ? FemboyRules.SafeBoneScale(value) :
                mode == 1 ? FemboyRules.SafeBoneOffset(value) :
                FemboyRules.SafeBoneRotation(value);
        }

        public bool IsFemboy
        {
            get { return ChaControl != null && ChaControl.sex == 1 && isFemboy; }
            set
            {
                isFemboy = ChaControl != null && ChaControl.sex == 1 && value;
                IsInitialized = true;
                ApplyMaleGeometry();
            }
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            if (maintainState) return;

            RestoreMaleGeometry();
            IsInitialized = true;
            isFemboy = false;
            geometryScaleX = geometryScaleY = geometryScaleZ = 1f;
            ResetValues(penisMove, 0f); ResetValues(penisRot, 0f);
            ResetValues(ballsScale, 1f); ResetValues(ballsMove, 0f);
            ResetValues(ballsRot, 0f);
            if (ChaControl == null || ChaControl.sex != 1) return;

            PluginData data = GetExtendedData();
            object value;
            if (data != null && data.data != null)
            {
                if (data.data.TryGetValue(FlagKey, out value) && value is bool && (bool)value)
                    isFemboy = true;
                geometryScaleX = ReadNumber(data, GeometryXKey, 0, 1f);
                geometryScaleY = ReadNumber(data, GeometryYKey, 0, 1f);
                geometryScaleZ = ReadNumber(data, GeometryZKey, 0, 1f);
                ReadValues(data, "PenisMove", penisMove, 1);
                ReadValues(data, "PenisRot", penisRot, 2);
                ReadValues(data, "BallsScale", ballsScale, 0);
                ReadValues(data, "BallsMove", ballsMove, 1);
                ReadValues(data, "BallsRot", ballsRot, 2);
            }
            ApplyMaleGeometry();
        }

        private static void ResetValues(float[] values, float fallback)
        {
            for (int i = 0; i < 3; ++i) values[i] = fallback;
        }

        private static float ReadNumber(PluginData data, string key, int mode, float fallback)
        {
            object value;
            if (data == null || data.data == null || !data.data.TryGetValue(key, out value))
                return fallback;
            if (value is float) return SafeGeometryValue(mode, (float)value);
            if (value is double) return SafeGeometryValue(mode, (float)(double)value);
            if (value is int) return SafeGeometryValue(mode, (int)value);
            return fallback;
        }

        private static void ReadValues(PluginData data, string prefix, float[] values, int mode)
        {
            for (int i = 0; i < 3; ++i)
                values[i] = ReadNumber(data, prefix + Axis[i], mode, mode == 0 ? 1f : 0f);
        }

        private static void WriteValues(PluginData data, string prefix, float[] values, float neutral)
        {
            for (int i = 0; i < 3; ++i)
                if (!Mathf.Approximately(values[i], neutral))
                    data.data.Add(prefix + Axis[i], values[i]);
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            if (!IsFemboy)
            {
                SetExtendedData(null);
                return;
            }
            PluginData data = new PluginData();
            data.version = 3;
            data.data.Add(FlagKey, true);
            if (!Mathf.Approximately(geometryScaleX, 1f)) data.data.Add(GeometryXKey, geometryScaleX);
            if (!Mathf.Approximately(geometryScaleY, 1f)) data.data.Add(GeometryYKey, geometryScaleY);
            if (!Mathf.Approximately(geometryScaleZ, 1f)) data.data.Add(GeometryZKey, geometryScaleZ);
            WriteValues(data, "PenisMove", penisMove, 0f);
            WriteValues(data, "PenisRot", penisRot, 0f);
            WriteValues(data, "BallsScale", ballsScale, 1f);
            WriteValues(data, "BallsMove", ballsMove, 0f);
            WriteValues(data, "BallsRot", ballsRot, 0f);
            SetExtendedData(data);
        }
    }
}
