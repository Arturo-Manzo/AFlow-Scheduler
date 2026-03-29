namespace AScheduler.Domain
{
    /// <summary>
    /// Specifies the type of task and its corresponding executor implementation.
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// An executable file (.exe) to be run directly.
        /// </summary>
        Exe,

        /// <summary>
        /// A batch file (.bat) to be executed via cmd.exe.
        /// </summary>
        Bat,

        /// <summary>
        /// A Python script to be executed by the Python interpreter.
        /// </summary>
        Python,

        /// <summary>
        /// An HTTP API request (GET, POST, PUT, DELETE).
        /// </summary>
        Api
    }
}