using SocketIO.Core;
using SocketIOClient;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace FLT_HuntMarker.Faloop
{
    // S rank spawn with known location
    public record FaloopSpawn(string MobId, uint WorldId, uint TerritoryId, int Instance, int LocationX, int LocationY);

    // Same protocol as Faloop web / desktop app (faloop.app), based on SlashNephy/Divination FaloopIntegration
    // 1. POST /api/auth/user/refresh -> anonymous sessionId + token
    // 2. POST /api/auth/user/login with username / password -> logged in sessionId
    // 3. socket.io /comms/socket.io with auth { sessionid }, "message" events carry mob reports
    public class FaloopClient : IDisposable
    {
        private const string ORIGIN = "https://faloop.app";
        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0";

        private readonly HttpClient http = new();
        private SocketIOClient.SocketIO socket;

        // Spawn report without location, waiting for spawn_location
        private readonly Dictionary<string, (string mobId, uint worldId, uint territoryId, int instance)> pending = new();

        public event Action<FaloopSpawn> OnSpawn;
        public event Action<string> OnStatus;

        public bool IsConnected => socket?.Connected == true;

        public async Task<bool> ConnectAsync(string username, string password)
        {
            Disconnect();

            OnStatus?.Invoke("Logging in...");

            var refresh = await PostAsync("/api/auth/user/refresh", "/", null, new Dictionary<string, object> { { "sessionId", null } });
            if (refresh == null || refresh["success"]?.GetValue<bool>() != true)
            {
                OnStatus?.Invoke("Faloop refresh failed");
                return false;
            }

            var login = await PostAsync("/api/auth/user/login", "/login", refresh["data"]?["token"]?.GetValue<string>(), new Dictionary<string, object>
            {
                { "username", username },
                { "password", password },
                { "rememberMe", false },
                { "sessionId", refresh["data"]?["sessionId"]?.GetValue<string>() },
            });
            if (login == null || login["success"]?.GetValue<bool>() != true)
            {
                OnStatus?.Invoke("Faloop login failed");
                return false;
            }

            socket = new SocketIOClient.SocketIO(ORIGIN, new SocketIOOptions
            {
                EIO = EngineIO.V4,
                Transport = TransportProtocol.Polling,
                Path = "/comms/socket.io",
                ExtraHeaders = new Dictionary<string, string>
                {
                    { "Accept", "*/*" },
                    { "Referer", ORIGIN + "/" },
                    { "Origin", ORIGIN },
                    { "User-Agent", USER_AGENT },
                },
                Auth = new Dictionary<string, string> { { "sessionid", login["data"]?["sessionId"]?.GetValue<string>() } },
            });

            socket.OnConnected += (s, e) =>
            {
                OnStatus?.Invoke("Connected");
                socket.EmitAsync("ack");
            };
            socket.OnDisconnected += (s, e) => OnStatus?.Invoke("Disconnected : " + e);
            socket.OnReconnectAttempt += (s, e) => OnStatus?.Invoke("Reconnecting...");
            socket.OnError += (s, e) => OnStatus?.Invoke("Error : " + e);
            socket.On("message", HandleMessage);

            await socket.ConnectAsync();

            return true;
        }

        public void Disconnect()
        {
            if (socket == null)
                return;

            try
            {
                socket.DisconnectAsync();
                socket.Dispose();
            }
            catch
            {
                // pass
            }

            socket = null;
            pending.Clear();
        }

        private async Task<JsonNode> PostAsync(string path, string referer, string token, Dictionary<string, object> body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ORIGIN + path)
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            request.Headers.TryAddWithoutValidation("Origin", ORIGIN);
            request.Headers.TryAddWithoutValidation("Referer", ORIGIN + referer);
            request.Headers.TryAddWithoutValidation("User-Agent", USER_AGENT);
            if (token != null)
                request.Headers.TryAddWithoutValidation("Authorization", token);

            try
            {
                using var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;

                return JsonNode.Parse(await response.Content.ReadAsStringAsync());
            }
            catch (Exception e)
            {
                Trace.WriteLine("Faloop request failed : " + e.Message);
                return null;
            }
        }

        // { type: "mob", subType: "report", data: { action, data, id: { mobId, worldId, zoneInstance } } }
        private void HandleMessage(SocketIOResponse response)
        {
            try
            {
                var payload = JsonNode.Parse(response.GetValue<JsonElement>().GetRawText());
                if (payload?["type"]?.GetValue<string>() != "mob" || payload["subType"]?.GetValue<string>() != "report")
                    return;

                var report = payload["data"];
                string action = report?["action"]?.GetValue<string>();
                string mobId = report?["id"]?["mobId"]?.GetValue<string>();
                string world = report?["id"]?["worldId"]?.GetValue<string>();
                int instance = report?["id"]?["zoneInstance"]?.GetValue<int>() ?? 0;
                var data = report?["data"];

                if (mobId == null || !FaloopData.SRankMobs.Contains(mobId) ||
                    world == null || !FaloopData.Worlds.TryGetValue(world, out uint worldId))
                    return;

                string key = mobId + "_" + world + "_" + instance;

                switch (action)
                {
                    case "spawn":
                        {
                            string zone = data?["zoneId2"]?.GetValue<string>();
                            if (zone == null || !FaloopData.TerritoryTypes.TryGetValue(zone, out uint territoryId))
                                return;

                            int poi = data["zonePoiIds"] is JsonArray pois && pois.Count > 0 ? pois[0].GetValue<int>() : 0;
                            string location = GetLocation(data?["location"]?.GetValue<string>(), poi);

                            if (location == null)
                                pending[key] = (mobId, worldId, territoryId, instance);
                            else
                                RaiseSpawn(mobId, worldId, territoryId, instance, location);
                            break;
                        }
                    case "spawn_location":
                        {
                            if (!pending.Remove(key, out var spawn))
                                return;

                            string location = GetLocation(data?["location"]?.GetValue<string>(), data?["zonePoiId"]?.GetValue<int>() ?? 0);
                            if (location != null)
                                RaiseSpawn(spawn.mobId, spawn.worldId, spawn.territoryId, spawn.instance, location);
                            break;
                        }
                    case "spawn_false":
                    case "death":
                        pending.Remove(key);
                        break;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Faloop message parse failed : " + e.Message);
            }
        }

        private static string GetLocation(string location, int poi)
        {
            if (!string.IsNullOrEmpty(location))
                return location;

            return FaloopData.Locations.TryGetValue(poi, out string loc) ? loc : null;
        }

        private void RaiseSpawn(string mobId, uint worldId, uint territoryId, int instance, string location)
        {
            string[] xy = location.Split(",");
            if (xy.Length != 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y))
                return;

            OnSpawn?.Invoke(new FaloopSpawn(mobId, worldId, territoryId, instance, x, y));
        }

        public void Dispose()
        {
            Disconnect();
            http.Dispose();
        }
    }
}
