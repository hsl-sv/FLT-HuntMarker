﻿using Sharlayan;
using Sharlayan.Core;
using Sharlayan.Enums;
using Sharlayan.Models;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

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

                // supported: Global, Chinese, Korean
                GameRegion gameRegion = GameRegion.Global;

                GameLanguage gameLanguage = GameLanguage.English;

                // whether to always hit API on start to get the latest sigs based on patchVersion, or use the local json cache (if the file doesn't exist, API will be hit)
                bool useLocalCache = true;

                // patchVersion of game, or latest
                string patchVersion = "latest";
                _process = processes[0];

                ProcessModel processModel = new ProcessModel
                {
                    Process = _process

                };

                SharlayanConfiguration configuration = new SharlayanConfiguration
                {
                    GameLanguage = gameLanguage,
                    ProcessModel = processModel
                };

                try
                {
                    MemoryHandler = SharlayanMemoryManager.Instance.AddHandler(configuration);
                    MemoryHandler = SharlayanMemoryManager.Instance.GetHandler(processModel.ProcessID);
                    _reader = MemoryHandler.Reader;

                }
                catch (Exception e)
                {
                    Trace.WriteLine("Unable to add handlers... Maybe game has been updated?");

                    return false;
                }

                Console.WriteLine(processModel.ProcessID);

                Thread.Sleep(1000);

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

        public (uint mapID, uint mapIndex, uint mapTerritory) GetMap()
        {
            try
            {

                (uint mapID, uint mapIndex, uint mapTerritory) = _reader.GetMapInfo();
                return (mapID, mapIndex, mapTerritory);
                
            }
            catch
            {
                return (0, 0, 0);
            }
        }
    }
}
