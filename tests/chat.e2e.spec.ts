import { test, expect } from '@playwright/test';

const CHAT_PAGE_URL = process.env.CHAT_PAGE_URL ?? 'https://localhost:7163/Chat';
const ROOM_ID = process.env.CHAT_TEST_ROOM ?? 'room-123';
const TEST_MESSAGE = process.env.CHAT_TEST_MESSAGE ?? 'Hello from Playwright';

async function waitForLog(page, text, timeout = 15000) {
  return page.waitForSelector(`#log li:has-text("${text}")`, { timeout });
}

test.describe('Realtime chat test page', () => {
  test('logs in and sends a room message', async ({ page }) => {
    const loginResponsePromise = page.waitForResponse((response) =>
      response.url().endsWith('/api/auth/login') && response.status() === 200
    );

    const websocketPromise = page.waitForEvent('websocket', (ws) => ws.url().includes('/ws/chat'));

    await page.goto(CHAT_PAGE_URL, { waitUntil: 'domcontentloaded' });

    await test.step('SignalR client script is loaded', async () => {
      const hasSignalR = await page.evaluate(() => typeof window.signalR !== 'undefined');
      expect(hasSignalR).toBeTruthy();
    });

    await page.click('#loginForm button[type="submit"]');

    const loginResponse = await loginResponsePromise;
    const loginJson = await loginResponse.json();
    const token = loginJson.accessToken ?? loginJson.AccessToken ?? null;
    expect(token, 'Login response missing access token').toBeTruthy();

    const websocket = await websocketPromise;
    await expect.poll(() => websocket.isClosed()).toBeFalsy();

    await waitForLog(page, 'Connected to /ws/chat.');

    await page.fill('#roomId', ROOM_ID);
    await page.click('button:has-text("Join Room")');

    await page.fill('#message', TEST_MESSAGE);
    const sendLogPromise = waitForLog(page, `[SEND ROOM] #${ROOM_ID}: ${TEST_MESSAGE}`);
    await page.click('button:has-text("Send To Room")');
    await sendLogPromise;

    const messageLog = await page.waitForSelector('#log li:has-text("[MSG]")', { timeout: 15000 }).catch(() => null);
    expect(messageLog, 'Expected at least one [MSG] log entry after sending to room').not.toBeNull();

    console.log('Network summary:', {
      loginStatus: loginResponse.status(),
      loginUrl: loginResponse.url(),
      websocketUrl: websocket.url(),
    });
  });
});
