using System;
using System.Linq;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using System.Xml;
using System.Collections.Generic;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using System.Windows.Forms;

namespace XMLFormatter
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class FormatCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Path of the currently open file
        /// </summary>
        private string pathOfCurrentFile;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("8da6cd96-e6c5-4956-9f47-9e587f25711b");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormatCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private FormatCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static FormatCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in FormatCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new FormatCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            XmlDocument xmlDoc = GetCurrentDocumentAsXML();

            if (xmlDoc == null)
            {
                return;
            }

            XmlNode currentNode = xmlDoc.FirstChild;
            IterateChildrenNodesRecur(xmlDoc.FirstChild);

            xmlDoc.Save(pathOfCurrentFile);
        }

        private void IterateChildrenNodesRecur(XmlNode currentNode)
        {
            if (currentNode.NodeType == XmlNodeType.Comment || currentNode.Attributes.Count == 0)
            {
                //Pass children nodes
                for (int i = 0; i < currentNode.ChildNodes.Count; i++)
                {
                    IterateChildrenNodesRecur(currentNode.ChildNodes.Item(i));
                }
            }
            else
            {
                //Sort current node
                SortCurrentNodeAttributes(currentNode);

                //Pass children nodes
                for (int i = 0; i < currentNode.ChildNodes.Count; i++)
                {
                    IterateChildrenNodesRecur(currentNode.ChildNodes.Item(i));
                }
            }
        }

        private void SortCurrentNodeAttributes(XmlNode currentNode)
        {
            XmlAttribute[] allAttributesArr = new XmlAttribute[currentNode.Attributes.Count];
            currentNode.Attributes.CopyTo(allAttributesArr, 0);

            List<XmlAttribute> unknownAttributesList = new List<XmlAttribute>();
            List<XmlAttribute> knownAttributesList = new List<XmlAttribute>();

            List<string> attributesList = ((this.package as FormatCommandPackage).AttributesList).ToList();

            for (int i = 0; i < allAttributesArr.Length; i++)
            {
                if (attributesList.Exists(a => a == allAttributesArr[i].Name))
                {
                    knownAttributesList.Add(allAttributesArr[i]);
                }
                else
                {
                    unknownAttributesList.Add(allAttributesArr[i]);
                }
            }

            knownAttributesList = knownAttributesList.OrderBy(a => attributesList.IndexOf(a.Name)).ToList();
            //Unknown attributes are sorted alphabetically
            unknownAttributesList.Sort((x, y) => string.Compare(x.Name, y.Name));

            for (int i = 0; i < knownAttributesList.Count; i++)
            {
                currentNode.Attributes.Append(knownAttributesList[i]);
            }

            for (int i = 0; i < unknownAttributesList.Count; i++)
            {
                currentNode.Attributes.Append(unknownAttributesList[i]);
            }
        }

        /// <summary>
        /// Returns current opened document as an XML reference. 
        /// </summary>
        /// <returns>Returns null if either no documents are open or current document is not XML.</returns>
        private XmlDocument GetCurrentDocumentAsXML()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            Document doc = dte.ActiveDocument;

            if (doc == null)
            {
                MessageBox.Show("No active document", "XMLFormatter", MessageBoxButtons.OK);
                return null;
            }

            if (doc.Language == "XML")
            {
                XmlDocument xml = new XmlDocument();
                pathOfCurrentFile = doc.FullName;
                xml.Load(pathOfCurrentFile);
                return xml;
            }
            else
            {
                MessageBox.Show("Current document is not an XML type", "XMLFormatter", MessageBoxButtons.OK);
                return null;
            }

        }

    }
}
