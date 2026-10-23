using UnityEngine;

namespace MaximumUI {
    public sealed class MaximumUIHost : MonoBehaviour {
        private void Update() {
            MaximumUIRuntime.Update();
        }

        private void OnGUI() {
            MaximumUIRuntime.OnGUI();
        }
    }
}
