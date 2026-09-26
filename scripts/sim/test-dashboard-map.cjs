// Portal calibration anchors and independent shipped-data checks prevent a plausible but misplaced overlay.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const { projectMapPoint } = require(path.join(root, 'tests/Aion.Bots/Dashboard/dashboard-map.js'));
const catalog = JSON.parse(fs.readFileSync(path.join(root, 'tests/Aion.Bots/Dashboard/maps/catalog.json')));
const ishalgen = catalog.maps.find(m => m.mapId === 220010000);
assert.deepEqual(projectMapPoint({ x: 0, y: 740 }, ishalgen.calibration), { x: 0, y: 0 });
assert.deepEqual(projectMapPoint({ x: 2300, y: 3040 }, ishalgen.calibration), { x: 1, y: 1 });
assert.deepEqual(projectMapPoint({ x: 1150, y: 1890 }, ishalgen.calibration), { x: .5, y: .5 });
// Game X grows downward on the portal artwork; game Y grows rightward.
assert.equal(projectMapPoint({ x: 230, y: 740 }, ishalgen.calibration).y, .1);
assert.equal(projectMapPoint({ x: 0, y: 510 }, ishalgen.calibration).x, -.1);
for (const map of catalog.maps) {
  let count = 0;
  for (const source of map.sources) {
    const data = fs.readFileSync(path.join(root, source.path));
    assert.equal(crypto.createHash('sha256').update(data.toString().replace(/\r\n/g, '\n')).digest('hex'), source.sha256,
      `${source.path} changed; run python scripts/sim/import-dashboard-maps.py`);
    count += (data.toString().replace(/<!--[\s\S]*?-->/g, '').match(/<spot\s/g) || []).length;
  }
  assert.equal(map.spawns.length, count, `${map.name}: every shipped placement must be shown`);
  assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(root,
    'tests/Aion.Bots/Dashboard', map.image))).digest('hex'), map.imageSha256);
}
assert.ok(ishalgen.spawns.some(s => s.templateId === 210409 && s.kind === 'mob'));
assert.ok(ishalgen.spawns.some(s => s.templateId === 203550 && s.kind === 'npc'));
assert.ok(ishalgen.spawns.some(s => s.kind === 'gatherable'));
console.log('Dashboard map: calibration, image hashes and all shipped spawn placements verified.');
