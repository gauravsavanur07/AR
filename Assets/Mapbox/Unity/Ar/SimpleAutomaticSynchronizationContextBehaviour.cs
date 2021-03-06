namespace Mapbox.Unity.Ar
{
	using Mapbox.Unity.Map;
	using Mapbox.Unity.Location;
	using UnityARInterface;
	using UnityEngine;
	using Mapbox.Unity.Utilities;
	using System;

	public class SimpleAutomaticSynchronizationContextBehaviour : MonoBehaviour, ISynchronizationContext
	{
		[SerializeField]
		Transform _arPositionReference;

		[SerializeField]
		AbstractMap _map;

		[SerializeField]
		bool _useAutomaticSynchronizationBias;

		[SerializeField]
		AbstractAlignmentStrategy _alignmentStrategy;

		[SerializeField]
		float _synchronizationBias = 1f;

		[SerializeField]
		float _arTrustRange = 10f;

		[SerializeField]
		float _minimumDeltaDistance = 2f;

		[SerializeField]
		float _minimumDesiredAccuracy = 5f;

		SimpleAutomaticSynchronizationContext _synchronizationContext;

		float _lastHeading;
		float _lastHeight;

		ILocationProvider _locationProvider;

		public event Action<Alignment> OnAlignmentAvailable = delegate { };

		public ILocationProvider LocationProvider
		{
			private get
			{
				if (_locationProvider == null)
				{
#if UNITY_EDITOR
					_locationProvider = LocationProviderFactory.Instance.TransformLocationProvider;
#else
					_locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
#endif
				}

				return _locationProvider;
			}
			set
			{
				if (_locationProvider != null)
				{
					_locationProvider.OnLocationUpdated -= LocationProvider_OnLocationUpdated;

				}
				_locationProvider = value;
				_locationProvider.OnLocationUpdated += LocationProvider_OnLocationUpdated;
			}
		}

		void Start()
		{
			_alignmentStrategy.Register(this);
			_synchronizationContext = new SimpleAutomaticSynchronizationContext();
			_synchronizationContext.MinimumDeltaDistance = _minimumDeltaDistance;
			_synchronizationContext.ArTrustRange = _arTrustRange;
			_synchronizationContext.UseAutomaticSynchronizationBias = _useAutomaticSynchronizationBias;
			_synchronizationContext.SynchronizationBias = _synchronizationBias;
			_synchronizationContext.OnAlignmentAvailable += SynchronizationContext_OnAlignmentAvailable;
			_map.OnInitialized += Map_OnInitialized;


			// TODO: not available in ARInterface yet?!
			//UnityARSessionNativeInterface.ARSessionTrackingChangedEvent += UnityARSessionNativeInterface_ARSessionTrackingChanged;
			ARInterface.planeAdded += PlaneAddedHandler;
		}

		void OnDestroy()
		{
			_alignmentStrategy.Unregister(this);
			LocationProvider.OnLocationUpdated -= LocationProvider_OnLocationUpdated;
			ARInterface.planeAdded -= PlaneAddedHandler;
		}

		void Map_OnInitialized()
		{
			_map.OnInitialized -= Map_OnInitialized;

			// We don't want location updates until we have a map, otherwise our conversion will fail.
			LocationProvider.OnLocationUpdated += LocationProvider_OnLocationUpdated;
		}

		void PlaneAddedHandler(BoundedPlane plane)
		{
			_lastHeight = plane.center.y;
			Unity.Utilities.Console.Instance.Log(string.Format("AR Plane Height: {0}", _lastHeight), "yellow");
		}

		//void UnityARSessionNativeInterface_ARSessionTrackingChanged(UnityEngine.XR.iOS.UnityARCamera camera)
		//{
		//	Unity.Utilities.Console.Instance.Log(string.Format("AR Tracking State Changed: {0}: {1}", camera.trackingState, camera.trackingReason), "silver");
		//}

		void LocationProvider_OnLocationUpdated(Location location)
		{
			if (location.IsLocationUpdated)
			{
				if (location.Accuracy > _minimumDesiredAccuracy) //With this line, we can control accuracy of Gps updates. 
				{
					Unity.Utilities.Console.Instance.Log("Gps update ignored due to bad accuracy", "red");
				}
				else
				{
					var latitudeLongitude = location.LatitudeLongitude;
					Unity.Utilities.Console.Instance.Log(
						string.Format(
							"Location: {0},{1}\tAccuracy: {2}\tHeading: {3}"
							, latitudeLongitude.x
							, latitudeLongitude.y
							, location.Accuracy, location.Heading
						)
						, "lightblue"
					);

					var position = Conversions.GeoToWorldPosition(latitudeLongitude, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz();
					_synchronizationContext.AddSynchronizationNodes(location, position, _arPositionReference.localPosition);
				}


			}

		}

		void SynchronizationContext_OnAlignmentAvailable(Ar.Alignment alignment)
		{
			var position = alignment.Position;
			position.y = _lastHeight;
			alignment.Position = position;
			OnAlignmentAvailable(alignment);
		}
	}
}
