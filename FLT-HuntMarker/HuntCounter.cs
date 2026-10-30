using Sharlayan;
using Sharlayan.Core;
using Sharlayan.Enums;
using Sharlayan.Models;
using Sharlayan.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FLT_HuntMarker
{
    public partial class HuntCounter
    {
        public MemoryHandler MemoryHandler { get; set; }
        public bool CanGetProcess => _process != null;
        public bool ProcessHasExited => _process?.HasExited == null || _process.HasExited;

        private Reader _reader;
        private Process _process;
        private string _gameInstallPath;

        public bool Setup()
        {
            // DX11
            Process[] processes = Process.GetProcessesByName("ffxiv_dx11");

            if (processes.Length > 0)
            {
                _process = processes[0];

                ProcessModel processModel = new ProcessModel
                {
                    Process = _process
                };

                _gameInstallPath = System.IO.Path.GetDirectoryName(_process.MainModule?.FileName);

                SharlayanConfiguration configuration = new SharlayanConfiguration
                {
                    ProcessModel = processModel,
                    GameInstallPath = _gameInstallPath
                };

                try
                {
                    MemoryHandler = SharlayanMemoryManager.Instance.AddHandler(configuration);
                    _reader = MemoryHandler.Reader;
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Unable to add handlers... Maybe game has been updated?");

                    return false;
                }

                LoadExtraSignatures();

                Console.WriteLine(processModel.ProcessID);

                return true;
            }

            return false;
        }

        // Current player only, without full actor scan (GetActors is used by tracking thread)
        public ActorItem GetUser()
        {
            try
            {
                return _reader?.GetCurrentPlayer()?.Entity;
            }
            catch
            {
                return null;
            }
        }

        public ConcurrentDictionary<uint, ActorItem> GetMobs()
        {
            try
            {
                if (MemoryHandler.Reader.CanGetActors())
                {
                    var actors = _reader.GetActors();

                    return actors.CurrentMonsters;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private MethodInfo _getMapInfoMethod = null;

        public (uint mapID, uint mapIndex, uint mapTerritory) GetMap()
        {
            try
            {
                if (_getMapInfoMethod == null)
                {
                    _getMapInfoMethod = _reader.GetType().GetMethod(
                        "GetMapInfo",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
                    );
                }

                if (_getMapInfoMethod != null)
                {
                    object result = _getMapInfoMethod.Invoke(_reader, null);

                    if (result != null)
                    {
                        return ((uint, uint, uint))result;
                    }
                }

                return (0, 0, 0);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        private const string PUBLIC_INSTANCE_KEY = "FLT_PUBLIC_INSTANCE";
        private const string FCS_UISTATE = "FFXIVClientStructs.FFXIV.Client.Game.UI.UIState";
        private const string FCS_PUBLICINSTANCE = "FFXIVClientStructs.FFXIV.Client.Game.UI.PublicInstance";

        // Sharlayan does not read UIState.PublicInstance.InstanceId / Framework.UIModule, so scan them ourselves.
        // Signature and offsets come from the FFXIVClientStructs types merged (internalized) into Sharlayan.dll,
        // so they follow Sharlayan updates without hardcoding.
        private void LoadExtraSignatures()
        {
            List<Signature> signatures = new();

            try
            {
                Type uiState = GetFcsType(FCS_UISTATE);
                long offset = GetFieldOffset(uiState, "PublicInstance") + GetFieldOffset(GetFcsType(FCS_PUBLICINSTANCE), "InstanceId");

                signatures.Add(BuildFcsSignature(PUBLIC_INSTANCE_KEY, uiState, offset));
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unable to load PublicInstance signature : " + e.Message);
            }

            try
            {
                Type framework = GetFcsType(FCS_FRAMEWORK);

                signatures.Add(BuildFcsSignature(UIMODULE_KEY, framework, GetFieldOffset(framework, "UIModule")));
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unable to load Framework signature : " + e.Message);
            }

            if (signatures.Count > 0)
                MemoryHandler.Scanner.LoadOffsets(signatures.ToArray());
        }

        private static Type GetFcsType(string name)
        {
            return typeof(MemoryHandler).Assembly.GetType(name) ?? throw new InvalidOperationException(name + " not found in Sharlayan");
        }

        // Signature resolving to (static instance of type) + fieldOffset
        private static Signature BuildFcsSignature(string key, Type type, long fieldOffset)
        {
            // [StaticAddress(signature, relativeFollowOffset(s), isPointer)] on Type.Instance()
            Attribute staticAddress = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes())
                .FirstOrDefault(a => a.GetType().Name == "StaticAddressAttribute")
                ?? throw new InvalidOperationException("StaticAddress of " + type.Name + " not found");

            Type attrType = staticAddress.GetType();
            string pattern = (string)attrType.GetProperty("Signature").GetValue(staticAddress);
            ushort[] follows = (ushort[])attrType.GetProperty("RelativeFollowOffsets").GetValue(staticAddress);
            bool isPointer = (bool)attrType.GetProperty("IsPointer").GetValue(staticAddress);

            // Same conversion as Sharlayan's FFXIVClientStructsSignatureExtractor:
            // scanner lands after the pattern, rewind to the RIP-relative operand, then follow it
            string patternHex = pattern.Replace(" ", string.Empty).ToUpperInvariant();
            List<long> path = new() { -(patternHex.Length / 2 - follows[0]) };
            if (isPointer)
                path.Add(0);
            path.Add(fieldOffset);

            return new Signature
            {
                Key = key,
                Value = patternHex,
                ASMSignature = true,
                PointerPath = path,
            };
        }

        private static long GetFieldOffset(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            return field.GetCustomAttribute<FieldOffsetAttribute>().Value;
        }

        // Current public instance number (0 = area is not instanced)
        public uint GetInstance()
        {
            try
            {
                if (MemoryHandler != null &&
                    MemoryHandler.Scanner.Locations.TryGetValue(PUBLIC_INSTANCE_KEY, out MemoryLocation location))
                {
                    return MemoryHandler.GetUInt32(location);
                }
            }
            catch
            {
                // pass
            }

            return 0;
        }

        private const double ZONE_SETTLE_SECONDS = 3.0;
        private (uint territory, uint instance) _lastZone;
        private DateTime _zoneSettledAt = DateTime.MinValue;

        // False while zone is loading or just changed.
        // Sharlayan keeps previous zone actors until they expire, those would be marked on new map.
        public bool IsZoneSettled(uint mapTerritory, uint instance)
        {
            (uint, uint) zone = (mapTerritory, instance);

            if (zone != _lastZone)
            {
                // First read after hook has nothing stale
                if (_lastZone.territory != 0)
                    _zoneSettledAt = DateTime.Now.AddSeconds(ZONE_SETTLE_SECONDS);

                _lastZone = zone;
            }

            try
            {
                if (_reader.CanGetGameState())
                {
                    var state = _reader.GetGameState();

                    if (!state.IsReadyToRead || state.IsTeleporting)
                        _zoneSettledAt = DateTime.Now.AddSeconds(ZONE_SETTLE_SECONDS);
                }
            }
            catch
            {
                // pass
            }

            return DateTime.Now >= _zoneSettledAt;
        }

        // Flag generator, test bed
        // Make echo or flag in game needs manipulates memory, i guess, cannot go further
        public void FlagGenerator()
        {
            int _previousArrayIndex = 0;
            int _previousOffset = 0;

            Trace.WriteLine("Testing GetChatLog");

            try
            {
                if (MemoryHandler.Reader.CanGetChatLog())
                {
                    Sharlayan.Models.ReadResults.ChatLogResult readResult = _reader.GetChatLog(
                        _previousArrayIndex, _previousOffset);

                    ConcurrentQueue<ChatLogItem> chatLogEntries = readResult.ChatLogItems;

                    foreach (var item in chatLogEntries)
                    {
                        Trace.WriteLine(item.Message);
                    }
                }
            }
            catch
            {
                return;
            }
        }
    }
}