using System.Collections.Generic;
using ClothingCompression;
using MaximumUI;

// An anonymous fixture for the preview exporter. This builds the actual
// production view against external game doubles and never reads a config file.
public static class CompressionEditorPreview {
    public static Widget CreatePreview() {
        ChaControl character = new ChaControl();
        character.chaFile.parameter.fullname = "示例角色";
        CompressionController controller = new CompressionController { ChaControl = character, ChaFileControl = character.chaFile };
        ItemDescriptor socks = new ItemDescriptor { Kind = BoundItemKind.Clothing, Category = 7, Id = 101, Slot = 6, Name = "黑色长筒袜" };
        ItemDescriptor gloves = new ItemDescriptor { Kind = BoundItemKind.Clothing, Category = 5, Id = 102, Slot = 4, Name = "长手套" };
        ItemDescriptor band = new ItemDescriptor { Kind = BoundItemKind.Accessory, Category = 121, Id = 103, Slot = 2, Name = "腿环" };
        DeformationEntry thigh = DeformationEntry.Create(DeformationKind.Thigh);
        ItemCatalog.Bind(thigh, socks);
        thigh.Depth = 0.012f;
        thigh.ProximalBulge = 0.006f;
        thigh.DistalBulge = 0.004f;
        DeformationEntry arm = DeformationEntry.Create(DeformationKind.UpperArm);
        ItemCatalog.Bind(arm, gloves);
        DeformationEntry knee = DeformationEntry.Create(DeformationKind.Knee);
        ItemCatalog.Bind(knee, band);
        controller.Entries.Add(thigh);
        controller.Entries.Add(arm);
        controller.Entries.Add(knee);
        CompressionEditorView view = new CompressionEditorView("Clothing Compression · 形变编辑器") {
            Controller = controller,
            BindingItems = new List<ItemDescriptor> { socks, gloves, band },
            ImportEntries = new List<DeformationEntry> { knee.Clone(true), arm.Clone(true) }
        };
        return view.Build(null);
    }
}
