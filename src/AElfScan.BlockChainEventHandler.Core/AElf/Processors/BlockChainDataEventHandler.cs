using AElf.Contracts.Consensus.AEDPoS;
using AElf.CSharp.Core.Extension;
using AElfScan.AElf.DTOs;
using AElfScan.AElf.Entities.Es;
using AElfScan.AElf.Etos;
using AElfScan.Grains.EventData;
using AElfScan.Grains.Grain;
using AElfScan.Options;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace AElfScan.AElf.Processors;

public class BlockChainDataEventHandler : IDistributedEventHandler<BlockChainDataEto>, ITransientDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<BlockChainDataEventHandler> _logger;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;
    private readonly OrleansClientOption _orleansClientOption;

    public BlockChainDataEventHandler(
        IClusterClient clusterClient,
        ILogger<BlockChainDataEventHandler> logger,
        IObjectMapper objectMapper,
        IOptionsSnapshot<OrleansClientOption> orleansClientOption,
        IDistributedEventBus distributedEventBus)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
        _orleansClientOption = orleansClientOption.Value;
    }

    public async Task HandleEventAsync(BlockChainDataEto eventData)
    {
        _logger.LogInformation($"Received BlockChainDataEto form {eventData.ChainId}, start block: {eventData.Blocks.First().BlockNumber}, end block: {eventData.Blocks.Last().BlockNumber},");
        var blockGrain = _clusterClient.GetGrain<IBlockGrain>(_orleansClientOption.AElfBlockGrainPrimaryKey);
        foreach (var blockItem in eventData.Blocks)
        {
            var newBlockEto = ConvertToNewBlockEto(blockItem, eventData.ChainId);
            BlockEventData blockEvent = new BlockEventData();
            blockEvent = _objectMapper.Map<NewBlockEto, BlockEventData>(newBlockEto);

            //analysis lib found event content
            var libLogEvent = blockItem.Transactions?.SelectMany(t => t.LogEvents)
                .FirstOrDefault(e => e.EventName == "IrreversibleBlockFound");
            if (libLogEvent != null)
            {
                blockEvent.LibBlockNumber = AnalysisBlockLibFoundEvent(libLogEvent.ExtraProperties["Indexed"]);
            }
            
            List<BlockEventData> libBlockList = await blockGrain.SaveBlock(blockEvent);

            if (libBlockList != null)
            {
                //publish new block event
                await _distributedEventBus.PublishAsync(newBlockEto);
                
                if (libBlockList.Count > 0)
                {
                    //publish confirm blocks event
                    var confirmBlockList =
                        _objectMapper.Map<List<BlockEventData>, List<ConfirmBlockEto>>(libBlockList);
                    await _distributedEventBus.PublishAsync(new ConfirmBlocksEto()
                        { ConfirmBlocks = confirmBlockList });
                }
                
                
            }
        }
        await Task.CompletedTask;
    }

    private long AnalysisBlockLibFoundEvent(string logEventIndexed)
    {
        List<string> IndexedList =
            JsonConvert.DeserializeObject<List<string>>(logEventIndexed);
        var libFound = new IrreversibleBlockFound();
        libFound.MergeFrom(ByteString.FromBase64(IndexedList[0]));
        _logger.LogInformation(
            $"IrreversibleBlockFound: {libFound}");
        return libFound.IrreversibleBlockHeight;
    }

    private NewBlockEto ConvertToNewBlockEto(BlockEto blockItem,string chainId)
    {
        NewBlockEto newBlock = _objectMapper.Map<BlockEto, NewBlockEto>(blockItem);
        // newBlock.Id = Guid.NewGuid();
        newBlock.Id = newBlock.BlockHash;
        newBlock.ChainId = chainId;
        newBlock.IsConfirmed = false;
        foreach (var transaction in newBlock.Transactions)
        {
            transaction.ChainId = chainId;
            transaction.BlockHash = newBlock.BlockHash;
            transaction.PreviousBlockHash = newBlock.PreviousBlockHash;
            transaction.BlockNumber = newBlock.BlockNumber;
            transaction.BlockTime = newBlock.BlockTime;
            transaction.IsConfirmed = false;

            foreach (var logEvent in transaction.LogEvents)
            {
                logEvent.ChainId = chainId;
                logEvent.BlockHash = newBlock.BlockHash;
                logEvent.PreviousBlockHash = newBlock.PreviousBlockHash;
                logEvent.BlockNumber = newBlock.BlockNumber;
                logEvent.BlockTime = newBlock.BlockTime;
                logEvent.IsConfirmed = false;
                logEvent.TransactionId = transaction.TransactionId;
            }
        }

        return newBlock;
    }
}