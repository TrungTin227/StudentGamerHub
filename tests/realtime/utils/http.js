const fetch = require('cross-fetch');
const { randomUUID } = require('crypto');
const { URL } = require('url');

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
  } catch (err) {
    body = text;
  }

  return { response, body };
}

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

async function register(baseUrl, payload) {
  const url = new URL('/api/auth/user-register', baseUrl).toString();
  return fetchJson(url, {
    method: 'POST',
    body: JSON.stringify({
      fullName: payload.fullName,
      gender: payload.gender,
      university: payload.university,
      email: payload.email,
      phoneNumber: payload.phone,
      password: payload.password
    })
  });
}

async function getProfile(baseUrl, token) {
  const url = new URL('/api/auth/me', baseUrl).toString();
  return fetchJson(url, {
    method: 'GET',
    headers: {
      Authorization: `Bearer ${token}`
    }
  });
}

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

async function searchCommunities(baseUrl, token, size = 1) {
  const url = new URL(`/api/communities?size=${size}`, baseUrl).toString();
  return fetchJson(url, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` }
  });
}

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

async function joinRoom(baseUrl, token, roomId, password = null) {
  const url = new URL(`/api/rooms/${roomId}/join`, baseUrl).toString();
  return fetchJson(url, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({ password })
  });
}

async function ensureUser(baseUrl, userConfig, logger) {
  const identifier = userConfig.email;
  const loginResult = await login(baseUrl, userConfig);
  if (loginResult.response.ok) {
    const token = loginResult.body.accessToken;
    const profile = await getProfile(baseUrl, token);
    if (!profile.response.ok) {
      throw new Error(`Failed to fetch profile for ${identifier}: ${profile.response.status} ${profile.response.statusText}`);
    }
    logger?.log('auth.login', 'PASS', { email: identifier, status: loginResult.response.status });
    return { token, user: profile.body };
  }

  if (loginResult.response.status === 401 || loginResult.response.status === 400) {
    logger?.log('auth.register', 'INFO', { email: identifier, status: loginResult.response.status });
    const regResult = await register(baseUrl, userConfig);
    if (!regResult.response.ok) {
      throw new Error(`Failed to register ${identifier}: ${regResult.response.status} ${JSON.stringify(regResult.body)}`);
    }
    logger?.log('auth.register', 'PASS', { email: identifier, status: regResult.response.status });
    return ensureUser(baseUrl, userConfig, logger);
  }

  throw new Error(`Unexpected login failure for ${identifier}: ${loginResult.response.status} ${JSON.stringify(loginResult.body)}`);
}

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
  if (!(joinResult.response.status === 204 || joinResult.response.status === 200)) {
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
