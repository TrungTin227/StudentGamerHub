const signalR = require('@microsoft/signalr');
const WebSocket = require('ws');
const fetch = require('cross-fetch');

function prepareEnvironment() {
  if (typeof global.WebSocket === 'undefined') {
    global.WebSocket = WebSocket;
  }
  if (typeof global.fetch === 'undefined') {
    global.fetch = fetch;
  }
}

function buildConnection(url, token, logger, caseName) {
  prepareEnvironment();
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => token,
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

  connection.onreconnecting(err => {
    logger?.log('signalr.reconnecting', 'INFO', { case: caseName, message: err?.message });
  });
  connection.onreconnected(id => {
    logger?.log('signalr.reconnected', 'INFO', { connectionId: id });
  });
  connection.onclose(err => {
    if (err) {
      logger?.log('signalr.closed', 'FAIL', { message: err.message });
    } else {
      logger?.log('signalr.closed', 'INFO', { graceful: true });
    }
  });

  return connection;
}

module.exports = {
  buildConnection,
  prepareEnvironment
};
