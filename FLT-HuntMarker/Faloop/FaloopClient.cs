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
    public enum FaloopState
    {
        Off,
        Connecting,
        Connected,
        Disconnected,
    }

    // S rank spawn with known location. Scheduled = early access (not public yet)
    public record FaloopSpawn(string MobId, string World, uint WorldId, uint TerritoryId, int Instance, int LocationX, int LocationY, bool Scheduled);

    // Same protocol as Faloop web / desktop app (faloop.app), based on
    // SlashNephy/Divination FaloopIntegration and jshade2710/faloop-messenger
    // 1. POST /api/auth/user/refresh -> anonymous sessionId + token
    // 2. POST /api/auth/user/login with username / password -> logged in sessionId
    // 3. socket.io /comms/socket.io with auth { sessionid }, emit "ack" after connect,
    //    "message" events carry mob reports of all worlds
    // Early access accounts get "spawn" with isScheduled = true before public, then "spawn_release"
    public class FaloopClient : IDisposable
    {
        private const string ORIGIN = "https://faloop.app";
        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0";

        private readonly HttpClient http = new();
        private SocketIOClient.SocketIO socket;

        // Spawn report without location, waiting for spawn_location / spawn_release
        private readonly Dictionary<string, (string mobId, string world, uint worldId, uint territoryId, int instance, bool scheduled)> pending = new();

        // Reports are for all worlds and ranks, so only count them in log
        private int reportCount;
        private int sRankCount;
        private DateTime countSince = DateTime.Now;
        private bool firstReport;

        public event Action<FaloopSpawn> OnSpawn;
        public event Action<FaloopState, string> OnStatus;

        public bool IsConnected => socket?.Connected == true;

        public static void Log(string message)
        {
            Trace.WriteLine("[Faloop " + DateTime.Now.ToString("HH:mm:ss") + "] " + message);
        }

        private void SetStatus(FaloopState state, string text)
        {
            Log("Status " + state + " : " + text);
            OnStatus?.Invoke(state, text);
        }

        public async Task<bool> ConnectAsync(string username, string password)
        {
            Disconnect();

            SetStatus(FaloopState.Connecting, "Logging in...");

            var refresh = await PostAsync("/api/auth/user/refresh", "/", null, new Dictionary<string, object> { { "sessionId", null } });
            if (refresh == null || refresh["success"]?.GetValue<bool>() != true)
            {
                SetStatus(FaloopState.Disconnected, "Refresh failed");
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
                SetStatus(FaloopState.Disconnected, "Login failed");
                return false;
            }

            Log("Login OK as " + username);
            SetStatus(FaloopState.Connecting, "Connecting socket...");

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
                SetStatus(FaloopState.Connected, "Connected");
                socket.EmitAsync("ack");
            };
            socket.OnDisconnected += (s, e) => SetStatus(FaloopState.Disconnected, "Disconnected : " + e);
            socket.OnReconnectAttempt += (s, e) => SetStatus(FaloopState.Connecting, "Reconnecting (" + e + ")...");
            socket.OnReconnected += (s, e) => SetStatus(FaloopState.Connected, "Reconnected");
            socket.OnError += (s, e) => SetStatus(FaloopState.Disconnected, "Error : " + e);
            // "message" is logged in HandleMessage
            socket.OnAny((name, response) =>
            {
                if (name != "message")
                    Log("Event '" + name + "' " + Truncate(response.ToString(), 300));
            });
            socket.On("message", HandleMessage);

            try
            {
                await socket.ConnectAsync();
            }
            catch (Exception e)
            {
                SetStatus(FaloopState.Disconnected, "Socket failed : " + e.Message);
                return false;
            }

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
                string text = await response.Content.ReadAsStringAsync();

                // Response may carry session / token, do not log body
                Log("POST " + path + " -> " + (int)response.StatusCode);

                if (!response.IsSuccessStatusCode)
                    return null;

                return JsonNode.Parse(text);
            }
            catch (Exception e)
            {
                Log("POST " + path + " failed : " + e.Message);
                return null;
            }
        }

        // { type: "mob", subType: "report", data: { action, data, id: { mobId, worldId, zoneInstance } } }
        private void HandleMessage(SocketIOResponse response)
        {
            try
            {
                var payload = JsonNode.Parse(response.GetValue<JsonElement>().GetRawText());
                string type = payload?["type"]?.GetValue<string>();
                string subType = payload?["subType"]?.GetValue<string>();

                if (type != "mob" || subType != "report")
                {
                    Log("Message skipped, type=" + type + " subType=" + subType);
                    return;
                }

                var report = payload["data"];
                string action = report?["action"]?.GetValue<string>();
                string mobId = report?["id"]?["mobId"]?.GetValue<string>();
                string world = report?["id"]?["worldId"]?.GetValue<string>();
                int instance = report?["id"]?["zoneInstance"]?.GetValue<int>() ?? 0;
                var data = report?["data"];

                string desc = action + " mob=" + mobId + " world=" + world + " instance=" + instance;

                if (!firstReport)
                {
                    firstReport = true;
                    Log("First report received : " + desc);
                }

                reportCount++;
                if (DateTime.Now - countSince > TimeSpan.FromMinutes(1))
                {
                    Log("Received " + reportCount + " reports (S : " + sRankCount + ") in last minute");
                    reportCount = 0;
                    sRankCount = 0;
                    countSince = DateTime.Now;
                }

                if (mobId == null || !FaloopData.SRankMobs.Contains(mobId))
                    return;

                sRankCount++;

                Log("Report S " + desc + " data=" + Truncate(data?.ToJsonString(), 300));

                if (world == null || !FaloopData.Worlds.TryGetValue(world, out uint worldId))
                {
                    Log("  unknown world, skipped");
                    return;
                }

                string key = mobId + "_" + world + "_" + instance;

                switch (action)
                {
                    case "spawn":
                        {
                            string zone = data?["zoneId2"]?.GetValue<string>();
                            if (zone == null || !FaloopData.TerritoryTypes.TryGetValue(zone, out uint territoryId))
                            {
                                Log("  unknown zone '" + zone + "', skipped");
                                return;
                            }

                            bool scheduled = data["isScheduled"]?.GetValue<bool>() == true;
                            string location = GetLocation(data["location"]?.GetValue<string>(), GetFirstPoi(data["zonePoiIds"]));

                            if (location == null)
                            {
                                Log("  no location yet, waiting spawn_location" + (scheduled ? " (early access)" : ""));
                                pending[key] = (mobId, world, worldId, territoryId, instance, scheduled);
                            }
                            else
                            {
                                pending.Remove(key);
                                RaiseSpawn(mobId, world, worldId, territoryId, instance, location, scheduled);
                            }
                            break;
                        }
                    case "spawn_location":
                    case "spawn_release":
                        {
                            if (!pending.TryGetValue(key, out var spawn))
                            {
                                Log("  no pending spawn (already handled or spawn missed), skipped");
                                return;
                            }

                            string location = GetLocation(data?["location"]?.GetValue<string>(),
                                data?["zonePoiId"]?.GetValue<int>() ?? GetFirstPoi(data?["zonePoiIds"]));
                            if (location == null)
                            {
                                Log("  still no location");
                                return;
                            }

                            pending.Remove(key);
                            RaiseSpawn(spawn.mobId, spawn.world, spawn.worldId, spawn.territoryId, spawn.instance, location,
                                action == "spawn_location" && spawn.scheduled);
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
                Log("Message parse failed : " + e.Message + " raw=" + Truncate(response.ToString(), 300));
            }
        }

        private static int GetFirstPoi(JsonNode pois)
        {
            return pois is JsonArray array && array.Count > 0 ? array[0].GetValue<int>() : 0;
        }

        private static string GetLocation(string location, int poi)
        {
            if (!string.IsNullOrEmpty(location))
                return location;

            return FaloopData.Locations.TryGetValue(poi, out string loc) ? loc : null;
        }

        private void RaiseSpawn(string mobId, string world, uint worldId, uint territoryId, int instance, string location, bool scheduled)
        {
            string[] xy = location.Split(",");
            if (xy.Length != 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y))
            {
                Log("  bad location '" + location + "'");
                return;
            }

            Log("  spawn -> territory=" + territoryId + " location=" + location + (scheduled ? " (early access)" : ""));
            OnSpawn?.Invoke(new FaloopSpawn(mobId, world, worldId, territoryId, instance, x, y, scheduled));
        }

        private static string Truncate(string text, int length)
        {
            if (text == null)
                return "null";

            return text.Length <= length ? text : text.Substring(0, length) + "...";
        }

        public void Dispose()
        {
            Disconnect();
            http.Dispose();
        }
    }
}
