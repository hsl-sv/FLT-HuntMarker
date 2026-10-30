using Lumina.Excel.Sheets;
using Sharlayan;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FLT_HuntMarker
{
    // Map flag (same as Ctrl + right click on map) and current world, for Faloop tracking
    public partial class HuntCounter
    {
        private const string UIMODULE_KEY = "FLT_UIMODULE";
        private const string FCS_FRAMEWORK = "FFXIVClientStructs.FFXIV.Client.System.Framework.Framework";
        private const string FCS_UIMODULE_INTERFACE = "FFXIVClientStructs.FFXIV.Client.UI.UIModuleInterface";
        private const string FCS_AGENTMODULE = "FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentModule";
        private const string FCS_AGENTID = "FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId";
        private const string FCS_AGENTMAP = "FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap";
        private const string FCS_FLAGMAPMARKER = "FFXIVClientStructs.FFXIV.Client.UI.Agent.FlagMapMarker";
        private const string FCS_MAPMARKERBASE = "FFXIVClientStructs.FFXIV.Client.UI.Agent.MapMarkerBase";
        private const string FCS_CHARACTER = "FFXIVClientStructs.FFXIV.Client.Game.Character.Character";

        private const uint FLAG_ICON = 0xEC91; // Same default as AgentMap.SetFlagMapMarker

        // Offsets read from ClientStructs once
        private record FlagLayout(
            uint GetAgentModuleIndex, long Agents, int MapAgentId,
            long FlagMarker, long FlagMarkerCount,
            long TerritoryId, long MapId, long XFloat, long YFloat,
            long IconId, long MarkerX, long MarkerY);

        private FlagLayout _flagLayout;
        private long? _currentWorldOffset;
        private Lumina.GameData _lumina;

        // Current world of player (World row id), 0 if unknown
        public uint GetCurrentWorld()
        {
            try
            {
                if (MemoryHandler == null ||
                    !MemoryHandler.Scanner.Locations.TryGetValue(Signatures.CHARMAP_KEY, out MemoryLocation charmap))
                    return 0;

                // First entry of object table is local player
                IntPtr player = MemoryHandler.ReadPointer(charmap);
                if (player == IntPtr.Zero)
                    return 0;

                _currentWorldOffset ??= GetFieldOffset(GetFcsType(FCS_CHARACTER), "CurrentWorld");

                return MemoryHandler.GetUInt16(player, _currentWorldOffset.Value);
            }
            catch
            {
                return 0;
            }
        }

        // Map row of territory from game data (sqpack)
        public (uint mapId, ushort sizeFactor, short offsetX, short offsetY)? GetMapInfo(uint territoryId)
        {
            try
            {
                _lumina ??= CreateLumina();

                TerritoryType? territory = _lumina.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId);
                Map? map = territory?.Map.ValueNullable;
                if (map == null)
                    return null;

                return (map.Value.RowId, map.Value.SizeFactor, map.Value.OffsetX, map.Value.OffsetY);
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unable to read map info : " + e.Message);
                return null;
            }
        }

        private Lumina.GameData CreateLumina()
        {
            // Process path is <install>/game/ffxiv_dx11.exe, check both like Sharlayan
            string sqpack = System.IO.Path.Combine(_gameInstallPath, "sqpack");
            if (!System.IO.Directory.Exists(sqpack))
                sqpack = System.IO.Path.Combine(_gameInstallPath, "game", "sqpack");

            return new Lumina.GameData(sqpack, new Lumina.LuminaOptions { PanicOnSheetChecksumMismatch = false });
        }

        // Set map flag like AgentMap.SetFlagMapMarker, location is pixel on 2048x2048 map texture (Faloop format).
        // External app cannot call game functions, so agent fields are written directly.
        // Map window shows it after reopen, <flag> in chat works right away.
        public bool SetFlag(uint territoryId, int locationX, int locationY)
        {
            try
            {
                var map = GetMapInfo(territoryId);
                if (map == null)
                    return false;

                (uint mapId, ushort sizeFactor, short offsetX, short offsetY) = map.Value;

                // Pixel -> world X/Z, same as Divination FaloopIntegration MobSpawnEvent.WorldPosition
                float worldX = (locationX - 1024f) * 100f / sizeFactor - offsetX;
                float worldZ = (locationY - 1024f) * 100f / sizeFactor - offsetY;

                IntPtr agentMap = GetAgentMap();
                if (agentMap == IntPtr.Zero)
                    return false;

                FlagLayout l = _flagLayout;
                IntPtr flag = agentMap + (int)l.FlagMarker;

                using var writer = new ProcessWriter(_process);

                // Hide flag while writing, then show
                writer.Write(agentMap + (int)l.FlagMarkerCount, new byte[] { 0 });
                writer.Write(flag + (int)l.TerritoryId, BitConverter.GetBytes(territoryId));
                writer.Write(flag + (int)l.MapId, BitConverter.GetBytes(mapId));
                writer.Write(flag + (int)l.XFloat, BitConverter.GetBytes(worldX));
                writer.Write(flag + (int)l.YFloat, BitConverter.GetBytes(worldZ));
                writer.Write(flag + (int)l.IconId, BitConverter.GetBytes(FLAG_ICON));
                // Map marker position is world * 16 (see AgentMap.AddMapMarker)
                writer.Write(flag + (int)l.MarkerX, BitConverter.GetBytes((short)(worldX * 16.0f)));
                writer.Write(flag + (int)l.MarkerY, BitConverter.GetBytes((short)(worldZ * 16.0f)));
                writer.Write(agentMap + (int)l.FlagMarkerCount, new byte[] { 1 });

                return true;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unable to set flag : " + e.Message);
                return false;
            }
        }

        // Framework->UIModule->GetAgentModule()->Agents[AgentId.Map]
        private IntPtr GetAgentMap()
        {
            if (MemoryHandler == null ||
                !MemoryHandler.Scanner.Locations.TryGetValue(UIMODULE_KEY, out MemoryLocation uiModuleField))
                return IntPtr.Zero;

            _flagLayout ??= ResolveFlagLayout();

            IntPtr uiModule = MemoryHandler.ReadPointer(uiModuleField);
            if (uiModule == IntPtr.Zero)
                return IntPtr.Zero;

            // GetAgentModule() is a virtual function. It is a one-liner returning a member,
            // "lea rax, [rcx + disp32]; ret" (48 8D 81 xx xx xx xx C3), so take disp32 from the code.
            IntPtr vtable = MemoryHandler.ReadPointer(uiModule);
            IntPtr function = MemoryHandler.ReadPointer(vtable, _flagLayout.GetAgentModuleIndex * 8);
            byte[] code = MemoryHandler.GetByteArray(function, 8);

            IntPtr agentModule;
            if (code[0] == 0x48 && code[1] == 0x8D && code[2] == 0x81 && code[7] == 0xC3)
                agentModule = uiModule + BitConverter.ToInt32(code, 3);
            else if (code[0] == 0x48 && code[1] == 0x8B && code[2] == 0x81 && code[7] == 0xC3)
                agentModule = MemoryHandler.ReadPointer(uiModule, BitConverter.ToInt32(code, 3));
            else
            {
                Trace.WriteLine("Unknown GetAgentModule code : " + BitConverter.ToString(code));
                return IntPtr.Zero;
            }

            return MemoryHandler.ReadPointer(agentModule, _flagLayout.Agents + _flagLayout.MapAgentId * 8);
        }

        private static FlagLayout ResolveFlagLayout()
        {
            MethodInfo getAgentModule = GetFcsType(FCS_UIMODULE_INTERFACE).GetMethod("GetAgentModule",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Attribute virtualFunction = getAgentModule.GetCustomAttributes()
                .First(a => a.GetType().Name == "VirtualFunctionAttribute");
            uint index = (uint)virtualFunction.GetType().GetProperty("Index").GetValue(virtualFunction);

            Type agentMap = GetFcsType(FCS_AGENTMAP);
            Type flagMarker = GetFcsType(FCS_FLAGMAPMARKER);
            Type markerBase = GetFcsType(FCS_MAPMARKERBASE);
            long markerOffset = GetFieldOffset(flagMarker, "MapMarker");

            return new FlagLayout(
                index,
                GetFieldOffset(GetFcsType(FCS_AGENTMODULE), "_agents"),
                Convert.ToInt32(Enum.Parse(GetFcsType(FCS_AGENTID), "Map")),
                // Older ClientStructs : FlagMapMarker / IsFlagMarkerSet
                GetFieldOffset(agentMap, "_flagMapMarkers", "FlagMapMarker"),
                GetFieldOffset(agentMap, "FlagMarkerCount", "IsFlagMarkerSet"),
                GetFieldOffset(flagMarker, "TerritoryId"),
                GetFieldOffset(flagMarker, "MapId"),
                GetFieldOffset(flagMarker, "XFloat"),
                GetFieldOffset(flagMarker, "YFloat"),
                markerOffset + GetFieldOffset(markerBase, "IconId"),
                markerOffset + GetFieldOffset(markerBase, "X"),
                markerOffset + GetFieldOffset(markerBase, "Y"));
        }

        private static long GetFieldOffset(Type type, string fieldName, string fallbackName)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            return type.GetField(fieldName, flags) != null ? GetFieldOffset(type, fieldName) : GetFieldOffset(type, fallbackName);
        }

        // WriteProcessMemory wrapper (app runs as administrator)
        private sealed class ProcessWriter : IDisposable
        {
            private const uint PROCESS_VM_OPERATION = 0x0008;
            private const uint PROCESS_VM_WRITE = 0x0020;

            private readonly IntPtr handle;

            public ProcessWriter(Process process)
            {
                handle = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_WRITE, false, process.Id);
                if (handle == IntPtr.Zero)
                    throw new InvalidOperationException("OpenProcess failed : " + Marshal.GetLastWin32Error());
            }

            public void Write(IntPtr address, byte[] data)
            {
                if (!WriteProcessMemory(handle, address, data, data.Length, out _))
                    throw new InvalidOperationException("WriteProcessMemory failed : " + Marshal.GetLastWin32Error());
            }

            public void Dispose()
            {
                CloseHandle(handle);
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, int size, out IntPtr written);

            [DllImport("kernel32.dll")]
            private static extern bool CloseHandle(IntPtr handle);
        }
    }
}
