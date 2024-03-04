namespace AElfIndexer.Sdk;

public class BlockDataProcessingContext
{
    public string ChainId { get; }
    public string BlockHash { get; }
    public long BlockHeight { get; }
    public DateTime BlockTime { get; }
    public bool IsRollback { get; }
    
    public BlockDataProcessingContext(string chainId, string blockHash, long blockHeight, DateTime blockTime, bool isRollback)
    {
        ChainId = chainId;
        BlockHash = blockHash;
        BlockHeight = blockHeight;
        BlockTime = blockTime;
        IsRollback = isRollback;
    }
}