using UnityEngine;
using AngryKoala.Pooling;
using AngryKoala.Pooling.Demo;
using AngryKoala.Services;

public class CubeSpawner : MonoBehaviour
{
    [SerializeField] private float _spawnDelay;

    private int _cubeCounter;
    private float _spawnTimer;

    private IPoolService _poolService;

    private void Start()
    {
        _poolService = ServiceLocator.Get<IPoolService>();
    }

    private void Update()
    {
        if (_spawnTimer >= _spawnDelay)
        {
            if (_cubeCounter % 3 == 0)
            {
                PoolableCube poolableCube = _poolService.Get<PoolableCube>(CubePoolKeys.RedCube);
                poolableCube.transform.position = Vector3.up * 18f;
                poolableCube.transform.rotation = Quaternion.identity;
            }
            else if (_cubeCounter % 3 == 1)
            {
                PoolableCube poolableCube = _poolService.Get<PoolableCube>(CubePoolKeys.GreenCube);
                poolableCube.transform.position = Vector3.up * 18f;
                poolableCube.transform.rotation = Quaternion.identity;
            }
            else
            {
                PoolableCube poolableCube = _poolService.Get<PoolableCube>(CubePoolKeys.BlueCube);
                poolableCube.transform.position = Vector3.up * 18f;
                poolableCube.transform.rotation = Quaternion.identity;
            }

            _cubeCounter++;
            _spawnTimer = 0f;
        }
        else
        {
            _spawnTimer += Time.deltaTime;
        }
    }
}