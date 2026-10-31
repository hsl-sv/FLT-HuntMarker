using Lumina.Excel.Sheets;
using Sharlayan;
using System;
using System.Diagnostics;

namespace FLT_HuntMarker
{
    // Current world and map info, for Faloop tracking
    public partial class HuntCounter
    {
        private const string FCS_CHARACTER = "FFXIVClientStructs.FFXIV.Client.Game.Character.Character";

        private long? _currentWorldOffset;
        private Lumina.GameData _lumina;

        // Current world of player (World row id), 0 if unknown
        public uint GetCurrentWorld()
        {
            try
            {
                if (MemoryHandler == null ||
                    !MemoryHandler.Scanner.Locations.TryGetValue(Signatures.CHARMAP_KEY, out MemoryLocation charmap))
                {
                    Faloop.FaloopClient.Log("GetCurrentWorld : CHARMAP not scanned");
                    return 0;
                }

                // First entry of object table is local player
                IntPtr player = MemoryHandler.ReadPointer(charmap);
                if (player == IntPtr.Zero)
                {
                    Faloop.FaloopClient.Log("GetCurrentWorld : player pointer is null");
                    return 0;
                }

                _currentWorldOffset ??= GetFieldOffset(GetFcsType(FCS_CHARACTER), "CurrentWorld");

                return MemoryHandler.GetUInt16(player, _currentWorldOffset.Value);
            }
            catch (Exception e)
            {
                Faloop.FaloopClient.Log("GetCurrentWorld failed : " + e);
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
    }
}
