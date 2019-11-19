using ARLocation;
using ARLocation.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class WaterMesh : MonoBehaviour
{
    // Detta objektet är länkningen mellan location och Unity positionen
    public class GlobalLocalPosition
    {
        public Location location;       //Global
        public Vector3 localLocation;   //Local
        //public GameObject gameObject; // GO
        public LocationData locationData;

        public GlobalLocalPosition(Location gLocation, Vector3 lLocation)
        {
            location = gLocation;
            localLocation = lLocation;
        }
    }

    public class LocationsStateData
    {
        public List<GlobalLocalPosition> globalLocalPositions = new List<GlobalLocalPosition>();
        public uint LocationUpdatedCount;
        public uint PositionUpdatedCount;
        public bool Paused;

        public List<GlobalLocalPosition> GetGlobalLocalPosition()
        {
            return globalLocalPositions;
        }
    }

    #region Serialized fields

    [Serializable]
    public class PlaceAtOptions
    {
        [Tooltip(
             "The smoothing factor for movement due to GPS location adjustments; if set to zero it is disabled."),
         Range(0, 1)]
        public float MovementSmoothing = 0.05f;

        [Tooltip(
            "The maximum number of times this object will be affected by GPS location updates. Zero means no limits are imposed.")]
        public int MaxNumberOfLocationUpdates = 8;

        [Tooltip("If true, use a moving average filter.")]
        public bool UseMovingAverage;

        [Tooltip(
            "If true, the object will be hidden until the object is placed at the geolocation. If will enable/disable the MeshRenderer or SkinnedMeshRenderer " +
            "when available, and enable/disable all child game objects.")]
        public bool HideObjectUntilItIsPlaced = true;
    }

    [SerializeField]
    private string FileAddress;
    [FormerlySerializedAs("altitudeMode")]
    [Space(4)]

    [Tooltip("The altitude mode. 'Absolute' means absolute altitude, relative to the sea level. 'DeviceRelative' meas it is " +
            "relative to the device's initial position. 'GroundRelative' means relative to the nearest detected plane, and 'Ignore' means the " +
            "altitude is ignored (equivalent to setting it to zero).")]
    public AltitudeMode AltitudeMode = AltitudeMode.GroundRelative;
    [Space(4.0f)]

    [Header("Debug")]
    [Tooltip("When debug mode is enabled, this component will print relevant messages to the console. Filter by 'PlateAtLocation' in the log output to see the messages.")]
    public bool DebugMode;


    [Space(4.0f)] public PlaceAtOptions PlacementOptions = new PlaceAtOptions();

    #endregion Serialized fields

    public bool UseGroundHeight => AltitudeMode == AltitudeMode.GroundRelative;

    private LocationsStateData state = new LocationsStateData();
    private ARLocationProvider locationProvider;
    private Transform arLocationRoot;
    private List<SmoothMove> smoothMoves = new List<SmoothMove>();
    private MovingAveragePosition movingAverageFilter;
    private ARLocationManager arLocationManager;
    private Transform mainCameraTransform;
    private bool hasInitialized;
    private List<Location> csvWaterLocations;
    private ARTransformationManager arTransformationManager;



    public void Start()
    {
        locationProvider = ARLocationProvider.Instance;
        arLocationManager = ARLocationManager.Instance;
        arLocationRoot = arLocationManager.gameObject.transform;
        mainCameraTransform = arLocationManager.MainCamera.transform;
        locationProvider.OnLocationUpdatedEvent(locationUpdatedHandler);
        locationProvider.OnProviderRestartEvent(ProviderRestarted);
        arTransformationManager = ARTransformationManager.Instance;


        if (locationProvider == null)
        {
            Debug.LogError("[AR+GPS][PlaceAtLocation]: LocationProvider GameObject or Component not found.");
            return;
        }
    }

    public void Restart()
    {
        ARLocationManager.Instance.ResetARSession((() =>
        {
            Debug.Log("AR+GPS and AR Session were restarted!");
        }));

        //ARLocation.Utils.Logger.LogFromMethod("WaterMesh", "Restart", $"({gameObject.name}) - Restarting!", DebugMode);

        //state = new LocationsStateData();
        //hasInitialized = false;
        //ARLocationManager.Instance.Restart();

        //if (locationProvider.IsEnabled)
        //{
        //    locationUpdatedHandler(locationProvider.CurrentLocation, locationProvider.LastLocation);
        //}
    }

    public void SetPositionsToHandleLocations(List<Location> locationWithWaterHeight)
    {
        csvWaterLocations = locationWithWaterHeight;
    }

    public LocationsStateData GetLocationsStateData()
    {
        return state;
    }

    private void Initialize(Location deviceLocation)
    {
        try
        {
            foreach (Location loc in csvWaterLocations)
            {
                GlobalLocalPosition glp = new GlobalLocalPosition(loc, Vector3.zero);
                // comment out once we just render the mesh
                //GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                GameObject obj = new GameObject();
                //obj.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                obj.transform.SetParent(arLocationRoot.transform);
                obj.transform.localPosition = glp.localLocation;
                //glp.gameObject = obj; // GO
                // <---
                state.globalLocalPositions.Add(glp);
            }

            if (!hasInitialized)
            {
                if (PlacementOptions.HideObjectUntilItIsPlaced)
                {
                    //ToDo: Hide your mesh here!

                    //state.globalLocalPositions.ForEach(obj => Misc.HideGameObject(obj.gameObject)); // GO
                }

                if (PlacementOptions.MovementSmoothing > 0)
                {
                    //foreach (GlobalLocalPosition obj in state.globalLocalPositions)
                    //    smoothMoves.Add(SmoothMove.AddSmoothMove(obj.localLocation, PlacementOptions.MovementSmoothing));
                }

                //if (UseGroundHeight)
                //{
                //    groundHeight = gameObject.AddComponent<GroundHeight>();
                //    groundHeight.Settings.Altitude = (float)state.Altitude;
                //}

                if (PlacementOptions.UseMovingAverage)
                {
                    movingAverageFilter = new MovingAveragePosition
                    {
                        aMax = locationProvider.Provider.Options.AccuracyRadius > 0
                            ? locationProvider.Provider.Options.AccuracyRadius
                            : 20
                    };
                }
            }
        }
        catch (Exception ex)
        {
            ARLocation.Utils.Logger.LogFromMethod("WaterMesh", "Initialize", $"({ex.ToString()})", DebugMode);
        }
    }

    public void UpdatePosition(Location deviceLocation)
    {
        if (!hasInitialized)
        {
            Initialize(deviceLocation);
            hasInitialized = true;
            return;
        }

        //ARLocation.Utils.Logger.LogFromMethod("WaterMesh", "UpdatePosition", $"({gameObject.name}): Received location update, location = {deviceLocation}", DebugMode);

        if (state.Paused)
        {
            //ARLocation.Utils.Logger.LogFromMethod("WaterMesh", "UpdatePosition", $"({gameObject.name}): Updates are paused; returning", DebugMode);
            return;
        }

        // If we have reached the max number of location updates, do nothing
        if (PlacementOptions.MaxNumberOfLocationUpdates > 0 &&
            state.LocationUpdatedCount >= PlacementOptions.MaxNumberOfLocationUpdates)
        {
            return;
        }

        bool isHeightRelative = false;
        foreach (GlobalLocalPosition obj in state.globalLocalPositions)
        {
            //Vector3 targetPosition = Location.GetGameObjectPositionForLocation(
            //        arLocationRoot, mainCameraTransform, deviceLocation, obj.location, isHeightRelative
            //    );
            Vector3 targetPosition = arTransformationManager.GpsToArWorld(obj.location);


            // If GroundHeight is enabled, don't change the objects position
            if (UseGroundHeight)
            {
                targetPosition.y = transform.position.y;
            }

            // comment out once we just render the mesh
            //obj.gameObject.transform.position = targetPosition; // GO

            obj.localLocation = targetPosition;
        }
        PositionUpdated();
        state.LocationUpdatedCount++;
    }

    private void ProviderRestarted()
    {
        ARLocation.Utils.Logger.LogFromMethod("WaterMesh", "ProviderRestarted", $"({gameObject.name})", DebugMode);

        state.LocationUpdatedCount = 0;
        state.PositionUpdatedCount = 0;
    }

    private void locationUpdatedHandler(LocationReading currentLocation, LocationReading lastLocation)
    {
        //ARLocation.Utils.Logger.LogFromMethod("WaterMesh", "locationUpdatedHandler", $"({gameObject.name}): locationUpdatedHandler is called.");
        UpdatePosition(currentLocation.ToLocation());
    }


    private void PositionUpdated()
    {
        state.PositionUpdatedCount++;
        state.PositionUpdatedCount++;

        /* Sphere GameObject specifics */
        //if (PlacementOptions.HideObjectUntilItIsPlaced && state.PositionUpdatedCount <= 0)
        //{
        //    // ToDo: make the mesh visible here
        //    state.globalLocalPositions.ForEach(obj => Misc.ShowGameObject(obj.gameObject));
        //}
    }
}
