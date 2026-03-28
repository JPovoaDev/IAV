using UnityEngine;

public class WorldOfCubes : MonoBehaviour {

    public int size;

    void Start() {
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                for (int z = 0; z < size; z++) {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.position = new Vector3(x, y, z);

                }

    }

    // Update is called once per frame
    void Update() {

    }
}