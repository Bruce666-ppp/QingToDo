import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'site');
const files = [];
async function walk(folder) { for (const entry of await fs.readdir(folder, { withFileTypes: true })) { const p = path.join(folder, entry.name); if (entry.isDirectory()) await walk(p); else files.push(p); } }
await walk(root);
let references = 0;
for (const file of files.filter(p => p.endsWith('.html'))) {
  const html = await fs.readFile(file, 'utf8');
  assert.match(html, /<title>[^<]+<\/title>/, `${file}: title missing`);
  assert.match(html, /name="viewport"/, `${file}: viewport missing`);
  for (const [, raw] of html.matchAll(/(?:href|src)="([^"]+)"/g)) {
    if (/^(?:https?:|mailto:|tel:|data:|#)/i.test(raw)) continue;
    assert.ok(!raw.startsWith('/'), `Root-relative asset breaks project Pages: ${raw}`);
    const clean = decodeURIComponent(raw.split(/[?#]/)[0]);
    let target = path.resolve(path.dirname(file), clean);
    assert.ok(target === root || target.startsWith(root + path.sep), `Path escaped website: ${raw}`);
    const stat = await fs.stat(target).catch(() => null);
    assert.ok(stat, `Missing asset ${raw} in ${file}`);
    if (stat.isDirectory()) target = path.join(target, 'index.html');
    await fs.access(target); references++;
  }
}
const installer = await fs.readFile(path.join(root, 'downloads', 'QingToDo-Setup.exe'));
const zip = await fs.readFile(path.join(root, 'downloads', 'QingToDo-Windows.zip'));
assert.equal(installer.subarray(0, 2).toString(), 'MZ', 'Installer is not a Windows EXE');
assert.equal(zip.subarray(0, 2).toString(), 'PK', 'Download is not a ZIP');
assert.ok(installer.length > 10000 && zip.length > 10000, 'Download files are unexpectedly small');
const home = await fs.readFile(path.join(root, 'index.html'), 'utf8');
assert.equal((home.match(/class="sample-check"/g) || []).length, 3, 'The preview must show three tasks');
assert.ok(!/<form\b/i.test(home), 'This project site must not present a non-working contact form');
assert.match(home, /Bruce666-ppp\/QingToDo\/issues/, 'Feedback destination missing');
console.log(`Website checks passed: ${files.length} assets, ${references} local references, homepage, direct guide route and two valid downloads.`);
