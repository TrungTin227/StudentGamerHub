#!/usr/bin/env node
const { resolveConfig } = require('./utils/config');
const { JsonLogger } = require('./utils/logger');
const { ensureUser } = require('./utils/http');
const { buildConnection, prepareEnvironment } = require('./utils/signalr');

(async () => {
  const config = resolveConfig();
  prepareEnvironment();
  const logger = new JsonLogger('hub-ratelimit', config.logDir);

  try {
    const sender = await ensureUser(config.baseUrl, config.users.A, logger);
    const recipient = await ensureUser(config.baseUrl, config.users.B, logger);

    const channel = `dm:${[sender.user.id, recipient.user.id].sort().join('_')}`;
    const connection = buildConnection(config.hubUrl, sender.token, logger, 'ratelimit');

    await connection.start();
    await connection.invoke('JoinChannels', [channel]);

    const totalMessages = Number(process.env.RL_TOTAL || 40);
    const accepted = [];
    const rejected = [];

    for (let i = 0; i < totalMessages; i += 1) {
      const text = `ratelimit-${i}-${Date.now()}`;
      const start = Date.now();
      try {
        await connection.invoke('SendDm', recipient.user.id, text);
        const latency = Date.now() - start;
        accepted.push({ text, latency });
        logger.log('ratelimit.send', 'PASS', { text, index: i }, latency);
      } catch (err) {
        rejected.push({ text, error: err.message });
        logger.log('ratelimit.send', 'FAIL', { text, index: i, error: err.message });
      }
    }

    const allowed = config.chat.rateLimitMaxMessages;
    if (accepted.length !== allowed) {
      logger.log('validation.accepted', 'FAIL', { expected: allowed, actual: accepted.length });
      throw new Error(`Expected ${allowed} accepted messages but got ${accepted.length}`);
    }
    logger.log('validation.accepted', 'PASS', { expected: allowed, actual: accepted.length });

    const expectedRejected = totalMessages - allowed;
    if (rejected.length !== expectedRejected) {
      logger.log('validation.rejected', 'FAIL', { expected: expectedRejected, actual: rejected.length });
      throw new Error(`Expected ${expectedRejected} rejected messages but got ${rejected.length}`);
    }
    const rejectedCodes = rejected.map(r => r.error);
    logger.log('validation.rejected', 'PASS', { expected: expectedRejected, errorSamples: rejectedCodes.slice(0, 3) });

    accepted.forEach((sample, index) => {
      logger.log('metric.latency', 'INFO', { metric: 'dm_round_trip', sample: index + 1 }, sample.latency);
    });

    logger.log('summary', 'PASS', {
      totalMessages,
      accepted: accepted.length,
      rejected: rejected.length,
      expectedWindowSeconds: config.chat.rateLimitWindowSeconds
    });

    await connection.stop();
    logger.close();
    process.exit(0);
  } catch (err) {
    logger.log('summary', 'FAIL', { error: err.message, stack: err.stack });
    logger.close();
    console.error(err);
    process.exitCode = 1;
  }
})();
