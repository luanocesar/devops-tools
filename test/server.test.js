'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { createServer } = require('../server/server');

test('create project, download client script, deploy', async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dt-'));
  let deployed = null;
  const srv = createServer({ dataDir: dir, runDeploy: async (p, z) => { deployed = p.name; return 'ok'; } });
  await new Promise((r) => srv.listen(0, r));
  const base = `http://127.0.0.1:${srv.address().port}/devops-tools`;
  try {
    let r = await fetch(`${base}/api/projects`, { method: 'POST', body: JSON.stringify({ name: '../bad', siteName: 's', apps: [{ name: 'a', targetPath: 'c:\\x' }] }) });
    assert.strictEqual(r.status, 400);
    r = await fetch(`${base}/api/projects`, { method: 'POST', body: JSON.stringify({ name: 'demo', siteName: 's', backendAppPool: 'p', clientBuildCommands: ['dotnet publish'], apps: [{ name: 'api', targetPath: 'c:\\x' }] }) });
    assert.strictEqual(r.status, 201);
    const { installUrl } = await r.json();
    const bat = await (await fetch(installUrl)).text();
    assert.match(bat, /dotnet publish/);
    assert.strictEqual((await fetch(`${base}/install-client?project=demo&token=x`)).status, 404);
    assert.strictEqual((await fetch(`${base}/api/projects/demo/deploy`, { method: 'POST', body: 'z' })).status, 401);
    const token = fs.readFileSync(path.join(dir, 'projects', 'demo', 'project.json'), 'utf8').match(/"token": "(\w+)"/)[1];
    r = await fetch(`${base}/api/projects/demo/deploy`, { method: 'POST', headers: { 'X-Devops-Token': token }, body: 'zipdata' });
    assert.strictEqual(r.status, 200);
    assert.strictEqual(deployed, 'demo');
  } finally { srv.close(); }
});
