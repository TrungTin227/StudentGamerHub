#!/usr/bin/env node
const { resolveConfig } = require('./utils/config');
const { JsonLogger } = require('./utils/logger');
const { ensureUser } = require('./utils/http');
const { buildConnection, prepareEnvironment } = require('./utils/signalr');
const { createRedisClient } = require('./utils/redis');
const { getField } = require('./utils/accessors');
const { randomUUID } = require('crypto');

(async () => {
  const config = resolveConfig();
  prepareEnvironment();
  const logger = new JsonLogger('hub-history', config.logDir);
  const redis = createRedisClient(config.redisUrl);

  try {
    await redis.connect();
    logger.log('setup.config', 'INFO', { hubUrl: config.hubUrl, redis: config.redisUrl, historyMax: config.chat.historyMax });

    const userA = await ensureUser(config.baseUrl, config.users.A, logger);
    const userB = await ensureUser(config.baseUrl, config.users.B, logger);

    const pair = [userA.user.id, userB.user.id].sort();
    const channel = `dm:${pair[0]}_${pair[1]}`;
    const key = `chat:${channel}`;

    logger.log('setup.channel', 'INFO', { channel, key });

    const seedCount = config.chat.historyMax + 20;
    const texts = [];
    let useStream = true;
    for (let i = 0; i < seedCount; i += 1) {
      const text = `history-${i}-${randomUUID()}`;
      texts.push(text);
      const now = Date.now().toString();
      if (useStream) {
        try {
          await redis.xadd(key, 'MAXLEN', '~', config.chat.historyMax, '*', 'fromUserId', userA.user.id, 'toUserId', userB.user.id, 'roomId', '', 'text', text, 'ts', now, 'channel', channel);
        } catch (streamErr) {
          useStream = false;
          await redis.rpush(key, JSON.stringify({
            id: `${now}-${randomUUID()}`,
            fromUserId: userA.user.id,
            toUserId: userB.user.id,
            roomId: '',
            text,
            ts: Number(now),
            channel
          }));
          await redis.ltrim(key, -config.chat.historyMax, -1);
        }
      } else {
        await redis.rpush(key, JSON.stringify({
          id: `${now}-${randomUUID()}`,
          fromUserId: userA.user.id,
          toUserId: userB.user.id,
          roomId: '',
          text,
          ts: Number(now),
          channel
        }));
        await redis.ltrim(key, -config.chat.historyMax, -1);
      }
    }
    await redis.expire(key, config.chat.historyTtlHours * 3600);
    logger.log('setup.seed', 'PASS', { inserted: seedCount });

    const connection = buildConnection(config.hubUrl, userA.token, logger, 'history');
    connection.on('history', (payload) => {
      const items = getField(payload, 'items');
      const nextAfterId = getField(payload, 'nextAfterId');
      logger.log('history.receive', 'INFO', { items: items?.length, nextAfterId });
    });

    await connection.start();
    logger.log('signalr.connected', 'PASS', { connectionId: connection.connectionId });

    await connection.invoke('JoinChannels', [channel]);

    // Attach promise before invoking history to avoid race
    const historyPromise = new Promise((resolve) => {
      const handler = (payload) => {
        connection.off('history', handler);
        resolve(payload);
      };
      connection.on('history', handler);
    });

    await connection.invoke('LoadHistory', channel, null, config.chat.historyMax + 50);
    logger.log('history.invoke', 'PASS', { requested: config.chat.historyMax + 50 });
    const history = await Promise.race([
      historyPromise,
      new Promise((_, reject) => setTimeout(() => reject(new Error('Timeout waiting for history payload')), 5000))
    ]);

    if (!history) {
      throw new Error('History payload missing');
    }

    const historyItems = getField(history, 'items') || [];
    const count = Array.isArray(historyItems) ? historyItems.length : 0;
    if (count > config.chat.historyMax) {
      logger.log('validation.size', 'FAIL', { count, max: config.chat.historyMax });
      throw new Error(`History exceeded limit ${config.chat.historyMax}`);
    }
    logger.log('validation.size', 'PASS', { count, max: config.chat.historyMax });

    const tailTexts = texts.slice(-count);
    const historyTexts = historyItems.map(item => getField(item, 'text'));
    const mismatch = tailTexts.some((text, idx) => text !== historyTexts[idx]);
    if (mismatch) {
      logger.log('validation.tail', 'FAIL', { expectedTail: tailTexts.slice(0, 5), actualTail: historyTexts.slice(0, 5) });
      throw new Error('History does not match seeded tail');
    }
    logger.log('validation.tail', 'PASS', { matched: count });

    const nextAfterId = getField(history, 'nextAfterId');
    if (!nextAfterId) {
      logger.log('validation.nextAfterId', 'FAIL', { message: 'Expected nextAfterId for pagination' });
      throw new Error('Missing nextAfterId');
    }
    logger.log('validation.nextAfterId', 'PASS', { nextAfterId });

    const newMessage = `history-new-${randomUUID()}`;
    await connection.invoke('SendDm', userB.user.id, newMessage);
    logger.log('signalr.send', 'PASS', { newMessage });

    const deltaPromise = new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Timeout waiting for incremental history')), 5000);
      const handler = (payload) => {
        const items = getField(payload, 'items') || [];
        if (items.some(item => getField(item, 'text') === newMessage)) {
          clearTimeout(timer);
          connection.off('history', handler);
          resolve(payload);
        }
      };
      connection.on('history', handler);
    });

    await connection.invoke('LoadHistory', channel, nextAfterId, 50);
    const deltaHistory = await deltaPromise;
    const deltaItems = getField(deltaHistory, 'items') || [];
    logger.log('history.delta', 'PASS', { items: deltaItems.length });

    const redisTtl = await redis.ttl(key);
    logger.log('redis.ttl', 'INFO', { key, ttlSeconds: redisTtl });

    const keyType = await redis.type(key);
    let redisLen;
    if (keyType === 'stream') {
      redisLen = await redis.xlen(key);
    } else if (keyType === 'list') {
      redisLen = await redis.llen(key);
    } else {
      redisLen = null;
    }
    logger.log('redis.length', 'INFO', { key, length: redisLen, type: keyType });

    logger.log('summary', 'PASS', { count, redisLen, redisTtl });

    await connection.stop();
    await redis.quit();
    logger.close();
    process.exit(0);
  } catch (err) {
    logger.log('summary', 'FAIL', { error: err.message, stack: err.stack });
    try { await redis.quit(); } catch (e) { /* ignore */ }
    logger.close();
    console.error(err);
    process.exitCode = 1;
  }
})();
