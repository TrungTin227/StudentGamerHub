const fetch = require('cross-fetch');
const { randomUUID } = require('crypto');
const { URL } = require('url');

function decodeJwtPayload(token) {
  if (!token) return null;
  const parts = token.split('.');
  if (parts.length < 2) return null;
  const b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 ? '='.repeat(4 - (b64.length % 4)) : '';
  try { return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf8')); } catch { return null; }
}

/** Map giới tính về enum số cho .NET binder */
function mapGender(g) {
  if (g === null || g === undefined) return 0; // Unknown
  if (typeof g === 'number') return g;
  const s = String(g).toLowerCase();
  if (s.startsWith('m')) return 1; // Male
  if (s.startsWith('f')) return 2; // Female
  return 0; // Unknown
}

/** Đổi key object sang PascalCase (Email, UserName, FullName, ...) */
function toPascalKeys(obj) {
  const out = {};
  for (const [k, v] of Object.entries(obj)) {
    const pascal = k.slice(0, 1).toUpperCase() + k.slice(1);
    out[pascal] = v;
  }
  return out;
}

async function fetchJson(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers || {})
    }
  });

  const text = await response.text();
  let body;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = text;
  }
  return { response, body };
}

/** Đăng nhập để lấy JWT */
async function login(baseUrl, credentials) {
  const url = new URL('/api/auth/login', baseUrl).toString();
  return fetchJson(url, {
    method: 'POST',
    body: JSON.stringify({
      userNameOrEmail: credentials.email,
      password: credentials.password
    })
  });
}

/** Đăng ký user – thử nhiều schema để khớp DTO RegisterRequest */
async function register(baseUrl, payload) {
  const url = new URL('/api/auth/user-register', baseUrl).toString();

  // Chuẩn hoá dữ liệu tối thiểu
  const email = payload.email;
  const password = payload.password;
  const userName = payload.userName || email;

  const nameGuess =
    payload.fullName ||
    payload.displayName ||
    (email ? email.split('@')[0] : `user-${randomUUID().slice(0, 6)}`);

  // normalized: gửi cả camelCase và có field quan trọng FullName
  const normalized = {
    email,
    userName,
    password,
    confirmPassword: payload.confirmPassword || password,
    fullName: nameGuess,          // <- nhiều dự án yêu cầu
    FullName: nameGuess,          // <- phòng case-sensitive/Required vào đúng "FullName"
    displayName: nameGuess,
    gender: mapGender(payload.gender),
    phoneNumber: payload.phoneNumber || payload.phone || null,
    university: payload.university || null,
    dateOfBirth: payload.dateOfBirth || '2000-01-01'
  };

  const pascal = toPascalKeys(normalized);

  // Header tuỳ chọn nếu server yêu cầu quyền đặc biệt để register
  const headers = {};
  if (process.env.ADMIN_TOKEN) {
    headers.Authorization = `Bearer ${process.env.ADMIN_TOKEN}`;
  }

  // Thử theo thứ tự phổ biến nhất
  const candidates = [
    { req: normalized }, // 1) Action param tên "req" (trường hợp bạn từng gặp)
    normalized,          // 2) Không bọc
    { req: pascal },     // 3) Bọc + PascalCase
    pascal               // 4) Không bọc + PascalCase
  ];

  let lastRes = null;
  for (const body of candidates) {
    const res = await fetchJson(url, { method: 'POST', headers, body: JSON.stringify(body) });
    // ok hoặc 409 (đã tồn tại) coi như qua
    if (res.response.ok || res.response.status === 409) return res;
    lastRes = res;
  }
  return lastRes || { response: { ok: false, status: 400 }, body: { error: 'register_failed' } };
}

/** Lấy hồ sơ /auth/me */
async function getProfile(baseUrl, token) {
  const url = new URL('/api/auth/me', baseUrl).toString();
  return fetchJson(url, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` }
  });
}

/** Tạo community */
async function createCommunity(baseUrl, token, name) {
  const url = new URL('/api/communities', baseUrl).toString();
  return fetchJson(url, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      idIgnored: null,
      name,
      description: 'Realtime QA Community',
      school: 'SignalR QA Academy',
      isPublic: true
    })
  });
}

/** Tìm community */
async function searchCommunities(baseUrl, token, size = 1) {
  const url = new URL(`/api/communities?size=${size}`, baseUrl).toString();
  return fetchJson(url, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` }
  });
}

/** Tạo club */
async function createClub(baseUrl, token, communityId, name) {
  const url = new URL('/api/clubs', baseUrl).toString();
  return fetchJson(url, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      communityId,
      name,
      description: 'Realtime QA Club',
      isPublic: true
    })
  });
}

/** Tạo room */
async function createRoom(baseUrl, token, payload) {
  const url = new URL('/api/rooms', baseUrl).toString();
  return fetchJson(url, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      clubId: payload.clubId,
      name: payload.name,
      description: payload.description || 'Realtime QA Room',
      joinPolicy: payload.joinPolicy ?? 0,
      password: payload.password ?? null,
      capacity: payload.capacity ?? null
    })
  });
}

/** Join room */
async function joinRoom(baseUrl, token, roomId, password = null) {
  const url = new URL(`/api/rooms/${roomId}/join`, baseUrl).toString();
  return fetchJson(url, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({ password })
  });
}

/** Đảm bảo có user (login nếu có, không thì register rồi login) */
async function ensureUser(baseUrl, userConfig, logger) {
  const identifier = userConfig.email;

  // Thử login trước
  const loginResult = await login(baseUrl, userConfig);
  if (loginResult.response.ok) {
  const token = loginResult.body?.accessToken || loginResult.body?.AccessToken;
  if (!token) {
    throw new Error(`Login succeeded but no accessToken in body: ${JSON.stringify(loginResult.body)}`);
  }

  // Thử gọi /me
  const profile = await getProfile(baseUrl, token);
  if (profile.response.ok) {
    logger?.log('auth.login', 'PASS', { email: identifier, status: loginResult.response.status });
    return { token, user: profile.body };
  }

  // Fallback: decode JWT để lấy userId, tiếp tục test không cần /me
  if (profile.response.status === 401) {
    const claims = decodeJwtPayload(token) || {};
    const userId =
      claims.sub ||
      claims.nameid ||
      claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
      claims['http://schemas.microsoft.com/identity/claims/objectidentifier'] ||
      null;

    if (!userId) {
      throw new Error(`Failed to fetch profile (401) and cannot extract userId from JWT`);
    }

    logger?.log('auth.login', 'PASS', { email: identifier, status: loginResult.response.status, note: 'token-only (me=401)' });
    return { token, user: { id: userId, email: identifier } };
  }

  // Lỗi khác
  throw new Error(`Failed to fetch profile for ${identifier}: ${profile.response.status} ${profile.response.statusText}`);
}


  // Nếu login fail do 400/401 → thử register
  if (loginResult.response.status === 401 || loginResult.response.status === 400) {
    logger?.log('auth.register', 'INFO', { email: identifier, status: loginResult.response.status });

    const regResult = await register(baseUrl, userConfig);
    if (!regResult.response.ok && regResult.response.status !== 409) {
      throw new Error(`Failed to register ${identifier}: ${regResult.response.status} ${JSON.stringify(regResult.body)}`);
    }
    logger?.log('auth.register', 'PASS', { email: identifier, status: regResult.response.status });

    // Đăng ký xong (hoặc đã tồn tại) → login lại
    const retry = await login(baseUrl, userConfig);
    if (!retry.response.ok) {
      throw new Error(`Login-after-register failed for ${identifier}: ${retry.response.status} ${JSON.stringify(retry.body)}`);
    }
    const token = retry.body.accessToken;
    const profile = await getProfile(baseUrl, token);
    if (!profile.response.ok) {
      throw new Error(`Failed to fetch profile for ${identifier}: ${profile.response.status} ${profile.response.statusText}`);
    }
    logger?.log('auth.login', 'PASS', { email: identifier, status: retry.response.status, note: 'after register' });
    return { token, user: profile.body };
  }

  // Lỗi khác
  throw new Error(`Unexpected login failure for ${identifier}: ${loginResult.response.status} ${JSON.stringify(loginResult.body)}`);
}

/** Đảm bảo có room để test (dùng ROOM_ID nếu có; không thì create) */
async function ensureRoom(baseUrl, host, member, logger, preferredRoomId = null) {
  if (preferredRoomId) {
    logger?.log('room.ensure', 'INFO', { roomId: preferredRoomId, note: 'Using ROOM_ID from environment' });
    return preferredRoomId;
  }

  logger?.log('room.ensure', 'INFO', { note: 'Discovering/creating room' });
  const communityName = `Realtime QA Community ${randomUUID().slice(0, 8)}`;
  const communitySearch = await searchCommunities(baseUrl, host.token, 1);

  let communityId;
  if (communitySearch.response.ok && Array.isArray(communitySearch.body?.items) && communitySearch.body.items.length > 0) {
    communityId = communitySearch.body.items[0].id;
    logger?.log('room.community', 'INFO', { communityId, source: 'existing' });
  } else {
    const communityCreate = await createCommunity(baseUrl, host.token, communityName);
    if (!communityCreate.response.ok) {
      throw new Error(`Failed to create community: ${communityCreate.response.status} ${JSON.stringify(communityCreate.body)}`);
    }
    communityId = communityCreate.body?.communityId || communityCreate.body;
    logger?.log('room.community', 'PASS', { communityId, source: 'created' });
  }

  const clubName = `Realtime QA Club ${randomUUID().slice(0, 6)}`;
  const clubCreate = await createClub(baseUrl, host.token, communityId, clubName);
  if (!clubCreate.response.ok) {
    throw new Error(`Failed to create club: ${clubCreate.response.status} ${JSON.stringify(clubCreate.body)}`);
  }
  const clubId = clubCreate.body?.id || clubCreate.body?.clubId || clubCreate.body;
  logger?.log('room.club', 'PASS', { clubId });

  const roomName = `Realtime QA Room ${randomUUID().slice(0, 4)}`;
  const roomCreate = await createRoom(baseUrl, host.token, {
    clubId,
    name: roomName,
    description: 'Realtime automation room',
    joinPolicy: 0
  });
  if (!roomCreate.response.ok) {
    throw new Error(`Failed to create room: ${roomCreate.response.status} ${JSON.stringify(roomCreate.body)}`);
  }
  const roomId = roomCreate.body?.roomId || roomCreate.body?.id || roomCreate.body;
  logger?.log('room.create', 'PASS', { roomId });

  const joinResult = await joinRoom(baseUrl, member.token, roomId);
  if (!(joinResult.response.status === 204 || joinResult.response.status === 200 || joinResult.response.status === 201)) {
    throw new Error(`Member failed to join room: ${joinResult.response.status} ${JSON.stringify(joinResult.body)}`);
  }
  logger?.log('room.join', 'PASS', { roomId, member: member.user.id });

  return roomId;
}

module.exports = {
  fetchJson,
  ensureUser,
  ensureRoom,
  getProfile,
  joinRoom
};
