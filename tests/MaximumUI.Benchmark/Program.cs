using System;
using System.Diagnostics;
using MaximumUI;

internal static class Program {
    private sealed class Sample {
        public double Ms;
        public long Bytes;
        public int Builds;
        public long Layouts;
    }

    private static void Main() {
#if FIXED
        int modes = 3;
        string version = "fixed";
#else
        int modes = 1;
        string version = "baseline";
#endif
        Console.WriteLine("Assembly: " + typeof(LayoutEngine).Assembly.Location);
        Console.WriteLine("Version: " + typeof(LayoutEngine).Assembly.GetName().Version);
        Console.WriteLine("1000 labels, 150 frames, 2 events/frame; median of 5 after 3 warmups; build/layout only, not Unity FPS.");
        for (int mode = 0; mode < modes; mode++) {
            for (int warmup = 0; warmup < 3; warmup++) Run(25, mode);
            Sample[] samples = new Sample[5];
            for (int trial = 0; trial < samples.Length; trial++) samples[trial] = Run(150, mode);
            Array.Sort(samples, delegate(Sample a, Sample b) { return a.Ms.CompareTo(b.Ms); });
            Sample median = samples[samples.Length / 2];
            Console.WriteLine(version + " mode=" + mode + " median_ms=" + median.Ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " bytes=" + median.Bytes + " builds=" + median.Builds + " layouts=" + median.Layouts);
        }
    }

    private static Sample Run(int frames, int mode) {
        LayoutEngine engine = new LayoutEngine();
#if FIXED
        LayoutCache cache = new LayoutCache(engine);
        ViewBuildSchedule schedule = new ViewBuildSchedule();
        MountOptions options = new MountOptions { BuildEveryFrame = mode != 2 };
#endif
        Widget tree = null;
        MuiRect viewport = new MuiRect(0f, 0f, 520f, 620f);
        Sample sample = new Sample();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch watch = Stopwatch.StartNew();
        for (int frame = 0; frame < frames; frame++) {
            for (int guiEvent = 0; guiEvent < 2; guiEvent++) {
                bool build = true;
#if FIXED
                build = mode == 0 || schedule.ShouldBuild(options, frame, tree != null);
#endif
                if (build) {
                    VStack stack = new VStack(2f);
                    for (int i = 0; i < 1000; i++) stack.Add(new Label("Benchmark row " + i));
                    tree = stack;
                    sample.Builds++;
#if FIXED
                    schedule.CompleteBuild(frame, schedule.Revision);
                    cache.Invalidate();
#endif
                }
#if FIXED
                LayoutNode layout = mode == 0 ? engine.Layout(tree, viewport) : cache.Layout(tree, viewport, "0");
#else
                LayoutNode layout = engine.Layout(tree, viewport);
#endif
                if (layout.Children.Count != 1000) throw new InvalidOperationException("Benchmark dropped rows");
                if (layout.Children[999].Path != "0.999") throw new InvalidOperationException("Benchmark lost widget identity");
            }
        }
        watch.Stop();
        sample.Ms = watch.Elapsed.TotalMilliseconds;
        sample.Bytes = GC.GetAllocatedBytesForCurrentThread() - before;
#if FIXED
        sample.Layouts = mode == 0 ? frames * 2 : cache.LayoutPassCount;
#else
        sample.Layouts = frames * 2;
#endif
        int expectedBuilds = mode == 0 ? frames * 2 : mode == 1 ? frames : 1;
        if (sample.Builds != expectedBuilds || sample.Layouts != expectedBuilds) throw new InvalidOperationException("Unexpected scheduling behavior");
        return sample;
    }
}
