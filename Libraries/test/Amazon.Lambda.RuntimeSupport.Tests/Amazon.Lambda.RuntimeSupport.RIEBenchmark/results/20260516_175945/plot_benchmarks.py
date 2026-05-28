#!/usr/bin/env python3
"""Plot benchmark results comparing with_fix vs without_fix vs threadpool_only."""

import os
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib
import numpy as np

matplotlib.use('Agg')

DATA_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(DATA_DIR, 'charts')
os.makedirs(OUTPUT_DIR, exist_ok=True)

CPUS = [1, 2, 4]
MC_PER_VCPU = [8, 16, 32]
WORKLOADS = [('sleep', '1000'), ('sleep', '100'), ('cpu50', '1000'), ('cpu100', '500')]

FIXES = [
    ('with_fix', 'green', 'Both fixes'),
    ('polling_conservative_tp', 'purple', '2 threads/vCPU'),
    ('threadpool_only', 'blue', 'Threadpool only'),
    ('without_fix', 'red', 'No fix (baseline)'),
]


def load_summary(fix, cpu, mc, workload, duration):
    path = os.path.join(DATA_DIR, f"{fix}_cpu{cpu}_mc{mc}_{workload}_{duration}ms.csv")
    return pd.read_csv(path) if os.path.exists(path) else None


def load_raw(fix, cpu, mc, workload, duration):
    path = os.path.join(DATA_DIR, f"{fix}_cpu{cpu}_mc{mc}_{workload}_{duration}ms_raw.csv")
    return pd.read_csv(path) if os.path.exists(path) else None


# ============ Chart 1: Success Rate vs Load ============
def plot_success_rate_charts():
    for cpu in CPUS:
        for mc_per in MC_PER_VCPU:
            mc = cpu * mc_per
            for workload, duration in WORKLOADS:
                fig, ax = plt.subplots(figsize=(8, 5))
                has_data = False
                for fix, color, label in FIXES:
                    df = load_summary(fix, cpu, mc, workload, duration)
                    if df is not None:
                        ax.plot(df['load_level'], df['success_rate'], '-o', color=color, markersize=4, label=label)
                        has_data = True
                if not has_data:
                    plt.close(fig)
                    continue
                ax.axvline(x=mc, color='gray', linestyle='--', alpha=0.5, label=f'MC={mc}')
                ax.set_xlabel('Concurrent Invocations (Load Level)')
                ax.set_ylabel('Success Rate (%)')
                ax.set_title(f'MC={mc}, {cpu} vCPU, {workload}/{duration}ms')
                ax.set_ylim(0, 105)
                ax.legend()
                ax.grid(True, alpha=0.3)
                fig.tight_layout()
                fig.savefig(os.path.join(OUTPUT_DIR, f'success_cpu{cpu}_mc{mc}_{workload}_{duration}ms.png'), dpi=150)
                plt.close(fig)


# ============ Chart 2: Summary Grid ============
def plot_summary_grid():
    fig, axes = plt.subplots(3, 3, figsize=(16, 12))
    fig.suptitle('Success Rate at Load=MC', fontsize=14)

    for row, cpu in enumerate(CPUS):
        for col, mc_per in enumerate(MC_PER_VCPU):
            mc = cpu * mc_per
            ax = axes[row, col]
            ax.set_title(f'{cpu} vCPU, MC={mc}', fontsize=10)

            workload_labels = [f'{w}/{d}ms' for w, d in WORKLOADS]
            x = np.arange(len(workload_labels))
            width = 0.2

            for i, (fix, color, label) in enumerate(FIXES):
                rates = []
                for workload, duration in WORKLOADS:
                    df = load_summary(fix, cpu, mc, workload, duration)
                    rate = df[df['load_level'] == mc]['success_rate'].values[0] if df is not None and mc in df['load_level'].values else 0
                    rates.append(rate)
                ax.bar(x + (i - 1.5) * width, rates, width, label=label, color=color, alpha=0.7)

            ax.set_ylim(0, 110)
            ax.set_xticks(x)
            ax.set_xticklabels(workload_labels, rotation=45, ha='right', fontsize=7)
            ax.set_ylabel('Success %')
            ax.axhline(y=100, color='green', linestyle=':', alpha=0.3)
            ax.axhline(y=50, color='red', linestyle=':', alpha=0.3)
            if row == 0 and col == 0:
                ax.legend(fontsize=8)

    fig.tight_layout()
    fig.savefig(os.path.join(OUTPUT_DIR, 'summary_grid.png'), dpi=150)
    plt.close(fig)


# ============ Chart 3: Latency vs Load (p99) ============
def plot_latency_charts():
    for cpu in CPUS:
        for mc_per in MC_PER_VCPU:
            mc = cpu * mc_per
            for workload, duration in WORKLOADS:
                fig, ax = plt.subplots(figsize=(8, 5))
                has_data = False
                for fix, color, label in FIXES:
                    df = load_summary(fix, cpu, mc, workload, duration)
                    if df is not None:
                        ax.plot(df['load_level'], df['p99_ms'], '-o', color=color, markersize=4, label=label)
                        has_data = True
                if not has_data:
                    plt.close(fig)
                    continue
                ax.axvline(x=mc, color='gray', linestyle='--', alpha=0.5, label=f'MC={mc}')
                ax.set_xlabel('Concurrent Invocations (Load Level)')
                ax.set_ylabel('p99 Latency (ms)')
                ax.set_title(f'p99 Latency: MC={mc}, {cpu} vCPU, {workload}/{duration}ms')
                ax.legend()
                ax.grid(True, alpha=0.3)
                fig.tight_layout()
                fig.savefig(os.path.join(OUTPUT_DIR, f'latency_cpu{cpu}_mc{mc}_{workload}_{duration}ms.png'), dpi=150)
                plt.close(fig)


# ============ Chart 4: Raw Scatter Plot ============
def plot_raw_scatter():
    scenarios = [(1, 16, 'sleep', '1000'), (1, 16, 'cpu50', '1000')]
    for cpu, mc, workload, duration in scenarios:
        for fix, color, label in FIXES:
            raw = load_raw(fix, cpu, mc, workload, duration)
            if raw is None:
                continue
            fig, ax = plt.subplots(figsize=(10, 5))
            success = raw[raw['success'] == 1]
            failure = raw[raw['success'] == 0]
            ax.scatter(success['timestamp_ms'] / 1000, success['latency_ms'], c='green', s=10, alpha=0.6, label='success')
            if len(failure) > 0:
                ax.scatter(failure['timestamp_ms'] / 1000, failure['latency_ms'], c='red', s=20, alpha=0.8, label='failure', marker='x')
            ax.set_xlabel('Time (s)')
            ax.set_ylabel('Latency (ms)')
            ax.set_title(f'{label}: MC={mc}, {cpu} vCPU, {workload}/{duration}ms')
            ax.legend()
            ax.grid(True, alpha=0.3)
            fig.tight_layout()
            fig.savefig(os.path.join(OUTPUT_DIR, f'scatter_{fix}_cpu{cpu}_mc{mc}_{workload}_{duration}ms.png'), dpi=150)
            plt.close(fig)


# ============ Markdown Report ============
def generate_report():
    lines = ['# Multi-Concurrency Benchmark Results\n\n']
    lines.append('## Summary\n\n')
    lines.append('Comparison of three configurations for the AWS Lambda .NET runtime multi-concurrency implementation:\n\n')
    lines.append('| Variant | Description |\n|---------|-------------|\n')
    lines.append('| **Both fixes** (`with_fix`) | Threadpool scaling + polling fix |\n')
    lines.append('| **2 threads/vCPU** (`polling_conservative_tp`) | Conservative polling (2 threads per vCPU) |\n')
    lines.append('| **Threadpool only** (`threadpool_only`) | Only the threading/threadpool change |\n')
    lines.append('| **No fix** (`without_fix`) | Baseline (no changes) |\n\n')
    lines.append('### Key Findings\n\n')
    lines.append('- **No fix (baseline)**: Success rate drops at load=3 (only 2 polling tasks available)\n')
    lines.append('- **Threadpool only**: Improves threading but still limited by polling task count\n')
    lines.append('- **Both fixes**: 100% success up to MC, graceful degradation beyond\n')

    # Compute improvement stats
    improvements_both = []
    improvements_tp = []
    for cpu in CPUS:
        for mc_per in MC_PER_VCPU:
            mc = cpu * mc_per
            for workload, duration in WORKLOADS:
                wf = load_summary('with_fix', cpu, mc, workload, duration)
                tp = load_summary('threadpool_only', cpu, mc, workload, duration)
                wof = load_summary('without_fix', cpu, mc, workload, duration)
                if wf is None or wof is None:
                    continue
                merged = pd.merge(wf[['load_level', 'success_rate']], wof[['load_level', 'success_rate']],
                                  on='load_level', suffixes=('_wf', '_wof'))
                diff = merged['success_rate_wf'] - merged['success_rate_wof']
                if diff.max() > 0:
                    improvements_both.append(diff.max())
                if tp is not None:
                    merged2 = pd.merge(tp[['load_level', 'success_rate']], wof[['load_level', 'success_rate']],
                                       on='load_level', suffixes=('_tp', '_wof'))
                    diff2 = merged2['success_rate_tp'] - merged2['success_rate_wof']
                    if diff2.max() > 0:
                        improvements_tp.append(diff2.max())

    if improvements_both:
        lines.append(f'- **Both fixes** improvement: +{min(improvements_both):.0f}% to +{max(improvements_both):.0f}% success rate vs baseline\n')
    if improvements_tp:
        lines.append(f'- **Threadpool only** improvement: +{min(improvements_tp):.0f}% to +{max(improvements_tp):.0f}% success rate vs baseline\n')

    lines.append('\n## Charts\n\n')
    lines.append('### 1. Success Rate vs Load\n\n')
    for cpu in CPUS:
        for mc_per in MC_PER_VCPU:
            mc = cpu * mc_per
            for workload, duration in WORKLOADS:
                fname = f'success_cpu{cpu}_mc{mc}_{workload}_{duration}ms.png'
                if os.path.exists(os.path.join(OUTPUT_DIR, fname)):
                    lines.append(f'![{cpu}vCPU MC={mc} {workload}/{duration}ms](charts/{fname})\n\n')

    lines.append('### 2. Summary Grid\n\n')
    lines.append('![Summary Grid](charts/summary_grid.png)\n\n')

    lines.append('### 3. p99 Latency vs Load\n\n')
    for cpu in CPUS:
        for mc_per in MC_PER_VCPU:
            mc = cpu * mc_per
            for workload, duration in WORKLOADS:
                fname = f'latency_cpu{cpu}_mc{mc}_{workload}_{duration}ms.png'
                if os.path.exists(os.path.join(OUTPUT_DIR, fname)):
                    lines.append(f'![{cpu}vCPU MC={mc} {workload}/{duration}ms](charts/{fname})\n\n')

    lines.append('### 4. Raw Scatter Plots\n\n')
    for fix, _, label in FIXES:
        for fname in sorted(os.listdir(OUTPUT_DIR)):
            if fname.startswith(f'scatter_{fix}'):
                lines.append(f'![{label}](charts/{fname})\n\n')

    with open(os.path.join(DATA_DIR, 'BENCHMARK_REPORT.md'), 'w') as f:
        f.writelines(lines)
    print(f"Report written to {os.path.join(DATA_DIR, 'BENCHMARK_REPORT.md')}")


if __name__ == '__main__':
    print("Generating success rate charts...")
    plot_success_rate_charts()
    print("Generating summary grid...")
    plot_summary_grid()
    print("Generating latency charts...")
    plot_latency_charts()
    print("Generating raw scatter plots...")
    plot_raw_scatter()
    print("Generating markdown report...")
    generate_report()
    print(f"Done! Charts saved to {OUTPUT_DIR}")
