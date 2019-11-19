using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

public class ARManager : MonoBehaviour
{
    public static ARManager Instance;

    [Tooltip("The AR Camera that will be used for rendering the AR content. If this is not set, the camera tagger as 'MainCamera' will be used." +
             " Make sure either this is set, or a camera is tagged as 'MainCamera', or an error will be thrown.")]
    public Camera Camera;

    [Tooltip("If true, wait until the AR Tracking starts to start with location and orientation updates and object placement.")]
    public bool WaitForARTrackingToStart = true;

    [Tooltip("If true, every time the AR tracking is lost and regained, the AR+GPS system is restarted, repositioning all the objects.")]
    public bool RestartWhenARTrackingIsRestored;

    [Tooltip("If true, the manager will set 'Application.targetFrameRate' to 60.")]
    public bool SetTargetFrameRateTo60Mhz = true;

    [Header("Debug")]
    [Tooltip("When debug mode is enabled, this component will print relevant messages to the console. Filter by 'ARLocationManager' in the log output to see the messages.")]
    public bool DebugMode;

    [Header("AR Session Events")]
    public UnityEvent OnTrackingStarted;
    public UnityEvent OnTrackingLost;
    public UnityEvent OnTrackingRestored;

    /// <summary>
    /// The instance of the 'IARSessionManager'. Handles the interface with the underlying AR session (i.e., Vuforia or AR Foundation).
    /// </summary>
    public ARLocation.Session.IARSessionManager SessionManager { get; private set; }

    /// <summary>
    /// The 'MainCamera' that is being used for rendering the AR content.
    /// </summary>
    public Camera MainCamera { get; private set; }

    //private ARLocationOrientation arLocationOrientation;
    private ARLocation.ARLocationProvider arLocationProvider;
    private ARTransformationManager arTransformationManager;
    private Action onARTrackingStartedAction;

    public void Awake()
    {
        Instance = this;
        if (SetTargetFrameRateTo60Mhz)
        {
            Application.targetFrameRate = 60;
        }

        MainCamera = Camera ? Camera : Camera.main;

        if (MainCamera == null)
        {
            throw new NullReferenceException("[AR+GPS][ARLocationManager#Start]: Missing Camera. " +
                                             "Either set the 'Camera' property to the AR Camera, or tag it as a 'MainCamera'.");
        }
    }

    private void Start()
    {
        //arLocationOrientation = GetComponent<ARLocationOrientation>();
        arLocationProvider = ARLocation.ARLocationProvider.Instance;
        arTransformationManager = ARTransformationManager.Instance;

#if !ARGPS_USE_VUFORIA
        var arSession = FindObjectOfType<ARSession>();

        if (!arSession)
        {
            throw new NullReferenceException("[AR+GPS][ARLocationManager#Start]: No ARSession found in the scene!");
        }

        SessionManager = new ARLocation.Session.ARFoundationSessionManager(arSession);
#else
            SessionManager = new VuforiaSessionManager();
#endif

        SessionManager.DebugMode = DebugMode;

        SessionManager.OnARTrackingStarted(ARTrackingStartedCallback);
        SessionManager.OnARTrackingRestored(ARTrackingRestoredCallback);
        SessionManager.OnARTrackingLost(ARTrackingLostCallback);
    }

    private void ARTrackingLostCallback()
    {
        OnTrackingLost?.Invoke();
    }

    private void ARTrackingRestoredCallback()
    {

        if (RestartWhenARTrackingIsRestored)
        {
            Restart();
        }

        OnTrackingRestored?.Invoke();
    }

    private void ARTrackingStartedCallback()
    {
        OnTrackingStarted?.Invoke();
        onARTrackingStartedAction?.Invoke();
    }

    /// <summary>
    /// This will reset the AR Session and the AR+GPS system, repositioning all objects.
    /// </summary>
    /// <param name="cb">Optional callback, called when the system has restarted.</param>
    public void ResetARSession(Action cb = null)
    {
        SessionManager?.Reset(() =>
        {
            Restart();
            cb?.Invoke();
        });
    }

    /// <summary>
    /// This will restart the AR+GPS system, repositioning all the objects.
    /// </summary>
    public void Restart()
    {
        //arLocationOrientation.Restart();
        arLocationProvider.Restart();
        arTransformationManager.Restart();
    }

    /// <summary>
    /// Returns a string describing the current AR session tracking status
    /// </summary>
    /// <returns></returns>
    public string GetARSessionInfoString()
    {
        return SessionManager != null ? SessionManager.GetSessionInfoString() : "None";
    }

    /// <summary>
    /// Returns a string describing the current AR Session provider, e.g., AR Foundation or Vuforia.
    /// </summary>
    /// <returns></returns>
    public string GetARSessionProviderString()
    {
        return SessionManager != null ? SessionManager.GetProviderString() : "None";
    }

    /// <summary>
    /// Add a event listener for when the AR Tracking starts.
    /// </summary>
    /// <param name="o"></param>
    public void OnARTrackingStarted(Action o)
    {
        if (SessionManager == null)
        {
            onARTrackingStartedAction += o;
        }
        else
        {
            SessionManager.OnARTrackingStarted(o);
        }
    }

    /// <summary>
    /// Add a event listener for when the AR Tracking regained after it was lost.
    /// </summary>
    /// <param name="callback"></param>
    public void OnARTrackingRestored(Action callback)
    {
        SessionManager?.OnARTrackingRestored(callback);
    }

    /// <summary>
    /// Add a event listener for when the AR Tracking is lost.
    /// </summary>
    /// <param name="callback"></param>
    public void OnARTrackingLost(Action callback)
    {
        SessionManager?.OnARTrackingLost(callback);
    }
}
