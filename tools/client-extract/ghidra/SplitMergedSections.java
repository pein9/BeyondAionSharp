// Themida merged game.dll's original .text/.rdata/.data/.pdata into one unnamed RWX
// section. Restore the original layout before analysis so data is not disassembled
// and the RTTI analyzer (which looks for blocks named .rdata/.data) can run.
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;

public class SplitMergedSections extends GhidraScript {
    @Override
    public void run() throws Exception {
        Memory mem = currentProgram.getMemory();
        Address base = currentProgram.getImageBase();
        long[] rva = { 0x1000L, 0xB80000L, 0xE96000L, 0x13AE000L };
        String[] name = { ".text", ".rdata", ".data", ".pdata" };
        boolean[] w = { false, false, true, false };
        boolean[] x = { true, false, false, false };
        for (int i = rva.length - 1; i >= 1; i--) {
            MemoryBlock b = mem.getBlock(base.add(rva[i]));
            if (!b.getStart().equals(base.add(rva[i]))) mem.split(b, base.add(rva[i]));
        }
        for (int i = 0; i < rva.length; i++) {
            MemoryBlock b = mem.getBlock(base.add(rva[i]));
            b.setName(name[i]);
            b.setPermissions(true, w[i], x[i]);
            println("BLOCK " + b.getName() + " " + b.getStart() + "-" + b.getEnd() + " rwx=" + b.isRead() + b.isWrite() + b.isExecute());
        }
        println("COMPILER " + currentProgram.getCompiler() + " FORMAT " + currentProgram.getExecutableFormat());
    }
}
