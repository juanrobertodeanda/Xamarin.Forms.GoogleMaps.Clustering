using System;
using CoreGraphics;
using CoreLocation;
using Google.Maps;
using Google.Maps.Utility;
using UIKit;

namespace Xamarin.Forms.GoogleMaps.Clustering.iOS
{
    public class ClusteredMarker : ClusterItem
    {
        public override CLLocationCoordinate2D Position => MarkerPosition;
        public string Title { get; set; }
        public string Snippet { get; set; }
        public bool Draggable { get; set; }
        public double Rotation { get; set; }
        public CGPoint GroundAnchor { get; set; }
        public CGPoint InfoWindowAnchor { get; set; }
        public bool Flat { get; set; }
        public float Opacity { get; set; }
        public UIImage Icon { get; set; }
        public int ZIndex { get; set; }
        public CLLocationCoordinate2D MarkerPosition { get; set; }
        public MapView Map { get;  set; }

        public Marker ConvertToMarket()
        {
            return new Marker
            {
                Title = Title,
                Draggable = Draggable,
                Snippet = Snippet,
                Rotation = Rotation,
                GroundAnchor = GroundAnchor,
                InfoWindowAnchor = InfoWindowAnchor,
                Flat = Flat,
                Opacity = Opacity,
                Icon = Icon,
                ZIndex = ZIndex, 
                Position = MarkerPosition,
                Map = Map
            };
        }
    }
}