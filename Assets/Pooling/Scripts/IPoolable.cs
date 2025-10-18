namespace AngryKoala.Pooling
{
    public interface IPoolable
    {
        void OnRequestedFromPool();
        void OnReturnedToPool();
    }
}