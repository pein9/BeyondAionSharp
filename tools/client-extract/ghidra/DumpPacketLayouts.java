// Dumps what the 4.8 client reads from each server packet, as TSV.
//
// The client's server-packet dispatch (ServerToClientRouter, game.dll) switches on the opcode and
// reads the payload through a pointer it builds as `*(packet + 0x18) + (int)*(packet + 0x20)`.
// For every opcode this script records each load whose address is a constant offset from that
// pointer, in the case itself and in the functions the pointer or the packet is passed to, and
// each memcpy of a constant length out of the payload. A load is written `offset:size@address`,
// the address being the first instruction that performs it.
// What it cannot follow it reports: a non-constant offset, a pointer stored in memory (a read
// cursor), an indirect or imported call, a call too deep. A layout is complete only when nothing
// was left unfollowed.
//
// Run headless on bin64/game.dll, imported with SplitMergedSections.java as the pre-script:
//   analyzeHeadless <projectDir> aion48/bin64 -process game.dll -noanalysis -readOnly \
//       -scriptPath tools/client-extract/ghidra -postScript DumpPacketLayouts.java <out.tsv>
//
// The addresses below are those of one build, so any other file is refused.
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSet;
import ghidra.program.model.lang.Register;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.pcode.HighFunction;
import ghidra.program.model.pcode.HighParam;
import ghidra.program.model.pcode.HighVariable;
import ghidra.program.model.pcode.PcodeOp;
import ghidra.program.model.pcode.PcodeOpAST;
import ghidra.program.model.pcode.Varnode;
import ghidra.program.model.symbol.FlowType;
import java.io.PrintWriter;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Deque;
import java.util.HashMap;
import java.util.HashSet;
import java.util.IdentityHashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.TreeSet;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class DumpPacketLayouts extends GhidraScript {
    static final String GAME_DLL_SHA256 = "d8645dadfb91e1c2af08a6c9ae313e3d5e07e59ed9b1d004dddcb7b516047296";
    static final long DISPATCH = 0x10362540L, TABLE = 0x103653b0L, EXIT_STUB = 0x1185bf6aL;
    static final long MEMCPY_THUNK = 0x10b230a6L; // JMP [0x10b81260]; every call site is (dst, src, byte count)
    static final int CASES = 0x131, PACKET_SLOT = 1, MAX_DEPTH = 3, MAX_CALLEE_BYTES = 40000;

    enum Kind { UNKNOWN, CYCLE, PACKET, PKTOFF4, LIN }

    /**
     * A value as far as it matters here. LIN is `buf * (packet buffer) + idx * (packet offset) + off`,
     * plus an unknown amount when `var` is set. The payload pointer is buf = idx = 1.
     */
    static final class Sym {
        final Kind kind;
        final int buf, idx;
        final long off;
        final boolean var;

        Sym(Kind kind, int buf, int idx, long off, boolean var) {
            this.kind = kind;
            this.buf = buf;
            this.idx = idx;
            this.off = off;
            this.var = var;
        }

        boolean isConst() { return kind == Kind.LIN && buf == 0 && idx == 0 && !var; }
        boolean isBase() { return kind == Kind.LIN && buf == 1 && idx == 1; }
        boolean hasPointer() { return kind == Kind.LIN && (buf != 0 || idx != 0); }
        Sym plus(Sym o, int sign) { return new Sym(Kind.LIN, buf + sign * o.buf, idx + sign * o.idx, off + sign * o.off, var || o.var); }
        Sym plus(long c) { return new Sym(Kind.LIN, buf, idx, off + c, var); }
        Sym unknownAmount() { return new Sym(Kind.LIN, buf, idx, off, true); }
        boolean same(Sym o) { return kind == o.kind && buf == o.buf && idx == o.idx && off == o.off && var == o.var; }
    }

    static final Sym UNKNOWN = new Sym(Kind.UNKNOWN, 0, 0, 0, false), CYCLE = new Sym(Kind.CYCLE, 0, 0, 0, false),
        PACKET = new Sym(Kind.PACKET, 0, 0, 0, false), BASE = new Sym(Kind.LIN, 1, 1, 0, false);

    static Sym constant(long c) { return new Sym(Kind.LIN, 0, 0, c, false); }

    /** One function analysed with one meaning for its parameters. */
    static final class Ctx {
        final int packetSlot, baseSlot;
        final Map<Varnode, Sym> memo = new IdentityHashMap<>();
        final Set<Varnode> open = Collections.newSetFromMap(new IdentityHashMap<>());

        Ctx(int packetSlot, int baseSlot) {
            this.packetSlot = packetSlot;
            this.baseSlot = baseSlot;
        }
    }

    static final class Result {
        final TreeMap<Long, String> reads = new TreeMap<>(); // offset * 256 + size -> the first instruction that loads it
        final TreeSet<Long> copies = new TreeSet<>(); // offset * 65536 + length, a memcpy out of the payload
        final TreeSet<String> unresolved = new TreeSet<>();
        final TreeSet<String> followed = new TreeSet<>();
        int varReads;

        void add(long off, int size, String at) {
            if (off >= 0 && off < 0x10000 && size > 0 && size < 256) reads.merge(off * 256 + size, at, (a, b) -> a.compareTo(b) <= 0 ? a : b);
            else unresolved.add("odd-read@" + at);
        }

        void merge(Result sub, long shift) {
            for (Map.Entry<Long, String> r : sub.reads.entrySet()) add((r.getKey() >> 8) + shift, (int) (r.getKey() & 0xff), r.getValue());
            for (long c : sub.copies) copies.add(((c >> 16) + shift) * 65536 + (c & 0xffff));
            unresolved.addAll(sub.unresolved);
            followed.addAll(sub.followed);
            varReads += sub.varReads;
        }
    }

    DecompInterface ifc;
    Memory mem;
    final Map<Address, HighFunction> highs = new HashMap<>();
    final Map<String, Result> done = new HashMap<>();
    final Set<String> running = new HashSet<>();

    HighFunction high(Function f) {
        if (highs.containsKey(f.getEntryPoint())) return highs.get(f.getEntryPoint());
        DecompileResults r = ifc.decompileFunction(f, 300, monitor);
        HighFunction hf = r.decompileCompleted() ? r.getHighFunction() : null;
        highs.put(f.getEntryPoint(), hf);
        return hf;
    }

    /** The constant c when the varnode is `something + c`, else null. */
    Long constOffset(Varnode v, int guard) {
        PcodeOp d = v.getDef();
        if (d == null || guard > 8) return null;
        switch (d.getOpcode()) {
            case PcodeOp.COPY:
            case PcodeOp.CAST:
                return constOffset(d.getInput(0), guard + 1);
            case PcodeOp.INT_ADD:
            case PcodeOp.PTRSUB: {
                Varnode a = d.getInput(0), b = d.getInput(1);
                if (!b.isConstant()) return null;
                Long inner = constOffset(a, guard + 1);
                return b.getOffset() + (inner == null ? 0 : inner);
            }
            case PcodeOp.PTRADD: {
                Varnode i = d.getInput(1), s = d.getInput(2);
                if (!i.isConstant() || !s.isConstant()) return null;
                Long inner = constOffset(d.getInput(0), guard + 1);
                return i.getOffset() * s.getOffset() + (inner == null ? 0 : inner);
            }
            default:
                return null;
        }
    }

    Sym resolve(Varnode v, Ctx c, int guard) {
        if (v == null || guard > 80) return UNKNOWN;
        if (v.isConstant()) return constant(v.getOffset());
        Sym m = c.memo.get(v);
        if (m != null) return m;
        if (!c.open.add(v)) return CYCLE;
        Sym s = resolveOp(v, c, guard);
        c.open.remove(v);
        if (s.kind != Kind.CYCLE) c.memo.put(v, s);
        return s;
    }

    Sym resolveOp(Varnode v, Ctx c, int guard) {
        PcodeOp d = v.getDef();
        if (d == null) {
            HighVariable h = v.getHigh();
            if (h instanceof HighParam) {
                int slot = ((HighParam) h).getSlot();
                if (slot == c.packetSlot) return PACKET;
                if (slot == c.baseSlot) return BASE;
            }
            return UNKNOWN;
        }
        switch (d.getOpcode()) {
            case PcodeOp.COPY:
            case PcodeOp.CAST:
            case PcodeOp.INDIRECT:
                return resolve(d.getInput(0), c, guard + 1);
            case PcodeOp.INT_SEXT:
            case PcodeOp.INT_ZEXT: {
                Sym a = resolve(d.getInput(0), c, guard + 1);
                if (a.kind == Kind.PKTOFF4) return new Sym(Kind.LIN, 0, 1, 0, false);
                return a.isConst() ? a : UNKNOWN;
            }
            case PcodeOp.LOAD: {
                Long co = constOffset(d.getInput(1), 0);
                if (co != null && v.getSize() == 8 && co == 0x18) return new Sym(Kind.LIN, 1, 0, 0, false);
                if (co != null && v.getSize() == 4 && co == 0x20) return new Sym(Kind.PKTOFF4, 0, 0, 0, false);
                return UNKNOWN;
            }
            case PcodeOp.INT_ADD:
            case PcodeOp.PTRSUB:
            case PcodeOp.INT_SUB: {
                Sym a = resolve(d.getInput(0), c, guard + 1), b = resolve(d.getInput(1), c, guard + 1);
                int sign = d.getOpcode() == PcodeOp.INT_SUB ? -1 : 1;
                if (a.kind == Kind.LIN && b.kind == Kind.LIN) return a.plus(b, sign);
                if (a.hasPointer()) return a.unknownAmount();
                if (b.hasPointer() && sign > 0) return b.unknownAmount();
                return UNKNOWN;
            }
            case PcodeOp.PTRADD: {
                Sym a = resolve(d.getInput(0), c, guard + 1), i = resolve(d.getInput(1), c, guard + 1);
                long size = d.getInput(2).isConstant() ? d.getInput(2).getOffset() : -1;
                if (a.kind == Kind.LIN && i.isConst() && size > 0) return a.plus(i.off * size);
                if (a.kind == Kind.LIN && i.kind == Kind.LIN && size == 1) return a.plus(i, 1);
                if (a.hasPointer()) return a.unknownAmount();
                if (i.hasPointer() && size == 1) return i.unknownAmount();
                return UNKNOWN;
            }
            case PcodeOp.MULTIEQUAL: {
                // A pointer that agrees on every path keeps its offset; one advanced in a loop does not.
                Sym first = null, pointer = null;
                boolean differs = false;
                for (int k = 0; k < d.getNumInputs(); k++) {
                    Sym a = resolve(d.getInput(k), c, guard + 1);
                    if (first == null) first = a;
                    else if (!first.same(a)) differs = true;
                    if (pointer == null && a.hasPointer()) pointer = a;
                }
                if (first == null) return UNKNOWN;
                if (!differs) return first;
                return pointer == null ? UNKNOWN : pointer.unknownAmount();
            }
            default:
                return UNKNOWN;
        }
    }

    boolean isImportThunk(Function f) {
        Instruction ins = getInstructionAt(f.getEntryPoint());
        return ins != null && ins.getFlowType().isJump() && ins.getFlowType().isComputed();
    }

    Result collect(HighFunction hf, Ctx c, AddressSet range, int depth) {
        Result r = new Result();
        Iterator<PcodeOpAST> it = hf.getPcodeOps();
        while (it.hasNext()) {
            PcodeOpAST op = it.next();
            Address at = op.getSeqnum().getTarget();
            if (range != null && !range.contains(at)) continue;
            int oc = op.getOpcode();
            if (oc == PcodeOp.LOAD) {
                Sym a = resolve(op.getInput(1), c, 0);
                if (!a.isBase()) continue;
                if (a.var) { r.varReads++; continue; }
                // The decompiler widens a narrow load to the element size of the pointer type it
                // guessed, then truncates it again: `(byte)p[1]` for `movzx r9d, byte ptr [rax+4]`.
                // When every use is such a truncation, the truncations are what the client reads.
                Varnode out = op.getOutput();
                List<long[]> pieces = new ArrayList<>();
                Iterator<PcodeOp> uses = out.getDescendants();
                while (pieces != null && uses != null && uses.hasNext()) {
                    PcodeOp use = uses.next();
                    if (use.getOpcode() == PcodeOp.SUBPIECE && use.getInput(0) == out && use.getInput(1).isConstant())
                        pieces.add(new long[] { use.getInput(1).getOffset(), use.getOutput().getSize() });
                    else pieces = null;
                }
                if (pieces == null || pieces.isEmpty()) r.add(a.off, out.getSize(), at.toString());
                else for (long[] p : pieces) r.add(a.off + p[0], (int) p[1], at.toString());
            }
            else if (oc == PcodeOp.STORE || (op.getOutput() != null && op.getOutput().isAddrTied())) {
                // The payload pointer put in memory: a cursor that other functions read through.
                Sym a = resolve(oc == PcodeOp.STORE ? op.getInput(2) : op.getOutput(), c, 0);
                if (a.isBase()) r.unresolved.add("stored@" + at);
            }
            else if (oc == PcodeOp.CALL || oc == PcodeOp.CALLIND) {
                for (int i = 1; i < op.getNumInputs(); i++) {
                    Sym a = resolve(op.getInput(i), c, 0);
                    if (!a.isBase() && a.kind != Kind.PACKET) continue;
                    String what = a.kind == Kind.PACKET ? "packet" : (a.var ? "var" : Long.toString(a.off));
                    if (oc == PcodeOp.CALLIND) { r.unresolved.add(what + "->indirect@" + at); continue; }
                    Address target = op.getInput(0).getAddress();
                    Function callee = getFunctionAt(target);
                    MemoryBlock b = mem.getBlock(target);
                    if (target.getOffset() == MEMCPY_THUNK && a.isBase()) {
                        Sym n = i == 2 && !a.var && op.getNumInputs() > 3 ? resolve(op.getInput(3), c, 0) : UNKNOWN;
                        if (n.isConst() && n.off > 0 && n.off < 0x10000 && a.off >= 0 && a.off < 0x10000)
                            r.copies.add(a.off * 65536 + n.off);
                        else r.unresolved.add(what + "->memcpy:var-length");
                        continue;
                    }
                    String why = callee == null ? "nofunction" : b == null || !b.getName().equals(".text") ? "outside-text"
                        : isImportThunk(callee) ? "import"
                        : a.var ? "var-offset" : depth >= MAX_DEPTH ? "depth"
                        : callee.getBody().getNumAddresses() > MAX_CALLEE_BYTES ? "large" : null;
                    if (why != null) { r.unresolved.add(what + "->" + target + ":" + why); continue; }
                    Result sub = analyze(callee, a.kind == Kind.PACKET ? i - 1 : -1, a.isBase() ? i - 1 : -1, depth + 1);
                    if (sub == null) { r.unresolved.add(what + "->" + target + ":undecompiled"); continue; }
                    r.followed.add(target.toString());
                    r.merge(sub, a.isBase() ? a.off : 0);
                }
            }
        }
        return r;
    }

    Result analyze(Function f, int packetSlot, int baseSlot, int depth) {
        String key = f.getEntryPoint() + "|" + packetSlot + "|" + baseSlot;
        if (done.containsKey(key)) return done.get(key);
        if (!running.add(key)) {
            Result r = new Result();
            r.unresolved.add("recursion@" + f.getEntryPoint());
            return r;
        }
        HighFunction hf = high(f);
        Result r = hf == null ? null : collect(hf, new Ctx(packetSlot, baseSlot), null, depth);
        running.remove(key);
        done.put(key, r);
        return r;
    }

    /** The instructions of one case: everything reachable from its entry without leaving through the exit stub. */
    AddressSet caseRange(Address start, Function dispatch, Set<Address> otherEntries, List<Instruction> straight) {
        AddressSet set = new AddressSet();
        Deque<Address> todo = new ArrayDeque<>();
        todo.add(start);
        boolean first = true;
        while (!todo.isEmpty() && set.getNumAddresses() < 20000) {
            Address a = todo.poll();
            boolean line = first;
            first = false;
            for (Instruction ins = getInstructionAt(a); ins != null; ins = ins.getNext()) {
                Address at = ins.getAddress();
                if (set.contains(at) || !dispatch.getBody().contains(at) || (otherEntries.contains(at) && !at.equals(start))) break;
                set.add(at, ins.getMaxAddress());
                FlowType ft = ins.getFlowType();
                if (line && straight != null) straight.add(ins);
                if (ft.isCall()) {
                    Address[] fl = ins.getFlows();
                    if (fl.length == 1 && fl[0].getOffset() == EXIT_STUB) break;
                    continue;
                }
                if (ft.isJump() || ft.isTerminal()) line = false;
                if (ft.isTerminal()) break;
                if (ft.isJump()) {
                    for (Address t : ins.getFlows()) todo.add(t);
                    if (ft.isUnConditional()) break;
                }
            }
        }
        return set;
    }

    static final Pattern MEM = Pattern.compile("(byte|word|dword|qword|xmmword) ptr \\[([A-Z0-9]+)(?: \\+ (0x[0-9a-f]+))?\\]");

    /**
     * Reads the straight-line start of a case off the disassembly. The decompiler drops a load whose
     * value it believes the callee ignores, and those loads are still what the client reads.
     */
    void asmReads(List<Instruction> straight, Result r) {
        Register base = null, half = null;
        for (Instruction ins : straight) {
            String text = ins.toString();
            Matcher m = MEM.matcher(text);
            boolean has = m.find();
            String mn = ins.getMnemonicString();
            Register dst = ins.getNumOperands() > 0 ? ins.getRegister(0) : null;
            if (has && mn.equals("MOVSXD") && m.group(1).equals("dword") && "0x20".equals(m.group(3)) && dst != null) {
                half = dst.getBaseRegister();
                continue;
            }
            if (has && mn.equals("ADD") && m.group(1).equals("qword") && "0x18".equals(m.group(3)) && dst != null
                    && dst.getBaseRegister().equals(half)) {
                base = half;
                half = null;
                continue;
            }
            if (base != null && has && m.group(2).equals(base.getName()) && !mn.equals("LEA")) {
                int size = m.group(1).equals("byte") ? 1 : m.group(1).equals("word") ? 2 : m.group(1).equals("dword") ? 4
                    : m.group(1).equals("qword") ? 8 : 16;
                int comma = text.indexOf(',');
                boolean store = comma > 0 && text.indexOf("ptr [") < comma && !mn.startsWith("CMP") && !mn.startsWith("TEST");
                if (!store) r.add(m.group(3) == null ? 0 : Long.decode(m.group(3)), size, ins.getAddress().toString());
            }
            for (Object o : ins.getResultObjects())
                if (o instanceof Register && base != null && ((Register) o).getBaseRegister().equals(base)) base = null;
        }
    }

    static String joinReads(TreeMap<Long, String> reads) {
        StringBuilder sb = new StringBuilder();
        for (Map.Entry<Long, String> r : reads.entrySet())
            sb.append(sb.length() > 0 ? "," : "").append(r.getKey() >> 8).append(':').append(r.getKey() & 0xff).append('@').append(r.getValue());
        return sb.toString();
    }

    static String joinCopies(TreeSet<Long> copies) {
        StringBuilder sb = new StringBuilder();
        for (long c : copies) sb.append(sb.length() > 0 ? "," : "").append(c >> 16).append(':').append(c & 0xffff);
        return sb.toString();
    }

    @Override
    public void run() throws Exception {
        String sha = currentProgram.getExecutableSHA256();
        if (!GAME_DLL_SHA256.equalsIgnoreCase(sha))
            throw new IllegalStateException("not the 4.8 bin64/game.dll this script was written for: " + sha);
        mem = currentProgram.getMemory();
        if (mem.getBlock(".rdata") == null)
            throw new IllegalStateException("import game.dll with SplitMergedSections.java as the pre-script");
        String[] args = getScriptArgs();
        ifc = new DecompInterface();
        ifc.openProgram(currentProgram);
        long image = currentProgram.getImageBase().getOffset();
        Function dispatch = getFunctionAt(toAddr(DISPATCH));
        HighFunction dhf = high(dispatch);
        if (dhf == null) throw new IllegalStateException("the dispatch did not decompile");

        Address[] target = new Address[CASES];
        Map<Address, Integer> uses = new HashMap<>();
        for (int i = 0; i < CASES; i++) {
            target[i] = toAddr(image + (mem.getInt(toAddr(TABLE + 4L * i)) & 0xffffffffL));
            uses.merge(target[i], 1, Integer::sum);
        }
        Address dflt = null;
        for (Map.Entry<Address, Integer> e : uses.entrySet()) if (dflt == null || e.getValue() > uses.get(dflt)) dflt = e.getKey();
        Set<Address> entries = new HashSet<>(uses.keySet());
        Ctx ctx = new Ctx(PACKET_SLOT, -1);
        Map<Address, String> rows = new HashMap<>();
        int complete = 0, handled = 0;

        try (PrintWriter out = new PrintWriter(args[0], "UTF-8")) {
            out.print("# game.dll sha256 " + sha + "\n");
            out.print("opcode\tkind\tcase_addr\thandlers\treads\tcopies\tvar_reads\tunresolved\tfollowed\n");
            for (int op = 0; op < CASES; op++) {
                Address t = target[op];
                if (t.equals(dflt)) { out.print(op + "\tdefault\t" + t + "\t\t\t\t0\t\t\n"); continue; }
                handled++;
                String row = rows.get(t);
                if (row == null) {
                    Instruction head = getInstructionAt(t);
                    boolean noop = head != null && head.getFlowType().isCall() && head.getFlows().length == 1
                        && head.getFlows()[0].getOffset() == EXIT_STUB;
                    List<Instruction> straight = new ArrayList<>();
                    AddressSet range = caseRange(t, dispatch, entries, straight);
                    Result r = collect(dhf, ctx, range, 0);
                    asmReads(straight, r);
                    TreeSet<String> handlers = new TreeSet<>();
                    for (Instruction ins = getInstructionAt(range.getMinAddress()); ins != null && range.contains(ins.getAddress()); ins = ins.getNext()) {
                        if (!ins.getFlowType().isCall()) continue;
                        Address[] fl = ins.getFlows();
                        if (fl.length != 1) { handlers.add("indirect"); continue; }
                        if (fl[0].getOffset() != EXIT_STUB) handlers.add(fl[0].toString());
                    }
                    row = (noop ? "noop" : "case") + "\t" + t + "\t" + String.join(",", handlers) + "\t" + joinReads(r.reads) + "\t"
                        + joinCopies(r.copies) + "\t" + r.varReads + "\t" + String.join(";", r.unresolved) + "\t"
                        + String.join(",", r.followed);
                    rows.put(t, row);
                    if (r.varReads == 0 && r.unresolved.isEmpty()) complete++;
                }
                out.print(op + "\t" + row + "\n");
            }
        }
        ifc.dispose();
        println("PACKET_LAYOUTS handled=" + handled + " distinct=" + rows.size() + " complete=" + complete
            + " functions_decompiled=" + highs.size());
    }
}
