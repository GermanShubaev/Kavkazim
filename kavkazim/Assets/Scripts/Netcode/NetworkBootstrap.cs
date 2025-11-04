using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kavkazim.Services;
using Unity.Netcode;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Netcode.Transports.UTP; 
using Unity.Networking.Transport.Relay;

namespace Kavkazim.Netcode
{
    public interface INetworkBootstrap
    {
        Task<bool> HostWithRelayAsync(string lobbyName, int maxPlayers);
        Task<bool> QuickJoinAsync();
        Task LeaveLobbyAsync();
        string CurrentJoinCode { get; }
    }

    public class NetworkBootstrap : INetworkBootstrap
    {
        private readonly IUnityAuthService _auth;
        private readonly IUnityRelayService _relay;
        private readonly IUnityLobbyService _lobby;

        private string _lobbyId;
        public string CurrentJoinCode { get; private set; }

        public NetworkBootstrap(IUnityAuthService auth, IUnityRelayService relay, IUnityLobbyService lobby)
        {
            _auth = auth; _relay = relay; _lobby = lobby;
        }

        public async Task<bool> HostWithRelayAsync(string lobbyName, int maxPlayers)
        {
            await _auth.InitializeAsync();
            if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
                await _auth.SignInAnonymouslyAsync(null);

            Allocation allocation = await _relay.CreateAllocationAsync(maxPlayers - 1);
            CurrentJoinCode = await _relay.GetJoinCodeAsync(allocation.AllocationId);

            var dt = new RelayServerData(allocation, "dtls");
            var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            utp.SetRelayServerData(dt);

            var lobbyData = new Dictionary<string, Unity.Services.Lobbies.Models.DataObject>
            {
                { "joinCode", new Unity.Services.Lobbies.Models.DataObject(Unity.Services.Lobbies.Models.DataObject.VisibilityOptions.Member, CurrentJoinCode) }
            };
            var lobby = await _lobby.CreateLobbyAsync(lobbyName, maxPlayers, lobbyData);
            _lobbyId = lobby.Id;

            return NetworkManager.Singleton.StartHost();
        }

        public async Task<bool> QuickJoinAsync()
        {
            await _auth.InitializeAsync();
            if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
                await _auth.SignInAnonymouslyAsync(null);

            var lobby = await _lobby.QuickJoinAsync();
            _lobbyId = lobby.Id;

            string joinCode = lobby.Data != null && lobby.Data.ContainsKey("joinCode")
                ? lobby.Data["joinCode"].Value
                : null;

            if (string.IsNullOrEmpty(joinCode)) return false;

            JoinAllocation joinAlloc = await _relay.JoinAllocationAsync(joinCode);
            var dt = new RelayServerData(joinAlloc, "dtls");
            var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            utp.SetRelayServerData(dt);

            return NetworkManager.Singleton.StartClient();
        }

        public async Task LeaveLobbyAsync()
        {
            if (!string.IsNullOrEmpty(_lobbyId))
            {
                try { await _lobby.LeaveLobbyAsync(_lobbyId); }
                catch { /* ignore */ }
                _lobbyId = null;
            }
        }
    }
}
