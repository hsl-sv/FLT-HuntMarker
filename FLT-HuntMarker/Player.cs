using Sharlayan.Core;
using System;
using System.ComponentModel;

namespace FLT_HuntMarker
{
    public class Player : ActorItem, ICoordinates, INotifyPropertyChanged
    {
        private string _currentMapImagePath;
        private Coords _coordinates;

        public Coords Coordinates
        {
            get => _coordinates;
            set
            {
                _coordinates = value;
                CoordsChanged?.Invoke(this, _coordinates);
            }
        }

        public new double HPPercent { get; set; }

        public event EventHandler<Coords> CoordsChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public string CurrentWorld { get; set; }
        public string PlayerIconImagePath { get; set; }

        public string CurrentMapImagePath
        {
            get => _currentMapImagePath;
            set
            {
                _currentMapImagePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentMapImagePath)));
            }
        }

        public Player()
        {

        }
        public Player(Coords coordinates, string name, string currentWorld, uint mapTerritory)
        {
            Coordinates = coordinates;
            Name = name;
            CurrentWorld = currentWorld;
            MapTerritory = mapTerritory;
        }
    }
}
