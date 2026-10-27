# Performance measurement

Measured 2026-10-04 in Unity 6000.4.4f1. Windows graphical Editor (native standard Hierarchy), 12th Gen Intel Core i5-12400F, 16222 MB RAM. Other user Editor projects remained open; this is a local Editor comparison, not a dedicated laboratory benchmark.

Scenes contain exactly 1,000 or 10,000 GameObjects, 100-node groups and BoxCollider on every tenth object. The scenes are generated under Assets/HierarchyToolkitBenchmarks, outside the distributed tool. No render-pipeline-specific work is involved. Hierarchy utility window: 480 × 640 GUI points; normal theme/configuration, default visible-component limit.

Before each scenario the native expansion state is reset; scrolling opens the first 100-node group so the tree really exceeds the viewport. The same replay runs disabled then enabled: scroll wheel, native right/left arrow expansion, and selection/framing of the same object sequence. Each scenario runs 40 Editor updates, excluding the first 10 as warm-up. Native HostView GUI delegate is instrumented temporarily and restored. Measurements cover its original standard Hierarchy GUI plus row extension callbacks, not the separate UIElements navigation container or delayed cache preparation time.

| Objects | Operation | Disabled mean / p95 ms | Enabled mean / p95 ms | Disabled / enabled frame GC bytes | GUI samples off / on |
| --- | --- | --- | --- | --- | --- |
| 1000 | scroll | 0.122 / 0.292 | 0.121 / 0.252 | 9321 / 8793 | 90 / 90 |
| 1000 | expand-collapse | 0.208 / 0.445 | 0.214 / 0.396 | 342533 / 344971 | 90 / 90 |
| 1000 | selection | 0.239 / 0.338 | 0.298 / 0.507 | 292250 / 293608 | 60 / 60 |
| 10000 | scroll | 0.122 / 0.263 | 0.128 / 0.314 | 25231 / 8799 | 90 / 90 |
| 10000 | expand-collapse | 0.426 / 1.063 | 0.424 / 0.893 | 326277 / 358476 | 90 / 90 |
| 10000 | selection | 1.125 / 2.716 | 1.091 / 2.482 | 371516 / 323593 | 60 / 60 |

## GC scope and validity

GC.GetAllocatedBytesForCurrentThread was calibrated with a real 8 KB allocation. Unity Mono returned no increase, so that counter is **unsupported**, and per-GUI allocation values are recorded as -1 rather than zero. The Unity ProfilerRecorder **GC Allocated In Frame** counter is valid; the table reports its mean per sampled Editor update. It includes the test runner, native Editor, cache preparation and other windows in this Editor process. It cannot isolate toolkit-only allocations. Do not interpret it as zero-allocation drawing, or subtract the noisy frame values to claim an exact per-row allocation budget.

## Conclusions and limits

There is measurable CPU overhead. Results do not demonstrate zero impact or superiority to another asset. Short runs, asynchronous Editor work, ordering and other open projects produce noise; a lower enabled result for a workload is not evidence of a speedup. Native tree drawing and frame-level GC were measured; navigation-container draw cost, delayed preparation CPU time, long-session memory, real human scrolling and profiling on macOS remain unmeasured. No asset scans, file I/O or LINQ occur in the row draw callback by source inspection.

The benchmark itself records GUI samples and fails if it observes none. Initial batchmode/nographics attempts correctly failed because they did not execute the native GUI; they are retained outside the repository as diagnostic evidence. Final graphical test results are recorded in Validation.md.

Raw results: [Performance-data.json](Performance-data.json). Reproduce with repository PerformanceGUI tests in a graphical Editor, filtered by category; do not use batchmode/nographics for this category. Prefer a longer alternating-order run on an otherwise idle machine before establishing a production budget.
