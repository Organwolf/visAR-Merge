using ARLocation;
using Assets.Scripts.GPStoWorldTransformation;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ARTransformationManager : MonoBehaviour
{
    public static ARTransformationManager Instance;
    // https://community.arm.com/developer/tools-software/graphics/b/blog/posts/indoor-real-time-navigation-with-slam-on-your-mobile
    public Text txtGPSSignalInfo;
    public Text txtUpdateInfo;
    public Text txtModelInfo;
    public GameObject[] toRelocate;


    private ARLocationProvider locationProvider;
    private Transform arLocationRoot;
    private ARLocationManager arLocationManager;
    private Transform mainCameraTransform;
    private GpsToWorldTransformation GpsToWorld = new MultivariateLinearRegressionOnXYZTransformation();

    public bool TransformationAvailable => GpsToWorld.TransformationAvailable;

    public int Percentage => GpsToWorld.Percentage;

    public void Awake()
    {
        Instance = this;
    }

    public void Restart()
    {
        GpsToWorld.Restart();
    }

    private TransformationRecord m_prevARPosePosition;
    // Start is called before the first frame update
    void Start()
    {
        locationProvider = ARLocationProvider.Instance;
        arLocationManager = ARLocationManager.Instance;
        arLocationRoot = arLocationManager.gameObject.transform;
        mainCameraTransform = arLocationManager.MainCamera.transform;
        locationProvider.OnLocationUpdatedEvent(locationUpdatedHandler);
        locationProvider.OnProviderRestartEvent(ProviderRestarted);
    }

    private void Update()
    {
        ShowUpdateInformation();
    }

    private void ShowUpdateInformation()
    {
        // Can comment out later on
        txtUpdateInfo.text = $"Frame Update \n" +
        $"Time: {DateTime.Now.ToString("HH:mm:ss")} \n" +
        $"Camera: {mainCameraTransform.position.x}, {mainCameraTransform.position.y}, {mainCameraTransform.position.z}";
    }

    public void Relocate()
    {
        GameObject[] gos = GameObject.FindGameObjectsWithTag("Relocate");
        foreach (GameObject go in gos)
        {
            if (go.GetComponent<PlaceAtLocation>() != null)
                go.GetComponent<PlaceAtLocation>().Restart();
        }
        //foreach (GameObject go in toRelocate)
        //{
            
        //    go.find
        //    //PlaceAtLocation[] children = go.GetComponentsInChildren<PlaceAtLocation>(true) as PlaceAtLocation[];
        //    for (int i = 0; i < go.transform.childCount; i++)
        //    {
        //        PlaceAtLocation pal = go.transform.GetChild(0).GetComponent<PlaceAtLocation>();
        //        if (pal != null) pal.Restart();
        //    }
        //    //foreach (PlaceAtLocation child in children)
        //    //    child.Restart();
        //}
    }

    #region GPS
    private void ProviderRestarted()
    {

    }

    private void locationUpdatedHandler(LocationReading currentLocation, LocationReading lastLocation)
    {
        UpdatePosition(currentLocation.ToLocation());
        GpsToWorld.SolveTransforamtion();
    }


    private void UpdatePosition(Location location)
    {
        ShowGPSSignalInformation(location, mainCameraTransform.position, DateTime.Now);
    }

    private void ShowGPSSignalInformation(Location gpsLocation, Vector3 arPosition, DateTime dateTime)
    {
        GpsToWorld.AddRecord(new TransformationRecord(gpsLocation, arPosition, dateTime));

        // Can comment out later on
        txtGPSSignalInfo.text = $"GPS Signal \n" +
            $"GPS Accuracy: {Math.Round(gpsLocation.Accuracy, 3)}\n" +
            $"GPS Pos: {Math.Round(gpsLocation.Longitude, 6)}, {Math.Round(gpsLocation.Latitude, 6)}, {Math.Round(gpsLocation.Altitude, 6)} \n" +
            $"AR Position: {Math.Round(arPosition.x, 3)}, {Math.Round(arPosition.y, 3)}, {Math.Round(arPosition.z, 3)} \n" +
            $"# Control Points: {GpsToWorld.NumberOfRecords}\n" +
            $"# Check Points: {GpsToWorld.NumberOfTestRecords}\n" +
            $"Time: {dateTime.ToString("HH:mm:ss")}";

        if (GpsToWorld.TransformationAvailable)
        {
            if (GpsToWorld.CalcualteError)
                txtModelInfo.text = $"Relative Error: {GpsToWorld.Error_Horizontal}\n" +
                    $"Number of test points: {GpsToWorld.NumberOfTestRecords}\n";

            int numOfPointsToTrans = 5;
            List<Location> gpsPositions = new List<Location>();
            gpsPositions.Add(new Location(55.70854, 13.200682));
            gpsPositions.Add(new Location(55.708434, 13.200575));
            gpsPositions.Add(new Location(55.708361, 13.200776));
            gpsPositions.Add(new Location(55.708292, 13.201016));
            gpsPositions.Add(gpsLocation);


            List<Vector3> calArPositions = GpsToWorld.TransformGpsToWorld(gpsPositions);
            string results = "";

            for (int i = 0; i < calArPositions.Count; i++)
            {
                results += $"Point number {i} - Distance to camera: {Math.Round(Location.HorizontalDistance(gpsLocation, gpsPositions[i]), 2)}\n" +
                    $"\tAR {Math.Round(calArPositions[i].x, 6)}, {Math.Round(calArPositions[i].y, 6)}, {Math.Round(calArPositions[i].z, 6)}\n" +
                    $"\tGPS {Math.Round(gpsPositions[i].Longitude, 6)}, {Math.Round(gpsPositions[i].Latitude, 6)}, {Math.Round(gpsPositions[i].Altitude, 6)}\n";
            }
            txtGPSSignalInfo.text += $"Transformation \n" +
                //$"Error: {Math.Round(error, 3)}\n" +
                //$"R2 for x: {Math.Round(r2[0], 3)} and y: {Math.Round(r2[1], 3)}" +
                $"Results for the {numOfPointsToTrans} points:\n" +
                $"{results}";
        }
    }

    internal Vector3 GpsToArWorld(Location location)
    {
        return GpsToWorld.TransformGpsToWorld(location);
    }


    #endregion GPS
}
