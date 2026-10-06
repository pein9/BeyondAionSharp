"""Audit the revised fresh-character SIM using its packet and checkpoint receipts."""
import argparse
import collections
import hashlib
import json
import struct
import xml.etree.ElementTree as ET
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('evidence', type=Path)
parser.add_argument('build', help='Committed runtime SHA expected in the trace')
args = parser.parse_args()
folder = args.evidence.resolve()
repo = Path(__file__).resolve().parents[2]
read = lambda name: json.loads((folder / name).read_text(encoding='utf-8-sig'))
save = lambda name, value: (folder / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
done = read('continuous-completion.json')
assert done['verified'] and done['CreatedCharacter'] and done['LaterCapital']
character = done['CharacterId']
order = [f'l{i}' for i in range(1, 12)] + ['cg', 'l12']
assert [s['Stage'] for s in done['stages']] == ['ishalgen-ascension'] + order
login = read('login-observation.json')
assert login['CharacterId'] == character and login['Level'] == 1 and login['MapId'] == 220010000
early = read('early-ascension-completion.json')
assert early['before']['Level'] == 9 and early['before']['PlayerClass'] == 9
capital = read('capital-pass-completion.json')
assert capital['verified'] and capital['CharacterId'] == character
first = json.loads((repo / 'parity-artifacts/e2e/natural-capital-contract.json').read_text())
first_ids = {q['id'] for q in first['quests']}
assert first_ids <= set(capital['after']['CompletedQuestIds'])
assert {p['QuestId'] for p in capital['Payments']} == first_ids and len(capital['Payments']) == 10
assert capital['Experience'] - capital['StartingExperience'] == sum(p['Experience'] for p in capital['Payments'])
later = json.loads((repo / 'parity-artifacts/e2e/natural-later-capital-contract.json').read_text())
later_ids = {q['id'] for q in later['quests']}
previous = read('bridge-completion.json')['after']
assert {2008, 2009, 2904, 24010} <= set(previous['CompletedQuestIds'])
bridge_receipt = read('later-capital-bridge-checkpoint.json')
assert bridge_receipt['verified'] and bridge_receipt['segment'] == 'bridge' and bridge_receipt['characterId'] == character
assert bridge_receipt['before'] == bridge_receipt['after']
assert bridge_receipt['after']['characterId'] == character and bridge_receipt['after']['level'] == previous['Level']
last_clock = done['stages'][0]['ElapsedMillis']
last_deaths = done['stages'][0]['Deaths']
for leg, stage in zip(order, done['stages'][1:]):
    proof = read(f'altgard-{leg}-completion.json')
    receipt = read(f'later-capital-{leg}-checkpoint.json')
    after = proof['after']
    assert proof['verified'] and proof['CharacterId'] == character and after['CharacterId'] == character
    assert not after['IsDead'] and set(previous['CompletedQuestIds']) <= set(after['CompletedQuestIds'])
    assert receipt['verified'] and receipt['segment'] == leg and receipt['characterId'] == character
    assert receipt['before'] == receipt['after']
    assert stage['StartedMillis'] >= last_clock and stage['ElapsedMillis'] > stage['StartedMillis']
    assert stage['StartedExperience'] <= stage['Experience'] and stage['Level'] == after['Level']
    assert stage['Deaths'] == proof['Deaths'] and stage['NewDeaths'] == stage['Deaths'] - last_deaths
    last_clock, last_deaths, previous = stage['ElapsedMillis'], stage['Deaths'], after
final = done['Endpoint']
counts = {q['QuestId']: q['CompleteCount'] for q in final['CompletedQuests']}
expected = {2008, 2009, 2904, 24010} | first_ids | later_ids
expected.update(q['id'] for q in json.loads((repo / 'parity-artifacts/e2e/natural-ishalgen-contract.json').read_text())['quests'])
for contract in (repo / 'parity-artifacts/e2e').glob('natural-altgard*-contract.json'):
    if 'probe' not in contract.name:
        expected.update(q['id'] for q in json.loads(contract.read_text())['quests'])
assert set(counts) == expected, dict(Missing=sorted(expected - set(counts)), Unexpected=sorted(set(counts) - expected))
for quest in expected:
    assert counts[quest] == 1, quest
assert 24114 not in counts
items = collections.Counter()
for item in final['Inventory']:
    items[item['ItemId']] += item['Count']
assert (items[186000006], items[186000007], items[188053787]) == (19, 7, 1)
assert items[169100000] == items[169200002] == 8
assert not any(items[i] for i in (182207007, 182207008, 182207009, 182207013, 182207026, 182207040, 182207064, 140000001, 140000098))
assert not any(s['SkillId'] == 11504 or s['SkillType'] in (1, 3) for s in final['Skills'])
cg = read('altgard-cg-completion.json')['after']['CoinGearProgress']
assert sorted(p['ItemId'] for p in cg['Purchases']) == [111501065, 112501015, 113501074]
assert sum(p['BeforeCoins'] - p['AfterCoins'] for p in cg['Purchases']) == 4
earned = [i for i in read('altgard-l10-completion.json')['after']['Inventory'] if i['ItemId'] == 101501357]
assert len(earned) == 1 and earned[0]['Count'] == 1
staff = earned[0]
assert cg['StaffObjectId'] == staff['ObjectId']
for leg in ('l11', 'cg', 'l12'):
    carried = [i for i in read(f'altgard-{leg}-completion.json')['after']['Inventory'] if i['ItemId'] == staff['ItemId']]
    assert len(carried) == 1 and (carried[0]['ObjectId'], carried[0]['Count']) == (staff['ObjectId'], 1), leg
    # Java grants the earned item; ordinary CG preparation equips it before the reward receipt.
    if leg in ('cg', 'l12'):
        assert carried[0]['EquipmentSlot'] == 3, leg
    else:
        assert carried[0]['EquipmentSlot'] in (0, 3, 65535), leg
assert all(any(i['ObjectId'] == p['ObjectId'] and i['ItemId'] == p['ItemId'] and i['EquipmentSlot'] not in (0, 65535)
               for i in final['Inventory']) for p in cg['Purchases'])
visits = final['HaramelProgress']['Visits']
assert sum(v['BossMovieObserved'] and v['ChestResolved'] for v in visits) >= 2
assert any(v['PostBossQuests'] and v['FreshSpawnsObserved'] for v in visits)
assert final['MapId'] == 220030000 and not final['IsDead'] and done['ElapsedMillis'] == last_clock and done['Deaths'] == last_deaths
assert (final['Position']['X'] - 1660.43) ** 2 + (final['Position']['Y'] - 1813.49) ** 2 <= 60 ** 2

trace = next(folder.glob('*.trace.jsonl'))
packets, uses, removed = collections.Counter(), collections.Counter(), collections.Counter()
inventory, payments, deaths, outcomes, costs, starts, maps = {}, [], [], [], [], [], set()
book_consumption = []
quantities = {}
stage, position, combat, build, kinah = 'ishalgen', None, None, None, None
gross_received = gross_spent = revives = 0
for line_number, line in enumerate(trace.open(encoding='utf-8'), 1):
    row = json.loads(line)
    packet, fields = row['packet'], row['fields']
    packets[packet] += 1
    if packet == 'natural-run-context':
        build = fields['context']['Build']
    if packet == 'continuous-leg-start':
        stage = fields['leg']
        starts.append(stage)
    if packet == 'CM_MOVE':
        position = list(struct.unpack('<fff', bytes.fromhex(fields['bodyHex'])[:12]))
    if packet == 'combat-decision':
        combat = row
    if packet == 'SM_PLAYER_SPAWN':
        maps.add(fields.get('worldId', fields.get('mapId')))
    if packet == 'SM_DIE':
        deaths.append(dict(Number=len(deaths) + 1, Stage=stage, GameTime=row['vt'], TraceLine=line_number,
                           RecordedStep=row['step'], LastObservedPosition=position,
                           LastCombatStep=combat['step'] if combat else None, DeathPacket=fields))
    if packet == 'accept-client-death-and-revive-at-bound-obelisk':
        revives += 1
    if 'escort' in packet or 'carrier' in packet or 'timed' in packet:
        outcomes.append(row)
    if packet == 'later-capital-payment':
        payments.append(fields)
    if packet == 'later-capital-book-material-consumption':
        book_consumption.append(fields)
    if packet.endswith('result') and any(word in packet for word in ('shop', 'service', 'trade')):
        costs.append(row)
    for item in fields.get('items', []) if packet in ('SM_INVENTORY_INFO', 'SM_INVENTORY_ADD_ITEM') else []:
        inventory[item['objectId']] = item['itemId']
        quantities[item['objectId']] = item['itemCount']
        if item['itemId'] == 182400001:
            kinah = item['itemCount']
    if packet == 'SM_INVENTORY_UPDATE_ITEM' and inventory.get(fields['objectId']) == 182400001:
        current = fields['itemCount']
        if kinah is not None:
            gross_received += max(0, current - kinah)
            gross_spent += max(0, kinah - current)
        kinah = current
    if packet == 'SM_INVENTORY_UPDATE_ITEM':
        object_id, current = fields['objectId'], fields['itemCount']
        if object_id in inventory and object_id in quantities and current is not None:
            removed[inventory[object_id]] += max(0, quantities[object_id] - current)
            quantities[object_id] = current
    if packet == 'SM_DELETE_ITEM':
        object_id = fields['itemObjectId']
        if object_id in inventory:
            removed[inventory[object_id]] += quantities.pop(object_id, 0)
    if packet == 'CM_USE_ITEM':
        object_id = int.from_bytes(bytes.fromhex(fields['bodyHex'])[:4], 'little')
        assert object_id in inventory, ('unknown used item', object_id)
        uses[inventory[object_id]] += 1
assert args.build in build and starts == order and packets['create-natural-asmodian-priest'] == 1
assert {120010000, 120020000, 220010000, 220030000, 320010000, 320030000, 320070000, 300200000} <= maps, maps
assert len(deaths) == done['Deaths'] and revives <= len(deaths)
assert {p['quest'] for p in payments} == later_ids and len(payments) == 9
assert len(book_consumption) == 1 and book_consumption[0]['consumedHere']
book = book_consumption[0]
assert book['required'] == {'182207010': 3, '182207011': 2, '182207012': 1}
for item, required in book['required'].items():
    assert book['before'][item] - book['after'][item] == required and items[int(item)] == book['after'][item]
save('book-material-consumption.json', book)
quest_data = {int(q.get('id')): q for q in ET.parse(repo / 'game-server/data/static_data/quest_data/quest_data.xml').getroot()}
for p in payments:
    assert p['experience'] == int(quest_data[p['quest']].find('rewards').get('exp', '0'))
help_items = read('help-items.json')['helpItems']['supplied']
supplied = collections.Counter()
for delivery in help_items:
    supplied[delivery['ItemId']] += delivery['Count']
ledger_ids = set(supplied) | {i for i in uses if str(i).startswith(('160', '162', '164', '1693'))}
save('consumable-ledger.json', dict(Note='Use packets are attempts. InventoryUnitsRemoved includes consumption, sales and other removals; mixed stacks do not identify which supplied or earned unit was consumed.',
     Items=[dict(ItemId=i, Supplied=supplied[i], UsePackets=uses[i], InventoryUnitsRemoved=removed[i], Closing=items[i]) for i in sorted(ledger_ids)], Supplies=help_items))
save('death-ledger.json', dict(verified=True, CharacterId=character, SourceTrace=trace.name, RecordedDeaths=deaths, BindRevives=revives))
save('recorded-outcomes.json', outcomes)
save('cost-ledger.json', dict(FirstCapital=capital['Payments'], LaterCapital=payments, FirstCapitalTransport=capital['TransportFares'],
     GrossReceivedFromCurrencyUpdates=gross_received, GrossSpentFromCurrencyUpdates=gross_spent, CoinPurchases=cg['Purchases'], Events=costs))
save('stage-ledger.json', done['stages'])
provenance, cleanup = read('schema-provenance.json'), read('schema-cleanup.json')
assert provenance['owned'] and provenance['createdFresh'] and not provenance['restored'] and provenance['elapsedMillis'] == 0
assert cleanup['dropped'] and cleanup['database'] == provenance['database']
audit = dict(verified=True, CharacterId=character, Level=final['Level'], Completed=len(counts), Deaths=done['Deaths'],
             ElapsedMillis=last_clock, Experience=done['Experience'], ExperienceToNextLevel=done['ExperienceToNextLevel'],
             ExperienceToLevel32=done['ExperienceToLevel32'], Kinah=items[182400001], Iron=items[186000006], Bronze=items[186000007],
             StaffObjectId=staff['ObjectId'], HaramelVisits=len(visits), Build=build,
             TraceSha256=hashlib.file_digest(trace.open('rb'), 'sha256').hexdigest(), RegularSkills=final['Skills'],
             Equipped=[i for i in final['Inventory'] if i['EquipmentSlot'] not in (0, 65535)], LegOrder=order, Maps=list(maps), Packets=dict(packets))
save('audit.json', audit)
print(json.dumps({k: v for k, v in audit.items() if k not in ('RegularSkills', 'Equipped', 'Packets')}, indent=2))
