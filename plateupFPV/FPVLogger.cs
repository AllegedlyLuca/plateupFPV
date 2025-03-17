using KitchenLib.Logging;
using System.Diagnostics;

namespace FirstPersonView
{
    class FPVLogger
    {
        internal static KitchenLogger Logger;

        /// <summary>
        /// Provides the core logging functionality for this mod.
        /// </summary>
        /// <param name="newLogger"></param>
        public FPVLogger(KitchenLogger newLogger)
        {
            Logger = newLogger;
        }

        /// <summary>
        /// Writes a log entry to the output log.
        /// </summary>
        /// <param name="type">The type of log we're using.  0 = info, 1 = warning, 2 = error.</param>
        /// <param name="message">The message to be logged.</param>
        private static void Log(int type, string message)
        {
            switch (type)
            {
                case 0:
                    Logger.LogInfo(message);
                    return;
                case 1:
                    Logger.LogWarning(message);
                    return;
                case 2:
                    Logger.LogError(message);
                    return;
            }
            Logger.LogInfo(message);
        }

        /// <summary>
        /// Writes an info-level log entry to the output log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Info(string message)
        {
            Log(0, message);
        }

        /// <summary>
        /// Writes an info-level log entry to the output log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Info(object message)
        {
            Log(0, message.ToString());
        }

        /// <summary>
        /// Writes a warning-level log entry to the output log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Warn(string message)
        {
            Log(1, message);
        }

        /// <summary>
        /// Writes a warning-level log entry to the output log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Warn(object message)
        {
            Log(1, message.ToString());
        }

        /// <summary>
        /// Writes a error-level log entry to the output log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Error(string message)
        {
            Log(2, message);
        }

        /// <summary>
        /// Writes a error-level log entry to the output log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Error(object message)
        {
            Log(2, message.ToString());
        }

        /// <summary>
        /// Writes a debug log entry at info level, prefixed with [DEBUG] and a 3-tier method call trace to assist in locating where the call came from.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Debug(string message)
        {
            if (PreferenceHandler.GetDebugSetting())
            {
                Info($"[DEBUG][{GetMethodPathForDebugLogging()}] " + message);
            }
        }

        /// <summary>
        /// Writes a debug log entry at info level, prefixed with [DEBUG] and a 3-tier method call trace to assist in locating where the call came from.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public static void Debug(object message)
        {
            if (PreferenceHandler.GetDebugSetting())
            {
                Info($"[DEBUG][{GetMethodPathForDebugLogging()}] " + message.ToString());
            }
        }

        /// <summary>
        /// Acquires the method call trace for use in the DebugLog method.
        /// </summary>
        /// <returns>Returns a string concatonation of the method call trace, with a depth of 3 starting at offset 2.</returns>
        private static string GetMethodPathForDebugLogging()
        {
            StackTrace stackTrace = new StackTrace();
            int WorkableFrames = new StackTrace().GetFrames().Length;
            string TraceFrames = "";

            int FramesToCapture = WorkableFrames > 3 ? 3 : WorkableFrames;
            int FrameStartingOffset = 2;

            for (int i = FrameStartingOffset; i <= (FrameStartingOffset + FramesToCapture); i++)
            {
                if (i >= WorkableFrames)
                    break;

                TraceFrames += (TraceFrames == "" ? "" : ", ") + stackTrace.GetFrame(i).GetMethod().Name;
            }

            return TraceFrames;
        }
    }
}
