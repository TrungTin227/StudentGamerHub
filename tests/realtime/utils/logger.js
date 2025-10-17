const fs = require('fs');
const path = require('path');

class JsonLogger {
  constructor(caseName, logDir) {
    this.caseName = caseName;
    this.logDir = logDir;
    if (!fs.existsSync(logDir)) {
      fs.mkdirSync(logDir, { recursive: true });
    }
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    this.filePath = path.join(logDir, `${caseName}-${timestamp}.log`);
    this.stream = fs.createWriteStream(this.filePath, { flags: 'a' });
  }

  log(step, result, details = {}, latencyMs = null) {
    const payload = {
      timestamp: new Date().toISOString(),
      case: this.caseName,
      step,
      result,
      latencyMs: latencyMs != null ? Number(latencyMs) : undefined,
      details
    };
    this.stream.write(`${JSON.stringify(payload)}\n`);
    const consoleParts = [payload.timestamp, `[${result}]`, `${this.caseName}::${step}`];
    if (latencyMs != null) {
      consoleParts.push(`${latencyMs.toFixed(2)}ms`);
    }
    if (details && Object.keys(details).length > 0) {
      consoleParts.push(JSON.stringify(details));
    }
    // eslint-disable-next-line no-console
    console.log(consoleParts.join(' '));
  }

  close() {
    this.stream.end();
  }
}

module.exports = {
  JsonLogger
};
