/* Copyright 2009 HPDI, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Main form for the application.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public partial class MainForm : Form
    {
        private readonly Dictionary<int, EncodingInfo> codePages = new Dictionary<int, EncodingInfo>();
        private Vss2GitService service;

        public MainForm()
        {
            InitializeComponent();
            UserFeedbackService.Current.HandleUserFeedback += Current_HandleUserFeedback;
        }

        private void Current_HandleUserFeedback(object sender, UserFeedbackEventArgs args)
        {

            var result = MessageBox.Show(args.Message, args.Title, (MessageBoxButtons)args.Options, (MessageBoxIcon)args.Icon);
            args.FeedbackResult = (FeedbackResult)result;
        }

        private void goButton_Click(object sender, EventArgs e)
        {
            try
            {
                Encoding encoding = Encoding.Default;
                EncodingInfo encodingInfo;
                if (codePages.TryGetValue(encodingComboBox.SelectedIndex, out encodingInfo))
                {
                    encoding = encodingInfo.GetEncoding();
                }

                service = new Vss2GitService(vssDirTextBox.Text,
                    vssProjectTextBox.Text.Split(';'),
                    outDirTextBox.Text,
                    logTextBox.Text,
                    excludeTextBox.Text,
                    encoding,
                    transcodeCheckBox.Checked,
                    (double)anyCommentUpDown.Value,
                    (double)sameCommentUpDown.Value,
                    domainLabel.Text
                    );

               
                WriteSettings();

                service.StartMigration();
               
                statusTimer.Enabled = true;
                goButton.Enabled = false;
            }
            catch (Exception ex)
            {
                ShowException(ex.Message);
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            if(service != null)
            {
                service.CancelMigration();
            }
        }

        private void statusTimer_Tick(object sender, EventArgs e)
        {
            if(service != null)
            {
                string status = string.Empty;
                string time = string.Empty;
                string files = string.Empty;
                string revisions = string.Empty;
                string changesets = string.Empty;
                string exceptions = string.Empty;

                service.GetCurrentState(out status, out time, out files, out revisions, out changesets, out exceptions);

                statusLabel.Text = status;
                timeLabel.Text = time;
                fileLabel.Text = files;
                revisionLabel.Text = revisions;
                changeLabel.Text = changesets;

                if (!service.IsRunning)
                {
                    statusTimer.Enabled = false;
                    goButton.Enabled = true;
                }
                ShowException(exceptions);
            }
        }

        private void ShowException(string exceptions)
        {
            if (!string.IsNullOrWhiteSpace(exceptions))
            {
                MessageBox.Show(exceptions, "Unhandled Exception",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Text += " " + Assembly.GetExecutingAssembly().GetName().Version;

            var defaultCodePage = Encoding.Default.CodePage;
            var description = string.Format("System default - {0}", Encoding.Default.EncodingName);
            var defaultIndex = encodingComboBox.Items.Add(description);
            encodingComboBox.SelectedIndex = defaultIndex;

            var encodings = Encoding.GetEncodings();
            foreach (var encoding in encodings)
            {
                var codePage = encoding.CodePage;
                description = string.Format("CP{0} - {1}", codePage, encoding.DisplayName);
                var index = encodingComboBox.Items.Add(description);
                codePages[index] = encoding;
                if (codePage == defaultCodePage)
                {
                    codePages[defaultIndex] = encoding;
                }
            }

            ReadSettings();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            WriteSettings();
            if(service != null && service.IsRunning)
            {
                service.CancelMigration();
            }
        }

        private void ReadSettings()
        {
            var settings = Vss2GitUI.Properties.Settings.Default;
            vssDirTextBox.Text = settings.VssDirectory;
            vssProjectTextBox.Text = settings.VssProject;
            excludeTextBox.Text = settings.VssExcludePaths;
            outDirTextBox.Text = settings.GitDirectory;
            domainTextBox.Text = settings.DefaultEmailDomain;
            logTextBox.Text = settings.LogFile;
            transcodeCheckBox.Checked = settings.TranscodeComments;
            forceAnnotatedCheckBox.Checked = settings.ForceAnnotatedTags;
            anyCommentUpDown.Value = settings.AnyCommentSeconds;
            sameCommentUpDown.Value = settings.SameCommentSeconds;
        }

        private void WriteSettings()
        {
            var settings = Vss2GitUI.Properties.Settings.Default;
            settings.VssDirectory = vssDirTextBox.Text;
            settings.VssProject = vssProjectTextBox.Text;
            settings.VssExcludePaths = excludeTextBox.Text;
            settings.GitDirectory = outDirTextBox.Text;
            settings.DefaultEmailDomain = domainTextBox.Text;
            settings.LogFile = logTextBox.Text;
            settings.TranscodeComments = transcodeCheckBox.Checked;
            settings.ForceAnnotatedTags = forceAnnotatedCheckBox.Checked;
            settings.AnyCommentSeconds = (int)anyCommentUpDown.Value;
            settings.SameCommentSeconds = (int)sameCommentUpDown.Value;
            settings.Save();
        }
    }
}
