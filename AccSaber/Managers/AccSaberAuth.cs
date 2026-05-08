using System;
using System.Threading;
using System.Threading.Tasks;
using SiraUtil.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using AccSaber.Models;
using System.Linq;

namespace AccSaber.Managers
{
    internal sealed class AccSaberAuth
    {
        public PlayerSession? _playerSession { get; set; }
        private readonly SiraLog _log;
        public event Action? RelationStatusChanged;
        public List<AccSaberRelation> Relations { get; set; } = new List<AccSaberRelation>();
        public AccSaberAuth(SiraLog log)
        {
            _log = log;
        }

        public async Task Login()
        {
            if (_playerSession is not null)
                return;

            var platformUserModel = Plugin.Container.TryResolve<IPlatformUserModel>();
            var authToken = await platformUserModel.GetUserAuthToken();
            var userInfo = await platformUserModel.GetUserInfo(CancellationToken.None);
            var token = "";
            var provider = "";

            switch (userInfo.platform)
            {
                case UserInfo.Platform.Steam:
                    token = authToken.token;
                    provider = "steamTicket";
                    break;
                case UserInfo.Platform.Oculus:
                    token = authToken.token + "," + (await platformUserModel.RequestXPlatformAccessToken(CancellationToken.None)).token;
                    provider = "oculusTicket";
                    break;
            }
            var playerInfo = new PlayerSession(provider, token);

            var retry = 0;

            while (retry < 3)
            {
                if(await RunAuth(playerInfo))
                {
                    _playerSession = playerInfo;
                    await GetRelations();
                    _log.Info("Logged into AccSaber");
                    break;
                }
                else
                {
                    _log.Info($"Retrying ({retry + 1} out of 3)");
                    retry++;
                    await Task.Delay(5000);
                }
            }
        }

        internal class AuthResponse
        {
            [JsonProperty("accessToken")]
            internal string AccessToken { get; set; } = null!;

            [JsonProperty("refreshToken")]
            internal string RefreshToken { get; set; } = null!;

        }

        private async Task<bool> RunAuth(PlayerSession playerInfo)
        {
            Dictionary<string, string> jsonValues = new();
            jsonValues.Add("provider", playerInfo.Provider);
            jsonValues.Add("ticket", playerInfo.Token);

            StringContent sc = new(JsonConvert.SerializeObject(jsonValues), System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await Plugin.WebClient.PostAsync($"/v1/auth/ingame", sc);
                var parsedStr = await response.Content.ReadAsStringAsync();

                if (parsedStr != null)
                {
                    var parsed = JObject.Parse(parsedStr);
                    var AuthResponse = JsonConvert.DeserializeObject<AuthResponse>(parsed.ToString());

                    Plugin.WebClient.DefaultRequestHeaders.Add("Cookie", $"accessToken={AuthResponse.AccessToken}; refreshToken={AuthResponse.RefreshToken}");
                    Plugin.WebClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthResponse.AccessToken);
                }

                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed user authentication: {ex.Message}");
                return false;
            }
        }
        
        public enum Relation
        {
            Follower = 0,
            Rival = 1,
            Blocked = 2
        }

        private string GetRelation(Relation relation)
        {
            var _relation = relation switch
            {
                Relation.Follower => "follower",
                Relation.Rival => "rival",
                _ => "blocked",
            };
            return _relation;
        }

        private async Task GetRelations()
        {
            try
            {
                RelationStatusChanged!.Invoke();
                var response = await Plugin.WebClient.GetAsync("/v1/users/me/relations?page=0&size=99999");

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _log.Error(response);
                    return;
                }

                Relations.Clear();

                var parsedStr = await response.Content.ReadAsStringAsync();

                if (parsedStr != null)
                {
                    var parsed = JObject.Parse(parsedStr);
                    if (parsed["content"] is JArray content)
                    {
                        var relations = JsonConvert.DeserializeObject<List<AccSaberRelation>>(content.ToString());

                        foreach (var relation in relations)
                        {
                            Relations.Add(relation);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
        }



        public async Task CreateRelation(Relation relation, string userid)
        {
            if(_playerSession != null)
            {
                Dictionary<string, string> jsonValues = new();
                jsonValues.Add("targetUserId", userid);
                jsonValues.Add("type", GetRelation(relation));

                StringContent sc = new(JsonConvert.SerializeObject(jsonValues), System.Text.Encoding.UTF8, "application/json");
                try
                {
                    var response = await Plugin.WebClient.PostAsync($"/v1/users/me/relations", sc);

                    await GetRelations();
                }
                catch (Exception e)
                {
                    _log.Error(e.Message);
                }
            }
        }

        public async Task RemoveRelation(Relation relationType, string userid)
        {
            if (_playerSession != null)
            {
                var relation = Relations.Find(x => (x.TargetUserId == userid));

                if (relation.Type != GetRelation(relationType))
                    return;

                try
                {
                    var response = await Plugin.WebClient.DeleteAsync($"/v1/users/me/relations/{relation.ID}");

                    await GetRelations();
                }
                catch (Exception e)
                {
                    _log.Error(e.Message);
                }
            }
        }

        public bool IsFriend(string userid)
        {
            var relation = Relations.Find(x => (x.TargetUserId == userid));

            if (relation is null)
                return false;

            return relation.Type == "follower";
        }
    }

    internal class PlayerSession
    {
        internal string Token { get; set; } = null!;
        internal string Provider { get; set; } = null!;

        internal PlayerSession(string _provider, string _token)
        {
            Provider = _provider;
            Token = _token;
        }
    }
}
