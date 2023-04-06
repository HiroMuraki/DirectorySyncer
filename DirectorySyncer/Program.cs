using System.CommandLine;
using System.Data;

namespace DirectorySyncer;

internal class Program
{
    private enum Mode
    {
        Dual,
        SourceToTarget
    }

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

    private static string _directoryA = string.Empty;
    private static string _directoryB = string.Empty;
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

        var rootCommand = new RootCommand()
        {
            sourceDirectory,
            targetDirectory,
            mode,
        };

        rootCommand.SetHandler(RootCommandHandler, sourceDirectory, targetDirectory, mode);

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

    private static async Task RootCommandHandler(string sourceDirectory, string targetDirectory, Mode mode)
    {
        _directoryA = sourceDirectory;
        _directoryB = targetDirectory;

        SyncFile[] syncFiles;
        switch (mode)
        {
            case Mode.Dual:
                syncFiles = Syncer.Default.GetDualSyncFiles(_directoryA, _directoryB).OrderBy(s => s.SyncMode).ToArray();
                break;
            case Mode.SourceToTarget:
                syncFiles = Syncer.Default.GetSyncFiles(_directoryA, _directoryB).OrderBy(s => s.SyncMode).ToArray();
                break;
            default:
                throw new ArgumentException($"Invalid sync mode {mode}");
        }

        PrintSyncingInfo(syncFiles);
        WriteLine();

        if (!syncFiles.Any())
        {
            WriteLine(new WriteInfo("All files are already synced, nothing to be synced.", ConsoleColor.Green));
            return;
        }

        if (PromptConfirmation())
        {
            WriteLine(new WriteInfo("[Syncing started]", ConsoleColor.Green));
            try
            {
                await Syncer.Default.SyncFilesAsync(syncFiles);
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

        static void PrintSyncingInfo(SyncFile[] syncFiles)
        {
            WriteLine(GetDirectoryInfo(_directoryA), new WriteInfo(": ", ConsoleColor.DarkCyan), new WriteInfo(_directoryA, ConsoleColor.DarkCyan));
            WriteLine(GetDirectoryInfo(_directoryB), new WriteInfo(": ", ConsoleColor.Yellow), new WriteInfo(_directoryB, ConsoleColor.Yellow));

            WriteLine(new WriteInfo("["));
            for (int i = 0; i < syncFiles.Length; i++)
            {
                var updateColor = syncFiles[i].SyncMode switch
                {
                    SyncMode.Copy => ConsoleColor.Red,
                    SyncMode.Update => ConsoleColor.Green,
                    _ => throw new ArgumentException($"Invalid sync mode {syncFiles[i].SyncMode}"),
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
                new WriteInfo($"Copy: {syncFiles.Count(s => s.SyncMode == SyncMode.Copy)}", ConsoleColor.Red),
                WriteInfo.Space, WriteInfo.Space, WriteInfo.Space, WriteInfo.Space,
                new WriteInfo($"Update: {syncFiles.Count(s => s.SyncMode == SyncMode.Update)}", ConsoleColor.Green));

            static WriteInfo GetDirectoryInfo(string directory)
            {
                string dirSymbol;
                ConsoleColor color;

                if (directory == _directoryA)
                {
                    dirSymbol = "{A}";
                    color = ConsoleColor.DarkCyan;
                }
                else if (directory == _directoryB)
                {
                    dirSymbol = "{B}";
                    color = ConsoleColor.Yellow;
                }
                else
                {
                    throw new ArgumentException($"The value of directory should be {_directoryA} or {_directoryB}.");
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