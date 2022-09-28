using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AElfScan.AElf.Entities.Es;
using AElfScan.AElf.Etos;
using Microsoft.Extensions.Logging;
using Nest;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Index = System.Index;

namespace AElfScan.AElf;

public class BlockHandler:IDistributedEventHandler<NewBlockEto>,
    IDistributedEventHandler<ConfirmBlocksEto>,ITransientDependency
{
    private readonly INESTRepository<Block, Guid> _blockIndexRepository;
    private readonly ILogger<BlockHandler> _logger;

    public BlockHandler(
        INESTRepository<Block,Guid> blockIndexRepository,
        ILogger<BlockHandler> logger)
    {
        _blockIndexRepository = blockIndexRepository;
        _logger = logger;
    }

    public async Task HandleEventAsync(NewBlockEto eventData)
    {
        var block = eventData;
        _logger.LogInformation($"test block is adding, id: {block.BlockHash}  , BlockNumber: {block.BlockNumber} , IsConfirmed: {block.IsConfirmed}");
        var blockIndex = await _blockIndexRepository.GetAsync(q=>
            q.Term(i=>i.Field(f=>f.BlockHash).Value(eventData.BlockHash)));
        if (blockIndex != null)
        {
            _logger.LogInformation($"block already exist-{blockIndex}, Add failure!");
        }
        else
        {
            await _blockIndexRepository.AddAsync(eventData);
        }
        
    }

    public async Task HandleEventAsync(ConfirmBlocksEto eventData)
    {
        foreach (var confirmBlock in eventData.ConfirmBlocks)
        {
            _logger.LogInformation($"block:{confirmBlock.BlockNumber} is confirming");
            var blockIndex = await _blockIndexRepository.GetAsync(q=>
                q.Term(i=>i.Field(f=>f.BlockHash).Value(confirmBlock.BlockHash)));
            if (blockIndex != null)
            {
                blockIndex.IsConfirmed = true;
                foreach (var transaction in blockIndex.Transactions)
                {
                    transaction.IsConfirmed = true;
                    foreach (var logEvent in transaction.LogEvents)
                    {
                        logEvent.IsConfirmed = true;
                    }
                }

                await _blockIndexRepository.UpdateAsync(blockIndex);
            }
            else
            {
                _logger.LogInformation($"Confirm failure,block{confirmBlock.BlockHash} is not exist!");
                throw new DataException($"Block {confirmBlock.BlockHash} is not exist,confirm block failure!");
            }
        
            //find the same height blocks
            var mustQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
            mustQuery.Add(q => q.Term(i => i.Field(f => f.BlockNumber).Value(confirmBlock.BlockNumber)));
            QueryContainer Filter(QueryContainerDescriptor<Block> f) => f.Bool(b => b.Must(mustQuery));

            var forkBlockList=await _blockIndexRepository.GetListAsync(Filter);
            if (forkBlockList.Item1 == 0)
            {
                continue;
            }

            //delete the same height fork block
            foreach (var forkBlock in forkBlockList.Item2)
            {
                if (forkBlock.BlockHash == confirmBlock.BlockHash)
                {
                    continue;
                }
                _blockIndexRepository.DeleteAsync(forkBlock.Id);
                _logger.LogInformation($"block {forkBlock.BlockHash} has been deleted.");
            }
        }
        
    }
}