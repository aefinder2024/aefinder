using AElfIndexer.Block.Dtos;
using AElfIndexer.Grains.State.Client;
using AutoMapper;

namespace GraphQL;

public class TestGraphQLAutoMapperProfile:Profile
{
    public TestGraphQLAutoMapperProfile()
    {
        CreateMap<TestBlockIndex, TestBlock>();
        CreateMap<BlockInfo, TestBlockIndex>();
    }
    
}