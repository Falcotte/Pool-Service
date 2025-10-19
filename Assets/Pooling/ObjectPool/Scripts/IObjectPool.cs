using AngryKoala.Pooling;

namespace AngryKoala.Pooling
{
    public interface IObjectPool
    {
        IPoolable Get();
        void Return(IPoolable instance);
        
        int TotalCreated { get; }
        int MaxSize { get; }
        
        int AvailableCount { get; }
    }
}