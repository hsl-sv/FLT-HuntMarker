using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
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
    // 3. socket.io over websocket wss://faloop.app/messaging/socket.io/?EIO=4&transport=websocket
    //    (Faloop app moved from /comms to /messaging, /comms still accepts but sends only app ping)
    //    CONNECT with auth { sessionid }, emit "ack" after connect, "message" events carry mob reports.
    //    Raw websocket like faloop-messenger, SocketIOClient polling did not receive reports.
    // Engine.IO packet : 0 = open, 2 = ping, 3 = pong, 4 = message
    // Socket.IO packet (after 4) : 0 = connect, 1 = disconnect, 2 = event 42["name", data], 4 = error
    // Early access accounts get "spawn" with isScheduled = true before public, then "spawn_release"
    public class FaloopClient : IDisposable
    {
        private const string ORIGIN = "https://faloop.app";
        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0";

        private const string SOCKET_URL = "wss://faloop.app/messaging/socket.io/?EIO=4&transport=websocket";
        private const int RAW_LOG_COUNT = 30; // First packets are logged raw
        private const int ALIVE_LOG_PINGS = 12; // After raw log, log once per 12 pings (~5 min)

        private readonly HttpClient http = new();
        private CancellationTokenSource cts;
        private string sessionId;
        private volatile bool connected;
        private int rawLogged;
        private int pingCount;

        // Spawn report without location, waiting for spawn_location / spawn_release
        private readonly Dictionary<string, (string mobId, string world, uint worldId, uint territoryId, int instance, bool scheduled)> pending = new();

        // Reports are for all worlds and ranks, so only count them in log
        private int reportCount;
        private int sRankCount;
        private DateTime countSince = DateTime.Now;
        private bool firstReport;

        public event Action<FaloopSpawn> OnSpawn;
        public event Action<FaloopState, string> OnStatus;

        public bool IsConnected => connected;

        public static void Log(string message)
        {
            Trace.WriteLine("[Faloop " + DateTime.Now.ToString("HH:mm:ss") + "] " + message);
        }

        private void SetStatus(FaloopState state, string text)
        {
            Log("Status " + state + " : " + text);
            OnStatus?.Invoke(state, text);
        }

        // savedSession : session id copied from Faloop app (DevTools), resumed instead of id / pw login
        public async Task<bool> ConnectAsync(string username, string password, string savedSession)
        {
            Disconnect();

            SetStatus(FaloopState.Connecting, "Logging in...");

            if (!string.IsNullOrEmpty(savedSession))
            {
                // Same as Faloop app on page load : refresh with stored session resumes it if still valid
                var resume = await PostAsync("/api/auth/user/refresh", "/", null, new Dictionary<string, object> { { "sessionId", savedSession } });
                string resumed = resume?["data"]?["sessionId"]?.GetValue<string>();
                LogAuth("Refresh (saved session)", resume);

                if (resume?["success"]?.GetValue<bool>() == true && resumed == savedSession)
                {
                    Log("Saved session resumed");
                    sessionId = resumed;
                }
                else
                {
                    Log("Saved session expired or invalid (server returned " + (resumed == null ? "none" : "a new session") + ")");
                }
            }

            if (sessionId == null)
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    SetStatus(FaloopState.Disconnected, "Session expired, login needed");
                    return false;
                }

                var refresh = await PostAsync("/api/auth/user/refresh", "/", null, new Dictionary<string, object> { { "sessionId", null } });
                LogAuth("Refresh (anonymous)", refresh);
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
                LogAuth("Login", login);
                if (login == null || login["success"]?.GetValue<bool>() != true)
                {
                    SetStatus(FaloopState.Disconnected, "Login failed");
                    return false;
                }

                Log("Login OK as " + username);
                sessionId = login["data"]?["sessionId"]?.GetValue<string>();

                // Check what server thinks of the logged in session
                var check = await PostAsync("/api/auth/user/refresh", "/", null, new Dictionary<string, object> { { "sessionId", sessionId } });
                LogAuth("Refresh (after login)", check);
                string checkedSession = check?["data"]?["sessionId"]?.GetValue<string>();
                if (checkedSession != null && checkedSession != sessionId)
                {
                    Log("Server returned another session after login, using it");
                    sessionId = checkedSession;
                }
            }

            cts = new CancellationTokenSource();
            _ = Task.Run(() => ConnectLoop(cts.Token));

            return true;
        }

        public void Disconnect()
        {
            cts?.Cancel();
            cts = null;
            connected = false;
            sessionId = null;
            pending.Clear();
        }

        // Keep socket alive, reconnect with backoff
        private async Task ConnectLoop(CancellationToken ct)
        {
            int failures = 0;

            while (!ct.IsCancellationRequested)
            {
                DateTime started = DateTime.Now;

                try
                {
                    SetStatus(FaloopState.Connecting, failures == 0 ? "Connecting socket..." : "Reconnecting (" + failures + ")...");

                    using var ws = new ClientWebSocket();
                    ws.Options.SetRequestHeader("Origin", ORIGIN);
                    ws.Options.SetRequestHeader("User-Agent", USER_AGENT);

                    await ws.ConnectAsync(new Uri(SOCKET_URL), ct);
                    Log("WebSocket opened");
                    rawLogged = 0;
                    pingCount = 0;

                    await ReceiveLoop(ws, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Log("WebSocket error : " + e.Message);
                }

                connected = false;
                if (ct.IsCancellationRequested)
                    break;

                // Connection that dropped right away still backs off
                failures = DateTime.Now - started > TimeSpan.FromSeconds(30) ? 0 : failures + 1;
                int delay = Math.Min(120, 5 * (1 << Math.Min(failures, 5)));
                SetStatus(FaloopState.Disconnected, "Disconnected, retry in " + delay + "s");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // Off status is set by caller, this loop may end after a new one started
            connected = false;
        }

        private async Task ReceiveLoop(ClientWebSocket ws, CancellationToken ct)
        {
            byte[] buffer = new byte[65536];
            using var ms = new MemoryStream();

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;

                // Engine.IO ping is ~25s, no frame for 60s means dead socket
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
                idle.CancelAfter(TimeSpan.FromSeconds(60));

                do
                {
                    try
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), idle.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        Log("No frame for 60s, reconnecting");
                        return;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log("WebSocket closed by server : " + result.CloseStatus + " " + result.CloseStatusDescription);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                // Decode once, fragments may split multibyte characters
                string packet = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                await HandlePacket(ws, packet, ct);
            }
        }

        private async Task HandlePacket(ClientWebSocket ws, string packet, CancellationToken ct)
        {
            if (packet.Length == 0)
                return;

            if (rawLogged < RAW_LOG_COUNT)
            {
                rawLogged++;
                Log("<< " + Truncate(packet, 300));

                if (rawLogged == RAW_LOG_COUNT)
                    Log("Raw packet log limit reached, pings are logged every " + ALIVE_LOG_PINGS + " from now");
            }
            else if (packet == "2" && ++pingCount % ALIVE_LOG_PINGS == 0)
            {
                Log("Socket alive (" + pingCount + " pings after raw log)");
            }

            switch (packet[0])
            {
                case '0': // Engine.IO open -> Socket.IO connect with session
                    await Send(ws, "40" + new JsonObject { ["sessionid"] = sessionId }.ToJsonString(), ct);
                    break;
                case '2': // Engine.IO ping
                    await Send(ws, "3", ct);
                    break;
                case '4' when packet.Length > 1:
                    await HandleSocketIO(ws, packet.Substring(1), ct);
                    break;
            }
        }

        private async Task HandleSocketIO(ClientWebSocket ws, string packet, CancellationToken ct)
        {
            switch (packet[0])
            {
                case '0': // connected
                    connected = true;
                    SetStatus(FaloopState.Connected, "Connected");
                    await Send(ws, "42[\"ack\"]", ct); // Required by Faloop
                    break;
                case '1':
                    Log("Socket.IO disconnect from server");
                    connected = false;
                    break;
                case '2' when packet.Length > 1: // 42["name", data]
                    HandleEvent(packet.Substring(1));
                    break;
                case '4':
                    Log("Socket.IO error : " + Truncate(packet.Substring(1), 300));
                    break;
            }
        }

        private void HandleEvent(string json)
        {
            try
            {
                if (JsonNode.Parse(json) is not JsonArray args || args.Count < 1)
                    return;

                string name = args[0]?.GetValue<string>();
                if (name != "message")
                {
                    Log("Event '" + name + "' " + Truncate(json, 300));
                    return;
                }

                if (args.Count > 1)
                    HandleMessage(args[1]);
            }
            catch (Exception e)
            {
                Log("Event parse failed : " + e.Message + " raw=" + Truncate(json, 300));
            }
        }

        private async Task Send(ClientWebSocket ws, string packet, CancellationToken ct)
        {
            if (rawLogged < RAW_LOG_COUNT)
                Log(">> " + Truncate(packet.Replace(sessionId ?? "\0", "<session>"), 300));

            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(packet)), WebSocketMessageType.Text, true, ct);
        }

        public string SessionId => sessionId;

        // Log response shape and JWT claims to see if server treats session as logged in user.
        // Values of sessionId / token are not logged.
        private static void LogAuth(string step, JsonNode response)
        {
            if (response == null)
                return;

            try
            {
                if (response["data"] is JsonObject data)
                {
                    List<string> keys = new();
                    foreach (var kv in data)
                        keys.Add(kv.Key);

                    Log(step + " : success=" + response["success"] + " data keys=[" + string.Join(", ", keys) + "]");

                    if (data["token"]?.GetValue<string>() is string token)
                        Log(step + " : token claims " + Truncate(DecodeJwt(token), 500));
                }
                else
                {
                    Log(step + " : " + Truncate(response.ToJsonString(), 300));
                }
            }
            catch (Exception e)
            {
                Log(step + " : log failed " + e.Message);
            }
        }

        // Payload part of JWT ("JWT xxx.yyy.zzz" or "xxx.yyy.zzz")
        private static string DecodeJwt(string token)
        {
            string[] parts = token.Replace("JWT ", "").Replace("Bearer ", "").Split('.');
            if (parts.Length < 2)
                return "(not jwt)";

            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
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
        private void HandleMessage(JsonNode payload)
        {
            try
            {
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
                Log("Message parse failed : " + e.Message + " raw=" + Truncate(payload?.ToJsonString(), 300));
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
