using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class OverlayLayer {
        private readonly List<OverlayRequest> requests = new List<OverlayRequest>();

        public void Register(OverlayRequest request) {
            if (request == null) return;
            request.RegistrationOrder = requests.Count;
            requests.Add(request);
        }

        public void Render(RenderContext context) {
            requests.Sort(delegate(OverlayRequest a, OverlayRequest b) {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : a.RegistrationOrder.CompareTo(b.RegistrationOrder);
            });

            context.InputDialog = null;
            for (int i = 0; i < requests.Count; i++) {
                if (requests[i].ModalDialog != null) context.InputDialog = requests[i].ModalDialog;
            }
            for (int i = 0; i < requests.Count; i++) {
                OverlayRequest request = requests[i];
                if (request.Render != null) request.Render(context);
            }

            requests.Clear();
        }

        public void Clear() {
            requests.Clear();
        }
    }

    public sealed class OverlayRequest {
        public int SortOrder;
        public Rect Anchor;
        public Action<RenderContext> Render;
        internal Dialog ModalDialog;
        internal int RegistrationOrder;
    }
}
