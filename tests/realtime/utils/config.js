const fs = require('fs');
const path = require('path');
const stripJsonComments = require('strip-json-comments');

function readJsonFileSafe(filePath) {
  try {
    const raw = fs.readFileSync(filePath, 'utf8');
    const sanitized = stripJsonComments(raw);
    return JSON.parse(sanitized);
  } catch (err) {
    return null;
  }
}

function resolveLaunchUrls() {
  const launchSettings = readJsonFileSafe(path.join(__dirname, '..', '..', '..', 'WebAPI', 'Properties', 'launchSettings.json'));
  if (!launchSettings?.profiles) {
    return {};
  }

  const httpsProfile = Object.values(launchSettings.profiles).find(p => typeof p?.applicationUrl === 'string' && p.applicationUrl.includes('https://'));
  const httpProfile = Object.values(launchSettings.profiles).find(p => typeof p?.applicationUrl === 'string' && p.applicationUrl.includes('http://'));

  const parseFirstUrl = (profile, preferHttps = false) => {
    if (!profile?.applicationUrl) return undefined;
    const parts = profile.applicationUrl.split(';').map(p => p.trim()).filter(Boolean);
    if (parts.length === 0) return undefined;
    if (preferHttps) {
      const https = parts.find(p => p.startsWith('https://'));
      if (https) return https;
    }
    return parts[0];
  };

  return {
    httpsUrl: parseFirstUrl(httpsProfile, true),
    httpUrl: parseFirstUrl(httpProfile)
  };
}

function resolveAppSettings() {
  const devSettings = readJsonFileSafe(path.join(__dirname, '..', '..', '..', 'WebAPI', 'appsettings.Development.json'));
  const prodSettings = readJsonFileSafe(path.join(__dirname, '..', '..', '..', 'WebAPI', 'appsettings.json'));
  return {
    dev: devSettings ?? {},
    prod: prodSettings ?? {}
  };
}

function resolveBaseUrl() {
  if (process.env.BASE_URL) {
    return process.env.BASE_URL.replace(/\/$/, '');
  }

  const { httpsUrl, httpUrl } = resolveLaunchUrls();
  if (httpsUrl) return httpsUrl.replace(/\/$/, '');
  if (httpUrl) return httpUrl.replace(/\/$/, '');
  return 'https://localhost:7227';
}

function resolveRedis(appSettings) {
  if (process.env.REDIS) {
    return process.env.REDIS;
  }

  const redisConn = appSettings.dev?.Redis?.ConnectionString || appSettings.prod?.Redis?.ConnectionString;
  if (!redisConn) {
    return 'redis://localhost:6379';
  }

  if (redisConn.startsWith('redis://')) {
    return redisConn;
  }

  return `redis://${redisConn}`;
}

function resolveChatOptions(appSettings) {
  const chat = appSettings.dev?.Chat || appSettings.prod?.Chat || {};
  return {
    historyMax: Number(process.env.HISTORY_MAX ?? chat.HistoryMax ?? 200),
    historyTtlHours: Number(process.env.HISTORY_TTL_HOURS ?? chat.HistoryTtlHours ?? 48),
    rateLimitWindowSeconds: Number(process.env.RATE_LIMIT_WINDOW ?? chat.RateLimitWindowSeconds ?? 30),
    rateLimitMaxMessages: Number(process.env.RATE_LIMIT_MAX ?? chat.RateLimitMaxMessages ?? 30)
  };
}

function resolvePresenceOptions(appSettings) {
  const presence = appSettings.dev?.Presence || appSettings.prod?.Presence || {};
  return {
    ttlSeconds: Number(process.env.PRESENCE_TTL ?? presence.TtlSeconds ?? 60),
    heartbeatSeconds: Number(process.env.PRESENCE_HEARTBEAT ?? presence.HeartbeatSeconds ?? 30)
  };
}

function buildUserConfig(key, defaults) {
  return {
    email: process.env[`${key}_EMAIL`] || defaults.email,
    password: process.env[`${key}_PASSWORD`] || defaults.password,
    fullName: process.env[`${key}_FULLNAME`] || defaults.fullName,
    gender: process.env[`${key}_GENDER`] || defaults.gender,
    university: process.env[`${key}_UNIVERSITY`] || defaults.university,
    phone: process.env[`${key}_PHONE`] || defaults.phone
  };
}

function resolveConfig() {
  const appSettings = resolveAppSettings();
  const baseUrl = resolveBaseUrl();
  const hubUrl = (process.env.HUB_URL || `${baseUrl}/ws/chat`).replace(/\/$/, '');
  const presenceHubUrl = (process.env.PRESENCE_HUB_URL || `${baseUrl}/ws/presence`).replace(/\/$/, '');
  const redisUrl = resolveRedis(appSettings);
  const chat = resolveChatOptions(appSettings);
  const presence = resolvePresenceOptions(appSettings);

  const defaults = {
    A: {
      email: 'realtime-a@studentgamerhub.local',
      password: 'Password123!',
      fullName: 'Realtime QA A',
      gender: 'Female',
      university: 'SignalR QA Academy',
      phone: null
    },
    B: {
      email: 'realtime-b@studentgamerhub.local',
      password: 'Password123!',
      fullName: 'Realtime QA B',
      gender: 'Male',
      university: 'SignalR QA Academy',
      phone: null
    }
  };

  return {
    baseUrl,
    hubUrl,
    presenceHubUrl,
    redisUrl,
    chat,
    presence,
    users: {
      A: buildUserConfig('USER_A', defaults.A),
      B: buildUserConfig('USER_B', defaults.B)
    },
    room: {
      id: process.env.ROOM_ID ? process.env.ROOM_ID.trim() : null
    },
    logDir: process.env.LOG_DIR || path.join(__dirname, '..', 'logs')
  };
}

module.exports = {
  resolveConfig
};
