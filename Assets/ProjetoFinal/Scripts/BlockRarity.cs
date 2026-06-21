using System.Collections.Generic;

// lista de materiais aceites em apostas, do menos valioso (índice 0) ao mais valioso (último)
public class BlockRarityPF
{
    public static List<BlockPF.BlockType> ordemDeRaridade = new List<BlockPF.BlockType>()
    {
        BlockPF.BlockType.GRASS,
        BlockPF.BlockType.LEAVES,
        BlockPF.BlockType.WOOD,
        BlockPF.BlockType.STONE,
        BlockPF.BlockType.SAND,
        BlockPF.BlockType.SNOW,
        BlockPF.BlockType.OBSIDIAN
    };

    public static bool EApostavel(BlockPF.BlockType tipo)
    {
        return ordemDeRaridade.Contains(tipo);
    }
}