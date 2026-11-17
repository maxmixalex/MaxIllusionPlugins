using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;

namespace FemboyMode
{
    /// <summary>
    /// Persists a female-card-only character marker, without changing ChaControl.sex.
    /// </summary>
    public sealed class FemboyCharacterController : CharaCustomFunctionController
    {
        private const string FlagKey = "IsFemboy";
        private bool isFemboy;

        public bool IsFemboy
        {
            get { return ChaControl != null && ChaControl.sex == 1 && isFemboy; }
            set { isFemboy = ChaControl != null && ChaControl.sex == 1 && value; }
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            // Do not discard unsaved Maker edits during a state-maintaining reload.
            if (maintainState)
                return;

            isFemboy = false;
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
            data.version = 1;
            data.data.Add(FlagKey, true);
            SetExtendedData(data);
        }
    }
}
