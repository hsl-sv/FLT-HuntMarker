﻿using Sharlayan.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FLT_HuntMarker
{
    public class Mob : ActorItem, ICoordinates, INotifyPropertyChanged
    {
        private Coords _coordinates;
        private double _hPPercent;
        private double _hPPercentAsPercentage;

        public Coords Coordinates
        {
            get => _coordinates;
            set
            {
                _coordinates = value;
                CoordsChanged?.Invoke(this, _coordinates);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Coordinates)));
            }
        }

        public event EventHandler<Coords> CoordsChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; set; }
        public uint Key { get; set; }
        public int ModelID { get; set; }
        public int Count { get; set; }
        public string Rank { get; set; }
        public double HP { get; set; }
        public int MapTerritory { get; set; }
        public string MapImagePath { get; set; }
        public bool IsTracking { get; set; }
        public new double HPPercent
        {
            get => _hPPercent;
            set
            {
                _hPPercent = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HPPercent)));

                HPPercentAsPercentage = Math.Round(_hPPercent * 100, 2);
            }
        }

        public double HPPercentAsPercentage
        {
            get => _hPPercentAsPercentage;
            private set
            {
                _hPPercentAsPercentage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HPPercentAsPercentage)));
            }
        }

        public Mob()
        {

        }

        public Mob(string name, int count)
        {
            Name = name;
            Count = count;
        }

        public Mob(string name, double hp, uint id, bool trackStatus, Coords coordinates)
        {
            Name = name;
            HP = hp;
            Key = id;
            IsTracking = trackStatus;
            Coordinates = coordinates;
        }

        public Mob(Coords coordinates, string name, int modelId, string rank, double hp, int mapTerritory)
        {
            Coordinates = coordinates;
            Name = name;
            ModelID = modelId;
            Rank = rank;
            HP = hp;
            MapTerritory = mapTerritory;
        }

        public void UnregisterHandlers()
        {
            CoordsChanged = null;
            PropertyChanged = null;
        }
    }
}
