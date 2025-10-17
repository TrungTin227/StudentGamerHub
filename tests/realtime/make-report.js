#!/usr/bin/env node
const fs = require('fs');
const path = require('path');
const { computeQuantiles, asciiBarChart } = require('./utils/metrics');

function readLogs(logDir) {
  const logs = [];
  if (!fs.existsSync(logDir)) {
    return logs;
  }
  const files = fs.readdirSync(logDir).filter(f => f.endsWith('.log'));
  for (const file of files) {
    const fullPath = path.join(logDir, file);
    const lines = fs.readFileSync(fullPath, 'utf8').split('\n').filter(Boolean);
    for (const line of lines) {
      try {
        logs.push(JSON.parse(line));
      } catch (err) {
        // ignore parse errors
      }
    }
  }
  return logs;
}

function groupByCase(logs) {
  const map = new Map();
  for (const entry of logs) {
    if (!map.has(entry.case)) {
      map.set(entry.case, []);
    }
    map.get(entry.case).push(entry);
  }
  return map;
}

function summarizeCase(entries) {
  const summary = {
    result: 'UNKNOWN',
    notes: [],
    latencies: {},
    redis: [],
    presence: [],
    counts: {}
  };
  for (const entry of entries) {
    if (entry.step === 'summary') {
      summary.result = entry.result;
      summary.notes.push(entry.details || {});
    }
    if (entry.details?.metric && entry.latencyMs != null) {
      if (!summary.latencies[entry.details.metric]) {
        summary.latencies[entry.details.metric] = [];
      }
      summary.latencies[entry.details.metric].push(entry.latencyMs);
    }
    if (entry.step.startsWith('redis')) {
      summary.redis.push(entry.details);
    }
    if (entry.step.startsWith('presence')) {
      summary.presence.push({ step: entry.step, details: entry.details });
    }
    summary.counts[entry.step] = (summary.counts[entry.step] || 0) + 1;
  }
  return summary;
}

function renderLatencySection(latenciesByMetric) {
  const sections = [];
  for (const [metric, samples] of Object.entries(latenciesByMetric)) {
    const stats = computeQuantiles(samples);
    sections.push(`- **${metric}**: p50=${stats.p50?.toFixed(1) ?? 'n/a'}ms, p95=${stats.p95?.toFixed(1) ?? 'n/a'}ms, p99=${stats.p99?.toFixed(1) ?? 'n/a'}ms, count=${stats.count}`);
    sections.push('');
    sections.push('```');
    sections.push(asciiBarChart(metric, samples));
    sections.push('```');
    sections.push('');
  }
  return sections.join('\n');
}

function renderRedisSection(redisEntries) {
  if (!redisEntries || redisEntries.length === 0) {
    return 'No Redis metrics captured.';
  }
  const lines = ['| Key | Detail |', '| --- | --- |'];
  for (const entry of redisEntries) {
    lines.push(`| ${entry?.key ?? 'n/a'} | ${JSON.stringify(entry)} |`);
  }
  return lines.join('\n');
}

function renderSummaryTable(caseSummaries) {
  const headers = ['Case', 'Result', 'Notes'];
  const rows = [headers.join(' | '), headers.map(() => '---').join(' | ')];
  for (const [caseName, summary] of caseSummaries) {
    const noteText = summary.notes.map(n => JSON.stringify(n)).join('<br/>');
    rows.push(`${caseName} | ${summary.result} | ${noteText || ''}`);
  }
  return rows.join('\n');
}

function renderPresenceSection(presenceEntries) {
  if (!presenceEntries || presenceEntries.length === 0) {
    return 'No presence transitions captured.';
  }
  const lines = ['| Step | Details |', '| --- | --- |'];
  for (const entry of presenceEntries) {
    lines.push(`| ${entry.step} | ${JSON.stringify(entry.details)} |`);
  }
  return lines.join('\n');
}

function renderFixSuggestions(caseSummaries) {
  const failed = Array.from(caseSummaries).filter(([, summary]) => summary.result !== 'PASS');
  if (failed.length === 0) {
    return '- ✅ All scenarios passed. No fixes required.';
  }
  const suggestions = failed.map(([caseName, summary]) => `- **${caseName}**: Investigate ${JSON.stringify(summary.notes)}`);
  return suggestions.join('\n');
}

function main() {
  const logDir = process.env.LOG_DIR || path.join(__dirname, 'logs');
  const reportPath = path.join(__dirname, 'REPORT.md');
  const logs = readLogs(logDir);
  const grouped = groupByCase(logs);
  const caseSummaries = new Map();
  for (const [caseName, entries] of grouped) {
    caseSummaries.set(caseName, summarizeCase(entries));
  }

  const sections = [];
  sections.push('# Realtime Chat Regression Report');
  sections.push(`_Generated at ${new Date().toISOString()}_`);
  sections.push('');
  sections.push('## Summary');
  sections.push(renderSummaryTable(caseSummaries));
  sections.push('');

  sections.push('## Latency Distributions');
  if (caseSummaries.size === 0) {
    sections.push('No latency samples captured.');
  } else {
    for (const [caseName, summary] of caseSummaries) {
      if (!summary.latencies || Object.keys(summary.latencies).length === 0) continue;
      sections.push(`### ${caseName}`);
      sections.push(renderLatencySection(summary.latencies));
    }
  }

  sections.push('## Redis Verification');
  for (const [caseName, summary] of caseSummaries) {
    if (!summary.redis || summary.redis.length === 0) continue;
    sections.push(`### ${caseName}`);
    sections.push(renderRedisSection(summary.redis));
    sections.push('');
  }

  sections.push('## Presence Timeline');
  for (const [caseName, summary] of caseSummaries) {
    if (!summary.presence || summary.presence.length === 0) continue;
    sections.push(`### ${caseName}`);
    sections.push(renderPresenceSection(summary.presence));
    sections.push('');
  }

  sections.push('## Fix Suggestions');
  sections.push(renderFixSuggestions(caseSummaries));

  fs.writeFileSync(reportPath, sections.join('\n'));
  // eslint-disable-next-line no-console
  console.log(`Report written to ${reportPath}`);
}

main();
