// Assets/Scripts/Services/UnityRelayService.cs
using System;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace Kavkazim.Services
{
    public interface IUnityRelayService
    {
        Task<Allocation> CreateAllocationAsync(int maxConnections = 10);
        Task<JoinAllocation> JoinAllocationAsync(string joinCode);
        Task<string> GetJoinCodeAsync(Guid allocationId);
    }

    public class UnityRelayService : IUnityRelayService
    {
        public async Task<Allocation> CreateAllocationAsync(int maxConnections = 10)
            => await RelayService.Instance.CreateAllocationAsync(maxConnections);

        public async Task<JoinAllocation> JoinAllocationAsync(string joinCode)
            => await RelayService.Instance.JoinAllocationAsync(joinCode);

        public async Task<string> GetJoinCodeAsync(Guid allocationId)
            => await RelayService.Instance.GetJoinCodeAsync(allocationId);
    }
}