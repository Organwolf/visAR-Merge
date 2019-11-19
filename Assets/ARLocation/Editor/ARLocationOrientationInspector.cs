using UnityEditor;

namespace ARLocation
{
    [CustomEditor(typeof(ARLocationOrientation))]
    public class ARLocationOrientationInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

#if PLATFORM_ANDROID
            EditorGUILayout.HelpBox("On some Android devices, the magnetic compass data is not tilt compensated," +
                                    "so it is recommended that you check the 'ApplyCompassTiltCompensationOnAndroid' option above. " +
                                    "\n" +
                                    "Even with tilt-compensation, the compass data is bound to not work properly on landscape mode, so " +
                                    "we recommend to restrict the app to work only on portrait mode on Android.", MessageType.Warning);
#endif
        }
    }
}
