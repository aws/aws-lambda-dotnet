# Multi-Concurrency Benchmark Results

## Summary

Comparison of three configurations for the AWS Lambda .NET runtime multi-concurrency implementation:

| Variant | Description |
|---------|-------------|
| **Both fixes** (`with_fix`) | Threadpool scaling + polling fix |
| **2 threads/vCPU** (`polling_conservative_tp`) | Conservative polling (2 threads per vCPU) |
| **Threadpool only** (`threadpool_only`) | Only the threading/threadpool change |
| **No fix** (`without_fix`) | Baseline (no changes) |

### Key Findings

- **No fix (baseline)**: Success rate drops at load=3 (only 2 polling tasks available)
- **Threadpool only**: Improves threading but still limited by polling task count
- **Both fixes**: 100% success up to MC, graceful degradation beyond
- **Both fixes** improvement: +26% to +93% success rate vs baseline
- **Threadpool only** improvement: +20% to +81% success rate vs baseline

## Charts

### 1. Success Rate vs Load

![1vCPU MC=8 sleep/1000ms](charts/success_cpu1_mc8_sleep_1000ms.png)

![1vCPU MC=8 sleep/100ms](charts/success_cpu1_mc8_sleep_100ms.png)

![1vCPU MC=8 cpu50/1000ms](charts/success_cpu1_mc8_cpu50_1000ms.png)

![1vCPU MC=8 cpu100/500ms](charts/success_cpu1_mc8_cpu100_500ms.png)

![1vCPU MC=16 sleep/1000ms](charts/success_cpu1_mc16_sleep_1000ms.png)

![1vCPU MC=16 sleep/100ms](charts/success_cpu1_mc16_sleep_100ms.png)

![1vCPU MC=16 cpu50/1000ms](charts/success_cpu1_mc16_cpu50_1000ms.png)

![1vCPU MC=16 cpu100/500ms](charts/success_cpu1_mc16_cpu100_500ms.png)

![1vCPU MC=32 sleep/1000ms](charts/success_cpu1_mc32_sleep_1000ms.png)

![1vCPU MC=32 sleep/100ms](charts/success_cpu1_mc32_sleep_100ms.png)

![1vCPU MC=32 cpu50/1000ms](charts/success_cpu1_mc32_cpu50_1000ms.png)

![1vCPU MC=32 cpu100/500ms](charts/success_cpu1_mc32_cpu100_500ms.png)

![2vCPU MC=16 sleep/1000ms](charts/success_cpu2_mc16_sleep_1000ms.png)

![2vCPU MC=16 sleep/100ms](charts/success_cpu2_mc16_sleep_100ms.png)

![2vCPU MC=16 cpu50/1000ms](charts/success_cpu2_mc16_cpu50_1000ms.png)

![2vCPU MC=16 cpu100/500ms](charts/success_cpu2_mc16_cpu100_500ms.png)

![2vCPU MC=32 sleep/1000ms](charts/success_cpu2_mc32_sleep_1000ms.png)

![2vCPU MC=32 sleep/100ms](charts/success_cpu2_mc32_sleep_100ms.png)

![2vCPU MC=32 cpu50/1000ms](charts/success_cpu2_mc32_cpu50_1000ms.png)

![2vCPU MC=32 cpu100/500ms](charts/success_cpu2_mc32_cpu100_500ms.png)

![2vCPU MC=64 sleep/1000ms](charts/success_cpu2_mc64_sleep_1000ms.png)

![2vCPU MC=64 sleep/100ms](charts/success_cpu2_mc64_sleep_100ms.png)

![2vCPU MC=64 cpu50/1000ms](charts/success_cpu2_mc64_cpu50_1000ms.png)

![2vCPU MC=64 cpu100/500ms](charts/success_cpu2_mc64_cpu100_500ms.png)

![4vCPU MC=32 sleep/1000ms](charts/success_cpu4_mc32_sleep_1000ms.png)

![4vCPU MC=32 sleep/100ms](charts/success_cpu4_mc32_sleep_100ms.png)

![4vCPU MC=32 cpu50/1000ms](charts/success_cpu4_mc32_cpu50_1000ms.png)

![4vCPU MC=32 cpu100/500ms](charts/success_cpu4_mc32_cpu100_500ms.png)

![4vCPU MC=64 sleep/1000ms](charts/success_cpu4_mc64_sleep_1000ms.png)

![4vCPU MC=64 sleep/100ms](charts/success_cpu4_mc64_sleep_100ms.png)

![4vCPU MC=64 cpu50/1000ms](charts/success_cpu4_mc64_cpu50_1000ms.png)

![4vCPU MC=64 cpu100/500ms](charts/success_cpu4_mc64_cpu100_500ms.png)

![4vCPU MC=128 sleep/1000ms](charts/success_cpu4_mc128_sleep_1000ms.png)

![4vCPU MC=128 sleep/100ms](charts/success_cpu4_mc128_sleep_100ms.png)

![4vCPU MC=128 cpu50/1000ms](charts/success_cpu4_mc128_cpu50_1000ms.png)

![4vCPU MC=128 cpu100/500ms](charts/success_cpu4_mc128_cpu100_500ms.png)

### 2. Summary Grid

![Summary Grid](charts/summary_grid.png)

### 3. p99 Latency vs Load

![1vCPU MC=8 sleep/1000ms](charts/latency_cpu1_mc8_sleep_1000ms.png)

![1vCPU MC=8 sleep/100ms](charts/latency_cpu1_mc8_sleep_100ms.png)

![1vCPU MC=8 cpu50/1000ms](charts/latency_cpu1_mc8_cpu50_1000ms.png)

![1vCPU MC=8 cpu100/500ms](charts/latency_cpu1_mc8_cpu100_500ms.png)

![1vCPU MC=16 sleep/1000ms](charts/latency_cpu1_mc16_sleep_1000ms.png)

![1vCPU MC=16 sleep/100ms](charts/latency_cpu1_mc16_sleep_100ms.png)

![1vCPU MC=16 cpu50/1000ms](charts/latency_cpu1_mc16_cpu50_1000ms.png)

![1vCPU MC=16 cpu100/500ms](charts/latency_cpu1_mc16_cpu100_500ms.png)

![1vCPU MC=32 sleep/1000ms](charts/latency_cpu1_mc32_sleep_1000ms.png)

![1vCPU MC=32 sleep/100ms](charts/latency_cpu1_mc32_sleep_100ms.png)

![1vCPU MC=32 cpu50/1000ms](charts/latency_cpu1_mc32_cpu50_1000ms.png)

![1vCPU MC=32 cpu100/500ms](charts/latency_cpu1_mc32_cpu100_500ms.png)

![2vCPU MC=16 sleep/1000ms](charts/latency_cpu2_mc16_sleep_1000ms.png)

![2vCPU MC=16 sleep/100ms](charts/latency_cpu2_mc16_sleep_100ms.png)

![2vCPU MC=16 cpu50/1000ms](charts/latency_cpu2_mc16_cpu50_1000ms.png)

![2vCPU MC=16 cpu100/500ms](charts/latency_cpu2_mc16_cpu100_500ms.png)

![2vCPU MC=32 sleep/1000ms](charts/latency_cpu2_mc32_sleep_1000ms.png)

![2vCPU MC=32 sleep/100ms](charts/latency_cpu2_mc32_sleep_100ms.png)

![2vCPU MC=32 cpu50/1000ms](charts/latency_cpu2_mc32_cpu50_1000ms.png)

![2vCPU MC=32 cpu100/500ms](charts/latency_cpu2_mc32_cpu100_500ms.png)

![2vCPU MC=64 sleep/1000ms](charts/latency_cpu2_mc64_sleep_1000ms.png)

![2vCPU MC=64 sleep/100ms](charts/latency_cpu2_mc64_sleep_100ms.png)

![2vCPU MC=64 cpu50/1000ms](charts/latency_cpu2_mc64_cpu50_1000ms.png)

![2vCPU MC=64 cpu100/500ms](charts/latency_cpu2_mc64_cpu100_500ms.png)

![4vCPU MC=32 sleep/1000ms](charts/latency_cpu4_mc32_sleep_1000ms.png)

![4vCPU MC=32 sleep/100ms](charts/latency_cpu4_mc32_sleep_100ms.png)

![4vCPU MC=32 cpu50/1000ms](charts/latency_cpu4_mc32_cpu50_1000ms.png)

![4vCPU MC=32 cpu100/500ms](charts/latency_cpu4_mc32_cpu100_500ms.png)

![4vCPU MC=64 sleep/1000ms](charts/latency_cpu4_mc64_sleep_1000ms.png)

![4vCPU MC=64 sleep/100ms](charts/latency_cpu4_mc64_sleep_100ms.png)

![4vCPU MC=64 cpu50/1000ms](charts/latency_cpu4_mc64_cpu50_1000ms.png)

![4vCPU MC=64 cpu100/500ms](charts/latency_cpu4_mc64_cpu100_500ms.png)

![4vCPU MC=128 sleep/1000ms](charts/latency_cpu4_mc128_sleep_1000ms.png)

![4vCPU MC=128 sleep/100ms](charts/latency_cpu4_mc128_sleep_100ms.png)

![4vCPU MC=128 cpu50/1000ms](charts/latency_cpu4_mc128_cpu50_1000ms.png)

![4vCPU MC=128 cpu100/500ms](charts/latency_cpu4_mc128_cpu100_500ms.png)

### 4. Raw Scatter Plots

![Both fixes](charts/scatter_with_fix_cpu1_mc16_cpu50_1000ms.png)

![Both fixes](charts/scatter_with_fix_cpu1_mc16_sleep_1000ms.png)

![2 threads/vCPU](charts/scatter_polling_conservative_tp_cpu1_mc16_cpu50_1000ms.png)

![2 threads/vCPU](charts/scatter_polling_conservative_tp_cpu1_mc16_sleep_1000ms.png)

![Threadpool only](charts/scatter_threadpool_only_cpu1_mc16_cpu50_1000ms.png)

![Threadpool only](charts/scatter_threadpool_only_cpu1_mc16_sleep_1000ms.png)

![No fix (baseline)](charts/scatter_without_fix_cpu1_mc16_cpu50_1000ms.png)

![No fix (baseline)](charts/scatter_without_fix_cpu1_mc16_sleep_1000ms.png)

