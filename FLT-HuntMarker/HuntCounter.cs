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
    public class HuntCounter
    {
        public MemoryHandler MemoryHandler { get; set; }
        public bool CanGetProcess => _process != null;
        public bool ProcessHasExited => _process?.HasExited == null || _process.HasExited;

        private Reader _reader;
        private Process _process;

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

                SharlayanConfiguration configuration = new SharlayanConfiguration
                {
                    ProcessModel = processModel,
                    GameInstallPath = System.IO.Path.GetDirectoryName(_process.MainModule?.FileName)
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

                LoadPublicInstanceSignature();

                Console.WriteLine(processModel.ProcessID);

                return true;
            }

            return false;
        }

        public ActorItem GetUser()
        {
            if (MemoryHandler.Reader.CanGetActors())
            {
                var actors = _reader.GetActors();

                var p = _reader.GetCurrentPlayer()?.Entity ?? new ActorItem() { Name = "not found" };

                return p;
            }
            else
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

        // Sharlayan does not read UIState.PublicInstance.InstanceId, so scan it ourselves.
        // Signature and offsets come from the FFXIVClientStructs types merged (internalized) into Sharlayan.dll,
        // so they follow Sharlayan updates without hardcoding.
        private void LoadPublicInstanceSignature()
        {
            try
            {
                Assembly asm = typeof(MemoryHandler).Assembly;
                Type uiState = asm.GetType(FCS_UISTATE);
                Type publicInstance = asm.GetType(FCS_PUBLICINSTANCE);

                if (uiState == null || publicInstance == null)
                {
                    Trace.WriteLine("UIState / PublicInstance not found in Sharlayan");
                    return;
                }

                // [StaticAddress(signature, relativeFollowOffset(s), isPointer)] on UIState.Instance()
                Attribute staticAddress = uiState
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes())
                    .FirstOrDefault(a => a.GetType().Name == "StaticAddressAttribute");

                if (staticAddress == null)
                {
                    Trace.WriteLine("StaticAddress of UIState not found");
                    return;
                }

                Type attrType = staticAddress.GetType();
                string pattern = (string)attrType.GetProperty("Signature").GetValue(staticAddress);
                ushort[] follows = (ushort[])attrType.GetProperty("RelativeFollowOffsets").GetValue(staticAddress);
                bool isPointer = (bool)attrType.GetProperty("IsPointer").GetValue(staticAddress);

                long fieldOffset = GetFieldOffset(uiState, "PublicInstance") + GetFieldOffset(publicInstance, "InstanceId");

                // Same conversion as Sharlayan's FFXIVClientStructsSignatureExtractor:
                // scanner lands after the pattern, rewind to the RIP-relative operand, then follow it
                string patternHex = pattern.Replace(" ", string.Empty).ToUpperInvariant();
                List<long> path = new() { -(patternHex.Length / 2 - follows[0]) };
                if (isPointer)
                    path.Add(0);
                path.Add(fieldOffset);

                MemoryHandler.Scanner.LoadOffsets(new[]
                {
                    new Signature
                    {
                        Key = PUBLIC_INSTANCE_KEY,
                        Value = patternHex,
                        ASMSignature = true,
                        PointerPath = path,
                    }
                });
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unable to load PublicInstance signature : " + e.Message);
            }
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