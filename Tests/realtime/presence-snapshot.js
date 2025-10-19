#!/usr/bin/env node

/**
 * presence-snapshot.js
 * 
 * Snapshot Redis presence state for StudentGamerHub
 * 
 * Reads presence keys (presence:{userId}) and last seen (lastseen:{userId})
 * from Redis and outputs online/offline status with TTL information.
 * 
 * Usage:
 *   node tests/realtime/presence-snapshot.js
 *   node tests/realtime/presence-snapshot.js > presence.json
 *   REDIS=localhost:6379 node tests/realtime/presence-snapshot.js
 */

const Redis = require('ioredis');
const fs = require('fs');
const path = require('path');

// Read Redis connection string from appsettings or environment
function getRedisConnectionString() {
  // Check environment variable first
  if (process.env.REDIS) {
    return process.env.REDIS;
  }

  // Try to read from appsettings.json
  const appsettingsPaths = [
    path.join(__dirname, '../../WebAPI/appsettings.Development.json'),
    path.join(__dirname, '../../WebAPI/appsettings.json')
  ];

  for (const configPath of appsettingsPaths) {
    try {
      if (fs.existsSync(configPath)) {
        // Read and strip comments (JSONC format support)
        let content = fs.readFileSync(configPath, 'utf8');
        // Remove single-line comments
        content = content.replace(/\/\/.*$/gm, '');
        // Remove multi-line comments
        content = content.replace(/\/\*[\s\S]*?\*\//g, '');
        
        const config = JSON.parse(content);
        if (config.Redis?.ConnectionString) {
          console.error(`[INFO] Using Redis connection from: ${path.basename(configPath)}`);
          return config.Redis.ConnectionString;
        }
      }
    } catch (err) {
      console.error(`[WARN] Failed to read ${configPath}: ${err.message}`);
    }
  }

  // Default fallback
  console.error('[INFO] Using default Redis connection: localhost:6379');
  return 'localhost:6379';
}

// Parse Guid from Redis key (format: "presence:{guid}" or "lastseen:{guid}")
function extractUserId(key) {
  const parts = key.split(':');
  return parts.length > 1 ? parts[1] : null;
}

// Main snapshot function
async function capturePresenceSnapshot() {
  const connectionString = getRedisConnectionString();
  console.error(`[INFO] Connecting to Redis: ${connectionString}`);

  const redis = new Redis(connectionString, {
    lazyConnect: true,
    retryStrategy: (times) => {
      if (times > 3) {
        console.error('[ERROR] Redis connection failed after 3 retries');
        return null; // Stop retrying
      }
      return Math.min(times * 100, 2000);
    }
  });

  try {
    await redis.connect();
    console.error('[INFO] Connected to Redis successfully');

    // Scan for all presence keys
    const presenceKeys = [];
    let cursor = '0';

    do {
      const result = await redis.scan(
        cursor,
        'MATCH', 'presence:*',
        'COUNT', 100
      );
      cursor = result[0];
      presenceKeys.push(...result[1]);
    } while (cursor !== '0');

    console.error(`[INFO] Found ${presenceKeys.length} presence keys`);

    // Scan for all lastseen keys to capture offline users too
    const lastSeenKeys = [];
    cursor = '0';

    do {
      const result = await redis.scan(
        cursor,
        'MATCH', 'lastseen:*',
        'COUNT', 100
      );
      cursor = result[0];
      lastSeenKeys.push(...result[1]);
    } while (cursor !== '0');

    console.error(`[INFO] Found ${lastSeenKeys.length} lastseen keys`);

    // Collect all unique user IDs
    const userIds = new Set();
    presenceKeys.forEach(key => {
      const userId = extractUserId(key);
      if (userId) userIds.add(userId);
    });
    lastSeenKeys.forEach(key => {
      const userId = extractUserId(key);
      if (userId) userIds.add(userId);
    });

    console.error(`[INFO] Processing ${userIds.size} unique users`);

    // Build snapshot data
    const snapshot = {
      timestamp: new Date().toISOString(),
      redisConnection: connectionString,
      users: []
    };

    // Process each user
    const pipeline = redis.pipeline();
    const userIdArray = Array.from(userIds);

    userIdArray.forEach(userId => {
      pipeline.ttl(`presence:${userId}`);
      pipeline.get(`lastseen:${userId}`);
    });

    const results = await pipeline.exec();

    for (let i = 0; i < userIdArray.length; i++) {
      const userId = userIdArray[i];
      const ttlIndex = i * 2;
      const lastSeenIndex = i * 2 + 1;

      const ttlResult = results[ttlIndex];
      const lastSeenResult = results[lastSeenIndex];

      // Handle pipeline errors
      const ttl = ttlResult[0] ? -2 : ttlResult[1];
      const lastSeen = lastSeenResult[0] ? null : lastSeenResult[1];

      // Determine state based on TTL
      // ttl > 0: key exists with TTL (online)
      // ttl = -1: key exists but no TTL (shouldn't happen, but treat as online)
      // ttl = -2: key doesn't exist (offline)
      let state;
      if (ttl > 0 || ttl === -1) {
        state = 'online';
      } else {
        state = 'offline';
      }

      snapshot.users.push({
        userId,
        state,
        ttl: ttl > 0 ? ttl : null,
        lastSeen: lastSeen || null
      });
    }

    // Sort by state (online first), then by userId
    snapshot.users.sort((a, b) => {
      if (a.state !== b.state) {
        return a.state === 'online' ? -1 : 1;
      }
      return a.userId.localeCompare(b.userId);
    });

    // Calculate summary
    const onlineCount = snapshot.users.filter(u => u.state === 'online').length;
    const offlineCount = snapshot.users.filter(u => u.state === 'offline').length;

    snapshot.summary = {
      total: snapshot.users.length,
      online: onlineCount,
      offline: offlineCount
    };

    console.error(`[INFO] Summary: ${onlineCount} online, ${offlineCount} offline, ${snapshot.users.length} total`);

    // Output JSON to stdout
    console.log(JSON.stringify(snapshot, null, 2));

  } catch (err) {
    console.error(`[ERROR] Failed to capture snapshot: ${err.message}`);
    console.error(err.stack);
    process.exit(1);
  } finally {
    await redis.quit();
    console.error('[INFO] Disconnected from Redis');
  }
}

// Run if executed directly
if (require.main === module) {
  capturePresenceSnapshot().catch(err => {
    console.error(`[FATAL] ${err.message}`);
    process.exit(1);
  });
}

module.exports = { capturePresenceSnapshot };
