"""Recapture the exact updated reader without replacing the historical e2b0c35e2 fixture."""
import json
import capture_npc_shop as shop

SHA='f0c1a36950f7a8aa11dbc3a5f12d179fe293ba85'

def main():
    fixture=shop.capture(SHA,shop.ROOT/'artifacts/upstream/f0c1a3695-fixed-private-store-purchases-transferring-befor/java-capture')
    (shop.ROOT/'parity-artifacts/golden/formulas/NpcShop.f0c1a3695.json').write_text(json.dumps(fixture,indent=2)+'\n',encoding='utf-8')
    print('Captured',len(fixture['packetCases']),'reads from',SHA,'; historical e2b0c35e2 fixture remains separate.')

if __name__=='__main__':main()
