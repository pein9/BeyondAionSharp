"""GH-01: audit one uncaptured ordinary-fighter round from ntc-group-s4."""

import argparse
import json
from pathlib import Path


def audit(folder):
    round_record = json.loads((folder / "round.json").read_text(encoding="utf-8-sig"))
    if (round_record["from"] != "ntc-group-s4" or round_record["seed"] != 1
            or round_record["capture"] or round_record["worlds"] != 1
            or len(round_record["bots"]) != 1 or round_record["failedWorlds"]):
        raise ValueError("A survey is one uncaptured seed-1 round from the group world.")
    bot = round_record["bots"][0]
    trace_path = folder / "w1" / bot["line"] / (bot["line"] + ".trace.jsonl")
    rows = [json.loads(line) for line in trace_path.read_text(encoding="utf-8").splitlines()]
    self_id = bot["characterId"]
    starts = [r for r in rows if r["packet"] == "gh-survey-start"]
    ends = [r for r in rows if r["packet"] == "gh-survey-end"]
    fight = [r for r in rows if r["step"] == "gh-survey-fight"]
    hp = [(r["fields"]["currentHp"], r["fields"]["maxHp"])
          for r in fight if r["packet"] == "SM_STATUPDATE_HP"]
    hp += [(r["fields"]["hp"], r["fields"]["maxHp"])
           for r in fight if r["packet"] == "combat-decision"]
    percentages = [100 * current / maximum for current, maximum in hp if maximum > 0]
    incoming = [r for r in fight if r["packet"] == "SM_ATTACK_STATUS"
                and r["fields"].get("objectId") == self_id
                and r["fields"]["typeId"] not in (19, 20, 21, 22, 23)]
    percentages += [r["fields"]["hpOrMp"] for r in incoming]
    potions = [r for r in fight if r["packet"] == "combat-hot-potion"
               and r["fields"]["after"] == r["fields"]["before"] - 1]
    shield_attempts = [r for r in fight if r["packet"] == "help-item-used"
               and r["fields"].get("itemId") == 164000070]
    shields = [r for r in shield_attempts if r["fields"]["after"] == r["fields"]["before"] - 1]
    end = ends[-1]["fields"] if ends else {}
    lowest = min(percentages) if percentages else None
    def millis(row):
        hours, minutes, seconds = row["vt"].split(":")
        return round((int(hours) * 3600 + int(minutes) * 60 + float(seconds)) * 1000)

    result = {
        "run": round_record["run"], "from": round_record["from"], "line": bot["line"],
        "npcId": starts[-1]["fields"]["npcId"] if starts else None,
        "outcome": bot["outcome"], "message": bot["message"],
        "fightObserved": bool(fight), "lowestHpPercent": lowest,
        "lowestLivingHpPercent": min((p for p in percentages if p > 0), default=None),
        "lifePotions": len(potions), "antiShockScrolls": len(shields),
        "antiShockAttempts": len(shield_attempts),
        "antiShockRefusals": [r["fields"].get("refused") for r in shield_attempts
                               if r not in shields],
        "deaths": sum(r["packet"] == "SM_DIE" for r in rows),
        "killed": end.get("killed"), "retreats": end.get("retreats"),
        "hpObservations": len(percentages),
        "fightGameMillis": max(map(millis, fight)) - min(map(millis, fight)) if fight else None,
        "passed": bool(bot["outcome"] == "reached" and starts and ends
                       and lowest is not None and lowest < 50 and potions and shields),
    }
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("round", type=Path)
    args = parser.parse_args()
    result = audit(args.round)
    output = json.dumps(result, indent=2) + "\n"
    (args.round / "survey-audit.json").write_text(output, encoding="utf-8")
    print(output, end="")
    raise SystemExit(0 if result["passed"] else 1)
