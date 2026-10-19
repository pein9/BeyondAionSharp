"""Verify the actual Python spawn-table generator against the captured Java cases and native source conventions."""
import importlib.util,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('spawn_generator',Path(__file__).with_name('generate-handler-spawn-ids.py'));generator=importlib.util.module_from_spec(spec);spec.loader.exec_module(generator)
fixture=json.loads((root/'parity-artifacts/golden/formulas/QuestSpawn.cabd580f3.json').read_text())
def ids(source):return sorted({int(v) for m in generator.SPAWN_ARGUMENT.finditer(source) for v in generator.NPC_ID.findall(m.group('argument'))})
for case in fixture['regexCases']:assert ids(case['csharpSource'])==case['expected'],case
assert ids('x Spawn (211636, 1);')==[211636]
assert ids('x Spawn(npcId == 236227 ? 855708 : 855709, 1);')==[236227,855708,855709]
assert ids('x Spawn(1234567, 1);')==[]
assert generator.OUTPUT.read_text(encoding='utf-8-sig')==generator.render(generator.collect_ids())
print('15 exact Java source vectors, native whitespace/conditional/numeric boundaries and regenerated table pass')
