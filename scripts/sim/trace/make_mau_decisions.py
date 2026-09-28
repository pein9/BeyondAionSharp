#!/usr/bin/env python3
"""Extract auditable baseline decisions; outcomes describe only the executed path."""

import argparse
import hashlib
import json
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_dir", type=Path)
    args = parser.parse_args()
    traces = list(args.run_dir.glob("*.trace.jsonl"))
    if len(traces) != 1:
        raise ValueError(f"Expected one JSONL trace in {args.run_dir}; found {len(traces)}")
    rows = [json.loads(line) for line in traces[0].open(encoding="utf-8")]
    trace_line_by_identity = {id(row): index + 1 for index, row in enumerate(rows)}
    summary_path = args.run_dir / "summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    if digest(traces[0]) != summary["traceSha256"]:
        raise ValueError("Summary and trace digests differ")
    encounter_ends = {row["fields"]["encounterId"]: row for row in rows
                      if row["packet"] == "combat-encounter-end"}
    encounter_starts = {row["fields"]["encounterId"]: row for row in rows
                        if row["packet"] == "combat-encounter-start"}
    records = []
    for index, row in enumerate(rows):
        packet = row["packet"]
        if packet not in ("combat-decision", "pull-plan"):
            continue
        fields = row["fields"]
        if fields.get("seed") != summary["seed"] or not fields.get("policyVersion"):
            raise ValueError(f"Decision on trace line {index + 1} has no matching policy version/seed")
        candidates = fields.get("candidateActions")
        if not isinstance(candidates, list):
            raise ValueError(f"Decision on trace line {index + 1} lacks candidates")
        if packet == "combat-decision":
            encounter_id = fields["encounterId"]
            start = encounter_starts.get(encounter_id)
            end = encounter_ends.get(encounter_id)
            if start is None:
                raise ValueError(f"Combat decision {index + 1} has no encounter start")
            chosen = {"action": fields["action"], "skillId": fields["skillId"],
                      "targetObjectId": fields["targetObjectId"], "reason": fields["reason"]}
            matches = [candidate for candidate in candidates if candidate["Legal"] and
                       candidate["Action"] == chosen["action"] and
                       candidate["SkillId"] == chosen["skillId"]]
            if len(matches) != 1 or matches[0]["BaselineRejection"] is not None:
                raise ValueError(f"Combat decision {index + 1} has no unique selected legal candidate")
            if any(candidate["BaselineRejection"] is None for candidate in candidates
                   if candidate is not matches[0]):
                raise ValueError(f"Combat decision {index + 1} has an unexplained alternative")
            outcome = ({"status": "observed", "clientObservedKill": end["fields"]["clientObservedKill"],
                        "revives": end["fields"]["revives"], "error": end["fields"].get("error"),
                        "sourceTraceLine": trace_line_by_identity[id(end)]} if end else
                       {"status": "unresolved", "reason": "No encounter-end event in trace"})
            decision_type = "combat"
            scope = "all-baseline-combat-actions-with-observed-eligibility"
        else:
            decision_type = "pull"
            scope = fields["candidateScope"]
            action = fields["chosenAction"]
            chosen = {"action": action, "target": fields.get("target"),
                      "firingPosition": fields.get("firingPosition"),
                      "reason": "Wait for helpers" if action == "wait" else
                                "No checked firing spot" if action == "no-plan" else
                                "Lowest helper count, highest clearance, then route order"}
            if action in ("pull", "wait"):
                matching = [candidate for candidate in candidates if candidate["Legal"] and
                            candidate["BaselineRejection"] is None]
                if action == "pull" and not matching:
                    raise ValueError(f"Pull decision {index + 1} has no selected legal spot")
                target_object_id = (matching[0]["TargetObjectId"] if action == "pull" else
                                    int(fields["target"].split("/")[-1]))
                later = next((event for event in rows[index + 1:] if
                              event["packet"] == "combat-encounter-end" and
                              event["fields"]["targetObjectId"] == target_object_id), None)
                outcome = ({"status": "observed", "clientObservedKill": later["fields"]["clientObservedKill"],
                            "revives": later["fields"]["revives"],
                            "linkedBy": "subsequent-combat-for-wait-target" if action == "wait" else
                                        "subsequent-combat-for-pulled-target",
                            "sourceTraceLine": trace_line_by_identity[id(later)]} if later else
                           {"status": "unresolved", "reason": "No later combat end for the selected target"})
            else:
                outcome = {"status": "no-encounter", "reason": "Planner found no checked firing spot"}
        records.append({"schemaVersion": 1, "run": summary["run"], "course": summary["course"],
                        "encounter": summary.get("encounter"), "seed": summary["seed"],
                        "policyVersion": fields["policyVersion"], "decisionType": decision_type,
                        "candidateScope": scope, "traceLine": index + 1, "vt": row["vt"],
                        "observedState": fields["observedState"],
                        "candidateActions": candidates, "chosenAction": chosen,
                        "eventualEncounterOutcome": outcome,
                        "outcomeAppliesTo": "executed-sequence-only; unchosen candidates are unlabeled"})
    output = args.run_dir / "training-decisions.jsonl"
    output.write_text("".join(json.dumps(record, separators=(",", ":")) + "\n" for record in records),
                      encoding="utf-8")
    summary["decisionRecords"] = len(records)
    summary["decisionTypes"] = {kind: sum(record["decisionType"] == kind for record in records)
                                for kind in ("combat", "pull")}
    summary["decisionRecordSha256"] = digest(output)
    summary["decisionRecordPath"] = str(output.resolve())
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"{summary['run']}: {len(records)} auditable decisions -> {output}")


if __name__ == "__main__":
    main()
