using Hpdi.VssLogicalLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Hpdi.Vss2Git
{
    public class Vss2GitService
    {
        private Logger logger;
        private Encoding encoding;
        private string logfile;
        private readonly WorkQueue workQueue = new WorkQueue(1);
        private bool transcode;
        private string vssDirectory;
        private IEnumerable<string> vssProjects;
        private RevisionAnalyzer revisionAnalyzer;
        private string excludes;
        private ChangesetBuilder changesetBuilder;
        private double combineAnyComment;
        private double combineSameComment;
        private string outputDirectory;
        private string emailDomain;

        public Vss2GitService(string vssDirectory, IEnumerable<string> vssProjects, string outputDirectory, string logfile, string excludes, Encoding encoding, bool transcode, double combineAnyComment, double combineSameComment, string emailDomain)
        {
            this.vssDirectory = vssDirectory;
            this.vssProjects = vssProjects;
            this.outputDirectory = outputDirectory;
            this.logfile = logfile;
            this.excludes = excludes;
            this.encoding = encoding;
            this.transcode = transcode;
            this.combineAnyComment = combineAnyComment;
            this.combineSameComment = combineSameComment;
            this.emailDomain = emailDomain;
        }

        public void StartMigration()
        {
            try
            {
                OpenLog(logfile);

                logger.WriteLine("VSS encoding: {0} (CP: {1}, IANA: {2})", encoding.EncodingName, encoding.CodePage, encoding.WebName);
                logger.WriteLine("Comment transcoding: {0}", transcode ? "enabled" : "disabled");

                var df = new VssDatabaseFactory(vssDirectory);
                df.Encoding = encoding;
                var db = df.Open();

                revisionAnalyzer = new RevisionAnalyzer(workQueue, logger, db);

                foreach (var itemPath in vssProjects)
                {
                    VssItem item;
                    try
                    {
                        item = db.GetItem(itemPath);
                    }
                    catch (VssPathException ex)
                    {
                        logger.WriteLine(ex.Message);
                        throw;
                    }

                    var project = item as VssProject;
                    if (project == null)
                    {
                        var msg = itemPath + " is not a project";
                        logger.WriteLine(msg);
                        throw new Exception(msg);
                    }


                    if (!string.IsNullOrEmpty(excludes))
                    {
                        revisionAnalyzer.ExcludeFiles = excludes;
                    }
                    revisionAnalyzer.AddItem(project);
                }

                changesetBuilder = new ChangesetBuilder(workQueue, logger, revisionAnalyzer);
                changesetBuilder.AnyCommentThreshold = TimeSpan.FromSeconds(combineAnyComment);
                changesetBuilder.SameCommentThreshold = TimeSpan.FromSeconds(combineSameComment);
                changesetBuilder.BuildChangesets();

                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    var gitExporter = new GitExporter(workQueue, logger,
                        revisionAnalyzer, changesetBuilder);
                    if (!string.IsNullOrEmpty(emailDomain))
                    {
                        gitExporter.EmailDomain = emailDomain;
                    }
                    if (!transcode)
                    {
                        gitExporter.CommitEncoding = encoding;
                    }
                    gitExporter.ExportToGit(outputDirectory);
                }

                workQueue.Idle += delegate
                {
                    logger.Dispose();
                    logger = Logger.Null;
                };

            }
            catch (Exception ex)
            {
                logger.WriteLine(ex);
                throw;
            }
        }

        public string Version { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); } }

        public bool IsRunning
        {
            get
            {
                return !workQueue.IsIdle;
            }
        }

        public void GetCurrentState(out string status, out string time, out string files, out string revisions, out string changesets, out string exceptions)
        {
            status = string.Empty;
            time = string.Empty;
            files = string.Empty;
            revisions = string.Empty;
            changesets = string.Empty;
            exceptions = string.Empty;

            status = workQueue.LastStatus ?? "Idle";
            time = string.Format("Elapsed: {0:HH:mm:ss}", new DateTime(workQueue.ActiveTime.Ticks));

            if (revisionAnalyzer != null)
            {
                files = "Files: " + revisionAnalyzer.FileCount;
                revisions = "Revisions: " + revisionAnalyzer.RevisionCount;
            }

            if (changesetBuilder != null)
            {
                changesets = "Changesets: " + changesetBuilder.Changesets.Count;
            }

            var fetchedExceptions = workQueue.FetchExceptions();
            if (fetchedExceptions != null)
            {
                var sb = new StringBuilder();
                foreach (var exception in fetchedExceptions)
                {
                    string msg;
                    LogException(exception, out msg);
                    sb.AppendLine(msg);
                }
                exceptions = sb.ToString();
            }
        }

        private void LogException(Exception exception, out string message)
        {
            message = ExceptionFormatter.Format(exception);
            logger.WriteLine("ERROR: {0}", message);
            logger.WriteLine(exception);
        }

        public void CancelMigration()
        {
            workQueue.Abort();
            workQueue.WaitIdle();
        }

        private void OpenLog(string filename)
        {
            logger = string.IsNullOrEmpty(filename) ? Logger.Null : new Logger(filename);
        }
    }
}
