using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Settings;
using System.Linq;

namespace XMLFormatter
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(FormatCommandPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideOptionPage(typeof(OptionPageGrid),
    "XMLFormatter", "Known Attributes", 0, 0, true)]
    public sealed class FormatCommandPackage : AsyncPackage
    {
        /// <summary>
        /// FormatCommandPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "579ddaa9-78d5-4f53-96d3-d039a266f25c";

        const string CollectionPath = "Attributes";
        const string PropertyName = "AllAttributes";
        WritableSettingsStore userSettingsStore;

        public FormatCommandPackage() { }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await FormatCommand.InitializeAsync(this);

            //Read settings
            SettingsManager settingsManager = new ShellSettingsManager(this as System.IServiceProvider);
            userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            //Load settings on init
            LoadSettings();

            //Save settings on close
            ((OptionPageGrid)GetDialogPage(typeof(OptionPageGrid))).onClose = SaveSettings;
            //Load settings on open
            ((OptionPageGrid)GetDialogPage(typeof(OptionPageGrid))).onOpen = LoadSettings;
        }

        public String[] AttributesList
        {
            set
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                page.AttributesList = value;
            }
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.AttributesList;
            }
        }

        void LoadSettings()
        {
            try
            {
                if (userSettingsStore.PropertyExists(CollectionPath, PropertyName))
                {
                    string value = userSettingsStore.GetString(CollectionPath, PropertyName);
                    AttributesList = value.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        void SaveSettings()
        {
            try
            {
                if (!userSettingsStore.CollectionExists(CollectionPath))
                {
                    userSettingsStore.CreateCollection(CollectionPath);
                }

                string value = string.Join(Environment.NewLine, AttributesList);
                userSettingsStore.SetString(CollectionPath, PropertyName, value);
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }
    }

    public class OptionPageGrid : DialogPage
    {
        private String[] attributesList = new String[0];
        public Action onClose;
        public Action onOpen;

        [Category("Attributes List")]
        [DisplayName("Attributes List")]
        [Description("Attributes list for the formatter. Attributes will be sorted by this priority. Not found attributes will be appended to the end and will be sorted alphabetically.")]
        public String[] AttributesList
        {
            get { return attributesList; }
            set { attributesList = value; }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            onClose();
        }

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            onOpen();
        }
    }
}
