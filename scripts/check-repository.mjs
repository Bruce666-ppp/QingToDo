import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
let checked = 0;
const findings = [];
async function walk(folder) {
  for (const item of await fs.readdir(folder, { withFileTypes: true })) {
    if (['.git', '.build', 'node_modules'].includes(item.name)) continue;
    const file = path.join(folder, item.name), relative = path.relative(root, file).replaceAll('\\', '/');
    if (item.isDirectory()) { assert.notEqual(item.name.toLowerCase(), 'data', `Personal data directory: ${relative}`); await walk(file); continue; }
    if (/^(?:\.env(?:\.|$)|id_rsa|id_ed25519)/i.test(item.name) || /\.(?:pem|key|pfx|p12)$/i.test(item.name) || /^todos\.json/i.test(item.name)) findings.push(relative);
    if (/\.(?:cs|ps1|md|txt|json|yml|html|js|mjs|css|svg|manifest)$/.test(item.name) || item.name.startsWith('.git')) {
      const text = await fs.readFile(file, 'utf8');
      const tokenPattern = new RegExp('(?:gh[pousr]_' + '[A-Za-z0-9]{30,}|github_pat_' + '[A-Za-z0-9_]{30,}|sk-' + '[A-Za-z0-9_-]{35,}|-----BEGIN ' + '(?:RSA |EC |OPENSSH )?PRIVATE KEY-----)');
      if (tokenPattern.test(text)) findings.push(relative + ' (possible secret)');
    }
    checked++;
  }
}
await walk(root);
assert.deepEqual(findings, [], 'Forbidden or sensitive repository files found');
console.log(`Repository checks passed: ${checked} files; no local task data, .env or detected keys/tokens.`);
