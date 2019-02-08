﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CoreGraphics;
using GMCluster;
using Google.Maps;
using UIKit;
using Xamarin.Forms.GoogleMaps.iOS.Extensions;
using Xamarin.Forms.GoogleMaps.iOS.Factories;
using Xamarin.Forms.GoogleMaps.Logics;

namespace Xamarin.Forms.GoogleMaps.Clustering.iOS
{
    internal partial class ClusterLogic : DefaultPinLogic<ClusteredMarker, MapView>
    {
        protected override IList<Pin> GetItems(Map map) => (map as ClusteredMap)?.ClusteredPins;

        private ClusteredMap ClusteredMap => (ClusteredMap) Map;

        private GMUClusterManager clusterManager;

        private bool onMarkerEvent;
        private Pin draggingPin;
        private volatile bool withoutUpdateNative = false;

        private readonly Action<Pin, Marker> onMarkerCreating;
        private readonly Action<Pin, Marker> onMarkerCreated;
        private readonly Action<Pin, Marker> onMarkerDeleting;
        private readonly Action<Pin, Marker> onMarkerDeleted;
        private readonly IImageFactory imageFactory;

        public ClusterLogic(
            IImageFactory imageFactory,
            Action<Pin, Marker> onMarkerCreating,
            Action<Pin, Marker> onMarkerCreated,
            Action<Pin, Marker> onMarkerDeleting,
            Action<Pin, Marker> onMarkerDeleted)
        {
            this.imageFactory = imageFactory;
            this.onMarkerCreating = onMarkerCreating;
            this.onMarkerCreated = onMarkerCreated;
            this.onMarkerDeleting = onMarkerDeleting;
            this.onMarkerDeleted = onMarkerDeleted;
        }

        internal override void Register(MapView oldNativeMap, Map oldMap, MapView newNativeMap, Map newMap)
        {
            base.Register(oldNativeMap, oldMap, newNativeMap, newMap);

            var clusteredNewMap = (ClusteredMap) newMap;
            IGMUClusterAlgorithm algorithm;
            switch (clusteredNewMap.ClusterOptions.Algorithm)
            {
                case ClusterAlgorithm.GridBased:
                    algorithm = new GMUGridBasedClusterAlgorithm();
                    break;
                case ClusterAlgorithm.VisibleNonHierarchicalDistanceBased:
                    throw new NotSupportedException("VisibleNonHierarchicalDistanceBased is only supported on Android");
                    break;
                default:
                    algorithm = new GMUNonHierarchicalDistanceBasedAlgorithm();
                    break;
            }

            var iconGenerator = new GmuClusterIconGeneratorHandler(ClusteredMap.ClusterOptions);

            var clusterRenderer = new GmuClusterRendererHandler(newNativeMap, iconGenerator);

            clusterManager = new GMUClusterManager(newNativeMap, algorithm, clusterRenderer);

            ClusteredMap.OnCluster = HandleClusterRequest;

            if (newNativeMap == null) return;
            newNativeMap.InfoTapped += OnInfoTapped;
            newNativeMap.InfoLongPressed += OnInfoLongPressed;
            newNativeMap.TappedMarker = HandleGmsTappedMarker;
            newNativeMap.InfoClosed += InfoWindowClosed;
            newNativeMap.DraggingMarkerStarted += DraggingMarkerStarted;
            newNativeMap.DraggingMarkerEnded += DraggingMarkerEnded;
            newNativeMap.DraggingMarker += DraggingMarker;
        }

        internal override void Unregister(MapView nativeMap, Map map)
        {
            if (nativeMap != null)
            {
                nativeMap.DraggingMarker -= DraggingMarker;
                nativeMap.DraggingMarkerEnded -= DraggingMarkerEnded;
                nativeMap.DraggingMarkerStarted -= DraggingMarkerStarted;
                nativeMap.InfoClosed -= InfoWindowClosed;
                nativeMap.TappedMarker = null;
                nativeMap.InfoTapped -= OnInfoTapped;
            }

            ClusteredMap.OnCluster = null;

            base.Unregister(nativeMap, map);
        }

        protected override ClusteredMarker CreateNativeItem(Pin outerItem)
        {
            var nativeMarker = new ClusteredMarker
            {
                Position = outerItem.Position.ToCoord(),
                Title = outerItem.Label,
                Snippet = outerItem.Address ?? string.Empty,
                Draggable = outerItem.IsDraggable,
                Rotation = outerItem.Rotation,
                GroundAnchor = new CGPoint(outerItem.Anchor.X, outerItem.Anchor.Y),
                Flat = outerItem.Flat,
                ZIndex = outerItem.ZIndex,
                Opacity = 1f - outerItem.Transparency
            };

            if (outerItem.Icon != null)
            {
                var factory = imageFactory ?? DefaultImageFactory.Instance;
                nativeMarker.Icon = factory.ToUIImage(outerItem.Icon);
            }

            onMarkerCreating(outerItem, nativeMarker);

            outerItem.NativeObject = nativeMarker;

            clusterManager.AddItem(nativeMarker);
            OnUpdateIconView(outerItem, nativeMarker);
            onMarkerCreated(outerItem, nativeMarker);

            return nativeMarker;
        }

        protected override ClusteredMarker DeleteNativeItem(Pin outerItem)
        {
            if (outerItem?.NativeObject == null)
                return null;
            var nativeMarker = outerItem.NativeObject as ClusteredMarker;

            onMarkerDeleting(outerItem, nativeMarker);

            nativeMarker.Map = null;

            clusterManager.RemoveItem(nativeMarker);

            if (ReferenceEquals(Map.SelectedPin, outerItem))
                Map.SelectedPin = null;

            onMarkerDeleted(outerItem, nativeMarker);

            return nativeMarker;
        }

        internal override void OnMapPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == Map.SelectedPinProperty.PropertyName)
            {
                if (!onMarkerEvent)
                    UpdateSelectedPin(Map.SelectedPin);
                Map.SendSelectedPinChanged(Map.SelectedPin);
            }
        }

        private void UpdateSelectedPin(Pin pin)
        {
            if (pin != null)
                NativeMap.SelectedMarker = (ClusteredMarker)pin.NativeObject;
            else
                NativeMap.SelectedMarker = null;
        }

        private Pin LookupPin(Marker marker)
        {
            var associatedClusteredMarker = marker.UserData;
            return GetItems(Map).FirstOrDefault(outerItem => ReferenceEquals(outerItem.NativeObject, associatedClusteredMarker));
        }

        private void HandleClusterRequest()
        {
            clusterManager.Cluster();
        }

        private void OnInfoTapped(object sender, GMSMarkerEventEventArgs e)
        {
            var targetPin = LookupPin(e.Marker);

            targetPin?.SendTap();

            if (targetPin != null)
            {
                Map.SendInfoWindowClicked(targetPin);
            }
        }

        private void OnInfoLongPressed(object sender, GMSMarkerEventEventArgs e)
        {
            var targetPin = LookupPin(e.Marker);
            
            if (targetPin != null)
                Map.SendInfoWindowLongClicked(targetPin);
        }

        private bool HandleGmsTappedMarker(MapView mapView, Marker marker)
        {
            var targetPin = LookupPin(marker);

            if (Map.SendPinClicked(targetPin))
                return true;

            try
            {
                onMarkerEvent = true;
                if (targetPin != null && !ReferenceEquals(targetPin, Map.SelectedPin))
                    Map.SelectedPin = targetPin;
            }
            finally
            {
                onMarkerEvent = false;
            }

            return false;
        }

        private void InfoWindowClosed(object sender, GMSMarkerEventEventArgs e)
        {
            var targetPin = LookupPin(e.Marker);

            try
            {
                onMarkerEvent = true;
                if (targetPin != null && ReferenceEquals(targetPin, Map.SelectedPin))
                    Map.SelectedPin = null;
            }
            finally
            {
                onMarkerEvent = false;
            }
        }

        private void DraggingMarkerStarted(object sender, GMSMarkerEventEventArgs e)
        {
            draggingPin = LookupPin(e.Marker);

            if (draggingPin != null)
            {
                UpdatePositionWithoutMove(draggingPin, e.Marker.Position.ToPosition());
                Map.SendPinDragStart(draggingPin);
            }
        }

        private void DraggingMarkerEnded(object sender, GMSMarkerEventEventArgs e)
        {
            if (draggingPin != null)
            {
                UpdatePositionWithoutMove(draggingPin, e.Marker.Position.ToPosition());
                Map.SendPinDragEnd(draggingPin);
                draggingPin = null;
            }
        }

        private void DraggingMarker(object sender, GMSMarkerEventEventArgs e)
        {
            if (draggingPin != null)
            {
                UpdatePositionWithoutMove(draggingPin, e.Marker.Position.ToPosition());
                Map.SendPinDragging(draggingPin);
            }
        }

        private void UpdatePositionWithoutMove(Pin pin, Position position)
        {
            try
            {
                withoutUpdateNative = true;
                pin.Position = position;
            }
            finally
            {
                withoutUpdateNative = false;
            }
        }

        protected override void OnUpdateAddress(Pin outerItem, ClusteredMarker nativeItem)
            => nativeItem.Snippet = outerItem.Address;

        protected override void OnUpdateLabel(Pin outerItem, ClusteredMarker nativeItem)
            => nativeItem.Title = outerItem.Label;

        protected override void OnUpdatePosition(Pin outerItem, ClusteredMarker nativeItem)
        {
            if (!withoutUpdateNative)
            {
                nativeItem.Position = outerItem.Position.ToCoord();
            }
        }

        protected override void OnUpdateType(Pin outerItem, ClusteredMarker nativeItem)
        {
        }

        protected override void OnUpdateIcon(Pin outerItem, ClusteredMarker nativeItem)
        {
            if (outerItem.Icon.Type == BitmapDescriptorType.View)
            {
                OnUpdateIconView(outerItem, nativeItem);
            }
            else
            {
                if (nativeItem?.Icon != null) 
                    nativeItem.Icon = DefaultImageFactory.Instance.ToUIImage(outerItem.Icon);
            }
        }

        protected override void OnUpdateIsDraggable(Pin outerItem, ClusteredMarker nativeItem)
        {
            nativeItem.Draggable = outerItem?.IsDraggable ?? false;
        }

        protected void OnUpdateIconView(Pin outerItem, ClusteredMarker nativeItem)
        {
            if (outerItem?.Icon?.Type == BitmapDescriptorType.View && outerItem?.Icon?.View != null)
            {
                NativeMap.InvokeOnMainThread(() =>
                {
                    var iconView = outerItem.Icon.View;
                    var nativeView = Utils.ConvertFormsToNative(iconView, new CGRect(0, 0, iconView.WidthRequest, iconView.HeightRequest));
                    nativeView.BackgroundColor = UIColor.Clear;
                    nativeItem.GroundAnchor = new CGPoint(iconView.AnchorX, iconView.AnchorY);
                    nativeItem.Icon = Utils.ConvertViewToImage(nativeView);
                });
            }
        }

        protected override void OnUpdateRotation(Pin outerItem, ClusteredMarker nativeItem)
        {
            nativeItem.Rotation = outerItem?.Rotation ?? 0f;
        }

        protected override void OnUpdateIsVisible(Pin outerItem, ClusteredMarker nativeItem)
        {
            if (outerItem?.IsVisible ?? false)
            {
                nativeItem.Map = NativeMap;
            }
            else
            {
                nativeItem.Map = null;
                if (ReferenceEquals(Map.SelectedPin, outerItem))
                    Map.SelectedPin = null;
            }
        }

        protected override void OnUpdateAnchor(Pin outerItem, ClusteredMarker nativeItem)
        {
            nativeItem.GroundAnchor = new CGPoint(outerItem.Anchor.X, outerItem.Anchor.Y);
        }

        protected override void OnUpdateFlat(Pin outerItem, ClusteredMarker nativeItem)
        {
            nativeItem.Flat = outerItem.Flat;
        }

        protected override void OnUpdateInfoWindowAnchor(Pin outerItem, ClusteredMarker nativeItem)
        {
            nativeItem.InfoWindowAnchor = new CGPoint(outerItem.Anchor.X, outerItem.Anchor.Y);
        }

        protected override void OnUpdateZIndex(Pin outerItem, ClusteredMarker nativeItem)
        {
            nativeItem.ZIndex = outerItem.ZIndex;
        }

        protected override void OnUpdateTransparency(Pin outerItem, ClusteredMarker nativeItem)
        {
            nativeItem.Opacity = 1f - outerItem.Transparency;
        }
    }
}