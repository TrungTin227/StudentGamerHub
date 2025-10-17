const IORedis = require('ioredis');

function createRedisClient(redisUrl) {
  return new IORedis(redisUrl, {
    lazyConnect: true,
    maxRetriesPerRequest: 2,
    enableReadyCheck: true
  });
}

module.exports = {
  createRedisClient
};
