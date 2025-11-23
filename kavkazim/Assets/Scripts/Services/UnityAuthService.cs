using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;

namespace Kavkazim.Services
{
    public interface IUnityAuthService
    {
        Task InitializeAsync();
        Task SignInAnonymouslyAsync(string displayName);
        string PlayerId { get; }
    }

    public class UnityAuthService : IUnityAuthService
    {
        public string PlayerId => AuthenticationService.Instance?.PlayerId;

        public async Task InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;
            await UnityServices.InitializeAsync();
        }

        public async Task SignInAnonymouslyAsync(string displayName)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                try { await AuthenticationService.Instance.UpdatePlayerNameAsync(displayName); }
                catch (Exception e) { Debug.LogWarning($"Display name set failed: {e.Message}"); }
            }
        }
    }
}