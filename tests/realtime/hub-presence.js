#!/usr/bin/env node
const { resolveConfig } = require('./utils/config');
const { JsonLogger } = require('./utils/logger');
const { ensureUser } = require('./utils/http');
const { buildConnection, prepareEnvironment } = require('./utils/signalr');
const { createRedisClient } = require('./utils/redis');

(async () => {
  const config = resolveConfig();
  prepareEnvironment();
  const logger = new JsonLogger('hub-presence', config.logDir);
  const redis = createRedisClient(config.redisUrl);

  try {
    await redis.connect();
    const user = await ensureUser(config.baseUrl, config.users.A, logger);

    const key = `presence:${user.user.id}`;
    const lastSeenKey = `lastseen:${user.user.id}`;

    const connection = buildConnection(config.presenceHubUrl, user.token, logger, 'presence');
    await connection.start();

    logger.log('presence.online', 'INFO', { ttlSeconds: config.presence.ttlSeconds });
    await connection.invoke('Heartbeat');
    logger.log('presence.heartbeat', 'PASS', { state: 'Online' });

    const ttlAfterHeartbeat = await redis.ttl(key);
    logger.log('redis.ttl', 'INFO', { key, ttlSeconds: ttlAfterHeartbeat });

    await new Promise(resolve => setTimeout(resolve, config.presence.heartbeatSeconds * 1000));
    logger.log('presence.state', 'INFO', { state: 'Away', note: `No heartbeat for ${config.presence.heartbeatSeconds}s` });

    const waitMs = (config.presence.ttlSeconds + 10) * 1000;
    await new Promise(resolve => setTimeout(resolve, waitMs));

    const exists = await redis.exists(key);
    const lastSeen = await redis.get(lastSeenKey);

    if (exists) {
      logger.log('presence.offline', 'FAIL', { keyStillExists: true });
      throw new Error('Presence key still exists after TTL');
    }
    logger.log('presence.offline', 'PASS', { lastSeen });

    logger.log('summary', 'PASS', { ttlSeconds: config.presence.ttlSeconds, heartbeatSeconds: config.presence.heartbeatSeconds });

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
