import { spawn } from 'node:child_process';
import { mkdtemp, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import net from 'node:net';

const root = resolve(fileURLToPath(new URL('../../../', import.meta.url)));
const nodeVersion = process.versions.node.split('.').map(Number);
const supported = nodeVersion[0] >= 20;
if (!supported) throw new Error('Use a Node version supported by Angular 22; see .nvmrc.');
const frontend = resolve(fileURLToPath(new URL('../', import.meta.url)));
const data = await mkdtemp(join(tmpdir(), 'mercado-e2e-'));
const children = [];
if (process.platform !== 'win32') throw new Error('This E2E runner targets Windows 11.');
const addresses = ['http://127.0.0.1:5000/api/v1/system/health', 'http://localhost:4200'];

async function free(port) {
  return new Promise((resolvePromise) => {
    const socket = net.connect(port, '127.0.0.1');
    socket.on('connect', () => { socket.destroy(); resolvePromise(false); });
    socket.on('error', () => resolvePromise(true));
  });
}
async function waitFor(url, child) {
  for (let attempt = 0; attempt < 120; attempt++) {
    if (child.exitCode !== null) throw new Error(`${url}: server exited (${child.exitCode})`);
    try {
      const response = await fetch(url, { signal: AbortSignal.timeout(1200) });
      if (response.ok) return;
    } catch { /* Server still starting. */ }
    await new Promise(done => setTimeout(done, 500));
  }
  throw new Error(`${url}: server did not start`);
}
function start(command, args, cwd, env = {}) {
  const process = spawn(command, args, { cwd, env: { ...globalThis.process.env, ...env }, stdio: 'inherit', windowsHide: true });
  children.push(process);
  return process;
}
async function stop(child) {
  if (child.exitCode !== null) return;
  if (process.platform === 'win32') {
    await new Promise(done => {
      const killer = spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
      killer.on('exit', done);
      killer.on('error', done);
    });
  } else child.kill('SIGTERM');
}
let exit = 1;
try {
  if (!(await free(5000)) || !(await free(4200))) throw new Error('Ports 5000 and 4200 must be free for isolated E2E.');
  const dll = join(root, 'src/backend/MercadoInteligente.Api/bin/Debug/net10.0/MercadoInteligente.Api.dll');
  const api = start('dotnet', [dll], root, { MARKET_DATA_DIR: data });
  await waitFor(addresses[0], api);
  const ui = start(process.execPath, ['node_modules/@angular/cli/bin/ng.js', 'serve', '--proxy-config', 'proxy.conf.json', '--host', 'localhost', '--port', '4200'], frontend, { PORT: '' });
  await waitFor(addresses[1], ui);
  exit = await new Promise((done, reject) => {
    const runner = start(process.execPath, ['node_modules/@playwright/test/cli.js', 'test', '--config', 'e2e/playwright.config.mjs'], frontend, { MARKET_E2E_DATA_DIR: data });
    runner.on('exit', code => done(code ?? 1));
    runner.on('error', reject);
  });
  const backups = await readdir(join(data, 'backups'));
  if (exit === 0 && !backups.some(name => /^market-.*\.db$/.test(name))) throw new Error('Browser backup did not create a SQLite snapshot.');
  if (exit === 0) {
    const endpoint = 'http://127.0.0.1:5000/api/v1/baskets';
    const before = await (await fetch(endpoint)).json();
    if (!before.length || !before.some(basket => basket.items?.length)) throw new Error('E2E did not persist a populated basket.');
    const historyEndpoint = 'http://127.0.0.1:5000/api/v1/history?limit=500';
    const historyBefore = process.env.MARKET_LIVE_E2E === '1' ? await (await fetch(historyEndpoint)).json() : null;
    if (historyBefore && !['D1', 'EXITO'].every(code => historyBefore.observations?.some(row => row.retailerCode === code)))
      throw new Error('LIVE E2E did not persist historical observations for both regional providers.');
    await stop(api);
    for (let attempt = 0; attempt < 30 && !(await free(5000)); attempt++)
      await new Promise(done => setTimeout(done, 200));
    if (!(await free(5000))) throw new Error('API did not release port 5000 for restart.');
    const restarted = start('dotnet', [dll], root, { MARKET_DATA_DIR: data });
    await waitFor(addresses[0], restarted);
    const after = await (await fetch(endpoint)).json();
    if (JSON.stringify(before) !== JSON.stringify(after)) throw new Error('Baskets changed after API process restart.');
    if (historyBefore) {
      const historyAfter = await (await fetch(historyEndpoint)).json();
      if (JSON.stringify(historyBefore.observations) !== JSON.stringify(historyAfter.observations))
        throw new Error('Price observations changed after API process restart.');
    }
    const health = await (await fetch(addresses[0])).json();
    if (health.database !== 'ok') throw new Error('SQLite integrity failed after API process restart.');
    console.log(`E2E restart PASS: ${after.length} baskets, ${historyBefore?.observations?.length ?? 0} checked LIVE observations, SQLite ${health.database}.`);
  }
} catch (error) {
  console.error(error);
  exit = 1;
} finally {
  for (const child of children.reverse()) await stop(child);
  await rm(data, { recursive: true, force: true, maxRetries: 5, retryDelay: 500 });
}
process.exitCode = exit;
