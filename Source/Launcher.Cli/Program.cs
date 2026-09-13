// COPYRIGHT 2026 by the Open Rails Linux Fork project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FreeTrainSimulator.Launcher
{
    /// <summary>
    /// The launcher for the Linux build.
    /// </summary>
    /// <remarks>
    /// Upstream's launcher is a Windows Forms application, so this build needs its own way to
    /// pick content and start a run. It works on the same content model the Windows menu does -
    /// the same folders, the same scanned routes, the same saved selections - so a profile stays
    /// usable from either.
    /// </remarks>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellation.Cancel();
            };

            try
            {
                return await Run(args, cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");
                return 130;
            }
            catch (LauncherException ex)
            {
                Console.Error.WriteLine($"fts: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> Run(string[] args, CancellationToken cancellationToken)
        {
            List<string> arguments = new List<string>(args ?? Array.Empty<string>());
            string command = arguments.Count > 0 ? arguments[0].ToLowerInvariant() : "help";
            if (arguments.Count > 0)
                arguments.RemoveAt(0);

            return command switch
            {
                "content" => await Commands.Content(arguments, cancellationToken).ConfigureAwait(false),
                "routes" => await Commands.Routes(arguments, cancellationToken).ConfigureAwait(false),
                "activities" => await Commands.Activities(arguments, cancellationToken).ConfigureAwait(false),
                "paths" => await Commands.Paths(arguments, cancellationToken).ConfigureAwait(false),
                "consists" => await Commands.Consists(arguments, cancellationToken).ConfigureAwait(false),
                "play" => await Commands.Play(arguments, cancellationToken).ConfigureAwait(false),
                "explore" => await Commands.Explore(arguments, cancellationToken).ConfigureAwait(false),
                "resume" => Commands.Resume(),
                "run" => Commands.RunRaw(arguments),
                "doctor" => await Commands.Doctor(cancellationToken).ConfigureAwait(false),
                "version" or "--version" or "-v" => Commands.Version(),
                "help" or "--help" or "-h" => Usage(0),
                _ => UnknownCommand(command),
            };
        }

        private static int UnknownCommand(string command)
        {
            Console.Error.WriteLine($"fts: unknown command '{command}'");
            return Usage(2);
        }

        private static int Usage(int exitCode)
        {
            (exitCode == 0 ? Console.Out : Console.Error).Write(
@"fts - Free Train Simulator launcher

  fts content                       list the content folders
  fts content add <name> <path>     add a folder of MSTS content
  fts content remove <name>         remove a folder
  fts content refresh               rescan every folder

  fts routes [folder]               list routes
  fts activities <route>            list a route's activities
  fts paths <route>                 list a route's player paths
  fts consists [folder]             list consists

  fts play <route> <activity>       start an activity
  fts explore <route> <path> <consist> [--time HH:MM] [--season summer]
                                    [--weather clear]
  fts resume                        continue the last save
  fts run -- <arguments>            start the simulator with raw arguments

  fts doctor                        check that this machine can run the simulator
  fts version                       print the version

Routes, activities, paths and consists are matched on their name, case
insensitively; a unique prefix is enough. Quote names containing spaces.
");
            return exitCode;
        }
    }

    /// <summary>An error worth reporting to the user without a stack trace.</summary>
    internal sealed class LauncherException : Exception
    {
        public LauncherException(string message) : base(message)
        {
        }

        public LauncherException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public LauncherException()
        {
        }
    }
}
