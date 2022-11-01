using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AElfScan.AElf.Dtos;
using AElfScan.AElf.Entities.Es;
using AElfScan.AElf.Etos;
using Nest;
using Shouldly;
using Volo.Abp.EventBus.Distributed;
using Xunit;

namespace AElfScan.AElf;

public class BlockAppServiceTests:AElfScanApplicationTestBase
{
    private readonly BlockAppService _blockAppService;
    private readonly INESTRepository<BlockIndex, string> _blockIndexRepository;
    private readonly INESTRepository<TransactionIndex, string> _transactionIndexRepository;
    private readonly INESTRepository<LogEventIndex, string> _logEventIndexRepository;

    public BlockAppServiceTests()
    {
        _blockAppService = GetRequiredService<BlockAppService>();
        _blockIndexRepository = GetRequiredService<INESTRepository<BlockIndex, string>>();
    }

    private async Task ClearBlockIndex(string chainId,long startBlockNumber,long endBlockNumber)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<BlockIndex>, QueryContainer>>();
        mustQuery.Add(q=>q.Term(i=>i.Field(f=>f.ChainId).Value(chainId)));
        // mustQuery.Add(q=>q.Term(i=>i.Field(f=>f.ChainId.Suffix("keyword")).Value(input.ChainId)));
        mustQuery.Add(q => q.Range(i => i.Field(f => f.BlockNumber).GreaterThanOrEquals(startBlockNumber)));
        mustQuery.Add(q => q.Range(i => i.Field(f => f.BlockNumber).LessThanOrEquals(endBlockNumber)));
        QueryContainer Filter(QueryContainerDescriptor<BlockIndex> f) => f.Bool(b => b.Must(mustQuery));
        var filterList=await _blockIndexRepository.GetListAsync(Filter);
        foreach (var deleteBlock in filterList.Item2)
        {
            await _blockIndexRepository.DeleteAsync(deleteBlock);
        }
    }

    [Fact]
    public async Task GetBlocksAsync_Test1_13()
    {
        //clear data for unit test
        await ClearBlockIndex("AELF", 100, 300);
        
        //Unit Test 1
        var block_100 =
            MockDataHelper.MockNewBlockEtoData(100, MockDataHelper.CreateBlockHash(),false);
        block_100.Transactions = new List<Transaction>();
        await _blockIndexRepository.AddAsync(block_100);

        GetBlocksInput getBlocksInput_test1 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 100,
            HasTransaction = true
        };
        List<BlockDto> blockDtos_test1 =await _blockAppService.GetBlocksAsync(getBlocksInput_test1);
        blockDtos_test1.Count.ShouldBeGreaterThan(0);
        blockDtos_test1[0].BlockNumber.ShouldBe(100);
        
        //Unit Test 2
        GetBlocksInput getBlocksInput_test2 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 50
        };
        List<BlockDto> blockDtos_test2 =await _blockAppService.GetBlocksAsync(getBlocksInput_test2);
        blockDtos_test2.Count.ShouldBe(0);
        
        //Unit Test 3
        GetBlocksInput getBlocksInput_test3 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 100
        };
        List<BlockDto> blockDtos_test3 =await _blockAppService.GetBlocksAsync(getBlocksInput_test3);
        blockDtos_test3.Count.ShouldBeGreaterThan(0);
        blockDtos_test3[0].BlockNumber.ShouldBe(100);
        
        //Unit Test 4
        var block_200 =
            MockDataHelper.MockNewBlockEtoData(200, MockDataHelper.CreateBlockHash(),false);
        await _blockIndexRepository.AddAsync(block_200);
        GetBlocksInput getBlocksInput_test4 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 300,
            HasTransaction = true
        };
        List<BlockDto> blockDtos_test4 =await _blockAppService.GetBlocksAsync(getBlocksInput_test4);
        blockDtos_test4.Count.ShouldBeGreaterThan(0);
        blockDtos_test4.ShouldContain(x=>x.BlockNumber==100);
        blockDtos_test4.ShouldContain(x=>x.BlockNumber==200);
        blockDtos_test4.ShouldNotContain(x=>x.BlockNumber==300);
        
        //Unit Test 5
        var block_180 = MockDataHelper.MockNewBlockEtoData(180, MockDataHelper.CreateBlockHash(),true);
        var block_180_fork = MockDataHelper.MockNewBlockEtoData(180, MockDataHelper.CreateBlockHash(),false);
        await _blockIndexRepository.AddAsync(block_180);
        await _blockIndexRepository.AddAsync(block_180_fork);
        GetBlocksInput getBlocksInput_test5 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 200,
            HasTransaction = false
        };
        List<BlockDto> blockDtos_test5 =await _blockAppService.GetBlocksAsync(getBlocksInput_test5);
        blockDtos_test5.Select(x=>x.BlockNumber==180).Count().ShouldBe(2);
        
        //Unit Test 6
        var block_1000 = MockDataHelper.MockNewBlockEtoData(1000, MockDataHelper.CreateBlockHash(),false);
        var block_1500 = MockDataHelper.MockNewBlockEtoData(1500, MockDataHelper.CreateBlockHash(),false);
        var block_1999 = MockDataHelper.MockNewBlockEtoData(1999, MockDataHelper.CreateBlockHash(),false);
        var block_2000 = MockDataHelper.MockNewBlockEtoData(2000, MockDataHelper.CreateBlockHash(),false);
        var block_3000 = MockDataHelper.MockNewBlockEtoData(3000, MockDataHelper.CreateBlockHash(),false);
        var block_4000 = MockDataHelper.MockNewBlockEtoData(4000, MockDataHelper.CreateBlockHash(),false);
        var block_4999 = MockDataHelper.MockNewBlockEtoData(4999, MockDataHelper.CreateBlockHash(),false);
        var block_5000 = MockDataHelper.MockNewBlockEtoData(5000, MockDataHelper.CreateBlockHash(),false);
        await _blockIndexRepository.AddAsync(block_1000);
        await _blockIndexRepository.AddAsync(block_1500);
        await _blockIndexRepository.AddAsync(block_1999);
        await _blockIndexRepository.AddAsync(block_2000);
        await _blockIndexRepository.AddAsync(block_3000);
        await _blockIndexRepository.AddAsync(block_4000);
        await _blockIndexRepository.AddAsync(block_4999);
        await _blockIndexRepository.AddAsync(block_5000);
        GetBlocksInput getBlocksInput_test6 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 1000,
            EndBlockNumber = 5000,
            HasTransaction = true
        };
        List<BlockDto> blockDtos_test6 =await _blockAppService.GetBlocksAsync(getBlocksInput_test6);
        blockDtos_test6.Max(x=>x.BlockNumber).ShouldBe(2000);
        
        //Unit Test 7
        GetBlocksInput getBlocksInput_test7 = new GetBlocksInput()
        {
            ChainId = "AELG",
            StartBlockNumber = 100,
            EndBlockNumber = 100,
            HasTransaction = true
        };
        List<BlockDto> blockDtos_test7 =await _blockAppService.GetBlocksAsync(getBlocksInput_test7);
        blockDtos_test7.Count().ShouldBe(0);
        
        //Unit Test 8
        GetBlocksInput getBlocksInput_test8 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 200,
            IsOnlyConfirmed = true,
            HasTransaction = true
        };
        List<BlockDto> blockDtos_test8 =await _blockAppService.GetBlocksAsync(getBlocksInput_test8);
        blockDtos_test8.Count().ShouldBe(1);
        blockDtos_test8[0].BlockHash.ShouldBe(block_180.BlockHash);
        
        //Unit Test 9
        GetBlocksInput getBlocksInput_test9 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 200,
            IsOnlyConfirmed = false,
            HasTransaction = true
        };
        List<BlockDto> blockDtos_test9 =await _blockAppService.GetBlocksAsync(getBlocksInput_test9);
        blockDtos_test9.Count().ShouldBe(4);
        blockDtos_test9.ShouldContain(x=>x.BlockNumber==100);
        blockDtos_test9.ShouldContain(x=>x.BlockNumber==200);
        blockDtos_test9.Select(x=>x.BlockNumber==180).Count().ShouldBe(2);
        
        //Unit Test 10
        var block_110 = MockDataHelper.MockNewBlockEtoData(110, MockDataHelper.CreateBlockHash(), true);
        var transaction_110 = MockDataHelper.MockTransactionWithLogEventData(110, block_110.BlockHash,
            MockDataHelper.CreateBlockHash(), true, "consensus_contract_address", "");
        block_110.Transactions = new List<Transaction>() { transaction_110 };
        await _blockIndexRepository.AddAsync(block_110);
        GetBlocksInput getBlocksInput_test10 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 110,
            IsOnlyConfirmed = true,
            HasTransaction = true,
            Events = new List<FilterContractEventInput>()
            {
                new FilterContractEventInput()
                {
                    ContractAddress = "consensus_contract_address"
                },
                new FilterContractEventInput()
                {
                    ContractAddress = "token_contract_address"
                }
            }
        };
        List<BlockDto> blockDtos_test10 =await _blockAppService.GetBlocksAsync(getBlocksInput_test10);
        blockDtos_test10.Count().ShouldBe(1);
        blockDtos_test10.ShouldContain(x=>x.BlockNumber==110);
        
        //Unit Test 11
        var block_105 = MockDataHelper.MockNewBlockEtoData(105, MockDataHelper.CreateBlockHash(), true);
        var transaction_105 = MockDataHelper.MockTransactionWithLogEventData(105, block_105.BlockHash,
            MockDataHelper.CreateBlockHash(), true, "contract_address_a", "UpdateTinyBlockInformation");
        block_105.Transactions = new List<Transaction>() { transaction_105 };
        var block_106 = MockDataHelper.MockNewBlockEtoData(106, MockDataHelper.CreateBlockHash(), true);
        var transaction_106 = MockDataHelper.MockTransactionWithLogEventData(106,block_106.BlockHash, MockDataHelper.CreateBlockHash(),
            true, "token_contract_address", "DonateResourceToken");
        block_106.Transactions = new List<Transaction>() { transaction_106 };
        await _blockIndexRepository.AddAsync(block_105);
        await _blockIndexRepository.AddAsync(block_106);
        GetBlocksInput getBlocksInput_test11 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 110,
            IsOnlyConfirmed = true,
            HasTransaction = true,
            Events = new List<FilterContractEventInput>()
            {
                new FilterContractEventInput()
                {
                    EventNames = new List<string>(){"UpdateValue","UpdateTinyBlockInformation"}
                },
                new FilterContractEventInput()
                {
                    EventNames = new List<string>(){"DonateResourceToken"}
                }
            }
        };
        List<BlockDto> blockDtos_test11 =await _blockAppService.GetBlocksAsync(getBlocksInput_test11);
        blockDtos_test11.Count().ShouldBe(2);
        blockDtos_test11.ShouldContain(x=>x.BlockNumber==105);
        blockDtos_test11.ShouldContain(x=>x.BlockNumber==106);
        
        //Unit Test 12
        var block_107 = MockDataHelper.MockNewBlockEtoData(107, MockDataHelper.CreateBlockHash(), true);
        var transaction_107 = MockDataHelper.MockTransactionWithLogEventData(107, block_107.BlockHash,
            MockDataHelper.CreateBlockHash(), true, "consensus_contract_address", "UpdateTinyBlockInformation");
        block_107.Transactions = new List<Transaction>() { transaction_107 };
        await _blockIndexRepository.AddAsync(block_107);
        GetBlocksInput getBlocksInput_test12 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 110,
            IsOnlyConfirmed = true,
            HasTransaction = true,
            Events = new List<FilterContractEventInput>()
            {
                new FilterContractEventInput()
                {
                    ContractAddress = "consensus_contract_address",
                    EventNames = new List<string>(){"UpdateValue","UpdateTinyBlockInformation"}
                },
                new FilterContractEventInput()
                {
                    ContractAddress = "token_contract_address",
                    EventNames = new List<string>(){"DonateResourceToken"}
                }
            }
        };
        List<BlockDto> blockDtos_test12 =await _blockAppService.GetBlocksAsync(getBlocksInput_test12);
        blockDtos_test12.Count().ShouldBe(2);
        blockDtos_test12.ShouldContain(x=>x.BlockHash==block_106.BlockHash);
        blockDtos_test12.ShouldContain(x=>x.BlockHash==block_107.BlockHash);
        blockDtos_test12.ShouldNotContain(x=>x.BlockHash==block_105.BlockHash);
        
        //Unit Test 13
        GetBlocksInput getBlocksInput_test13 = new GetBlocksInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 110,
            IsOnlyConfirmed = true,
            HasTransaction = true,
            Events = new List<FilterContractEventInput>()
            {
                new FilterContractEventInput()
                {
                    ContractAddress = "consensus_contract_address",
                    EventNames = new List<string>(){"DonateResourceToken"}
                }
            }
        };
        List<BlockDto> blockDtos_test13 =await _blockAppService.GetBlocksAsync(getBlocksInput_test13);
        blockDtos_test13.Count().ShouldBe(0);
    }

    private async Task ClearTransactionIndex(string chainId,long startBlockNumber,long endBlockNumber)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TransactionIndex>, QueryContainer>>();
        mustQuery.Add(q=>q.Term(i=>i.Field(f=>f.ChainId).Value(chainId)));
        mustQuery.Add(q => q.Range(i => i.Field(f => f.BlockNumber).GreaterThanOrEquals(startBlockNumber)));
        mustQuery.Add(q => q.Range(i => i.Field(f => f.BlockNumber).LessThanOrEquals(endBlockNumber)));
        QueryContainer Filter(QueryContainerDescriptor<TransactionIndex> f) => f.Bool(b => b.Must(mustQuery));
        var filterList=await _transactionIndexRepository.GetListAsync(Filter);
        foreach (var deleteTransaction in filterList.Item2)
        {
            await _transactionIndexRepository.DeleteAsync(deleteTransaction);
        }
    }
    
    [Fact]
    public async Task GetTransactionAsync_Test14_15()
    {
        //clear data for unit test
        ClearTransactionIndex("AELF", 100, 110);

        //Unit Test 14
        var transaction_100_1 = MockDataHelper.MockNewTransactionEtoData(100, true, "token_contract_address", "DonateResourceToken");
        var transaction_100_2 = MockDataHelper.MockNewTransactionEtoData(100, true, "", "");
        var transaction_100_3 = MockDataHelper.MockNewTransactionEtoData(100, true, "consensus_contract_address", "UpdateValue");
        var transaction_110 = MockDataHelper.MockNewTransactionEtoData(110, true, "consensus_contract_address", "UpdateTinyBlockInformation");
        await _transactionIndexRepository.AddAsync(transaction_100_1);
        await _transactionIndexRepository.AddAsync(transaction_100_2);
        await _transactionIndexRepository.AddAsync(transaction_100_3);
        await _transactionIndexRepository.AddAsync(transaction_110);
        GetTransactionsInput getTransactionsInput_test14 = new GetTransactionsInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 110
        };
        List<TransactionDto> transactionDtos_test14 =
            await _blockAppService.GetTransactionsAsync(getTransactionsInput_test14);
        transactionDtos_test14.Count().ShouldBe(4);
        
        //Unit Test 15
        GetTransactionsInput getTransactionsInput_test15 = new GetTransactionsInput()
        {
            ChainId = "AELF",
            StartBlockNumber = 100,
            EndBlockNumber = 110,
            IsOnlyConfirmed = true,
            Events = new List<FilterContractEventInput>()
            {
                new FilterContractEventInput()
                {
                    ContractAddress = "consensus_contract_address",
                },
                new FilterContractEventInput()
                {
                    ContractAddress = "token_contract_address",
                }
            }
        };
        List<TransactionDto> transactionDtos_test15 =
            await _blockAppService.GetTransactionsAsync(getTransactionsInput_test15);
        transactionDtos_test15.Count.ShouldBe(3);
        transactionDtos_test15.ShouldContain(x=>x.TransactionId==transaction_100_1.TransactionId);
        transactionDtos_test15.ShouldContain(x=>x.TransactionId==transaction_100_3.TransactionId);
        transactionDtos_test15.ShouldContain(x=>x.TransactionId==transaction_110.TransactionId);
    }
}