using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using StardewModdingAPI.Framework.Logging;
using StardewModdingAPI.Framework.Models;
using StardewModdingAPI.Internal;

namespace StardewModdingAPI.Framework
{
    /// <summary>Encapsulates monitoring and logic for a given module.</summary>
    internal class Monitor : IMonitor
    {
        /*********
        ** Properties
        *********/
        /// <summary>The name of the module which logs messages using this instance.</summary>
        private readonly string Source;

        /// <summary>Manages access to the console output.</summary>
        private readonly ConsoleInterceptionManager ConsoleManager;

        /// <summary>The log file to which to write messages.</summary>
        private readonly LogFileManager LogFile;

        /// <summary>The maximum length of the <see cref="LogLevel"/> values.</summary>
        private static readonly int MaxLevelLength = (from level in Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>() select level.ToString().Length).Max();

        /// <summary>The console text color for each log level.</summary>
        private readonly IDictionary<LogLevel, ConsoleColor> Colors;

        /// <summary>Propagates notification that SMAPI should exit.</summary>
        private readonly CancellationTokenSource ExitTokenSource;


        /*********
        ** Accessors
        *********/
        /// <summary>Whether SMAPI is aborting. Mods don't need to worry about this unless they have background tasks.</summary>
        public bool IsExiting => this.ExitTokenSource.IsCancellationRequested;

        /// <summary>Whether to show the full log stamps (with time/level/logger) in the console. If false, shows a simplified stamp with only the logger.</summary>
        internal bool ShowFullStampInConsole { get; set; }

        /// <summary>Whether to show trace messages in the console.</summary>
        internal bool ShowTraceInConsole { get; set; }

        /// <summary>Whether to write anything to the console. This should be disabled if no console is available.</summary>
        internal bool WriteToConsole { get; set; } = true;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="source">The name of the module which logs messages using this instance.</param>
        /// <param name="consoleManager">Manages access to the console output.</param>
        /// <param name="logFile">The log file to which to write messages.</param>
        /// <param name="exitTokenSource">Propagates notification that SMAPI should exit.</param>
        /// <param name="colorScheme">The console color scheme to use.</param>
        public Monitor(string source, ConsoleInterceptionManager consoleManager, LogFileManager logFile, CancellationTokenSource exitTokenSource, MonitorColorScheme colorScheme)
        {
            // validate
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("The log source cannot be empty.");

            // initialise
            this.Colors = Monitor.GetConsoleColorScheme(colorScheme);
            this.Source = source;
            this.LogFile = logFile ?? throw new ArgumentNullException(nameof(logFile), "The log file manager cannot be null.");
            this.ConsoleManager = consoleManager;
            this.ExitTokenSource = exitTokenSource;
        }

        /// <summary>Log a message for the player or developer.</summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log severity level.</param>
        public void Log(string message, LogLevel level = LogLevel.Debug)
        {
            this.LogImpl(this.Source, message, level, this.Colors[level]);
        }

        /// <summary>Immediately exit the game without saving. This should only be invoked when an irrecoverable fatal error happens that risks save corruption or game-breaking bugs.</summary>
        /// <param name="reason">The reason for the shutdown.</param>
        public void ExitGameImmediately(string reason)
        {
            this.LogFatal($"{this.Source} requested an immediate game shutdown: {reason}");
            this.ExitTokenSource.Cancel();
        }

        /// <summary>Write a newline to the console and log file.</summary>
        internal void Newline()
        {
            if (this.WriteToConsole)
                this.ConsoleManager.ExclusiveWriteWithoutInterception(Console.WriteLine);
            this.LogFile.WriteLine("");
        }

        /// <summary>Log console input from the user.</summary>
        /// <param name="input">The user input to log.</param>
        internal void LogUserInput(string input)
        {
            // user input already appears in the console, so just need to write to file
            string prefix = this.GenerateMessagePrefix(this.Source, LogLevel.Info);
            this.LogFile.WriteLine($"{prefix} $>{input}");
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Log a fatal error message.</summary>
        /// <param name="message">The message to log.</param>
        private void LogFatal(string message)
        {
            this.LogImpl(this.Source, message, LogLevel.Error, ConsoleColor.White, background: ConsoleColor.Red);
        }

        /// <summary>Write a message line to the log.</summary>
        /// <param name="source">The name of the mod logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log level.</param>
        /// <param name="color">The console foreground color.</param>
        /// <param name="background">The console background color (or <c>null</c> to leave it as-is).</param>
        private void LogImpl(string source, string message, LogLevel level, ConsoleColor color, ConsoleColor? background = null)
        {
            // generate message
            string prefix = this.GenerateMessagePrefix(source, level);
            string fullMessage = $"{prefix} {message}";
            string consoleMessage = this.ShowFullStampInConsole ? fullMessage : $"[{source}] {message}";

            // write to console
            if (this.WriteToConsole && (this.ShowTraceInConsole || level != LogLevel.Trace))
            {
                this.ConsoleManager.ExclusiveWriteWithoutInterception(() =>
                {
                    if (this.ConsoleManager.SupportsColor)
                    {
                        if (background.HasValue)
                            Console.BackgroundColor = background.Value;
                        Console.ForegroundColor = color;
                        Console.WriteLine(consoleMessage);
                        Console.ResetColor();
                    }
                    else
                        Console.WriteLine(consoleMessage);
                });
            }

            // write to log file
            this.LogFile.WriteLine(fullMessage);
        }

        /// <summary>Generate a message prefix for the current time.</summary>
        /// <param name="source">The name of the mod logging the message.</param>
        /// <param name="level">The log level.</param>
        private string GenerateMessagePrefix(string source, LogLevel level)
        {
            string levelStr = level.ToString().ToUpper().PadRight(Monitor.MaxLevelLength);
            return $"[{DateTime.Now:HH:mm:ss} {levelStr} {source}]";
        }

        /// <summary>Get the color scheme to use for the current console.</summary>
        /// <param name="colorScheme">The console color scheme to use.</param>
        private static IDictionary<LogLevel, ConsoleColor> GetConsoleColorScheme(MonitorColorScheme colorScheme)
        {
            // auto detect color scheme
            if (colorScheme == MonitorColorScheme.AutoDetect)
            {
                if (Constants.Platform == Platform.Mac)
                    colorScheme = MonitorColorScheme.LightBackground; // MacOS doesn't provide console background color info, but it's usually white.
                else
                    colorScheme = Monitor.IsDark(Console.BackgroundColor) ? MonitorColorScheme.DarkBackground : MonitorColorScheme.LightBackground;
            }

            // get colors for scheme
            switch (colorScheme)
            {
                case MonitorColorScheme.DarkBackground:
                    return new Dictionary<LogLevel, ConsoleColor>
                    {
                        [LogLevel.Trace] = ConsoleColor.DarkGray,
                        [LogLevel.Debug] = ConsoleColor.DarkGray,
                        [LogLevel.Info] = ConsoleColor.White,
                        [LogLevel.Warn] = ConsoleColor.Yellow,
                        [LogLevel.Error] = ConsoleColor.Red,
                        [LogLevel.Alert] = ConsoleColor.Magenta
                    };

                case MonitorColorScheme.LightBackground:
                    return new Dictionary<LogLevel, ConsoleColor>
                    {
                        [LogLevel.Trace] = ConsoleColor.DarkGray,
                        [LogLevel.Debug] = ConsoleColor.DarkGray,
                        [LogLevel.Info] = ConsoleColor.Black,
                        [LogLevel.Warn] = ConsoleColor.DarkYellow,
                        [LogLevel.Error] = ConsoleColor.Red,
                        [LogLevel.Alert] = ConsoleColor.DarkMagenta
                    };

                default:
                    throw new NotSupportedException($"Unknown color scheme '{colorScheme}'.");
            }
        }

        /// <summary>Get whether a console color should be considered dark, which is subjectively defined as 'white looks better than black on this text'.</summary>
        /// <param name="color">The color to check.</param>
        private static bool IsDark(ConsoleColor color)
        {
            switch (color)
            {
                case ConsoleColor.Black:
                case ConsoleColor.Blue:
                case ConsoleColor.DarkBlue:
                case ConsoleColor.DarkMagenta: // Powershell
                case ConsoleColor.DarkRed:
                case ConsoleColor.Red:
                    return true;

                default:
                    return false;
            }
        }
    }
}
