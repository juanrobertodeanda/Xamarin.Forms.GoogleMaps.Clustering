using Com.Google.Maps.Android.Clustering;
using System.Collections;
using System.Collections.Generic;

namespace Xamarin.Forms.GoogleMaps.Clustering.Android
{
    internal class ClusterLogicHandler : Java.Lang.Object,
        ClusterManager.IOnClusterClickListener,
        ClusterManager.IOnClusterItemClickListener,
        ClusterManager.IOnClusterInfoWindowClickListener,
        ClusterManager.IOnClusterItemInfoWindowClickListener
    {
        private ClusteredMap map;
        private ClusterManager clusterManager;
        private ClusterLogic logic;

        public ClusterLogicHandler(ClusteredMap map, ClusterManager manager, ClusterLogic logic)
        {
            this.map = map;
            clusterManager = manager;
            this.logic = logic;
        }

        public bool OnClusterClick(ICluster cluster)
        {
            var items = new List<Position>();
            foreach(var item in cluster.Items)
            {
                var a = item as ClusteredMarker;
                items.Add(new Position(a.Position.Latitude, a.Position.Longitude));
            }
            map.SendClusterClicked(items, cluster.Items.Count, new Position(cluster.Position.Latitude, cluster.Position.Longitude));
            return true;
        }

        public bool OnClusterItemClick(Java.Lang.Object nativeItemObj)
        {
            var targetPin = logic.LookupPin(nativeItemObj as ClusteredMarker);
           
            targetPin?.SendTap();

            if (targetPin != null)
            {
                if (!ReferenceEquals(targetPin, map.SelectedPin))
                    map.SelectedPin = targetPin;
                map.SendPinClicked(targetPin);
            }

            return false;
        }

        public void OnClusterInfoWindowClick(ICluster cluster)
        {

        }

        public void OnClusterItemInfoWindowClick(Java.Lang.Object nativeItemObj)
        {
            var targetPin = logic.LookupPin(nativeItemObj as ClusteredMarker);
           
            targetPin?.SendTap();

            if (targetPin != null)
            {
                map.SendInfoWindowClicked(targetPin);
            }
        }
    }
}