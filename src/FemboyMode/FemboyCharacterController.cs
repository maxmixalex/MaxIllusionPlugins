using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Persists a female-card-only character marker, without changing ChaControl.sex.
    /// </summary>
    public sealed partial class FemboyCharacterController : CharaCustomFunctionController
    {
        private const string FlagKey = "IsFemboy";
        private const string GeometryXKey = "BodyBoneScaleX";
        private const string GeometryYKey = "BodyBoneScaleY";
        private const string GeometryZKey = "BodyBoneScaleZ";
        private bool isFemboy;
        public bool IsInitialized { get; private set; }
        private float geometryScaleX = 1f, geometryScaleY = 1f, geometryScaleZ = 1f;

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
            // Do not discard unsaved Maker edits during a state-maintaining reload.
            if (maintainState)
                return;

            RestoreMaleGeometry();
            IsInitialized = true;
            isFemboy = false;
            geometryScaleX = geometryScaleY = geometryScaleZ = 1f;
            if (ChaControl == null || ChaControl.sex != 1)
                return;

            PluginData data = GetExtendedData();
            object value;
            if (data != null && data.data != null &&
                data.data.TryGetValue(FlagKey, out value) &&
                value is bool && (bool)value)
            {
                isFemboy = true;
            }
            if (data != null && data.data != null)
            {
                geometryScaleX = ReadScale(data, GeometryXKey);
                geometryScaleY = ReadScale(data, GeometryYKey);
                geometryScaleZ = ReadScale(data, GeometryZKey);
            }
            ApplyMaleGeometry();
        }

        private static float ReadScale(PluginData data, string key)
        {
            object value;
            if (!data.data.TryGetValue(key, out value))
                return 1f;

            // Old and third-party cards can contain malformed numeric values.
            // Never send NaN or infinity into a bone Transform.
            if (value is float) return FemboyRules.SafeBoneScale((float)value);
            if (value is double) return FemboyRules.SafeBoneScale((float)(double)value);
            return 1f;
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            if (!IsFemboy)
            {
                // A normal card does not need extra data.
                SetExtendedData(null);
                return;
            }

            PluginData data = new PluginData();
            data.version = 2;
            data.data.Add(FlagKey, true);
            if (!Mathf.Approximately(geometryScaleX, 1f))
                data.data.Add(GeometryXKey, geometryScaleX);
            if (!Mathf.Approximately(geometryScaleY, 1f))
                data.data.Add(GeometryYKey, geometryScaleY);
            if (!Mathf.Approximately(geometryScaleZ, 1f))
                data.data.Add(GeometryZKey, geometryScaleZ);
            SetExtendedData(data);
        }
    }
}
