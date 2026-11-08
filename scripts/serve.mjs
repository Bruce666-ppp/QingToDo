import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'site');
const types = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.svg': 'image/svg+xml', '.txt': 'text/plain; charset=utf-8', '.zip': 'application/zip', '.exe': 'application/octet-stream' };
const port = Number(process.env.PORT || 4173);
const server = http.createServer(async (req, res) => {
  try {
    let pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    if (pathname === '/QingToDo') { res.writeHead(301, { Location: '/QingToDo/' }); res.end(); return; }
    if (pathname.startsWith('/QingToDo/')) pathname = pathname.slice('/QingToDo'.length);
    const target = path.resolve(root, '.' + pathname);
    if (target !== root && !target.startsWith(root + path.sep)) throw new Error('Invalid path');
    let file = target;
    const info = await fs.stat(file);
    if (info.isDirectory()) {
      if (!pathname.endsWith('/')) { res.writeHead(301, { Location: req.url + '/' }); res.end(); return; }
      file = path.join(file, 'index.html');
    }
    const bytes = await fs.readFile(file);
    res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store', 'Content-Length': bytes.length });
    res.end(req.method === 'HEAD' ? undefined : bytes);
  } catch { res.writeHead(404, { 'Content-Type': 'text/html; charset=utf-8' }); res.end(await fs.readFile(path.join(root, '404.html'))); }
});
server.listen(port, '127.0.0.1', () => console.log(`Local: http://127.0.0.1:${port}/QingToDo/`));
