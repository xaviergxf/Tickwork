namespace Tickwork.Utilities.Enums
{
    public enum TickworkStartMode
    {
        /// <summary>
        /// Start job processing immediately when UseTickwork is called.
        /// Background services are registered and start automatically.
        /// </summary>
        Immediate,
        
        /// <summary>
        /// Background services are registered but skip the first run.
        /// Job processing needs to be started manually via ITickworkHostScheduler.
        /// </summary>
        Manual
    }
}