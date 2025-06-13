namespace Common.Events.Interfaces
{
    public interface IEventBus
    {
        void StartTransaction();
        void Publish<T>(T @event) where T : class;
        void Commit();
        void Rollback();
        void Subscribe<TEvent>() where TEvent : class;
    }

}
