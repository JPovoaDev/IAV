using UnityEngine;

public class vertices_Mesh : MonoBehaviour {
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        Mesh mesh = new Mesh();
        Vector3[] vertices = {
        new Vector3(-0.5f, 0.5f, 0f), // 0: top-left
        new Vector3( 0.5f, 0.5f, 0f), // 1: top-right
        new Vector3(-0.5f, -0.5f, 0f), // 2: bottom-left
        new Vector3( 0.5f, -0.5f, 0f), // 3: bottom-right
        };

        mesh.vertices = vertices;
        int[] triangles = { 0, 3, 1, 0, 2, 3 }; // os vossos índices aqui
        mesh.triangles = triangles;
        Vector3[] normals = {
        Vector3.back,
        Vector3.back,
        Vector3.back,
        Vector3.back,
        };
        mesh.normals = normals;
        Vector2[] uv = {
        new Vector2(0f, 1f), // vertex 0  canto sup-esq da textura
        new Vector2(1f, 1f), // vertex 1  canto sup-dir
        new Vector2(0f, 0f), // vertex 2  canto inf-esq
        new Vector2(1f, 0f), // vertex 3  canto inf-dir
        };
        mesh.uv = uv;

        gameObject.AddComponent<MeshFilter>().mesh = mesh;
        gameObject.AddComponent<MeshRenderer>();

    }

    // Update is called once per frame
    void Update() {

    }
}