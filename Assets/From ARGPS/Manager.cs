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
    //private WaterMesh waterMesh;
    private DelaunayMesh delaunayMesh;
    private WallPlacement wallPlacement;
    //private List<Location> withinRadiusData;
    private List<Location> entireCSVData;
    private float offset = 0f;
    private bool meshGenerated = false;
    private Vector3 lastScannedPosition = Vector3.zero;
    private ARTransformationManager aRTransformationManager;
    private ARLocationProvider aRLocationProvider;
    private bool planeDetectionActivated = false;
    private bool scanningActivated = false;

    private void Awake()
    {
        //waterMesh = GetComponent<WaterMesh>();
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

    public void ActivateScanning()
    {
        scanningActivated = true;
    }

    private void Update()
    {
        if (aRTransformationManager.TransformationAvailable && !meshGenerated)
        {
            if(!planeDetectionActivated && scanningActivated)
            {
                // enable plane detection
                wallPlacement.TogglePlaneDetection();

                SSTools.ShowMessage("Scan the ground", SSTools.Position.top, SSTools.Time.threeSecond);
                SSTools.ShowMessage("And Select ground", SSTools.Position.bottom, SSTools.Time.threeSecond);

                planeDetectionActivated = true;
            }
            
            if(wallPlacement.IsGroundPlaneSet())
            {
                // get device location
                var deviceLocation = aRLocationProvider.LastLocation;

                // get the points around the user
                var locationsToCreateMeshWith = CSV_extended.PointsWithinRadius(entireCSVData, radius, deviceLocation.ToLocation());

                // get the closest point
                closestPoint = CSV_extended.ClosestPointGPS(locationsToCreateMeshWith, deviceLocation.ToLocation());
                float heightAtCamera = (float)closestPoint.Height;

                Debug.Log("Height at camera:" + heightAtCamera);

                // Convert GPS data to local data.
                var localPositionsToGenerate = new List<Vector3>();

                //float currentWaterHeight = 0;
                foreach (var gpsLocation in locationsToCreateMeshWith)
                {
                    var unityPosition = aRTransformationManager.GpsToArWorld(gpsLocation);

                    // Om detta behövs senare sätt in det i en egen funktion
                    float calculatedHeight = 0;

                    float height = (float)gpsLocation.Height;
                    float waterheight = (float)gpsLocation.WaterHeight;
                    bool insidebuilding = gpsLocation.Building;
                    float nearestneighborheight = (float)gpsLocation.NearestNeighborHeight;
                    float nearestneighborwater = (float)gpsLocation.NearestNeighborWater;

                    if (insidebuilding)
                    {
                        if (nearestneighborheight != -9999)
                        {
                            calculatedHeight = CalculateRelativeHeight(heightAtCamera, nearestneighborheight, nearestneighborwater);
                            //currentWaterHeight = nearestneighborwater;
                        }
                    }
                    else
                    {
                        calculatedHeight = CalculateRelativeHeight(heightAtCamera, height, waterheight);
                        //currentWaterHeight = waterheight;
                    }

                    Debug.Log("Calculated height: " + calculatedHeight);

                    unityPosition.y = calculatedHeight;
                    localPositionsToGenerate.Add(unityPosition);
                }
            
                meshGenerated = GenerateMesh(localPositionsToGenerate);

                if (meshGenerated)
                {
                    //EnableWallPlacementAndUpdateCurrentWater(currentWaterHeight);
                    //EnableWallPlacement();
                    wallPlacement.WaterMeshGenerated(true);
                    wallPlacement.ToggleWallPlacement();
                    Debug.Log("Wallplacement enabled");
                }
            }
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
        //waterMesh.Restart();
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

    //private void InitializeWaterMesh()
    //{        
    //    //withinRadiusData = CSV_extended.PointsWithinRadius(entireCSVData, radius, deviceLocation);
    //    //waterMesh.SetPositionsToHandleLocations(withinRadiusData);
    //}

    public bool GenerateMesh(List<Vector3> points)
    {
        // Toast instruction
        // SSTools.ShowMessage("Place walls if needed", SSTools.Position.bottom, SSTools.Time.threeSecond);

        var groundPlaneTransform = wallPlacement.GetGroundPlaneTransform();

        if (groundPlaneTransform != null)
        {
            delaunayMesh.Generate(points, groundPlaneTransform);
        }
        else
        {
            Debug.Log("Groundplane == null");
            delaunayMesh.Generate(points, transform);
        }

        //Returns true if mesh was generated successsfully. So far we will always say true but might change in future.
        return true;
    }

    //private void EnableWallPlacementAndUpdateCurrentWater(double currentWaterHeight)    
    private void EnableWallPlacement()
    {
        //wallPlacement.SetWallPlacementEnabled(true);
        //wallPlacement.WaterMeshGenerated(true);
        //wallPlacement.SetCurrentWaterHeight(currentWaterHeight);
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
        {
            togglePlacementText.text = "Place Measuring Stick";
        }
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