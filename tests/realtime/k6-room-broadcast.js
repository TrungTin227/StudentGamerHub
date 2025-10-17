import http from 'k6/http';
import ws from 'k6/ws';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const baseUrl = __ENV.BASE_URL || 'https://localhost:7227';
const hubUrl = (__ENV.HUB_URL || `${baseUrl}/ws/chat`).replace(/\/$/, '');
const roomId = __ENV.ROOM_ID;
const accessToken = __ENV.ACCESS_TOKEN;
const vusTarget = Number(__ENV.VUS || 50);
const holdDuration = __ENV.HOLD_DURATION || '1m';
const messageIntervalMs = Number(__ENV.MESSAGE_INTERVAL_MS || 1000);
const sessionDurationMs = Number(__ENV.SESSION_DURATION_MS || 60000);

if (!roomId) {
  throw new Error('ROOM_ID environment variable is required for k6 broadcast test.');
}
if (!accessToken) {
  throw new Error('ACCESS_TOKEN environment variable is required for k6 broadcast test.');
}

export const options = {
  scenarios: {
    broadcast: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: vusTarget },
        { duration: holdDuration, target: vusTarget },
        { duration: '30s', target: 0 }
      ],
      gracefulRampDown: '10s'
    }
  }
};

const latencyTrend = new Trend('room_broadcast_latency', true);
const errorRate = new Rate('room_broadcast_errors');

function parseMessages(data) {
  const records = [];
  const frames = data.split('\u001e').filter(Boolean);
  for (const frame of frames) {
    try {
      records.push(JSON.parse(frame));
    } catch (err) {
      // skip invalid frame
    }
  }
  return records;
}

export default function () {
  const negotiateUrl = `${hubUrl}/negotiate?negotiateVersion=1`;
  const negotiateRes = http.post(negotiateUrl, null, {
    headers: {
      Authorization: `Bearer ${accessToken}`
    }
  });

  check(negotiateRes, {
    'negotiate status 200': (r) => r.status === 200
  });

  const negotiateBody = negotiateRes.json();
  const connectionId = negotiateBody.connectionId;
  if (!connectionId) {
    errorRate.add(1);
    throw new Error(`Missing connectionId in negotiate response: ${negotiateRes.body}`);
  }

  const wsUrl = hubUrl
    .replace(/^http/, 'ws')
    .concat(`?id=${connectionId}&access_token=${encodeURIComponent(accessToken)}`);

  const sendMap = new Map();

  const res = ws.connect(wsUrl, {}, (socket) => {
    const closeTimer = socket.setTimeout(() => {
      socket.close();
    }, sessionDurationMs);

    socket.on('open', () => {
      socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e');
      const joinMessage = {
        type: 1,
        target: 'JoinChannels',
        arguments: [[`room:${roomId}`]]
      };
      socket.send(JSON.stringify(joinMessage) + '\u001e');
    });

    socket.on('message', (data) => {
      const records = parseMessages(data);
      for (const record of records) {
        if (record.type === 6) {
          // ping/keep-alive
          continue;
        }
        if (record.type === 1 && record.target === 'msg' && Array.isArray(record.arguments)) {
          const payload = record.arguments[0];
          if (payload?.text && sendMap.has(payload.text)) {
            const started = sendMap.get(payload.text);
            const latency = Date.now() - started;
            latencyTrend.add(latency);
            sendMap.delete(payload.text);
          }
        }
      }
    });

    socket.on('error', (err) => {
      errorRate.add(1);
    });

    socket.on('close', () => {
      socket.clearTimeout(closeTimer);
    });

    const sender = () => {
      const text = `k6-room-${__VU}-${Date.now()}`;
      sendMap.set(text, Date.now());
      const message = {
        type: 1,
        target: 'SendToRoom',
        arguments: [roomId, text]
      };
      socket.send(JSON.stringify(message) + '\u001e');
    };

    sender();
    const interval = socket.setInterval(() => {
      sender();
    }, messageIntervalMs);

    socket.setTimeout(() => {
      socket.clearInterval(interval);
      socket.close();
    }, sessionDurationMs);
  });

  check(res, {
    'ws status 101': (r) => r && r.status === 101
  });

  sleep(1);
}
