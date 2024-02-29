using AElfIndexer.Client.BlockState;
using AElfIndexer.Grains.Grain.BlockStates;
using Volo.Abp.DependencyInjection;

namespace AElfIndexer.Client.BlockExecution;

public interface IBlockExecutionService
{
    Task ExecuteAsync(string chainId, string branchBlockHash);
}

public class BlockExecutionService : IBlockExecutionService, ITransientDependency
{
    private readonly IAppBlockStateSetProvider _appBlockStateSetProvider;
    private readonly IFullBlockProcessor _fullBlockProcessor;
    private readonly IAppDataIndexManagerProvider _appDataIndexManagerProvider;

    public BlockExecutionService(IAppBlockStateSetProvider appBlockStateSetProvider,
        IFullBlockProcessor fullBlockProcessor, IAppDataIndexManagerProvider appDataIndexManagerProvider)
    {
        _appBlockStateSetProvider = appBlockStateSetProvider;
        _fullBlockProcessor = fullBlockProcessor;
        _appDataIndexManagerProvider = appDataIndexManagerProvider;
    }

    public async Task ExecuteAsync(string chainId, string branchBlockHash)
    {
        var blockStateSets = await GetUnProcessedBlockStateSetsAsync(chainId, branchBlockHash);
        if (!await IsProcessAsync(chainId, blockStateSets))
        {
            return;
        }
        
        var rollbackBlockStateSets = await GetToBeRollbackBlockStateSetsAsync(chainId, blockStateSets);
        
        foreach (var blockStateSet in rollbackBlockStateSets)
        {
            await _fullBlockProcessor.ProcessAsync(blockStateSet.Block, true);
            await SetBlockStateSetProcessedAsync(chainId, blockStateSet, false);
        }
        
        foreach (var blockStateSet in blockStateSets)
        {
            await _fullBlockProcessor.ProcessAsync(blockStateSet.Block, false);
            await SetBlockStateSetProcessedAsync(chainId, blockStateSet, true);
        }

        var longestChainBlockStateSet = blockStateSets.LastOrDefault();
        if (longestChainBlockStateSet != null)
        {
            await _appBlockStateSetProvider.SetBestChainBlockStateSetAsync(chainId, longestChainBlockStateSet.Block.BlockHash);
        }

        await _appDataIndexManagerProvider.SavaDataAsync();
    }
    
    private async Task<List<BlockStateSet>> GetUnProcessedBlockStateSetsAsync(string chainId, string branchBlockHash)
    {
        var blockStateSets = new List<BlockStateSet>();

        var blockStateSet = await _appBlockStateSetProvider.GetBlockStateSetAsync(chainId, branchBlockHash);
        while (blockStateSet != null && !blockStateSet.Processed)
        {
            blockStateSets.Add(blockStateSet);
            blockStateSet =
                await _appBlockStateSetProvider.GetBlockStateSetAsync(chainId, blockStateSet.Block.PreviousBlockHash);
        }

        blockStateSets.Reverse();
        return blockStateSets;
    }
    
    private async Task<List<BlockStateSet>> GetToBeRollbackBlockStateSetsAsync(string chainId, List<BlockStateSet> toExecuteBlockStateSets)
    {
        var rollbackBlockStateSets = new List<BlockStateSet>();
        var bestChainBlockStateSet = await _appBlockStateSetProvider.GetBestChainBlockStateSetAsync(chainId);
        if (bestChainBlockStateSet == null)
        {
            return rollbackBlockStateSets;
        }

        var toExecutePreviousBlockHashes = new HashSet<string>();
        foreach (var l in toExecuteBlockStateSets)
        {
            toExecutePreviousBlockHashes.Add(l.Block.PreviousBlockHash);
        }

        var blockHash = bestChainBlockStateSet.Block.BlockHash;
        while (!toExecutePreviousBlockHashes.Contains(blockHash))
        {
            var blockStateSet = await _appBlockStateSetProvider.GetBlockStateSetAsync(chainId, blockHash);
            if (blockStateSet == null)
            {
                break;
            }
            rollbackBlockStateSets.Add(blockStateSet);
            blockHash = blockStateSet.Block.PreviousBlockHash;
        }

        rollbackBlockStateSets.Reverse();
        return rollbackBlockStateSets;
    }
    
    private async Task<bool> IsProcessAsync(string chainId, List<BlockStateSet> blockStateSets)
    {
        if (blockStateSets.Count == 0)
        {
            return false;
        }

        return await IsInLibBranchAsync(chainId, blockStateSets.First().Block.BlockHash);
    }
    
    private async Task<bool> IsInLibBranchAsync(string chainId, string blockHash)
    {
        var lastIrreversibleBlockStateSet = await _appBlockStateSetProvider.GetLastIrreversibleBlockStateSetAsync(chainId);
        if (lastIrreversibleBlockStateSet == null)
        {
            return true;
        }

        while (true)
        {
            var blockStateSet = await _appBlockStateSetProvider.GetBlockStateSetAsync(chainId, blockHash);
            if (blockStateSet == null || blockStateSet.Block.BlockHeight < lastIrreversibleBlockStateSet.Block.BlockHeight)
            {
                return false;
            }

            if (blockStateSet.Block.Confirmed)
            {
                return true;
            }
            blockHash = blockStateSet.Block.PreviousBlockHash;
        }
    }
    
    private async Task SetBlockStateSetProcessedAsync(string chainId, BlockStateSet blockStateSet, bool processed)
    {
        blockStateSet.Processed = processed;
        await _appBlockStateSetProvider.UpdateBlockStateSetAsync(chainId, blockStateSet);
    }
    
}