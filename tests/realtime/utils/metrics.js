function computeQuantiles(values) {
  if (!values || values.length === 0) {
    return { count: 0, p50: null, p95: null, p99: null, min: null, max: null, mean: null };
  }
  const sorted = [...values].sort((a, b) => a - b);
  const percentile = (p) => {
    if (sorted.length === 0) return null;
    const idx = (p / 100) * (sorted.length - 1);
    const lower = Math.floor(idx);
    const upper = Math.ceil(idx);
    if (lower === upper) return sorted[lower];
    const weight = idx - lower;
    return sorted[lower] * (1 - weight) + sorted[upper] * weight;
  };
  const sum = sorted.reduce((acc, v) => acc + v, 0);
  return {
    count: sorted.length,
    min: sorted[0],
    max: sorted[sorted.length - 1],
    mean: sum / sorted.length,
    p50: percentile(50),
    p95: percentile(95),
    p99: percentile(99)
  };
}

function asciiBarChart(label, values) {
  if (!values || values.length === 0) {
    return `${label}: (no samples)`;
  }
  const stats = computeQuantiles(values);
  const max = stats.max || 1;
  const scale = max > 0 ? 40 / max : 1;
  const bars = values.map(v => {
    const width = Math.max(1, Math.round(v * scale));
    return `${v.toFixed(1).padStart(6)} ms | ${'#'.repeat(width)}`;
  });
  return [`${label} (count=${stats.count}, min=${stats.min?.toFixed(1)}ms, max=${stats.max?.toFixed(1)}ms, mean=${stats.mean?.toFixed(1)}ms)`, ...bars].join('\n');
}

module.exports = {
  computeQuantiles,
  asciiBarChart
};
