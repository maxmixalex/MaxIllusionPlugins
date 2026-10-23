// Explicit boundaries for the production View. XML, editor operations, naming
// dialogs, composition and callbacks are real; applying a value to a live face,
// animation state, random timer and native mount are represented by recorders.
using System.Collections.Generic;

namespace ExpressionControl {
    public partial class ExpressionControl {
        private const string MaximumUiMountId = "ExpressionControl-headless";
        internal readonly List<FemaleData> femaleList = new List<FemaleData>();
        internal int currentFemaleNum, selectedScenePreset = -1, TimerResets;
        internal bool isSortExpression, isSortExpressionModify;
        private readonly Dictionary<int, string> eyeBrowPtnNameDict = new Dictionary<int, string>();
        private readonly Dictionary<int, string> eyesPtnNameDict = new Dictionary<int, string>();
        private readonly Dictionary<int, string> mouthPtnNameDict = new Dictionary<int, string>();

        private void SetProgramTimer(FemaleData female) { TimerResets++; }
        internal void TestReadError(System.Exception error) { presetReadError = error; }
        internal void TestReload() { ReloadMaximumUiPresets(); }
        internal bool TestDeleteExpression(string guid) { return XML_DeleteExpression(guid); }

        public partial class FemaleData {
            public Program program = new Program();
            public float programTimer = 5f, programTimerNow = 3f;
            public bool programValid, lockEyeY, lockEyeSmall;
            public string fullName { get { return female.chaFile.parameter.fullname; } }
            public int maxEyebrowPtn = 5, maxEyesPtn = 10, maxMouthPtn = 15;
            public int Reads, StateUpdates, AppliedParts;
            public string LastAction;
            public PartType LastPart;
            public float LastValue;
            public Expression AppliedExpression;

            public void Update() { Reads++; }
            public void UpdateState(bool force, string motion, bool fera) { StateUpdates++; }
            public void SetExpression(Expression value) { AppliedExpression = value; }
            public void SetProgram(Program value) { program = value; }
            public void SetPart(PartType part, int value) { AppliedParts++; LastPart = part; LastValue = value; }
            public void PrevPart(PartType part) { LastAction = "prev"; LastPart = part; }
            public void NextPart(PartType part) { LastAction = "next"; LastPart = part; }
            public void ChangeOpen(PartType part, float value) { LastAction = "open"; LastPart = part; LastValue = value; }
            public void ChangeEyeY(float value) { LastAction = "eye-y"; LastValue = value; }
            public void ChangeEyeSmall(float value) { LastAction = "eye-small"; LastValue = value; }
            public void ChangeHighlight(bool value) { LastAction = "highlight"; LastValue = value ? 1f : 0f; }
            public void ChangeBlink(bool value) { LastAction = "blink"; LastValue = value ? 1f : 0f; }
            public void ChangeTear(byte value) { LastAction = "tear"; LastValue = value; }
            public void ChangeHohoAka(float value) { LastAction = "blush"; LastValue = value; }
            public void ChangeBlinkRate(float value) { LastAction = "blink-rate"; LastValue = value; }
            public void ChangeBlinkSpeed(float value) { LastAction = "blink-speed"; LastValue = value; }
        }
    }
}
