#!/usr/bin/env node
const { resolveConfig } = require('./utils/config');
const { JsonLogger } = require('./utils/logger');
const { ensureUser, ensureRoom } = require('./utils/http');
const { buildConnection, prepareEnvironment } = require('./utils/signalr');
const { getField } = require('./utils/accessors');
const { randomUUID } = require('crypto');

(async () => {
  const config = resolveConfig();
  prepareEnvironment();
  const logger = new JsonLogger('hub-room', config.logDir);

  try {
    logger.log('setup.config', 'INFO', { hubUrl: config.hubUrl, roomId: config.room.id });
    const host = await ensureUser(config.baseUrl, config.users.A, logger);
    const member = await ensureUser(config.baseUrl, config.users.B, logger);

    const roomId = await ensureRoom(config.baseUrl, host, member, logger, config.room.id);
    const channel = `room:${roomId}`;

    const hostConn = buildConnection(config.hubUrl, host.token, logger, 'host');
    const memberConn = buildConnection(config.hubUrl, member.token, logger, 'member');

    const extraConnections = [];
    const clones = Number(process.env.ROOM_CLONES || 2);
    for (let i = 0; i < clones; i += 1) {
      extraConnections.push(buildConnection(config.hubUrl, member.token, logger, `clone-${i + 1}`));
    }

    const latencies = [];
    const sendTimes = new Map();
    const messageId = `ROOM-${randomUUID()}`;
    const listeners = [];

    function registerListener(connName, connection) {
      const handler = (payload) => {
        const text = getField(payload, 'text');
        if (text === messageId) {
          const start = sendTimes.get(text);
          if (start) {
            const latency = Date.now() - start;
            latencies.push(latency);
            logger.log('receive.room', 'PASS', { connection: connName, messageId: getField(payload, 'id') }, latency);
          }
        }
      };
      connection.on('msg', handler);
      listeners.push({ connection, handler });
    }

    registerListener('host', hostConn);
    registerListener('member', memberConn);
    extraConnections.forEach((conn, idx) => registerListener(`clone-${idx + 1}`, conn));

    await hostConn.start();
    await memberConn.start();
    for (const conn of extraConnections) {
      await conn.start();
    }

    logger.log('signalr.connected', 'PASS', { connections: 2 + extraConnections.length, channel });

    await hostConn.invoke('JoinChannels', [channel]);
    await memberConn.invoke('JoinChannels', [channel]);
    for (const conn of extraConnections) {
      await conn.invoke('JoinChannels', [channel]);
    }
    logger.log('signalr.join', 'PASS', { channel, participants: 2 + extraConnections.length });

    sendTimes.set(messageId, Date.now());
    await hostConn.invoke('SendToRoom', roomId, messageId);
    logger.log('signalr.send', 'PASS', { from: host.user.id, roomId, messageId });

    const expectedReceivers = 2 + extraConnections.length;
    const waitFor = Date.now() + 5000;
    let receivedCount = 0;
    while (Date.now() < waitFor) {
      receivedCount = latencies.length;
      if (receivedCount >= expectedReceivers) break;
      await new Promise(resolve => setTimeout(resolve, 50));
    }

    if (latencies.length < expectedReceivers) {
      logger.log('validation.broadcast', 'FAIL', { expectedReceivers, actualReceivers: latencies.length });
      throw new Error('Not all participants received broadcast');
    }

    latencies.forEach((ms, index) => {
      logger.log('metric.latency', 'INFO', { metric: 'room_round_trip', sample: index + 1 }, ms);
    });

    logger.log('summary', 'PASS', { samples: latencies.length, roomId, channel });

    for (const { connection, handler } of listeners) {
      connection.off('msg', handler);
    }

    await hostConn.stop();
    await memberConn.stop();
    for (const conn of extraConnections) {
      await conn.stop();
    }
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
