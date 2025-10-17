#!/usr/bin/env node
const { resolveConfig } = require('./utils/config');
const { JsonLogger } = require('./utils/logger');
const { ensureUser } = require('./utils/http');
const { buildConnection, prepareEnvironment } = require('./utils/signalr');
const { getField } = require('./utils/accessors');
const { randomUUID } = require('crypto');

function forceTransportDrop(connection) {
  const transport = connection?.connection?.transport;
  if (transport && typeof transport.stop === 'function') {
    transport.stop();
  } else if (transport?.socket?.close) {
    transport.socket.close();
  } else {
    throw new Error('Unable to access underlying transport for simulated drop');
  }
}

(async () => {
  const config = resolveConfig();
  prepareEnvironment();
  const logger = new JsonLogger('hub-reconnect', config.logDir);

  try {
    const userA = await ensureUser(config.baseUrl, config.users.A, logger);
    const userB = await ensureUser(config.baseUrl, config.users.B, logger);
    const channel = `dm:${[userA.user.id, userB.user.id].sort().join('_')}`;

    const connA = buildConnection(config.hubUrl, userA.token, logger, 'A');
    const connB = buildConnection(config.hubUrl, userB.token, logger, 'B');

    const receivedByA = [];
    const sendTimes = new Map();

    connA.on('msg', (payload) => {
      const text = getField(payload, 'text');
      if (text) {
        const start = sendTimes.get(text);
        if (start) {
          const latency = Date.now() - start;
          logger.log('receive.dm', 'INFO', { from: getField(payload, 'fromUserId'), text }, latency);
        }
        receivedByA.push(text);
      }
    });

    await connA.start();
    await connB.start();
    await connA.invoke('JoinChannels', [channel]);
    await connB.invoke('JoinChannels', [channel]);

    const baseline = `reconnect-baseline-${randomUUID()}`;
    sendTimes.set(baseline, Date.now());
    await connB.invoke('SendDm', userA.user.id, baseline);

    const waitForBaseline = Date.now() + 3000;
    while (!receivedByA.includes(baseline) && Date.now() < waitForBaseline) {
      await new Promise(resolve => setTimeout(resolve, 50));
    }
    if (!receivedByA.includes(baseline)) {
      throw new Error('Failed to receive baseline message');
    }

    logger.log('phase.baseline', 'PASS', {});

    const dropStartedAt = Date.now();
    const offlinePromise = new Promise((resolve) => {
      connA.onreconnecting(() => {
        logger.log('connection.state', 'INFO', { state: 'reconnecting' });
        resolve();
      });
    });
    forceTransportDrop(connA);
    await offlinePromise;

    const offlineMessage = `reconnect-offline-${randomUUID()}`;
    await connB.invoke('SendDm', userA.user.id, offlineMessage);
    logger.log('phase.offlineSend', 'INFO', { offlineMessage });

    const reconnected = await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Reconnect timeout')), 10000);
      connA.onreconnected(() => {
        clearTimeout(timer);
        const latency = Date.now() - dropStartedAt;
        logger.log('connection.state', 'PASS', { state: 'reconnected', latencyMs: latency });
        resolve(latency);
      });
    });

    if (reconnected === undefined) {
      throw new Error('Reconnect failed');
    }

    const historyPromise = new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('LoadHistory timeout')), 5000);
      const handler = (payload) => {
        const items = getField(payload, 'items') || [];
        if (items.some(item => getField(item, 'text') === offlineMessage)) {
          clearTimeout(timer);
          connA.off('history', handler);
          resolve(payload);
        }
      };
      connA.on('history', handler);
    });

    await connA.invoke('LoadHistory', channel, null, 50);
    const history = await historyPromise;

    const historyItems = getField(history, 'items') || [];
    const containsOffline = historyItems.some(item => getField(item, 'text') === offlineMessage);
    if (!containsOffline) {
      logger.log('validation.history', 'FAIL', { offlineMessage });
      throw new Error('Offline message missing after reconnect');
    }
    logger.log('validation.history', 'PASS', { offlineMessage });

    const duplicates = receivedByA.filter((text, idx) => receivedByA.indexOf(text) !== idx);
    if (duplicates.length > 0) {
      logger.log('validation.duplicates', 'FAIL', { duplicates });
      throw new Error('Duplicate messages detected after reconnect');
    }
    logger.log('validation.duplicates', 'PASS', { received: receivedByA.length });

    logger.log('summary', 'PASS', { reconnectLatencyMs: reconnected, recoveredMessages: 1 });

    await connA.stop();
    await connB.stop();
    logger.close();
    process.exit(0);
  } catch (err) {
    logger.log('summary', 'FAIL', { error: err.message, stack: err.stack });
    logger.close();
    console.error(err);
    process.exitCode = 1;
  }
})();
