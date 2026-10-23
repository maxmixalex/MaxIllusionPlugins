import copy
import importlib.util
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location("render_gallery", Path(__file__).resolve().parents[1] / "render_gallery.py")
gallery = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(gallery)


class GalleryCoverageTests(unittest.TestCase):
    def fixture(self):
        previews = []
        for palette, size in (("Metro Light", "standard"), ("Metro Dark", "standard"),
                              ("Metro Light", "minimum"), ("Metro Light", "large-text")):
            previews.append(dict(file=f"page-{palette}-{size}.svg", page="controls", palette=palette,
                                 size=size, state="default", scrollAreas=[]))
        return dict(galleryPages=["controls"], previews=previews, errors=[])

    def test_all_pages_need_both_themes_minimum_and_large_text(self):
        manifest = self.fixture()
        self.assertEqual(gallery.validate_manifest(manifest), [])
        manifest["previews"].pop()
        self.assertTrue(any("large-text" in message for message in gallery.validate_manifest(manifest)))

    def test_catalog_states_cannot_silently_disappear(self):
        manifest = self.fixture()
        manifest["galleryCases"] = [dict(file="disabled.svg", page="controls", state="disabled", size="standard", palette="Metro Light")]
        self.assertTrue(any("Missing catalog case" in message for message in gallery.validate_manifest(manifest)))
        manifest["previews"].append(dict(manifest["galleryCases"][0], scrollAreas=[]))
        self.assertEqual(gallery.validate_manifest(manifest), [])

    def test_virtual_list_cannot_build_all_data_rows_for_a_small_viewport(self):
        manifest = self.fixture()
        source = manifest["previews"][0]
        source["scrollAreas"] = [dict(Path="0.list", Key="list", Maximum=0, ViewHeight=400, ContentHeight=400,
                                     Offset=0, Virtual=True, VisibleHeight=400, VisibleContentStart=0,
                                     RowHeight=32, ItemCount=10000, BuiltRows=10000)]
        self.assertTrue(any("visible-row budget" in message for message in gallery.validate_manifest(manifest)))
        source["scrollAreas"][0]["BuiltRows"] = 16
        self.assertEqual(gallery.validate_manifest(manifest), [])

    def test_regular_scroll_checks_continuous_coverage_not_only_endpoints(self):
        manifest = self.fixture()
        source = manifest["previews"][0]
        source["scrollAreas"] = [dict(Path="0.content", Key="content", Maximum=800,
                                     ViewHeight=200, ContentHeight=1000, Offset=0, Virtual=False,
                                     VisibleHeight=200, VisibleContentStart=0)]
        bottom = copy.deepcopy(source)
        bottom.update(file=source["file"][:-4] + "-scroll1-1.svg", scrollOf=source["file"])
        bottom["scrollAreas"][0]["Offset"] = 800
        bottom["scrollAreas"][0]["VisibleContentStart"] = 800
        manifest["previews"].append(bottom)
        self.assertTrue(any("not fully covered" in message for message in gallery.validate_manifest(manifest)))
        for index, offset in enumerate((200, 400, 600), 2):
            sample = copy.deepcopy(bottom)
            sample["file"] = source["file"][:-4] + f"-scroll1-{index}.svg"
            sample["scrollAreas"][0]["Offset"] = offset
            sample["scrollAreas"][0]["VisibleContentStart"] = offset
            manifest["previews"].append(sample)
        self.assertEqual(gallery.validate_manifest(manifest), [])

    def test_virtual_list_requires_middle_sample(self):
        manifest = self.fixture()
        source = manifest["previews"][0]
        source["scrollAreas"] = [dict(Path="0.list", Key="list", Maximum=100000,
                                     ViewHeight=400, ContentHeight=100400, Offset=0, Virtual=True,
                                     VisibleHeight=400, VisibleContentStart=0)]
        for index, offset in enumerate((100000, 50000)):
            sample = copy.deepcopy(source)
            sample.update(file=source["file"][:-4] + f"-scroll1-{index}.svg", scrollOf=source["file"])
            sample["scrollAreas"][0]["Offset"] = offset
            sample["scrollAreas"][0]["VisibleContentStart"] = offset
            manifest["previews"].append(sample)
            messages = gallery.validate_manifest(manifest)
            self.assertEqual(bool(messages), index == 0)

    def test_hidden_nested_list_offsets_do_not_count_as_visual_coverage(self):
        manifest = self.fixture()
        source = manifest["previews"][0]
        source["scrollAreas"] = [dict(Path="0.content.list", Key="list", Maximum=100000,
                                     ViewHeight=160, ContentHeight=100160, Offset=0, Virtual=True,
                                     VisibleHeight=0, VisibleContentStart=0)]
        for index, offset in enumerate((0, 50000, 100000)):
            sample = copy.deepcopy(source)
            sample.update(file=f"nested-{index}.svg", scrollOf=source["file"])
            sample["scrollAreas"][0]["Offset"] = offset
            manifest["previews"].append(sample)
        self.assertTrue(any("top/middle/bottom" in message for message in gallery.validate_manifest(manifest)))
        for sample in manifest["previews"][-3:]:
            sample["scrollAreas"][0]["VisibleHeight"] = 160
        self.assertEqual(gallery.validate_manifest(manifest), [])

    def test_partial_ancestor_clipping_checks_only_visible_content_interval(self):
        manifest = self.fixture()
        source = manifest["previews"][0]
        source["scrollAreas"] = [dict(Path="0.content.nested", Key="nested", Maximum=100,
                                     ViewHeight=200, ContentHeight=300, Offset=0, Virtual=False,
                                     VisibleHeight=100, VisibleContentStart=0)]
        sample = copy.deepcopy(source)
        sample.update(file="bottom.svg", scrollOf=source["file"])
        sample["scrollAreas"][0].update(Offset=100, VisibleContentStart=200)
        manifest["previews"].append(sample)
        self.assertTrue(any("not fully covered" in message for message in gallery.validate_manifest(manifest)))
        sample["scrollAreas"][0].update(VisibleContentStart=100, VisibleHeight=200)
        self.assertEqual(gallery.validate_manifest(manifest), [])

    def test_export_errors_and_duplicate_artifacts_fail(self):
        manifest = self.fixture()
        manifest["errors"].append("Production control outside window")
        manifest["previews"].append(copy.deepcopy(manifest["previews"][0]))
        errors = gallery.validate_manifest(manifest)
        self.assertIn("Production control outside window", errors)
        self.assertTrue(any("Duplicate" in message for message in errors))


class RenderedGlyphTests(unittest.TestCase):
    def root(self, **attributes):
        root = ET.Element("{http://www.w3.org/2000/svg}svg")
        attributes = {"id": "label", "data-content-box": "0,0,100,24", "data-visible-box": "0,0,100,24",
                      "data-can-clip": "false", "data-layer": "0", "data-widget-path": "0.label", **attributes}
        ET.SubElement(root, "{http://www.w3.org/2000/svg}text", attributes).text = "Label"
        return root

    def test_actual_renderer_bounds_can_reject_measurement_that_would_fit(self):
        root = self.root()
        self.assertEqual(gallery.verify_rendered_text(root, "label,1,4,98,16", "frame"), [])
        result = gallery.verify_rendered_text(root, "label,1,4,105,16", "frame")
        self.assertEqual(result[0]["Kind"], "rendered-text-overflow")

    def test_hidden_content_and_scrollable_input_clipping_are_not_false_failures(self):
        query = "label,1,4,180,30"
        self.assertEqual(gallery.verify_rendered_text(self.root(**{"data-visible-box": "0,0,0,0"}), query, "frame"), [])
        self.assertEqual(gallery.verify_rendered_text(self.root(**{"data-can-clip": "true"}), query, "frame"), [])

    def test_overlay_does_not_overlap_background_but_same_control_text_does(self):
        root = self.root()
        second = ET.SubElement(root, "{http://www.w3.org/2000/svg}text", dict(root[0].attrib))
        second.set("id", "value")
        second.text = "Value"
        query = "label,1,4,98,16\nvalue,80,4,20,16"
        self.assertEqual(gallery.verify_rendered_text(root, query, "frame")[0]["Kind"], "rendered-text-overlap")
        second.set("data-layer", "1")
        self.assertEqual(gallery.verify_rendered_text(root, query, "frame"), [])


if __name__ == "__main__":
    unittest.main()
