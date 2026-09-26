"""Copy portal artwork/calibration and snapshot this checkout's normal spawn references.

No portal writes or 5.8 comparison overlays. Re-run after changing spawn XML.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--portal", type=Path, default=ROOT.parent / "aion-portal")
    args = parser.parse_args()
    portal = args.portal / "assets/maps"
    output = ROOT / "tests/Aion.Bots/Dashboard/maps"
    output.mkdir(parents=True, exist_ok=True)
    static = ROOT / "game-server/data/static_data"
    templates = {}
    for filename, tag, key in [("npcs/npc_templates.xml", "npc_template", "npc_id"),
                               ("gatherables/gatherable_templates.xml", "gatherable_template", "id")]:
        for _, element in ET.iterparse(static / filename):
            if element.tag == tag:
                templates[int(element.attrib[key])] = dict(element.attrib)
                element.clear()
    maps = []
    for definition in json.loads((portal / "manifest.json").read_text())['maps']:
        if definition['mapId'] not in (220010000, 320010000):
            continue
        layer = definition['layers'][0]
        image = portal / layer['asset']
        digest = hashlib.sha256(image.read_bytes()).hexdigest()
        if digest != layer['assetSha256']:
            raise ValueError(f"Portal image hash disagrees with manifest: {image}")
        shutil.copyfile(image, output / image.name)
        sources = list(definition['sourceRelativePaths'])
        sources += [p.relative_to(ROOT).as_posix() for p in sorted((static / "spawns/Gather").glob(f"{definition['mapId']}_*.xml"))]
        spawns, receipts = [], []
        for source in sources:
            path = ROOT / source
            receipts.append({'path': source, 'sha256': hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()})
            for spawn_map in ET.parse(path).getroot().iter('spawn_map'):
                if int(spawn_map.attrib['map_id']) != definition['mapId']:
                    continue
                for spawn in spawn_map.findall('spawn'):
                    template_id = int(spawn.attrib['npc_id'])
                    template = templates.get(template_id, {})
                    kind = 'gatherable' if '/Gather/' in source else 'mob' if template.get('type') == 'MONSTER' else 'npc'
                    for spot in spawn.findall('spot'):
                        spawns.append({'templateId': template_id, 'name': template.get('name', str(template_id)).strip().title(),
                                       'kind': kind, 'level': int(template.get('level', 0)),
                                       'x': float(spot.attrib['x']), 'y': float(spot.attrib['y']), 'z': float(spot.attrib['z']),
                                       'heading': int(spot.get('h', 0)),
                                       'condition': spawn.get('time', 'ALL'), 'pool': int(spawn.get('pool', 0))})
        maps.append({'mapId': definition['mapId'], 'name': definition['name'],
                     'image': '/maps/' + image.name, 'assetKind': layer['assetKind'], 'imageSha256': digest,
                     'calibration': definition['calibration'], 'projection': 'calibrated-game-y-x',
                     'sources': receipts, 'spawns': spawns})
        print(f"{definition['name']}: {len(spawns)} normal spawn references")
    (output / 'catalog.json').write_text(json.dumps({'maps': maps}, separators=(',', ':')) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
