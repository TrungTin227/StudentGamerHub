function getField(obj, key) {
  if (!obj) return undefined;
  if (Object.prototype.hasOwnProperty.call(obj, key)) {
    return obj[key];
  }
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  if (Object.prototype.hasOwnProperty.call(obj, pascal)) {
    return obj[pascal];
  }
  const camel = key.charAt(0).toLowerCase() + key.slice(1);
  if (Object.prototype.hasOwnProperty.call(obj, camel)) {
    return obj[camel];
  }
  const lower = key.toLowerCase();
  for (const prop of Object.keys(obj)) {
    if (prop.toLowerCase() === lower) {
      return obj[prop];
    }
  }
  return undefined;
}

module.exports = {
  getField
};
