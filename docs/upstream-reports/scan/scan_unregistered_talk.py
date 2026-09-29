"""Scan Java quest handlers for NPC ids used in onDialogEvent that the same handler
never registers for a talk event.

Read-only. Usage: python scan_unregistered_talk.py <java-repo-root> [--json out.json]

Classification per (handler, npc id):
  UNREGISTERED  the id never appears in any registerQuestNpc(...) call of the handler
                (literal, int constant, or element of an int array/list iterated into
                registerQuestNpc) -> the talk path can never reach onDialogEvent for it.
  NO_TALK       the id is registered, but never with addOnTalkEvent (only quest start,
                kill, attack, distance, ...). Quest-start NPCs are legitimately reached via
                the client's quest list (questId != 0), so this bucket is noisier.
Files whose register() passes a non-resolvable expression to registerQuestNpc are flagged
DYNAMIC and their ids are reported separately (probable false positives).
"""
import json
import os
import re
import sys

NPC_ID = r"[2-8]\d{5}"  # 6-digit npc ids (200000-899999); dialog pages are 4-digit
CASE_RE = re.compile(r"\bcase\s+(" + NPC_ID + r"|[A-Z_][A-Z0-9_]*)\s*[:,]")
CMP_RE = re.compile(
    r"\b(?:targetId|npcId|targetNpcId|npc_id|getTargetId\(\)|getNpcId\(\))\s*==\s*(" + NPC_ID + r"|[A-Z_][A-Z0-9_]*)\b")
CMP_RE2 = re.compile(
    r"\b(" + NPC_ID + r"|[A-Z_][A-Z0-9_]*)\s*==\s*(?:targetId|npcId|targetNpcId|getTargetId\(\)|getNpcId\(\))")
CONST_RE = re.compile(r"\b(?:static\s+)?(?:final\s+)?(?:static\s+)?int\s+([A-Za-z_]\w*)\s*=\s*(\d+)\s*;")
ARRAY_RE = re.compile(r"\bint\s*\[\]\s*([A-Za-z_]\w*)\s*=\s*(?:new\s+int\s*\[\]\s*)?\{([^}]*)\}", re.S)
ARRAY_RE2 = re.compile(r"\bint\s+([A-Za-z_]\w*)\s*\[\]\s*=\s*(?:new\s+int\s*\[\]\s*)?\{([^}]*)\}", re.S)
LIST_RE = re.compile(r"\b(?:List|Set|Collection)<Integer>\s+([A-Za-z_]\w*)\s*=\s*(?:List|Set|Arrays)\.(?:of|asList)\(([^)]*)\)", re.S)
REG_CALL_RE = re.compile(r"registerQuestNpc\(\s*([^,()]+?)\s*(?:,[^)]*)?\)((?:\s*\.\s*\w+\([^)]*\))*)")
FOR_RE = re.compile(r"for\s*\(\s*(?:final\s+)?(?:int|Integer)\s+(\w+)\s*:\s*([^)]+?)\s*\)")


def strip_comments(src):
    src = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), src, flags=re.S)
    return re.sub(r"//[^\n]*", "", src)


def method_body(src, name):
    m = re.search(r"\b(?:public|protected|private)?\s*(?:boolean|void)\s+" + name + r"\s*\([^)]*\)\s*(?:throws[^{]*)?\{", src)
    if not m:
        return None
    i = m.end()
    depth = 1
    while i < len(src) and depth:
        c = src[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
        i += 1
    return src[m.end():i - 1]


def ints(text):
    return [int(x) for x in re.findall(r"\b\d+\b", text)]


def scan_file(path):
    src = strip_comments(open(path, encoding="utf-8", errors="replace").read())
    consts = {n: int(v) for n, v in CONST_RE.findall(src)}
    arrays = {}
    for rx in (ARRAY_RE, ARRAY_RE2, LIST_RE):
        for n, body in rx.findall(src):
            vals = []
            for tok in body.split(","):
                tok = tok.strip()
                if tok.isdigit():
                    vals.append(int(tok))
                elif tok in consts:
                    vals.append(consts[tok])
            arrays[n] = vals

    def resolve(tok):
        tok = tok.strip()
        if tok.isdigit():
            return int(tok)
        return consts.get(tok)

    dlg = method_body(src, "onDialogEvent")
    if dlg is None:
        return None
    used = set()
    for rx in (CASE_RE, CMP_RE, CMP_RE2):
        for tok in rx.findall(dlg):
            v = resolve(tok)
            if v is not None and 200000 <= v <= 899999:
                used.add(v)
    if not used:
        return None

    reg = method_body(src, "register") or ""
    loop_vars = {}
    for var, coll in FOR_RE.findall(reg):
        coll = coll.strip()
        if coll in arrays:
            loop_vars[var] = arrays[coll]
        else:
            # inline array literal e.g. for (int id : new int[] { 1, 2 })
            loop_vars[var] = [v for v in ints(coll)] or None
    registered, talk, dynamic = {}, set(), False
    for arg, chain in REG_CALL_RE.findall(reg):
        arg = arg.strip()
        vals = None
        v = resolve(arg)
        if v is not None:
            vals = [v]
        elif arg in loop_vars and loop_vars[arg]:
            vals = loop_vars[arg]
        else:
            dynamic = True
            continue
        for v in vals:
            registered.setdefault(v, set()).update(re.findall(r"\.\s*(\w+)\(", chain))
            if "addOnTalkEvent" in chain:
                talk.add(v)
    # Helper-based registration (e.g. registerQuestNpc inside a helper or template super call)
    if "registerQuestNpc" not in reg:
        dynamic = True

    out = []
    for npc in sorted(used):
        if npc in talk:
            continue
        kind = "NO_TALK" if npc in registered else "UNREGISTERED"
        out.append({"npc": npc, "kind": kind, "registered_as": sorted(registered.get(npc, []))})
    return {"dynamic": dynamic, "findings": out, "used": sorted(used)}


def main():
    root = sys.argv[1]
    qdir = os.path.join(root, "game-server", "data", "handlers", "quest")
    results = {}
    scanned = 0
    for dp, _, fns in os.walk(qdir):
        for fn in fns:
            if not fn.endswith(".java"):
                continue
            r = scan_file(os.path.join(dp, fn))
            if r is None:
                continue
            scanned += 1
            if r["findings"]:
                results[os.path.relpath(os.path.join(dp, fn), root).replace("\\", "/")] = r
    unreg = {f: r for f, r in results.items() if not r["dynamic"] and any(x["kind"] == "UNREGISTERED" for x in r["findings"])}
    notalk = {f: r for f, r in results.items() if not r["dynamic"] and any(x["kind"] == "NO_TALK" for x in r["findings"])}
    dyn = {f: r for f, r in results.items() if r["dynamic"]}
    print(f"handlers with onDialogEvent npc ids scanned: {scanned}")
    print(f"UNREGISTERED candidates (static register): {len(unreg)} handlers")
    for f in sorted(unreg):
        ids = [x["npc"] for x in unreg[f]["findings"] if x["kind"] == "UNREGISTERED"]
        print(f"  {f}: {ids}")
    print(f"NO_TALK (registered, but not for talk) : {len(notalk)} handlers")
    for f in sorted(notalk):
        ids = [(x['npc'], x['registered_as']) for x in notalk[f]["findings"] if x["kind"] == "NO_TALK"]
        print(f"  {f}: {ids}")
    print(f"DYNAMIC register (not resolved, probable false positives): {len(dyn)} handlers")
    for f in sorted(dyn):
        print(f"  {f}: {[(x['npc'], x['kind']) for x in dyn[f]['findings']]}")
    if "--json" in sys.argv:
        with open(sys.argv[sys.argv.index("--json") + 1], "w") as fh:
            json.dump(results, fh, indent=1)


if __name__ == "__main__":
    main()
