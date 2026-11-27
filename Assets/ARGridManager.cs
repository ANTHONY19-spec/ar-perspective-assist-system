using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARGridManager : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public GameObject gridPointPrefab;

    // Stores the hit data when your tap hits an AR plane
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // Stores the points you place so we can measure them later
    private List<GameObject> spawnedPoints = new List<GameObject>();

    void Update()
    {
        // 1. Check if the screen was touched this frame
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        // 2. Only trigger when the finger first touches the screen
        if (touch.phase == TouchPhase.Began)
        {
            // 3. Shoot a raycast from the touch position to detect AR planes
            if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
            {
                // 4. Get the exact position and rotation of where the raycast hit the floor
                Pose hitPose = hits[0].pose;

                // 5. Spawn your 3D point prefab at that exact location
                GameObject spawnedObject = Instantiate(gridPointPrefab, hitPose.position, hitPose.rotation);

                // 6. Add it to our list for future distance calculations
                spawnedPoints.Add(spawnedObject);

                // 7. If we have exactly 2 points, calculate the distance
                if (spawnedPoints.Count == 2)
                {
                    float distance = Vector3.Distance(spawnedPoints[0].transform.position, spawnedPoints[1].transform.position);
                    Debug.Log("Distance: " + distance + " meters");
                }
            }
        }
    }
}