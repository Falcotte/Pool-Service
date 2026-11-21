using System.Collections.Generic;
using AngryKoala.Services;
using UnityEngine;

namespace AngryKoala.Pooling.Demo
{
    public sealed class CubeSpawner : MonoBehaviour
    {
        [SerializeField] private List<PoolableCube> _cubePrefabs = new();

        [SerializeField] private float _spawnDelay;

        private int _nextPrefabIndex;
        private float _spawnTimer;

        private IPoolService _poolService;

        private const float _spawnHeight = 18f;
        private const float _cubeLifetimeSeconds = 10f;

        private void Start()
        {
            _poolService = ServiceLocator.Get<IPoolService>();

            StartCoroutine(_poolService.PrewarmAll());
        }

        private void Update()
        {
            _spawnTimer += Time.deltaTime;

            if (_spawnTimer < _spawnDelay || _cubePrefabs.Count == 0)
            {
                return;
            }

            _spawnTimer = 0f;

            PoolableCube prefab = _cubePrefabs[_nextPrefabIndex];
            _nextPrefabIndex = (_nextPrefabIndex + 1) % _cubePrefabs.Count;

            if (prefab == null)
            {
                return;
            }

            Vector3 spawnPosition = Vector3.up * _spawnHeight;
            PoolableCube cube = _poolService.Get(prefab, spawnPosition, Quaternion.identity);

            _poolService.Return(cube, _cubeLifetimeSeconds);
        }
    }
}
