using Fclp;
using Hpdi.Vss2Git;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vss2Git
{
    class Program
    {
        private static string vssPath = string.Empty;
        private static string vssProjects = string.Empty;
        private static string repoPath = string.Empty;
        private static string logPath = string.Empty;
        private static string excludes = string.Empty;
        private static Encoding encoding = Encoding.Default;
        private static bool transcode = true;
        private static double combineAnyComment = 30d;
        private static double combineSameComment = 600d;
        private static string emailDomain = string.Empty;
        private static bool noUserInteraction = false;


        static void Main(string[] args)
        {


            var parser = new FluentCommandLineParser();
            parser.Setup<string>("vssPath").Callback(p => vssPath = p).Required();
            parser.Setup<string>("vssProjects").Callback(p => vssProjects = p).Required();
            parser.Setup<string>("repoPath").Callback(p => repoPath = p).Required();
            parser.Setup<string>("logPath").Callback(p => logPath = p).Required();
            parser.Setup<string>("encoding").Callback(p =>
            {
                try
                {
                    var enc = Encoding.GetEncoding(p);
                    if (enc != null)
                    {
                        encoding = enc;
                    }
                }
                catch (Exception) { }
            });
            parser.Setup<string>("excludes").Callback(p => excludes = p);
            parser.Setup<bool>("transcode").Callback(p => transcode = p);
            parser.Setup<double>("combineAnyComment").Callback(p => combineAnyComment = p);
            parser.Setup<double>("combineSameComment").Callback(p => combineSameComment = p);
            parser.Setup<string>("emailDomain").Callback(p => emailDomain = p).Required();
            parser.Setup<bool>("noUserInteraction").Callback(p => noUserInteraction = p);

            parser.Parse(args);

            var service = new Vss2GitService(vssPath,
                    vssProjects.Split(';'),
                    repoPath,
                    logPath,
                    excludes,
                    encoding,
                    transcode,
                    combineAnyComment,
                    combineSameComment,
                    emailDomain
                    );
            UserFeedbackService.Current.HandleUserFeedback += Current_HandleUserFeedback;

            Console.WriteLine("Starting VSS to Git Migration");
            Console.WriteLine("*****************************");

            Console.WriteLine();
            Console.WriteLine($"Vss Path: {vssPath}");
            Console.WriteLine($"Vss Projects: {vssProjects}");
            Console.WriteLine($"Repo Path: {repoPath}");
            Console.WriteLine($"Log Path: {logPath}");
            Console.WriteLine($"Excludes: {excludes}");
            Console.WriteLine($"Encoding: {encoding.ToString()}");
            Console.WriteLine($"Transcode: {transcode}");
            Console.WriteLine($"Combine any comments within: {combineAnyComment}");
            Console.WriteLine($"Combine same comments within: {combineSameComment}");
            Console.WriteLine($"No user interaction: {noUserInteraction}");
            Console.WriteLine();

            var t1 = Task.Run(() =>
            {
                service.StartMigration();
                while (service.IsRunning)
                {
                    Thread.Sleep(1000); // todo implement correct async
                }
            });
            var t2 = Task.Run(() =>
            {
                Thread.Sleep(1000);
                while (service.IsRunning)
                {
                    ShowStatus(service);
                    Thread.Sleep(100);
                }
            });
            Task.WaitAll(new Task[] { t1, t2 });

            Console.WriteLine("VSS to Git Mîgration finished:");
            ShowStatus(service);
        }

        private static void ShowStatus(Vss2GitService service)
        {
            string status, time, files, revisions, changesets, exceptions = string.Empty;
            service.GetCurrentState(out status, out time, out files, out revisions, out changesets, out exceptions);
            int left = Console.CursorLeft;
            Console.Write($"{status}, {time}, {files}, {revisions}, {changesets}");
            Console.CursorLeft = left;
            if (!string.IsNullOrWhiteSpace(exceptions))
            {
                Console.WriteLine();
                Console.WriteLine(exceptions);
                Console.WriteLine();
            }
        }

        private static void Current_HandleUserFeedback(object sender, UserFeedbackEventArgs args)
        {
            if (!noUserInteraction)
            {
                Console.WriteLine();
                Console.WriteLine(args.Message);
                switch (args.Options)
                {
                    case FeedbackOptions.AbortRetryIgnore:
                        HandleUserFeedback("[A]bort, [R]etry, [I]gnore", args);
                        break;
                    case FeedbackOptions.OK:
                        HandleUserFeedback("[O]k", args);
                        break;
                    case FeedbackOptions.OKCancel:
                        HandleUserFeedback("[O]k, [C]ancel", args);
                        break;
                    case FeedbackOptions.RetryCancel:
                        HandleUserFeedback("[R]etry, [C]ancel", args);
                        break;
                    case FeedbackOptions.YesNo:
                        HandleUserFeedback("[Y]es, [N]o", args);
                        break;
                    case FeedbackOptions.YesNoCancel:
                        HandleUserFeedback("[Y]es, [N]o, [C]ancel", args);
                        break;
                }
                Console.WriteLine();
            }
            else
            {
                switch (args.Options)
                {
                    case FeedbackOptions.AbortRetryIgnore:
                        args.FeedbackResult = FeedbackResult.Ignore;
                        break;
                    case FeedbackOptions.OK:
                        args.FeedbackResult = FeedbackResult.OK;
                        break;
                    case FeedbackOptions.OKCancel:
                        args.FeedbackResult = FeedbackResult.OK;
                        break;
                    case FeedbackOptions.RetryCancel:
                        args.FeedbackResult = FeedbackResult.Cancel;
                        break;
                    case FeedbackOptions.YesNo:
                        args.FeedbackResult = FeedbackResult.Yes;
                        break;
                    case FeedbackOptions.YesNoCancel:
                        args.FeedbackResult = FeedbackResult.Yes;
                        break;
                }
            }

        }

        private static void HandleUserFeedback(string msg, UserFeedbackEventArgs args)
        {
            Console.WriteLine(msg);
            var result = Console.ReadLine();
            while (!msg.Contains("[" + result + "]"))
            {
                Console.WriteLine(msg);
            }
            switch (result)
            {
                case "A":
                case "a":
                    args.FeedbackResult = FeedbackResult.Abort;
                    break;
                case "R":
                case "r":
                    args.FeedbackResult = FeedbackResult.Retry;
                    break;
                case "I":
                case "i":
                    args.FeedbackResult = FeedbackResult.Ignore;
                    break;
                case "O":
                case "o":
                    args.FeedbackResult = FeedbackResult.OK;
                    break;
                case "C":
                case "c":
                    args.FeedbackResult = FeedbackResult.Cancel;
                    break;
                case "Y":
                case "y":
                    args.FeedbackResult = FeedbackResult.Yes;
                    break;
                case "N":
                case "n":
                    args.FeedbackResult = FeedbackResult.No;
                    break;
            }
        }
    }
}
