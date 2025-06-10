using DirectorySyncer.FilesSyncer;
using System.CommandLine;

namespace DirectorySyncer;

internal class Program
{
    private readonly record struct WriteInfo
    {
        public static WriteInfo Space { get; } = new(" ");

        public string Text { get; init; } = string.Empty;
        public ConsoleColor? ForegroundColor { get; init; }

        public WriteInfo()
        {
        }
        public WriteInfo(string text)
        {
            Text = text;
        }
        public WriteInfo(string text, ConsoleColor foregroundColor)
        {
            Text = text;
            ForegroundColor = foregroundColor;
        }
    }

    private static int _returnValue = 0;

    private static async Task<int> Main(string[] args)
    {
        var sourceDirectory = new Argument<string>(
            name: "sourceDirectory",
            description: "The source directory that you want to sync")
        {
            Arity = ArgumentArity.ExactlyOne,
        };

        var targetDirectory = new Argument<string>(
            name: "<targetDirectory>",
            description: "The target directory that you want to sync to")
        {
            Arity = ArgumentArity.ExactlyOne,
        };

        var mode = new Option<Mode>(
            name: "--mode",
            getDefaultValue: () => Mode.SourceToTarget,
            description: "The sync mode to use, valid options are:\n" +
                         $"- {nameof(Mode.SourceToTarget)}: Only the files in <sourceDirectory> can be synced to <targetDirectory>.\n" +
                         $"- {nameof(Mode.Dual)}: The <sourceDirectory> and <targetDirectory> can be both act as either the source or target for syncing.\n"
        );

        var autoSync = new Option<bool>(
            name: "--autoSync",
            getDefaultValue: () => false,
            description: "Auto sync if possible."
        );

        var rootCommand = new RootCommand()
        {
            sourceDirectory,
            targetDirectory,
            mode,
            autoSync
        };

        rootCommand.SetHandler(RootCommandHandler, sourceDirectory, targetDirectory, mode, autoSync);

        Syncer.Default.FileSynced += (s, e) =>
        {
            WriteLine(new WriteInfo($"[{e.Index + 1}] {e.SyncFile.SourcePath} -> {e.SyncFile.TargetPath}", ConsoleColor.Cyan));
        };
        Syncer.Default.ErrorOccured += (s, e) =>
        {
            WriteLine(new WriteInfo($"[Error] {e.ErrorMessage}", ConsoleColor.Red));
        };

        await rootCommand.InvokeAsync(args);

        return _returnValue;
    }

    private static async Task RootCommandHandler(string sourceDirectory, string targetDirectory, Mode mode, bool autoSync)
    {
        Syncer.Default.SourceDirectory = sourceDirectory;
        Syncer.Default.TargetDirectory = targetDirectory;
        Syncer.Default.Mode = mode;

        Syncer.Default.UpdateSyncFiles();

        PrintSyncingInfo(Syncer.Default.SyncFiles);
        WriteLine();

        if (!Syncer.Default.SyncFiles.Any())
        {
            WriteLine(new WriteInfo("All files are already synced, nothing to be synced.", ConsoleColor.Green));
            return;
        }

        if (autoSync || PromptConfirmation())
        {
            WriteLine(new WriteInfo("[Syncing started]", ConsoleColor.Green));
            try
            {
                await Syncer.Default.SyncAllFilesAsync();
                WriteLine(new WriteInfo("[Syncing completed]", ConsoleColor.Green));
            }
            catch (Exception e)
            {
                WriteLine(new WriteInfo(e.Message, ConsoleColor.Red));
                WriteLine(new WriteInfo("[Syncing interrupted]", ConsoleColor.Red));
            }
        }
        else
        {
            WriteLine(new WriteInfo("Syncing actions are cancelled. No changes were made to the directories.", ConsoleColor.DarkYellow));
            _returnValue = -1;
        }

        static bool PromptConfirmation()
        {
            Write(new WriteInfo("Do you want to apply these syncing actions?(y/n): "));
            return Console.ReadLine()?.ToLower() == "y";
        }

        static void PrintSyncingInfo(IReadOnlyList<SyncFile> syncFiles)
        {
            WriteLine(GetDirectoryInfo(Syncer.Default.SourceDirectory), new WriteInfo(": ", ConsoleColor.DarkCyan), new WriteInfo(Syncer.Default.SourceDirectory , ConsoleColor.DarkCyan));
            WriteLine(GetDirectoryInfo(Syncer.Default.TargetDirectory), new WriteInfo(": ", ConsoleColor.Yellow), new WriteInfo( Syncer.Default.TargetDirectory, ConsoleColor.Yellow));

            WriteLine(new WriteInfo("["));
            for (var i = 0; i < syncFiles.Count; i++)
            {
                var updateColor = syncFiles[i].SyncType switch
                {
                    SyncType.Copy => ConsoleColor.Red,
                    SyncType.Update => ConsoleColor.Green,
                    _ => throw new ArgumentException($"Invalid sync mode {syncFiles[i].SyncType}"),
                };

                WriteLine(
                    WriteInfo.Space, WriteInfo.Space, WriteInfo.Space, WriteInfo.Space,
                    new WriteInfo($"[{i + 1}]", updateColor),
                    WriteInfo.Space,
                    GetDirectoryInfo(syncFiles[i].SourceDirectory),
                    new WriteInfo(" > ", updateColor),
                    GetDirectoryInfo(syncFiles[i].TargetDirectory),
                    new WriteInfo(" : ", updateColor),
                    new WriteInfo(syncFiles[i].RelativePath, updateColor)
                );
            }
            WriteLine(new WriteInfo("]"));

            WriteLine(
                new WriteInfo($"Copy: {syncFiles.Count(s => s.SyncType == SyncType.Copy)}", ConsoleColor.Red),
                WriteInfo.Space, WriteInfo.Space, WriteInfo.Space, WriteInfo.Space,
                new WriteInfo($"Update: {syncFiles.Count(s => s.SyncType == SyncType.Update)}", ConsoleColor.Green));

            static WriteInfo GetDirectoryInfo(string directory)
            {
                string dirSymbol;
                ConsoleColor color;

                if (directory == Syncer.Default.SourceDirectory )
                {
                    dirSymbol = "{A}";
                    color = ConsoleColor.DarkCyan;
                }
                else if (directory ==  Syncer.Default.TargetDirectory)
                {
                    dirSymbol = "{B}";
                    color = ConsoleColor.Yellow;
                }
                else
                {
                    throw new ArgumentException($"The value of directory should be {Syncer.Default.SourceDirectory } or { Syncer.Default.TargetDirectory}.");
                }

                return new WriteInfo(dirSymbol, color);
            }
        }
    }

    private static void Write(params WriteInfo[] writeInfos)
    {
        foreach (var item in writeInfos)
        {
            if (item.ForegroundColor.HasValue)
            {
                var preColor = Console.ForegroundColor;
                Console.ForegroundColor = item.ForegroundColor.Value;
                Console.Write(item.Text);
                Console.ForegroundColor = preColor;
            }
            else
            {
                Console.Write(item.Text);
            }
        }
    }

    private static void WriteLine(params WriteInfo[] writeInfos)
    {
        Write(writeInfos);
        Console.WriteLine();
    }
}