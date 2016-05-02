using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using MediaPortal.GUI.Library;
using BDInfo;

namespace MediaPortal.Plugins.BDHandler
{
    public class BDInfo : BDROM
    {

        public string Title = string.Empty;

        public BDInfo(string path)
            : base(path)
        {

        }

        public new void Scan() {
            
            // perform the BDInfo scan
            base.Scan();

            // get the bd title from the meta xml
            string metaFilePath = Path.Combine(base.DirectoryBDMV.FullName, @"META\DL\bdmt_eng.xml");
            if (!File.Exists(metaFilePath))
                return;

            try
            {
                XPathDocument metaXML = new XPathDocument(metaFilePath);
                XPathNavigator navigator = metaXML.CreateNavigator();
                XmlNamespaceManager ns = new XmlNamespaceManager(navigator.NameTable);
                ns.AddNamespace("", "urn:BDA:bdmv;disclib");
                ns.AddNamespace("di", "urn:BDA:bdmv;discinfo");
                navigator.MoveToFirst();
                XPathNavigator node = navigator.SelectSingleNode("//di:discinfo/di:title/di:name", ns);
                string title = node.ToString().Trim();
                if (title != string.Empty)
                {
                    Title = title;
                    BDHandlerCore.LogDebug("Bluray Metafile='{0}', Title= '{1}'", metaFilePath, title);
                }
                else
                {
                    BDHandlerCore.LogDebug("Bluray Metafile='{0}': No Title Found.", metaFilePath);
                }
            }
            catch (Exception e)
            {
                BDHandlerCore.LogError("Meta File Error: ", e);
            }

        }

    }
}
