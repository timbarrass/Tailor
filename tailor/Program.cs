using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailBlazer.Domain.FileHandling;

namespace tailor
{
    class Program
    {
        struct EntryData
        {
            public string RootPath;

            public string File;

            public Line Entry;
        }

        static void Main(string[] args)
        {
            var files = args;

            var padlock = new object();
            var buffer = new Dictionary<string, EntryData>();
            var changes = new Dictionary<string, object>();
            var latest = new Dictionary<string, int>();

            // Start a watcher for each file
            var tf = new TaskFactory();
            foreach (var file in files)
            {
                tf.StartNew(() =>
                {
                    var info = new FileInfo(file);

                    var predicate = new Func<string, bool>(s => { return true; });
                    var search = info.Search(predicate);

                    var sub = search
                        .DistinctUntilChanged()
                        .Subscribe(x =>
                        {
                            foreach (var line in x.ReadLines(new ScrollRequest(1)))
                            {
                                // TODO: note, actually want to lock around all of these -- needs to be consistent state
                                lock (padlock)
                                {
                                    if (!latest.ContainsKey(file) || (latest.ContainsKey(file) && latest[file] != line.Number))
                                    {
                                        buffer[file] = new EntryData { RootPath = info.DirectoryName, File = info.Name, Entry = line };
                                        latest[file] = line.Number;
                                        changes[file] = null;
                                    }
                                }
                            }
                        });
                }); // TODO: dispose on cancellation                
            }

            // Handle screen refresh
            var pathTrimLength = 20;
            var filenameTrimLength = 25;

            Observable.Interval(new TimeSpan(0, 0, 1))
                .Subscribe(x =>
                {
                    Console.Clear();
                    Console.WriteLine("{0} {1} {2}", "Root".PadRight(pathTrimLength), "File".PadRight(filenameTrimLength), "Entry");
                    var entryTrimLength = Console.WindowWidth - filenameTrimLength - pathTrimLength - 6; // 2 spaces, 3 ellipsis

                    foreach (var file in files)
                    {
                        string trimmedPath;
                        string trimmedFile;
                        string trimmedEntry;
                        bool changed;
                        lock (padlock)
                        { 
                            var data = buffer[file];
                            trimmedPath = data.RootPath.TrimWithEllipsis(pathTrimLength);
                            trimmedFile = data.File.TrimWithEllipsis(filenameTrimLength);
                            trimmedEntry = data.Entry.Text.TrimWithEllipsis(entryTrimLength);

                            changed = changes.ContainsKey(file);
                            changes.Remove(file);
                        }


                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("{0} {1} ", trimmedPath, trimmedFile);
                        Console.ForegroundColor = ConsoleColor.White;

                        if (changed)
                        {
                            Console.BackgroundColor = ConsoleColor.Yellow;
                            Console.ForegroundColor = ConsoleColor.Black;
                            
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        Console.WriteLine("{0}", trimmedEntry);

                        Console.ResetColor();
                    }
                });
                

            Console.ReadLine();
        }
    }
}
