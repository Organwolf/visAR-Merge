using ARLocation;
using ARLocation.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("AR+GPS/Place At Location")]
[HelpURL("https://http://docs.unity-ar-gps-location.com/guide/#placeatlocation")]
[DisallowMultipleComponent]
public class PlaceAtLocation : MonoBehaviour
{
    [Serializable]
    public class StateData
    {
        public Location Location;
        public uint LocationUpdatedCount;
        public uint PositionUpdatedCount;
        public bool Paused;
    }

    public ARLocation.PlaceAtLocation.LocationSettingsData LocationOptions = new ARLocation.PlaceAtLocation.LocationSettingsData();

    [Space(4.0f)] public ARLocation.PlaceAtLocation.PlaceAtOptions PlacementOptions = new ARLocation.PlaceAtLocation.PlaceAtOptions();

    [Space(4.0f)]

    [Header("Debug")]
    [Tooltip("When debug mode is enabled, this component will print relevant messages to the console. Filter by 'PlateAtLocation' in the log output to see the messages.")]
    public bool DebugMode;

    [Space(4.0f)]

    [Header("Events")]
    [Space(4.0f)]
    [Tooltip(
        "Event called when the object's location is updated. The arguments are the current GameObject, the location, and the number of location updates received " +
        "by the object so far.")]
    public ARLocation.PlaceAtLocation.ObjectUpdatedEvent ObjectLocationUpdated;

    [Tooltip(
        "Event called when the object's position is updated after a location update. " +
        "If the Movement Smoothing is larger than 0, this will fire at a later time than the Location Updated event.  The arguments are the current GameObject, the location, and the number of position updates received " +
        "by the object so far.")]
    public ARLocation.PlaceAtLocation.ObjectUpdatedEvent ObjectPositionUpdated;


    public Location Location
    {
        get => state.Location;

        set
        {
            if (!hasInitialized)
            {
                LocationOptions.LocationInput.LocationInputType =
                    LocationPropertyData.LocationPropertyType.Location;

                LocationOptions.LocationInput.LocationData = null;
                LocationOptions.LocationInput.Location = value.Clone();

                return;
            }

            if (groundHeight != null)
            {
                groundHeight.Settings.Altitude = (float)value.Altitude;
            }

            state.Location = value.Clone();
            UpdatePosition(locationProvider.CurrentLocation.ToLocation());
        }
    }

    public float SceneDistance
    {
        get
        {
            var cameraPos = mainCameraTransform.position;

            return Vector3.Distance(cameraPos, transform.position);
        }
    }

    public double RawGpsDistance =>
        Location.HorizontalDistance(locationProvider.Provider.CurrentLocationRaw.ToLocation(),
            state.Location);

    public bool Paused
    {
        get => state.Paused;
        set => state.Paused = value;
    }

    public bool UseGroundHeight => state.Location.AltitudeMode == AltitudeMode.GroundRelative;

    private StateData state = new StateData();

    private ARLocationProvider locationProvider;
    private Transform arLocationRoot;
    private SmoothMove smoothMove;
    private MovingAveragePosition movingAverageFilter;
    private GameObject debugPanel;
    private ARLocationManager arLocationManager;
    private Transform mainCameraTransform;
    private bool hasInitialized;
    private GroundHeight groundHeight;
    private ARTransformationManager arTransformationManager;

    // Use this for initialization
    void Start()
    {
        locationProvider = ARLocationProvider.Instance;
        arLocationManager = ARLocationManager.Instance;
        arLocationRoot = arLocationManager.gameObject.transform;
        mainCameraTransform = arLocationManager.MainCamera.transform;
        arTransformationManager = ARTransformationManager.Instance;

        if (locationProvider == null)
        {
            Debug.LogError("[AR+GPS][PlaceAtLocation]: LocationProvider GameObject or Component not found.");
            return;
        }

        Initialize();

        hasInitialized = true;
    }

    public void Restart()
    {
        state = new StateData();
        Initialize();

        if (locationProvider.IsEnabled)
        {
            locationUpdatedHandler(locationProvider.CurrentLocation, locationProvider.LastLocation);
        }
    }

    void Initialize()
    {
        state.Location = LocationOptions.GetLocation();

        Transform transform1;
        (transform1 = transform).SetParent(arLocationRoot.transform);
        transform1.localPosition = Vector3.zero;

        if (!hasInitialized)
        {
            if (PlacementOptions.HideObjectUntilItIsPlaced)
            {
                Misc.HideGameObject(gameObject);
            }

            if (PlacementOptions.MovementSmoothing > 0)
            {
                smoothMove = SmoothMove.AddSmoothMove(gameObject, PlacementOptions.MovementSmoothing);
            }

            if (UseGroundHeight)
            {
                groundHeight = gameObject.AddComponent<GroundHeight>();
                groundHeight.Settings.Altitude = (float)state.Location.Altitude;
            }

            if (PlacementOptions.UseMovingAverage)
            {
                movingAverageFilter = new MovingAveragePosition
                {
                    aMax = locationProvider.Provider.Options.AccuracyRadius > 0
                        ? locationProvider.Provider.Options.AccuracyRadius
                        : 20
                };
            }

            locationProvider.OnLocationUpdatedEvent(locationUpdatedHandler);
            locationProvider.OnProviderRestartEvent(ProviderRestarted);
        }

    }

    private void ProviderRestarted()
    {
        state.LocationUpdatedCount = 0;
        state.PositionUpdatedCount = 0;
    }

    private void locationUpdatedHandler(LocationReading currentLocation, LocationReading lastLocation)
    {
        UpdatePosition(currentLocation.ToLocation());
    }

    public void UpdatePosition(Location deviceLocation)
    {
        if (!arTransformationManager.TransformationAvailable)
            return;

        if (state.Paused)
            return;


        Vector3 targetPosition = new Vector3();
        var location = state.Location;
        var useSmoothMove = smoothMove != null;
        var isHeightRelative = location.AltitudeMode == AltitudeMode.DeviceRelative;
        // If we have reached the max number of location updates, do nothing
        if (PlacementOptions.MaxNumberOfLocationUpdates > 0 &&
            state.LocationUpdatedCount >= PlacementOptions.MaxNumberOfLocationUpdates)
        {
            return;
        }

        // Calculate the target position where the object will be placed next
        if (movingAverageFilter != null)
        {
            //var position = Location.GetGameObjectPositionForLocation(
            //    arLocationRoot, mainCameraTransform, deviceLocation, location, isHeightRelative
            //);

            Vector3 position = arTransformationManager.GpsToArWorld(location);

            var accuracy = locationProvider.CurrentLocation.accuracy;

            movingAverageFilter.AddEntry(new DVector3(position), accuracy);

            targetPosition = movingAverageFilter.CalculateAveragePosition().toVector3();
        }
        else
        {
            //targetPosition = Location.GetGameObjectPositionForLocation(
            //    arLocationRoot, mainCameraTransform, deviceLocation, location, isHeightRelative
            //);
            targetPosition = arTransformationManager.GpsToArWorld(location);
        }

        // If GroundHeight is enabled, don't change the objects position
        if (UseGroundHeight)
        {
            targetPosition.y = transform.position.y;
        }


        if (useSmoothMove && state.PositionUpdatedCount > 0)
        {
            smoothMove.Move(targetPosition, PositionUpdated);
        }
        else
        {
            transform.position = targetPosition;
            PositionUpdated();
        }

        state.LocationUpdatedCount++;
        ObjectLocationUpdated?.Invoke(gameObject, location, (int)state.LocationUpdatedCount);
    }

    private void PositionUpdated()
    {
        if (PlacementOptions.HideObjectUntilItIsPlaced && state.PositionUpdatedCount <= 0)
        {
            Misc.ShowGameObject(gameObject);
        }

        state.PositionUpdatedCount++;

        ObjectPositionUpdated?.Invoke(gameObject, state.Location, (int)state.PositionUpdatedCount);
    }

    public static GameObject CreatePlacedInstance(GameObject go, Location location, ARLocation.PlaceAtLocation.PlaceAtOptions options, bool useDebugMode = false)
    {
        var instance = Instantiate(go, ARLocationManager.Instance.gameObject.transform);

        AddPlaceAtComponent(instance, location, options, useDebugMode);

        return instance;
    }

    public static PlaceAtLocation AddPlaceAtComponent(GameObject go, Location location, ARLocation.PlaceAtLocation.PlaceAtOptions options,
        bool useDebugMode = false)
    {
        var placeAt = go.AddComponent<PlaceAtLocation>();

        placeAt.PlacementOptions = options;
        placeAt.LocationOptions.LocationInput.LocationInputType =
            LocationPropertyData.LocationPropertyType.Location;
        placeAt.LocationOptions.LocationInput.Location = location.Clone();
        placeAt.DebugMode = useDebugMode;

        return placeAt;
    }
}
