using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

namespace Kavkazim.Services
{
    public interface IUnityLobbyService
    {
        Task<Lobby> CreateLobbyAsync(string name, int maxPlayers, Dictionary<string, DataObject> data);
        Task<Lobby> QuickJoinAsync();
        Task<Lobby> JoinByCodeAsync(string lobbyCode);
        Task LeaveLobbyAsync(string lobbyId);
    }

    public class UnityLobbyService : IUnityLobbyService
    {
        public async Task<Lobby> CreateLobbyAsync(string name, int maxPlayers, Dictionary<string, DataObject> data)
        {
            var options = new CreateLobbyOptions { Data = data, IsPrivate = false };
            return await LobbyService.Instance.CreateLobbyAsync(name, maxPlayers, options);
        }

        public async Task<Lobby> QuickJoinAsync()
            => await LobbyService.Instance.QuickJoinLobbyAsync();

        public async Task<Lobby> JoinByCodeAsync(string lobbyCode)
            => await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

        public async Task LeaveLobbyAsync(string lobbyId)
            => await LobbyService.Instance.RemovePlayerAsync(lobbyId, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
    }
}