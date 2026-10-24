namespace ClothingCompression {
    // Shared by the native Maker controls and the MaximumUI editor.
    internal static class CompressionEditorSupport {
        internal static readonly string[] CoordinateNames = { "学生服（校内）", "学生服（放学）", "体操服", "泳装", "社团", "私服", "睡衣" };
        internal static readonly DeformationKind[] Kinds = { DeformationKind.Thigh, DeformationKind.LowerThigh, DeformationKind.Knee, DeformationKind.UpperArm, DeformationKind.Arm };

        internal static string KindName(DeformationKind kind) {
            return kind == DeformationKind.UpperArm ? "大臂" : kind == DeformationKind.Arm ? "前臂" :
                kind == DeformationKind.Knee ? "膝部" : kind == DeformationKind.LowerThigh ? "整腿定位" : "大腿";
        }

        internal static bool IsArm(DeformationKind kind) { return kind == DeformationKind.Arm || kind == DeformationKind.UpperArm; }

        internal static string FormatEntryLabel(int index, DeformationEntry entry) {
            string item = entry == null || entry.BindingKind == BoundItemKind.None || string.IsNullOrEmpty(entry.BindingName) ? "未绑定" : entry.BindingName;
            string kind = entry != null && entry.Kind == DeformationKind.LowerThigh ? "整腿" : KindName(entry == null ? DeformationKind.Thigh : entry.Kind);
            return (index + 1) + " " + item + "（" + kind + "）";
        }

        internal static void SetKind(DeformationEntry entry, DeformationKind kind) {
            if (entry.Kind == kind) return;
            entry.Kind = kind;
            entry.Position = kind == DeformationKind.Thigh ? 0.32f : kind == DeformationKind.LowerThigh ? 0.58f : kind == DeformationKind.Knee ? 0f : kind == DeformationKind.UpperArm ? 0.55f : 0.68f;
            entry.Width = kind == DeformationKind.Knee ? 0.14f : kind == DeformationKind.LowerThigh ? 0.12f : kind == DeformationKind.Thigh ? 0.09f : 0.10f;
            if (IsArm(kind)) entry.DistalBulge = 0f;
        }
    }
}
