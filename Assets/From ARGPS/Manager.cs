using ARLocation;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class Manager : MonoBehaviour
{
    [SerializeField] string pathToCSV;
    [SerializeField] double radius = 20.0;
    [SerializeField] Slider exaggerateHeightSlider;
    [SerializeField] Text togglePlacementText;
    [SerializeField] Button generateMeshButton;
    [SerializeField] Canvas informationCanvas;
    [SerializeField] Canvas settingsCanvas;
    [SerializeField] InputField boundsInput;
    [SerializeField] Camera aRCamera;
    [SerializeField] int bounds = 0;
    [SerializeField] Text[] informationTexts;

    private Location deviceLocation;
    private Location closestPoint;
    private WaterMesh waterMesh;
    private DelaunayMesh delaunayMesh;
    private WallPlacement wallPlacement;
    private List<Location> withinRadiusData;
    private List<Location> entireCSVData;
    private float offset = 0f;
    private bool meshGenerated = false;
    private Vector3 lastScannedPosition = Vector3.zero;
    private ARTransformationManager aRTransformationManager;
    private ARLocationProvider aRLocationProvider;
    private bool calibratedGPS = false;

    private void Awake()
    {
        waterMesh = GetComponent<WaterMesh>();
        delaunayMesh = GetComponent<DelaunayMesh>();
        wallPlacement = GetComponent<WallPlacement>();
        entireCSVData = CSV_extended.ParseCsvFileUsingResources(pathToCSV);
        generateMeshButton.interactable = false;
        informationCanvas.enabled = false;
        settingsCanvas.enabled = false;
        aRTransformationManager = ARTransformationManager.Instance;
        aRLocationProvider = ARLocationProvider.Instance;
        DisableInformationText();
    }

    private void Start()
    {
        // Toast instruction
        SSTools.ShowMessage("Walk around", SSTools.Position.top, SSTools.Time.threeSecond);

        if(PlayerPrefs.HasKey("Bounds"))
        {
            bounds = PlayerPrefs.GetInt("Bounds");
        }
    }

    private void Update()
    {
        // TODO
        // have to add a check for if the groundplane is set or not

        if (aRTransformationManager.TransformationAvailable && !calibratedGPS)
        {
            calibratedGPS = true;

            // DEBUG
            SSTools.ShowMessage("GPS Calibrated", SSTools.Position.top, SSTools.Time.threeSecond);

            // get device location
            var deviceLocation = aRLocationProvider.LastLocation;

            // get the points around the user
            withinRadiusData = CSV_extended.PointsWithinRadius(entireCSVData, radius, deviceLocation.ToLocation());

            // convert the relevant points from longitude and latitude to Unity AR space
            waterMesh.SetPositionsToHandleLocations(withinRadiusData);

            // get the closest point
            closestPoint = CSV_extended.ClosestPointGPS(withinRadiusData, deviceLocation.ToLocation());

            // DEBUG
            SSTools.ShowMessage("Points Loaded", SSTools.Position.bottom, SSTools.Time.threeSecond);

            generateMeshButton.interactable = true;
            
            // Further down the line I would like to generate the mesh directly
            // GenerateMesh();

        }
    }

    private void OnApplicationPause(bool pause)
    {
        // reset playerpreffs
        PlayerPrefs.DeleteKey("Bounds");
    }

    // CODE for out of bounds logic - should not be triggered before the calibration of the GPS is finished

    //private UnityEngine.Coroutine updateEachSecond;

    //private void OnEnable()
    //{
    //    updateEachSecond = StartCoroutine(OutOfBoundsCheck());
    //}

    //private void OnDisable()
    //{
    //    StopCoroutine(updateEachSecond);
    //    updateEachSecond = null;
    //}

    //private IEnumerator OutOfBoundsCheck()
    //{
    //    // Run the coroutine every 2 seconds
    //    var wait = new WaitForSecondsRealtime(2.0f);

    //    while (true)
    //    {
    //        //Debug.Log("New bounds: " + bounds);
    //        var distance = Vector3.Distance(aRCamera.transform.position, lastScannedPosition);

    //        if(distance > bounds)
    //        {
    //            SceneManager.LoadScene("MainScene");                
    //        }
    //        yield return wait;
    //    }
    //}

    // Currently not used - could be called from OutOfBoundsCheck
    private void PlayerOutOfBounds()
    {
        lastScannedPosition = aRCamera.transform.position;
        SSTools.ShowMessage("Out of bounds. Re-scan ground", SSTools.Position.top, SSTools.Time.twoSecond);
        delaunayMesh.ClearMesh();
        waterMesh.Restart();
        new WaitForSecondsRealtime(2f);
        wallPlacement.ResetScanning();
    }
    
    /*
    public void OnLocationProviderEnabled(LocationReading reading)
    {
        // this should be triggered once the 30 points are found and calculated

        deviceLocation = reading.ToLocation();
        InitializeWaterMesh();
        closestPoint = CSV_extended.ClosestPointGPS(withinRadiusData, deviceLocation);
    }

    public void OnLocationUpdated(LocationReading reading)
    {
        if (!aRTransformationManager.TransformationAvailable)
            return;
        
        // new transformed location
        var unityLoc = aRTransformationManager.GpsToArWorld(reading.ToLocation());
        // change this to artransformation manager
        deviceLocation = reading.ToLocation();

        if(!meshGenerated && wallPlacement.IsGroundPlaneSet())
        {
            generateMeshButton.interactable = true;
            meshGenerated = true;

            var stateData = waterMesh.GetLocationsStateData();
            var globalLocalPositions = stateData.GetGlobalLocalPosition();
            wallPlacement.SetCurrentGlobalLocalPositions(globalLocalPositions);
            wallPlacement.SetPointsWithinRadius(withinRadiusData);
            Debug.Log($"Size of points within radius: {withinRadiusData.Count}");
        }
    }

    */

    private void InitializeWaterMesh()
    {        
        withinRadiusData = CSV_extended.PointsWithinRadius(entireCSVData, radius, deviceLocation);
        waterMesh.SetPositionsToHandleLocations(withinRadiusData);
    }

    public void GenerateMesh()
    {
        // Toast instruction
        SSTools.ShowMessage("Place walls if needed", SSTools.Position.top, SSTools.Time.threeSecond);

        double currentWaterHeight = 0;
        var groundPlaneTransform = wallPlacement.GetGroundPlaneTransform();
        var stateData = waterMesh.GetLocationsStateData();
        var globalLocalPositions = stateData.GetGlobalLocalPosition();
        var points = new List<Vector3>();
        float heightAtCamera = (float)closestPoint.Height;

        foreach (var globalLocalPosition in globalLocalPositions)
        {
            float calculatedHeight = 0;
            float zPosition = globalLocalPosition.localLocation.z;
            float xPosition = globalLocalPosition.localLocation.x;
            Location location = globalLocalPosition.location;
            float height = (float)location.Height;
            float waterHeight = (float)location.WaterHeight;
            bool insideBuilding = location.Building;
            float nearestNeighborHeight = (float)location.NearestNeighborHeight;
            float nearestNeighborWater = (float)location.NearestNeighborWater;

            if (insideBuilding)
            {
                if (nearestNeighborHeight != -9999)
                {
                    calculatedHeight = CalculateRelativeHeight(heightAtCamera, nearestNeighborHeight, nearestNeighborWater);
                    currentWaterHeight = nearestNeighborWater;
                }
            }
            else
            {
                calculatedHeight = CalculateRelativeHeight(heightAtCamera, height, waterHeight);
                currentWaterHeight = waterHeight;
            }

            points.Add(new Vector3(xPosition, calculatedHeight, zPosition)); // Exaggerate height if needed
        }

        if (groundPlaneTransform != null)
        {
            delaunayMesh.Generate(points, groundPlaneTransform);
        }
        else
        {
            delaunayMesh.Generate(points, transform);
        }

        // Enable wall placement and update current water height at closest point
        wallPlacement.SetWallPlacementEnabled(true);
        wallPlacement.WaterMeshGenerated(true);
        wallPlacement.SetCurrentWaterHeight(currentWaterHeight);
        
    }

    private float CalculateRelativeHeight(float heightAtCamera, float heightAtPoint, float waterHeightAtPoint)
    {
        float relativeHeight = heightAtPoint - heightAtCamera + waterHeightAtPoint + offset;
        //Debug.Log($"heightAtCamera {heightAtCamera} heightAtPoint {heightAtPoint} waterHeightAtPoint {waterHeightAtPoint}. Relative height {relativeHeight}");
        return relativeHeight;
    }

    #region UI

    private void DisableInformationText()
    {
        foreach(var text in informationTexts)
        {
            text.enabled = false;
        }
    }

    public void SettingsDone()
    {
        // Get the new bounds from settings panel
        int newBounds = int.Parse(boundsInput.text);
        try
        {
            PlayerPrefs.SetInt("Bounds", newBounds);
            bounds = newBounds;
        }
        catch (Exception)
        {
            Debug.Log("input field not set");
        }

        // Reset coroutine with new bounds
        //StopCoroutine(updateEachSecond);
        //updateEachSecond = null;
        //updateEachSecond = StartCoroutine(OutOfBoundsCheck());

        ToggleSettings();
    }

    public void ToggleSettings()
    {
        if(settingsCanvas.enabled)
        {
            settingsCanvas.enabled = false;
        }
        else
        {
            settingsCanvas.enabled = true;
        }
    }

    // Show/hide information panel
    public void ToggleInformation()
    {
        if(informationCanvas.enabled)
        {
            informationCanvas.enabled = false;

            if(!wallPlacement.IsGroundPlaneSet())
            {
                SSTools.ShowMessage("Scan the ground", SSTools.Position.top, SSTools.Time.threeSecond);
            }
        }
        else
        {
            informationCanvas.enabled = true;
        }

        DisableInformationText();
        informationTexts[0].enabled = true;

    }

    // Display next information
    public void NextText()
    {
        var index = 0;
        var length = informationTexts.Length;
        foreach (var text in informationTexts)
        {
            index++;
            if(text.enabled & index < length)
            { 
                text.enabled = false;
                break;
            }
        }
        if(index < length)
        {
            informationTexts[index].enabled = true;
        }
    }

    // Exaggerate height of water
    public void AlterHeightOfMesh()
    {
        var sliderValue = exaggerateHeightSlider.value;
        if (sliderValue > 0)
        {
            sliderValue += 0.5f;
            sliderValue *= 2f;
            var logHeight = Mathf.Log(sliderValue);
            delaunayMesh.SetHeightToMesh(logHeight);
        }
    }

    public void RenderWalls()
    {
        wallPlacement.RenderWalls();
    }

    public void RemovePreviouseWall()
    {
        wallPlacement.RemovePreviousWall();
    }

    public void ToggleWallPlacement()
    {
        wallPlacement.ToggleWallPlacement();
        if (wallPlacement.GetWallPlacementEnabled())
            togglePlacementText.text = "Place Measuring Stick";
        else
        {
            togglePlacementText.text = "Place Walls";
        }
    }

    public void ReLoadScene()
    {
        SceneManager.LoadScene("MainScene");
    }

    public void OpenSettings()
    {
        SceneManager.LoadScene("Settings");
    }

    #endregion
}