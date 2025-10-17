#!/usr/bin/env node
const { resolveConfig } = require('./utils/config');
const { JsonLogger } = require('./utils/logger');
const { ensureUser } = require('./utils/http');
const { buildConnection, prepareEnvironment } = require('./utils/signalr');
const { getField } = require('./utils/accessors');
const { randomUUID } = require('crypto');

(async () => {
  const config = resolveConfig();
  prepareEnvironment();
  const logger = new JsonLogger('hub-smoke', config.logDir);

  try {
    logger.log('setup.config', 'INFO', { hubUrl: config.hubUrl, baseUrl: config.baseUrl });

    const userA = await ensureUser(config.baseUrl, config.users.A, logger);
    const userB = await ensureUser(config.baseUrl, config.users.B, logger);

    const pair = [userA.user.id, userB.user.id].sort();
    const channel = `dm:${pair[0]}_${pair[1]}`;
    const dmKey = `chat:${channel}`;

    logger.log('setup.channel', 'INFO', { channel, dmKey });

    const connA = buildConnection(config.hubUrl, userA.token, logger, 'A');
    const connB = buildConnection(config.hubUrl, userB.token, logger, 'B');

    const latencies = [];
    const received = [];
    const expectedText = `SMOKE-${randomUUID()}`;
    const sendTimes = new Map();

    connA.on('msg', (payload) => {
      const text = getField(payload, 'text');
      if (text === expectedText) {
        const started = sendTimes.get(text);
        if (started) {
          const latency = Date.now() - started;
          latencies.push(latency);
          logger.log('receive.self', 'PASS', {
            connection: 'A',
            messageId: getField(payload, 'id'),
            channel: getField(payload, 'channel')
          }, latency);
        }
        received.push({ connection: 'A', payload });
      }
    });

    connB.on('msg', (payload) => {
      const text = getField(payload, 'text');
      if (text === expectedText) {
        const started = sendTimes.get(text);
        if (started) {
          const latency = Date.now() - started;
          latencies.push(latency);
          logger.log('receive.peer', 'PASS', {
            connection: 'B',
            messageId: getField(payload, 'id'),
            channel: getField(payload, 'channel')
          }, latency);
        }
        received.push({ connection: 'B', payload });
      }
    });

    await connA.start();
    await connB.start();

    logger.log('signalr.connected', 'PASS', { connections: ['A', 'B'] });

    await connA.invoke('JoinChannels', [channel]);
    await connB.invoke('JoinChannels', [channel]);
    logger.log('signalr.join', 'PASS', { channel });

    const sendStart = Date.now();
    sendTimes.set(expectedText, sendStart);
    await connA.invoke('SendDm', userB.user.id, expectedText);
    logger.log('signalr.send', 'PASS', { to: userB.user.id, text: expectedText });

    const timeoutMs = 5000;
    const waitUntil = Date.now() + timeoutMs;
    while (received.length < 2 && Date.now() < waitUntil) {
      await new Promise(resolve => setTimeout(resolve, 50));
    }

    if (received.length < 2) {
      logger.log('validation.received', 'FAIL', { expected: 2, actual: received.length });
      throw new Error('Did not receive message on both connections');
    }

    const orderOk = received.every((entry, idx) => {
      if (idx === 0) return true;
      const prev = getField(received[idx - 1].payload, 'sentAt');
      const current = getField(entry.payload, 'sentAt');
      return prev <= current;
    });
    if (!orderOk) {
      logger.log('validation.order', 'FAIL', { message: 'Messages not in chronological order' });
      throw new Error('Message ordering failed');
    }
    logger.log('validation.order', 'PASS', { count: received.length });

    const ids = new Set(received.map(r => getField(r.payload, 'id')));
    if (ids.size !== received.length) {
      logger.log('validation.duplicate', 'FAIL', { uniqueIds: ids.size, total: received.length });
      throw new Error('Duplicate message identifiers detected');
    }
    logger.log('validation.duplicate', 'PASS', { uniqueIds: ids.size });

    latencies.forEach((ms, index) => {
      logger.log('metric.latency', 'INFO', { metric: 'dm_round_trip', sample: index + 1 }, ms);
    });

    logger.log('summary', 'PASS', { samples: latencies.length, channel });

    await connA.stop();
    await connB.stop();
    logger.log('signalr.stop', 'INFO', {});
    logger.close();
    process.exit(0);
  } catch (err) {
    logger.log('summary', 'FAIL', { error: err.message, stack: err.stack });
    logger.close();
    console.error(err);
    process.exitCode = 1;
  }
})();
