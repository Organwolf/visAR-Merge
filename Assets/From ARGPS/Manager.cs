﻿using ARLocation;
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
    [SerializeField] int bounds = 30;
    [SerializeField] Text[] informationTexts;
    [SerializeField] GameObject animatedArrowPrefab;

    // GUI
    [SerializeField] GameObject calibatedPannel;
    [SerializeField] GameObject notCalibatedPannel;
    [SerializeField] Text percentageToCalibrated;

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
    private float lastScannedHeight = 0f;
    private float heightAtCamera;
    private float timeSinceLastCalculation = 0f;
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
        generateMeshButton.interactable = true;
        informationCanvas.enabled = false;
        settingsCanvas.enabled = false;
        aRTransformationManager = ARTransformationManager.Instance;
        aRLocationProvider = ARLocationProvider.Instance;
        DisableInformationText();
    }

    private void Start()
    {
        // Instantiate the arrow prompt to walk around
        // I want to place the object in front of the user
        var arrowPrompt = Instantiate(animatedArrowPrefab);
        var arrowPosition = new Vector3(0, 0, 2);
        arrowPrompt.transform.position = arrowPosition;

        // Toast instruction
        SSTools.ShowMessage("Walk around", SSTools.Position.top, SSTools.Time.threeSecond);

        // If settings change the bounds apply those changes
        if(PlayerPrefs.HasKey("Bounds"))
        {
            bounds = PlayerPrefs.GetInt("Bounds");
        }
        SetCallibrated(false);
    }

    private void SetCallibrated(bool calibrated)
    {
        calibatedPannel.SetActive(calibrated);
        notCalibatedPannel.SetActive(!calibrated);
    }

    public void ActivateScanning()
    {
        scanningActivated = true;
    }

    private void FixedUpdate()
    {
        if (aRTransformationManager != null)
        {
            percentageToCalibrated.text = aRTransformationManager.Percentage.ToString() + " %";
            SetCallibrated(aRTransformationManager.TransformationAvailable);
        }
    }

    private void Update()
    {
        timeSinceLastCalculation += Time.deltaTime;
        /* If I set meshGenerated to false will I scan the ground again? */
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

            CalculateClosestPointAndGenerateMesh();

        }
    }

    private void CalculateClosestPointAndGenerateMesh()
    {
        if (wallPlacement.IsGroundPlaneSet())
        {
            // get device location
            var deviceLocation = aRLocationProvider.LastLocation;
            lastScannedPosition = aRCamera.transform.position;

            // get the points around the user
            var locationsToCreateMeshWith = CSV_extended.PointsWithinRadius(entireCSVData, radius, deviceLocation.ToLocation());

            // get the closest point
            closestPoint = CSV_extended.ClosestPointGPS(locationsToCreateMeshWith, deviceLocation.ToLocation());
            heightAtCamera = (float)closestPoint.Height;
            //lastScannedHeight = heightAtCamera;

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
                wallPlacement.SetWallPlacementEnabled(true);
                Debug.Log("Wallplacement enabled");
                // Send data to wallplacement class for measuring stick calculations
                wallPlacement.SetCurrentGlobalLocalPositions(locationsToCreateMeshWith, localPositionsToGenerate);
                timeSinceLastCalculation = 0;
            }
        }
    }

    // Reset playerpreffs
    private void OnApplicationPause(bool pause)
    {
        PlayerPrefs.DeleteKey("Bounds");
    }

    #region Coroutine

    // UnityEngine.Coroutine?
    private Coroutine checkIfMeshShouldReload;

    private void OnEnable()
    {
        checkIfMeshShouldReload = StartCoroutine(OutOfBoundsCheck());
    }

    private IEnumerator OutOfBoundsCheck()
    {
        // logic for the three different scenarios that should reload the mesh
        var wait = new WaitForSeconds(1f);

        // Regenerate mesh
        while(true)
        {
            // check if the user has moved 30m from where the app started
            var currentPosition = aRCamera.transform.position;
            var distance = Vector3.Distance(currentPosition, lastScannedPosition);

            if (distance > bounds)
            {
                lastScannedPosition = currentPosition;
                // Reload mesh at position
                CalculateClosestPointAndGenerateMesh();
                // --> Add code here
            }

            lastScannedHeight = heightAtCamera;
            // check if the user has moved 0.5m up or down from where the app started
            CalculateClosestPointAndGenerateMesh();

            float deltaHeight = lastScannedHeight - heightAtCamera;

            if(Mathf.Abs(deltaHeight) > 0.5f)
            {
                // Rescan ground
                // set meshGenerated to false
                // Re-calculate closest position and generate the mesh again
            }

            // check if the app has been running for 2min from when the app started
            if(timeSinceLastCalculation > 120)
            {
                // recalculate/regenerate mesh
                CalculateClosestPointAndGenerateMesh();
            }


        }

        yield return wait;
    }

    // Clean up by stopping the coroutine
    private void OnDisable()
    {
        StopCoroutine(checkIfMeshShouldReload);
        checkIfMeshShouldReload = null;
    }

    #endregion

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

    public void ClearMesh()
    {
        if(delaunayMesh.isMeshCreated())
        {
            delaunayMesh.ClearMesh();

        }
    }

    public void RegenerateMesh()
    {
        meshGenerated = false;
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