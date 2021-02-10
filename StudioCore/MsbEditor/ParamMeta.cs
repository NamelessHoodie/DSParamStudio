using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Numerics;
using ImGuiNET;
using SoulsFormats;
using System.Xml;

namespace StudioCore.MsbEditor
{
    public class ParamMetaData
    {
        private static Dictionary<PARAMDEF, ParamMetaData> _ParamMetas = new Dictionary<PARAMDEF, ParamMetaData>();
        
        internal Dictionary<string, ParamEnum> enums = new Dictionary<string, ParamEnum>();

        private const int XML_VERSION = 0;

        /// <summary>
        /// Max value of trailing digits used for offset, +1
        /// </summary>
        public int OffsetSize {get; set;}

        /// <summary>
        /// Provides a reordering of fields for display purposes only
        /// </summary>
        public List<string> AlternateOrder {get; set;}

        public static ParamMetaData Get(PARAMDEF def)
        {
            return _ParamMetas[def];
        }

        private static void Add(PARAMDEF key, ParamMetaData meta)
        {
            _ParamMetas.Add(key, meta);
        }

        private ParamMetaData(PARAMDEF def)
        {
            Add(def, this);
            foreach (PARAMDEF.Field f in def.Fields)
                new FieldMetaData(f);
            // Blank Metadata
        }

        private ParamMetaData(XmlDocument xml, PARAMDEF def)
        {
            XmlNode root = xml.SelectSingleNode("PARAMMETA");
            int xmlVersion = int.Parse(root.Attributes["XmlVersion"].InnerText);
            if (xmlVersion != XML_VERSION)
            {
                throw new InvalidDataException($"Mismatched XML version; current version: {XML_VERSION}, file version: {xmlVersion}");
            }
            Add(def, this);
            
            XmlNode self = root.SelectSingleNode("Self");
            if (self != null)
            {
                XmlAttribute Off = self.Attributes["OffsetSize"];
                if (Off != null)
                {
                    OffsetSize = int.Parse(Off.InnerText);
                }
                XmlAttribute AltOrd = self.Attributes["AlternativeOrder"];
                if (AltOrd != null)
                {
                    AlternateOrder = new List<string>(AltOrd.InnerText.Split(',', StringSplitOptions.RemoveEmptyEntries));
                }
            }

            foreach (XmlNode node in root.SelectNodes("Enums/Enum"))
            {
                ParamEnum en = new ParamEnum(node);
                enums.Add(en.name, en);
            }
 
            Dictionary<string, int> nameCount = new Dictionary<string, int>();
            foreach (PARAMDEF.Field f in def.Fields)
            {
                try
                {
                    string name = Regex.Replace(f.InternalName, @"[^a-zA-Z0-9_]", "");
                    if (Regex.IsMatch(name, $@"^\d"))
                    {
                        name = "_" + name;
                    }
                    int c = nameCount.GetValueOrDefault(name, 0);
                    XmlNodeList nodes = root.SelectNodes($"Field/{name}");
                    //XmlNode pairedNode = root.SelectSingleNode($"Field/{}");
                    XmlNode pairedNode = nodes[c];
                    nameCount[name] = c + 1;

                    if (pairedNode == null)
                    {
                        new FieldMetaData(f);
                        continue;
                    }
                    new FieldMetaData(this, pairedNode, f);
                }
                catch
                {
                    new FieldMetaData(f);
                }
            }
        }

        public static ParamMetaData XmlDeserialize(string path, PARAMDEF def)
        {
            if (!File.Exists(path))
            {
                return new ParamMetaData(def);
            }
            var mxml = new XmlDocument();
            try
            {
                mxml.Load(path);
                return new ParamMetaData(mxml, def);
            }
            catch
            {
                return new ParamMetaData(def);
            }
        }
    }

    public class FieldMetaData
    {
        private static Dictionary<PARAMDEF.Field, FieldMetaData> _FieldMetas = new Dictionary<PARAMDEF.Field, FieldMetaData>();

        /// <summary>
        /// Name of another Param that a Field may refer to.
        /// </summary>
        public List<string> RefTypes { get; set; }

        /// <summary>
        /// Name linking fields from multiple params that may share values.
        /// </summary>
        public string VirtualRef {get; set;}

        /// <summary>
        /// Set of generally acceptable values, named
        /// </summary>
        public ParamEnum EnumType {get; set;}

        /// <summary>
        /// Alternate name for a field not provided by source defs or paramfiles.
        /// </summary>
        public string AltName {get; set;}

        /// <summary>
        /// A big tooltip to explain the field to the user
        /// </summary>
        public string Wiki {get; set;}

        /// <summary>
        /// Is this u8 field actually a boolean?
        /// </summary>
        public bool IsBool {get; set;}

        public static FieldMetaData Get(PARAMDEF.Field def)
        {
            return _FieldMetas[def];
        }

        private static void Add(PARAMDEF.Field key, FieldMetaData meta)
        {
            _FieldMetas.Add(key, meta);
        }

        public FieldMetaData(PARAMDEF.Field field)
        {
            Add(field, this);
            // Blank Metadata
        }

        public FieldMetaData(ParamMetaData parent, XmlNode fieldMeta, PARAMDEF.Field field)
        {
            Add(field, this);
            RefTypes = null;
            VirtualRef = null;
            XmlAttribute Ref = fieldMeta.Attributes["Refs"];
            if (Ref != null)
            {
                RefTypes = new List<string>(Ref.InnerText.Split(","));
            }
            XmlAttribute VRef = fieldMeta.Attributes["VRef"];
            if (VRef != null)
            {
                VirtualRef = VRef.InnerText;
            }
            XmlAttribute Enum = fieldMeta.Attributes["Enum"];
            if (Enum != null)
            {
                EnumType = parent.enums.GetValueOrDefault(Enum.InnerText, null);
            }
            XmlAttribute AlternateName = fieldMeta.Attributes["AltName"];
            if (AlternateName != null)
            {
                AltName = AlternateName.InnerText;
            }
            XmlAttribute WikiText = fieldMeta.Attributes["Wiki"];
            if (WikiText != null)
            {
                Wiki = WikiText.InnerText.Replace("\\n", "\n");
            }
            XmlAttribute IsBoolean = fieldMeta.Attributes["IsBool"];
            if (IsBoolean != null)
            {
                IsBool = true;
            }
        }
    }

    public class ParamEnum
    {
        public string name;
        public Dictionary<string, string> values = new Dictionary<string, string>(); // using string as an intermediate type. first string is value, second is name.
        
        public ParamEnum(XmlNode enumNode)
        {
            name = enumNode.Attributes["Name"].InnerText;
            foreach (XmlNode option in enumNode.SelectNodes("Option"))
            {
                values.Add(option.Attributes["Value"].InnerText, option.Attributes["Name"].InnerText);
            }
        }
    }
}