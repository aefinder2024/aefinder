using AElfIndexer.BlockScan;
using AElfIndexer.Grains.State.BlockScan;
using Orleans;

namespace AElfIndexer.Grains.Grain.BlockScan;

public interface IClientGrain: IGrainWithStringKey
{
    Task<string> AddSubscriptionInfoAsync(List<SubscriptionInfo> subscriptionInfos);
    Task<List<SubscriptionInfo>> GetSubscriptionInfoAsync(string version);
    Task AddBlockScanIdAsync(string version, string id);
    Task<List<string>> GetBlockScanIdsAsync(string version);
    Task<bool> IsVersionRunningAsync(string version, string token);
    Task UpgradeVersionAsync();
    Task RemoveVersionInfoAsync(string version);
    Task<VersionStatus> GetVersionStatusAsync(string version);
    Task StartAsync(string version);
    Task PauseAsync(string version);
    Task<ClientVersion> GetVersionAsync();
    Task SetTokenAsync(string version);
    Task<string> GetTokenAsync(string version);
    Task StopAsync(string version);
}