// State containers for the linked production editing/lock methods. No copy of
// their control flow is kept here. The full Unity component remains a net35 build.
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace ExpressionControl {
    public partial class ExpressionControl {
        internal XmlMgr xml;
        internal Program currentProgram;
        internal int currentProgramNum, selectedProgramUnit;
        internal static bool isStudio;

        public partial class FemaleData {
            public ChaControl female;
            public FaceListCtrl faceListCtrl;
            public Studio.OCIChar ociFemale;
            public Expression expression, expressionRealtime;
            public Coroutine coEyeOpenSlide, coMouthOpenSlide, coHohoAkaSlide;
            public bool ignoreMouthOpen, ignoreMouthLoad;
            public bool lockExpression, lockEyebrowPtn, lockEyePtn, lockMouthPtn;
            public bool lockEyeOpen, lockMouthOpen, lockTear, lockHohoAka, lockHighlight, lockBlink;
        }
    }

    internal static class ObjectExtension {
        // .NET 9 removed the runtime BinaryFormatter implementation used by the
        // net35 plugin. Replace only that serialization boundary in this host.
        public static T DeepClone<T>(this T source) {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (MemoryStream stream = new MemoryStream()) {
                serializer.Serialize(stream, source);
                stream.Position = 0;
                return (T)serializer.Deserialize(stream);
            }
        }
    }
}
