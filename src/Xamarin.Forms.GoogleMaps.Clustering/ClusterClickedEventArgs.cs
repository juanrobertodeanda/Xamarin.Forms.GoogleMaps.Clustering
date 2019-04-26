using System;
using System.Collections;
using System.Collections.Generic;

namespace Xamarin.Forms.GoogleMaps.Clustering
{
    public sealed class ClusterClickedEventArgs : EventArgs
    {
        public bool Handled { get; set; } = false;
        public int ItemsCount { get; }
        public Position ClusterPosition { get; set; }

        public List<Position> Items { get; set; }

        internal ClusterClickedEventArgs(int itemsCount, Position position)
        {
            ItemsCount = itemsCount;
            ClusterPosition = position;
        }

        internal ClusterClickedEventArgs(List<Position> items, int itemsCount, Position position)
        {
            ItemsCount = itemsCount;
            ClusterPosition = position;
            Items = items;
        }
    }
}