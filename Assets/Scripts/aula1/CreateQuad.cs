using UnityEngine;

public class CreateQuad : MonoBehaviour {
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        // Frente (+Z): v4, v5, v0, v1
        CreateQuadMethod(new Vector3[] {
        new Vector3(-0.5f,  0.5f,  0.5f),
        new Vector3( 0.5f,  0.5f,  0.5f),
        new Vector3(-0.5f, -0.5f,  0.5f),
        new Vector3( 0.5f, -0.5f,  0.5f),
    }, Vector3.back);

        // Trás (-Z): v6, v7, v2, v3
        CreateQuadMethod(new Vector3[] {
        new Vector3( 0.5f,  0.5f, -0.5f),
        new Vector3(-0.5f,  0.5f, -0.5f),
        new Vector3( 0.5f, -0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f, -0.5f),
    }, Vector3.back);

        // Cima (+Y): v7, v6, v4, v5  (atenção à ordem!)
        CreateQuadMethod(new Vector3[] {
        new Vector3(-0.5f,  0.5f, -0.5f),
        new Vector3( 0.5f,  0.5f, -0.5f),
        new Vector3(-0.5f,  0.5f,  0.5f),
        new Vector3( 0.5f,  0.5f,  0.5f),
    }, Vector3.back);

        // Baixo (-Y)
        CreateQuadMethod(new Vector3[] {
        new Vector3(-0.5f, -0.5f,  0.5f),
        new Vector3( 0.5f, -0.5f,  0.5f),
        new Vector3(-0.5f, -0.5f, -0.5f),
        new Vector3( 0.5f, -0.5f, -0.5f),
    }, Vector3.back);

        // Esquerda (-X)
        CreateQuadMethod(new Vector3[] {
        new Vector3(-0.5f,  0.5f, -0.5f),
        new Vector3(-0.5f,  0.5f,  0.5f),
        new Vector3(-0.5f, -0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f,  0.5f),
    }, Vector3.back);

        // Direita (+X)
        CreateQuadMethod(new Vector3[] {
        new Vector3( 0.5f,  0.5f,  0.5f),
        new Vector3( 0.5f,  0.5f, -0.5f),
        new Vector3( 0.5f, -0.5f,  0.5f),
        new Vector3( 0.5f, -0.5f, -0.5f),
    }, Vector3.back);

        CombineQuads();
    }

    // Update is called once per frame
    void Update() {

        /* Vector3[] verts = GetComponent<MeshFilter>().mesh.vertices;
         verts[0] = verts[0] + Vector3.up * 0.1f;
         GetComponent<MeshFilter>().mesh.vertices = verts;*/
        transform.Rotate(Vector3.up, 30f * Time.deltaTime);// tipo os item quando caem no minecraft


    }
    void CreateQuadMethod(Vector3[] faceVertices, Vector3 normal) {
        Mesh mesh = new Mesh();

        // Os 4 vértices da face
        mesh.vertices = faceVertices;

        // Dois triângulos que formam o quad (ordem horária vista de fora)
        mesh.triangles = new int[] { 0, 3, 1, 0, 2, 3 };

        // A normal é a mesma para todos os 4 vértices
        mesh.normals = new Vector3[] { normal, normal, normal, normal };

        // UVs standard
        mesh.uv = new Vector2[] {
           new Vector2(0f, 2f),
           new Vector2(2f, 2f),
           new Vector2(0f, 0f),
           new Vector2(2f, 0f),


         };


        GameObject quad = new GameObject("Quad");
        quad.transform.parent = transform;
        quad.AddComponent<MeshFilter>().mesh = mesh;
        quad.AddComponent<MeshRenderer>();


    }
    public Material material;

    void CombineQuads() {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];
        for (int i = 0; i < meshFilters.Length; i++) {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
        }
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = new Mesh();
        mf.mesh.CombineMeshes(combine);
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mr.material = material;
        // Destruir os quads temporários
        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }
}
