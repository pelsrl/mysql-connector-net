// Copyright (c) 2004, 2022, Oracle and/or its affiliates.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License, version 2.0, as
// published by the Free Software Foundation.
//
// This program is also distributed with certain software (including
// but not limited to OpenSSL) that is licensed under separate terms,
// as designated in a particular file or component or in included license
// documentation.  The authors of MySQL hereby grant you an
// additional permission to link the program and your derivative works
// with the separately licensed software that they have included with
// MySQL.
//
// Without limiting anything contained in the foregoing, this file,
// which is part of MySQL Connector/NET, is also subject to the
// Universal FOSS Exception, version 1.0, a copy of which can be found at
// http://oss.oracle.com/licenses/universal-foss-exception.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License, version 2.0, for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.Security.Permissions;
using System.Xml;

namespace MySql.Data.MySqlClient
{
  /// <summary>
  /// We are adding a custom installer class to our assembly so our installer
  /// can make proper changes to the machine.config file.
  /// </summary>
  [RunInstaller(true)]
  [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
  [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
  public class CustomInstaller : Installer
  {
    /// <summary>
    /// We override Install so we can add our assembly to the proper
    /// machine.config files.
    /// </summary>
    /// <param name="stateSaver"></param>
    public override void Install(System.Collections.IDictionary stateSaver)
    {
      base.Install(stateSaver);
      AddProviderToMachineConfig();
    }

    private static void AddProviderToMachineConfig()
    {
      object installRoot = Registry.GetValue(
        @"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework\",
        "InstallRoot", null);
      if (installRoot == null)
        throw new Exception("Unable to retrieve install root for .NET framework");
      UpdateMachineConfigs(installRoot.ToString(), true);

      string installRoot64 = installRoot.ToString();
      installRoot64 = installRoot64.Substring(0, installRoot64.Length - 1);
      installRoot64 = string.Format("{0}64{1}", installRoot64,
        Path.DirectorySeparatorChar);
      if (Directory.Exists(installRoot64))
        UpdateMachineConfigs(installRoot64, true);
    }

    internal static void UpdateMachineConfigs(string rootPath, bool add)
    {
      string[] dirs = new string[2] { "v2.0.50727", "v4.0.30319" };
      foreach (string frameworkDir in dirs)
      {
        string path = rootPath + frameworkDir;

        string configPath = String.Format(@"{0}\CONFIG", path);
        if (Directory.Exists(configPath))
        {
          if (add)
            AddProviderToMachineConfigInDir(configPath);
          else
            RemoveProviderFromMachineConfigInDir(configPath);
        }
      }
    }

    private static XmlElement CreateNodeAssemblyBindingRedirection(XmlElement mysqlNode, XmlDocument doc, string oldVersion, string newVersion)
    {
      if (doc == null || mysqlNode == null)
        return null;

      XmlElement dA;
      XmlElement aI;
      XmlElement bR;

      string ns = "urn:schemas-microsoft-com:asm.v1";

      //mysql.data
      dA = (XmlElement)doc.CreateNode(XmlNodeType.Element, "dependentAssembly", ns);
      aI = (XmlElement)doc.CreateNode(XmlNodeType.Element, "assemblyIdentity", ns);
      aI.SetAttribute("name", "MySql.Data");
      aI.SetAttribute("publicKeyToken", "c5687fc88969c44d");
      aI.SetAttribute("culture", "neutral");

      bR = (XmlElement)doc.CreateNode(XmlNodeType.Element, "bindingRedirect", ns);
      bR.SetAttribute("oldVersion", oldVersion);
      bR.SetAttribute("newVersion", newVersion);
      dA.AppendChild(aI);
      dA.AppendChild(bR);
      mysqlNode.AppendChild(dA);

      //mysql.data.entityframework
      dA = (XmlElement)doc.CreateNode(XmlNodeType.Element, "dependentAssembly", ns);
      aI = (XmlElement)doc.CreateNode(XmlNodeType.Element, "assemblyIdentity", ns);
      aI.SetAttribute("name", "MySql.Data.EntityFramework");
      aI.SetAttribute("publicKeyToken", "c5687fc88969c44d");
      aI.SetAttribute("culture", "neutral");

      bR = (XmlElement)doc.CreateNode(XmlNodeType.Element, "bindingRedirect", ns);
      bR.SetAttribute("oldVersion", oldVersion);
      bR.SetAttribute("newVersion", newVersion);
      dA.AppendChild(aI);
      dA.AppendChild(bR);
      mysqlNode.AppendChild(dA);

      //mysql.web

      dA = (XmlElement)doc.CreateNode(XmlNodeType.Element, "dependentAssembly", ns);
      aI = (XmlElement)doc.CreateNode(XmlNodeType.Element, "assemblyIdentity", ns);
      aI.SetAttribute("name", "MySql.Web");
      aI.SetAttribute("publicKeyToken", "c5687fc88969c44d");
      aI.SetAttribute("culture", "neutral");

      bR = (XmlElement)doc.CreateNode(XmlNodeType.Element, "bindingRedirect", ns);
      bR.SetAttribute("oldVersion", oldVersion);
      bR.SetAttribute("newVersion", newVersion);
      dA.AppendChild(aI);
      dA.AppendChild(bR);
      mysqlNode.AppendChild(dA);

      return mysqlNode;
    }

    private static void AddProviderToMachineConfigInDir(string path)
    {
      string configFile = String.Format(@"{0}\machine.config", path);
      if (!File.Exists(configFile)) return;

      // now read the config file into memory
      StreamReader sr = new StreamReader(configFile);
      string configXML = sr.ReadToEnd();
      sr.Close();

      // load the XML into the XmlDocument
      XmlDocument doc = new XmlDocument();
      doc.LoadXml(configXML);

      doc = RemoveOldBindingRedirection(doc);

      // create our new node
      XmlElement newNode = (XmlElement)doc.CreateNode(XmlNodeType.Element, "add", "");

      // add the proper attributes
      newNode.SetAttribute("name", "MySQL Data Provider");
      newNode.SetAttribute("invariant", "MySql.Data.MySqlClient");
      newNode.SetAttribute("description", ".Net Framework Data Provider for MySQL");

      // add the type attribute by reflecting on the executing assembly
      Assembly a = Assembly.GetExecutingAssembly();
      string type = String.Format("MySql.Data.MySqlClient.MySqlClientFactory, {0}", a.FullName.Replace("Installers", "Data"));
      newNode.SetAttribute("type", type);

      XmlNodeList nodes = doc.GetElementsByTagName("DbProviderFactories");

      foreach (XmlNode node in nodes[0].ChildNodes)
      {
        if (node.Attributes == null) continue;
        foreach (XmlAttribute attr in node.Attributes)
        {
          if (attr.Name == "invariant" && attr.Value == "MySql.Data.MySqlClient")
          {
            nodes[0].RemoveChild(node);
            break;
          }
        }
      }

      nodes[0].AppendChild(newNode);

      try
      {
        XmlElement mysqlNode;

        //add binding redirection to our assemblies
        if (doc.GetElementsByTagName("assemblyBinding").Count == 0)
        {
          mysqlNode = (XmlElement)doc.CreateNode(XmlNodeType.Element, "assemblyBinding", "");
          mysqlNode.SetAttribute("xmlns", "urn:schemas-microsoft-com:asm.v1");
        }
        else
        {
          mysqlNode = (XmlElement)doc.GetElementsByTagName("assemblyBinding")[0];
        }

        string newVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        mysqlNode = CreateNodeAssemblyBindingRedirection(mysqlNode, doc, "8.0.30", newVersion);

        XmlNodeList runtimeNode = doc.GetElementsByTagName("runtime");
        runtimeNode[0].AppendChild(mysqlNode);
      }
      catch { }

      // Save the document to a file and auto-indent the output.
      XmlTextWriter writer = new XmlTextWriter(configFile, null);
      writer.Formatting = Formatting.Indented;
      doc.Save(writer);
      writer.Flush();
      writer.Close();
    }

    private static XmlDocument RemoveOldBindingRedirection(XmlDocument doc)
    {
      if (doc.GetElementsByTagName("assemblyBinding").Count == 0) return doc;

      XmlNodeList nodesDependantAssembly = doc.GetElementsByTagName("assemblyBinding")[0].ChildNodes;
      if (nodesDependantAssembly != null)
      {
        int nodesCount = nodesDependantAssembly.Count;
        for (int i = 0; i < nodesCount; i++)
        {
          if (nodesDependantAssembly[0].ChildNodes[0].Attributes[0].Name == "name"
             &&
             nodesDependantAssembly[0].ChildNodes[0].Attributes[0].Value.Contains("MySql"))
          {
            doc.GetElementsByTagName("assemblyBinding")[0].RemoveChild(nodesDependantAssembly[0]);
          }
        }
      }
      return doc;
    }

    /// <summary>
    /// We override Uninstall so we can remove out assembly from the
    /// machine.config files.
    /// </summary>
    /// <param name="savedState"></param>
    public override void Uninstall(System.Collections.IDictionary savedState)
    {
      base.Uninstall(savedState);
      RemoveProviderFromMachineConfig();
    }

    private static void RemoveProviderFromMachineConfig()
    {
      object installRoot = Registry.GetValue(
        @"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework\",
        "InstallRoot", null);
      if (installRoot == null)
        throw new Exception("Unable to retrieve install root for .NET framework");
      UpdateMachineConfigs(installRoot.ToString(), false);

      string installRoot64 = installRoot.ToString();
      installRoot64 = installRoot64.Substring(0, installRoot64.Length - 1);
      installRoot64 = string.Format("{0}64{1}", installRoot64,
        Path.DirectorySeparatorChar);
      if (Directory.Exists(installRoot64))
        UpdateMachineConfigs(installRoot64, false);
    }

    private static void RemoveProviderFromMachineConfigInDir(string path)
    {
      string configFile = String.Format(@"{0}\machine.config", path);
      if (!File.Exists(configFile)) return;

      // now read the config file into memory
      StreamReader sr = new StreamReader(configFile);
      string configXML = sr.ReadToEnd();
      sr.Close();

      // load the XML into the XmlDocument
      XmlDocument doc = new XmlDocument();
      doc.LoadXml(configXML);

      XmlNodeList nodes = doc.GetElementsByTagName("DbProviderFactories");
      foreach (XmlNode node in nodes[0].ChildNodes)
      {
        if (node.Attributes == null) continue;
        string name = node.Attributes["name"].Value;
        if (name == "MySQL Data Provider")
        {
          nodes[0].RemoveChild(node);
          break;
        }
      }

      try
      {
        doc = RemoveOldBindingRedirection(doc);
      }
      catch { }

      // Save the document to a file and auto-indent the output.
      XmlTextWriter writer = new XmlTextWriter(configFile, null);
      writer.Formatting = Formatting.Indented;
      doc.Save(writer);
      writer.Flush();
      writer.Close();
    }
  }
}