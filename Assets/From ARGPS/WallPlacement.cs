/**
 *  Clean-up for re-use in MainScene 17/10
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Events;
using ARLocation;
using System;

#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

public class WallPlacement : MonoBehaviour
{
    private class Wall
    {
        public GameObject startPoint;
        public GameObject endPoint;
        public GameObject quad;
        public GameObject line;

        public Wall(GameObject sPoint, GameObject ePoint, GameObject wall, GameObject lineRenderer)
        {
            startPoint = sPoint;
            endPoint = ePoint;
            quad = wall;
            line = lineRenderer;
        }
    }

    // Prefabs & materials
    [SerializeField] GameObject groundPlanePrefab;
    [SerializeField] GameObject clickPointPrefab;
    [SerializeField] GameObject measuringStickPrefab;
    [SerializeField] Material[] materialForWalls;

    // Line renderer
    [SerializeField] GameObject lineRendererPrefab;

    // AR
    [SerializeField] ARRaycastManager arRaycastManager;
    [SerializeField] ARPlaneManager arPlaneManager;
    [SerializeField] Camera arCamera;

    // startPoint & endPoint
    private GameObject startPoint;
    private GameObject endPoint;
    private LineRenderer measureLine;
    private GameObject measuringstick;

    // Plane, water & wall variables
    private List<Wall> walls = new List<Wall>();
    private bool planeIsPlaced;
    private float wallHeight = 4.0f;
    private GameObject groundPlane = null;
    private bool wallPlacementEnabled = false;
    private List<GameObject> listOfWallMeshes;
    private double currentWaterHeight = 0;
    private bool waterMeshRendered = false;
    private List<WaterMesh.GlobalLocalPosition> currentGlobalLocalPositions;
    private List<Location> dataWithinRadius;
    private textOverlay measuringStickTextOverlay;

    // Raycasts
    private List<ARRaycastHit> hitsAR = new List<ARRaycastHit>();
    private int groundLayerMask = 1 << 8;

    private void Awake()
    {
        // Walls
        listOfWallMeshes = new List<GameObject>();

        // startPoint & endPoint
        startPoint = Instantiate(clickPointPrefab, Vector3.zero, Quaternion.identity);
        endPoint = Instantiate(clickPointPrefab, Vector3.zero, Quaternion.identity);
        measuringstick = Instantiate(measuringStickPrefab, Vector3.zero, Quaternion.identity);
        measuringStickTextOverlay = measuringstick.GetComponent<textOverlay>();
        startPoint.SetActive(false);
        endPoint.SetActive(false);
        measuringstick.SetActive(false);
        measureLine = GetComponent<LineRenderer>();
        measureLine.enabled = false;
    }

    private void Start()
    {
#if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
        }
#endif
    }

    private void Update()
    {
        if (!EventSystem.current.IsPointerOverGameObject(0))
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);

                if (TouchPhase.Began == touch.phase)
                {
                    if (planeIsPlaced && waterMeshRendered) // < --solves the issue w wallplacement before mesh gen.
                    {
                        Ray ray = arCamera.ScreenPointToRay(touch.position);
                        RaycastHit hitInfo;

                        if (Physics.Raycast(ray, out hitInfo, groundLayerMask))
                        {
                            if (wallPlacementEnabled)
                            {
                                startPoint.transform.SetPositionAndRotation(hitInfo.point, Quaternion.identity);

                                if (walls.Count > 0)
                                {
                                    foreach (var obj in walls)
                                    {
                                        float dist = 0;
                                        dist = Vector3.Distance(obj.endPoint.transform.position, startPoint.transform.position);
                                        if (dist < 0.1)
                                        {
                                            startPoint.transform.position = obj.endPoint.transform.position;
                                        }

                                        dist = Vector3.Distance(obj.startPoint.transform.position, startPoint.transform.position);
                                        if (dist < 0.1)
                                        {
                                            startPoint.transform.position = obj.startPoint.transform.position;
                                        }
                                    }
                                }
                            }

                            // Placement of measuring stick
                            else
                            {
                                var cameraForward = arCamera.transform.forward;
                                var cameraBearing = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;
                                measuringstick.transform.SetPositionAndRotation(hitInfo.point, Quaternion.LookRotation(cameraBearing));
                                measuringstick.SetActive(true);
                                var waterHeightInCm = CalculateWaterHeightAtPosision(new Vector3(measuringstick.transform.position.x, 0, measuringstick.transform.position.z)) * 100;
                                AdjustFontSize();
                                measuringStickTextOverlay.SetText($"Water height: \n{waterHeightInCm.ToString("0.00")} cm");
                            }
                        }
                    }

                    else if (!planeIsPlaced)
                    {
                        if (arRaycastManager.Raycast(touch.position, hitsAR, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon))
                        {
                            Debug.Log("Placed the plane");
                            var hitPose = hitsAR[0].pose;
                            groundPlane = Instantiate(groundPlanePrefab, hitPose.position, hitPose.rotation);
                            planeIsPlaced = true;
                            TogglePlaneDetection();
                        }
                    }
                }

                else if (TouchPhase.Moved == touch.phase && wallPlacementEnabled)
                {
                    Ray ray = arCamera.ScreenPointToRay(touch.position);
                    RaycastHit hitInfo;

                    if (Physics.Raycast(ray, out hitInfo, groundLayerMask))
                    {
                        endPoint.SetActive(true);
                        endPoint.transform.SetPositionAndRotation(hitInfo.point, Quaternion.identity);
                    }

                    if (endPoint.activeSelf && !startPoint.activeSelf)
                    {
                        startPoint.SetActive(true);
                    }
                }

                else if (TouchPhase.Ended == touch.phase && wallPlacementEnabled && startPoint.activeSelf && endPoint.activeSelf)
                {
                    if (walls.Count > 0)
                    {
                        foreach (var obj in walls)
                        {
                            float dist = 0;
                            dist = Vector3.Distance(obj.endPoint.transform.position, endPoint.transform.position);
                            if (dist < 0.1)
                            {
                                endPoint.transform.position = obj.endPoint.transform.position;
                            }

                            dist = Vector3.Distance(obj.startPoint.transform.position, endPoint.transform.position);
                            if (dist < 0.1)
                            {
                                endPoint.transform.position = obj.startPoint.transform.position;
                            }
                        }
                    }

                    // De-activates objects/lines smaller than 20 cm
                    if (Vector3.Distance(startPoint.transform.position, endPoint.transform.position) < 0.2f)
                    {
                        startPoint.SetActive(false);
                        endPoint.SetActive(false);
                        measureLine.enabled = false;
                        return;
                    }

                    // Create the start and endpoint
                    var startPointObject = Instantiate(clickPointPrefab, startPoint.transform.position, Quaternion.identity);
                    var endPointObject = Instantiate(clickPointPrefab, endPoint.transform.position, Quaternion.identity);

                    // Disable temporary line renderer and create a new one
                    measureLine.enabled = false;
                    var lRenderer = DrawLineBetweenTwoPoints(startPoint, endPoint);

                    // Create a wall with the startpoint and endpoint as corner vertices
                    var wall = CreateQuadFromPoints(startPointObject.transform.position, endPointObject.transform.position);

                    // Then disable the startPoint and endPoint
                    startPoint.SetActive(false);
                    endPoint.SetActive(false);

                    // Instantiate a wall object with the current data and add it to a list
                    Wall currentWall = new Wall(startPointObject, endPointObject, wall, lRenderer);
                    walls.Add(currentWall);
                }

                else if (!endPoint.activeSelf && startPoint.activeSelf)
                {
                    startPoint.SetActive(false);
                }
            }
        }

        // Draws a line while placing the endpoint
        if (startPoint.activeSelf && endPoint.activeSelf)
        {
            measureLine.enabled = true;
            measureLine.SetPosition(0, startPoint.transform.position);
            measureLine.SetPosition(1, endPoint.transform.position);
        }
    }

    private void AdjustFontSize()
    {
        var fontSizeAtStart = 0.6f;
        var multiplier = 1f;
        var cameraPosition = arCamera.transform.position;
        var measuringstickPosition = measuringstick.transform.position;
        var currentDistance = Vector3.Distance(cameraPosition, measuringstickPosition);

        if (currentDistance > 1)
        {
            multiplier = Mathf.Log(currentDistance, 10) + 1;
        }

        var newFontSize = fontSizeAtStart * multiplier;
        measuringStickTextOverlay.SetFontSize(newFontSize);
        Debug.Log("Font size: " + newFontSize);
    }

    internal void SetPointsWithinRadius(List<Location> pointsWithinSetRadius)
    {
        dataWithinRadius = pointsWithinSetRadius;
    }

    public void SetCurrentGlobalLocalPositions(List<WaterMesh.GlobalLocalPosition> globalLocalPositions)
    {
        currentGlobalLocalPositions = globalLocalPositions;
        
        var lengthOfList = currentGlobalLocalPositions.Count;
        Debug.Log($"length of global local positions: {lengthOfList} ");
    }

    public double CalculateWaterHeightAtPosision(Vector3 stickPosition)
    {
        var minDistance = float.MaxValue;
        float distance;
        Location closestLocation = null;
        int length = currentGlobalLocalPositions.Count;
        int index = 0;

        // Separate function (?)
        for (int i= 0;  i < length; i++)
        {
            var locationData = currentGlobalLocalPositions[i];
            distance = Vector3.Distance(locationData.localLocation, stickPosition);

            if(distance < minDistance)
            {
                minDistance = distance;
                closestLocation = locationData.location;
                index = i;
            }
        }

        Location point = dataWithinRadius[index];

        // DEBUG STUFF
        Debug.Log($"Index: Long: {point.Longitude}  Lat: {point.Latitude}  Water height: {point.WaterHeight}");
        Debug.Log($"locationData:  Long: {closestLocation.Longitude}  Lat: {closestLocation.Latitude}  Water height: {closestLocation.WaterHeight}");

        if (point.Building)
        {
            return point.NearestNeighborWater;
        }
        else
        {
            return point.WaterHeight;
        }
    }

    public void ResetScanning()
    {
        planeIsPlaced = false;
        waterMeshRendered = false;
        groundPlane = null;
        measuringstick.SetActive(false);
        RemoveAllWalls();
        if(!arPlaneManager.enabled)
        {
            arPlaneManager.enabled = true;
        }
    }

    public void WaterMeshGenerated(bool state)
    {
        waterMeshRendered = state;
    }

    public void SetCurrentWaterHeight(double currentHeight)
    {
        currentWaterHeight = currentHeight;
    }

    public void RemovePreviousWall()
    {
        Debug.Log("Entered remove wall funciton");
        var length = walls.Count;
        if (length >= 1)
        {
            Wall wallToRemove = walls[length - 1];
            Destroy(wallToRemove.startPoint);
            Destroy(wallToRemove.endPoint);
            Destroy(wallToRemove.quad);
            Destroy(wallToRemove.line);
            walls.RemoveAt(length - 1);
            Debug.Log("Length of walls: " + walls.Count);
        }
    }

    private void RemoveAllWalls()
    {
        foreach(var wall in walls)
        {
            RemovePreviousWall();
        }
    }

    public bool GetWallPlacementEnabled() => wallPlacementEnabled;

    public void SetWallPlacementEnabled(bool state) => wallPlacementEnabled = state;

    public Transform GetGroundPlaneTransform()
    {
        if (groundPlane != null && planeIsPlaced)
        {
            return groundPlane.transform;
        }
        else
        {
            return null;
        }
    }

    public bool IsGroundPlaneSet()
    {
        if(groundPlane == null)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    // Helper functions
    private GameObject DrawLineBetweenTwoPoints(GameObject startPoint, GameObject endPoint)
    {
        var lineRendererGameObject = Instantiate(lineRendererPrefab);
        var lineRenderer = lineRendererGameObject.GetComponent<LineRenderer>();
        lineRenderer.SetPosition(0, startPoint.transform.position);
        lineRenderer.SetPosition(1, endPoint.transform.position);

        return lineRendererGameObject;
    }

    public void ToggleWallPlacement()
    {
        wallPlacementEnabled = !wallPlacementEnabled;
    }

    public void TogglePlaneDetection()
    {
        arPlaneManager.enabled = !arPlaneManager.enabled;

        // Go though each plane
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            plane.gameObject.SetActive(arPlaneManager.enabled);
        }
    }

    private GameObject CreateQuadFromPoints(Vector3 firstPoint, Vector3 secondPoint)
    {
        Debug.Log("CreateQuadeFromPoints");

        GameObject newMeshObject = new GameObject("wall");
        MeshFilter newMeshFilter = newMeshObject.AddComponent<MeshFilter>();
        newMeshObject.AddComponent<MeshRenderer>();

        // ge varje mesh ett material - 0: Occlusion
        newMeshObject.GetComponent<Renderer>().material = materialForWalls[0];
        Mesh newMesh = new Mesh();

        Vector3 heightVector = new Vector3(0, wallHeight, 0);

        // Adding a negative offset in Y would render the walls from below the mesh and upwards 
        newMesh.vertices = new Vector3[]
        {
            firstPoint,
            secondPoint,
            firstPoint + heightVector,
            secondPoint + heightVector
        };

        newMesh.triangles = new int[]
        {
            0,2,1,1,2,3,
        };

        newMesh.RecalculateNormals();
        newMesh.RecalculateTangents();
        newMesh.RecalculateBounds();

        newMeshFilter.mesh = newMesh;

        // At first the meshes aren't visible
        newMeshObject.SetActive(false);

        // Add the mesh to the list
        listOfWallMeshes.Add(newMeshObject);

        // returning the quad
        return newMeshObject;
    }

    public void RenderWalls()
    {
        if(walls.Count > 0)
        {
            foreach(var wall in walls)
            {
                wall.startPoint.SetActive(false);
                wall.endPoint.SetActive(false);
                wall.quad.SetActive(true);
                wall.line.SetActive(false);
            }
        }
    }
}
