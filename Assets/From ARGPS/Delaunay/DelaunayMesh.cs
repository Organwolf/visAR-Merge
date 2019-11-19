using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;
using TriangleNet.Topology;
using System.Linq;

public partial class DelaunayMesh : MonoBehaviour
{
    // Size of each chunk - currently larger enough to fit the mesh in 1 chunk
    [SerializeField] private int trianglesInChunk = 5000;

    // Prefab which is generated for each chunk of the mesh.
    public Transform chunkPrefab = null;

    // Elevations at each point in the mesh
    private List<float> elevations;

    // The delaunay mesh
    private TriangleNet.Mesh mesh = null;
    private List<Transform> chunks = new List<Transform>();

    // Boolean set to true when the mesh has been generated
    private bool isMeshGenerated = false;

    public void Generate(IEnumerable<Vector3> locations, Transform groundPlaneTransform)
    {

        Polygon polygon = new Polygon();
        elevations = new List<float>();

        Debug.Log($"Mesh Generated with {locations.ToList().Count} points.");

        // Create separate polygons for the triangulation
        foreach (var location in locations)
        {
            polygon.Add(new Vertex(location.x, location.z));
        }

        TriangleNet.Meshing.ConstraintOptions options = new TriangleNet.Meshing.ConstraintOptions() { ConformingDelaunay = false };
        mesh = (TriangleNet.Mesh)polygon.Triangulate(options);

        var globalLocalPositions = locations.ToArray();
        for (int i = 0; i < globalLocalPositions.Length; i++)
        {
            var currentPosition = globalLocalPositions[i];
            var waterHeight = currentPosition.y;

            if(waterHeight != -9999)
            {
                elevations.Add((float)waterHeight + groundPlaneTransform.position.y);
            }
            else
            {
                elevations.Add(groundPlaneTransform.position.y);
            }
        }
        ClearMesh();
        MakeMesh();
        // Boolean that is set once the mesh is created
        isMeshGenerated = true;
    }

    public void MakeMesh()
    {
        // Instantiate an enumerator to go over the Triangle.Net triangles - they don't
        // provide any array-like interface for indexing
        IEnumerator<Triangle> triangleEnumerator = mesh.Triangles.GetEnumerator();

        var numberOfChunks = 1;
        // Create more than one chunk, if necessary
        for (int chunkStart = 0; chunkStart < mesh.Triangles.Count; chunkStart += trianglesInChunk)
        {
            Debug.Log($"Chunk {numberOfChunks++}!");
            // Vertices in the unity mesh
            List<Vector3> vertices = new List<Vector3>();

            // Per-vertex normals
            List<Vector3> normals = new List<Vector3>();

            // Per-vertex UVs - unused here, but Unity still wants them
            List<Vector2> uvs = new List<Vector2>();

            // Triangles - each triangle is made of three indices in the vertices array
            List<int> triangles = new List<int>();

            // Iterate over all the triangles until we hit the maximum chunk size
            int chunkEnd = chunkStart + trianglesInChunk;
            for (int i = chunkStart; i < chunkEnd; i++)
            {
                if (!triangleEnumerator.MoveNext())
                {
                    // If we hit the last triangle before we hit the end of the chunk, stop
                    break;
                }

                // Get the current triangle
                Triangle triangle = triangleEnumerator.Current;

                // For the triangles to be right-side up, they need
                // to be wound in the opposite direction
                Vector3 v0 = GetPoint3D(triangle.vertices[2].id);
                Vector3 v1 = GetPoint3D(triangle.vertices[1].id);
                Vector3 v2 = GetPoint3D(triangle.vertices[0].id);

                // This triangle is made of the next three vertices to be added
                triangles.Add(vertices.Count);
                triangles.Add(vertices.Count + 1);
                triangles.Add(vertices.Count + 2);

                // Add the vertices
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);

                // Compute the normal - flat shaded, so the vertices all have the same normal
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                // If you want to texture your terrain, UVs are important,
                // but I just use a flat color so put in dummy coords
                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 0.0f));
            }

            // Create the actual Unity mesh object
            Mesh chunkMesh = new Mesh();
            chunkMesh.vertices = vertices.ToArray();
            chunkMesh.uv = uvs.ToArray();
            chunkMesh.triangles = triangles.ToArray();
            chunkMesh.normals = normals.ToArray();

            // Instantiate the GameObject which will display this chunk
            Transform chunk = Instantiate<Transform>(chunkPrefab, transform.position, transform.rotation);
            chunk.GetComponent<MeshFilter>().mesh = chunkMesh;
            chunk.GetComponent<MeshCollider>().sharedMesh = chunkMesh;
            chunk.transform.parent = transform;

            chunks.Add(chunk);
        }
    }

    public bool isMeshCreated()
    {
        return isMeshGenerated;
    }

    public void ClearMesh()
    {
        if (chunks != null && chunks.Count > 0)
        {
            Debug.Log("Clearing the mesh");
            foreach (Transform chunk in chunks)
                Destroy(chunk.gameObject);
        }
        chunks.Clear();
    }

    public GameObject GetMesh()
    {
        if (chunks.Count == 1)
        {
            return chunks[0].gameObject;
        }
        else
            return null;
    }

    // Used for the exaggerated height of the water mesh
    public void SetHeightToMesh(float newHeight)
    {
        if (chunks.Count == 1)
        {
            chunks[0].transform.position = new Vector3(chunks[0].transform.position.x, newHeight, chunks[0].transform.position.z);
        }
    }

    // No defensive prog. I assume that we only have one chunk
    public float GetHeightOfMesh()
    {
        float height = chunks[0].transform.position.y;
        return height;
    }

    // Returns a point's local coordinates
    private Vector3 GetPoint3D(int index)
    {
        Vertex vertex = mesh.vertices[index];
        float elevation = elevations[index];
        return new Vector3((float)vertex.x, elevation, (float)vertex.y);
    }


}