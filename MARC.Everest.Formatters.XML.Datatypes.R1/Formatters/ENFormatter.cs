/* 
 * Copyright 2008-2012 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: computc
 * Date: 9/14/2009 10:10:30 AM
 */
using System;
using System.Collections.Generic;
using System.Text;
using MARC.Everest.DataTypes;
using MARC.Everest.Connectors;
using MARC.Everest.Exceptions;
using MARC.Everest.Xml;
using System.Reflection;
using MARC.Everest.DataTypes.Primitives;

namespace MARC.Everest.Formatters.XML.Datatypes.R1.Formatters
{
    /// <summary>
    /// Entity Name Formatter
    /// </summary>
    public class ENFormatter : IDatatypeFormatter
    {

        /// <summary>
        /// Mapping to/from the Data Types R1 "each element has its own name"
        /// </summary>
        private static Dictionary<EntityNamePartType?, string> mapping = new Dictionary<EntityNamePartType?, string>();
        private static Dictionary<string, EntityNamePartType?> reverseMapping = new Dictionary<string, EntityNamePartType?>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static ENFormatter()
        {
            // Setup mappings
            mapping.Add(EntityNamePartType.Delimiter, "delimiter");
            mapping.Add(EntityNamePartType.Family, "family");
            mapping.Add(EntityNamePartType.Given, "given");
            mapping.Add(EntityNamePartType.Prefix, "prefix");
            mapping.Add(EntityNamePartType.Suffix, "suffix");

            // Reverse mapping
            foreach (KeyValuePair<EntityNamePartType?, string> kv in mapping)
                reverseMapping.Add(kv.Value, kv.Key);
        }

        #region IDatatypeFormatter Members

     
        /// <summary>
        /// Graph <paramref name="o"/> onto <paramref name="s"/>
        /// </summary>
        /// <param name="s">The stream to graph to</param>
        /// <param name="o">The object to graph</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.String.Format(System.String,System.Object,System.Object)")]
        public void Graph(System.Xml.XmlWriter s, object o, DatatypeFormatterGraphResult result)
        {
            EN instance = o as EN;

            // Do a base format
            ANYFormatter baseFormatter = new ANYFormatter();
            baseFormatter.Graph(s, o as ANY, result);

            // Null flavor
            if (instance.NullFlavor != null)
                return;

            // use
            if (instance.Use != null)
                s.WriteAttributeString("use", Util.ToWireFormat(instance.Use));

            // parts
            if (instance.Part != null)
                foreach (ENXP part in instance.Part)
                {
                    EntityNamePartType? pt = part.Type;
                    SET<CS<EntityNamePartQualifier>> qualifiers = new SET<CS<EntityNamePartQualifier>>();
                    if (part.Qualifier != null)
                        foreach (var qlf in part.Qualifier)
                            qualifiers.Add(qlf.Clone() as CS<EntityNamePartQualifier>);

                    // Title part type?
                    if (pt == EntityNamePartType.Title)
                    {
                        part.Qualifier.Add(new CS<EntityNamePartQualifier>() 
                        { 
                            Code = CodeValue<EntityNamePartQualifier>.Parse("TITLE") }
                        );
                        pt = null;
                    }

                    // Possible to match qualifier to a part tpye if none specified!
                    if (!qualifiers.IsEmpty)
                    {
                        CS<EntityNamePartQualifier> pfx = qualifiers.Find(a => a.Code.Equals(EntityNamePartQualifier.Prefix)),
                            sfx = qualifiers.Find(a=>a.Code.Equals(EntityNamePartQualifier.Suffix));
                        if (pfx != null)
                        {
                            pt = EntityNamePartType.Prefix;
                            qualifiers.Remove(pfx);
                        }
                        else if (sfx != null)
                        {
                            pt = EntityNamePartType.Suffix;
                            qualifiers.Remove(sfx);
                        }
                    }

                    // Part type is not set so do it inline
                    if (pt == null)
                    {
                        if (!qualifiers.IsEmpty)
                            result.AddResultDetail(new NotSupportedChoiceResultDetail(ResultDetailType.Warning, "Part has qualifier but is not being rendered as a type element, qualifier will be dropped", s.ToString(), null));
                        s.WriteString(part.Value);
                    }
                    else if (mapping.ContainsKey(pt))
                    {
                        var prt = part.Clone() as ENXP;
                        prt.Type = pt;
                        prt.Qualifier = qualifiers;
                        s.WriteStartElement(mapping[pt], "urn:hl7-org:v3");
                        ENXPFormatter enFormatter = new ENXPFormatter();
                        enFormatter.Graph(s, prt, result);
                        s.WriteEndElement();
                    }
                    else
                        throw new MessageValidationException(string.Format("Can't represent entity name part '{0}' in datatypes R1 at '{1}'", pt, (s as XmlStateWriter).CurrentPath));

                }

            // base details
        }

        /// <summary>
        /// Parse and object from <paramref name="s"/>
        /// </summary>
        /// <param name="s">The stream to parse</param>
        /// <returns>The parsed object</returns>
        public object Parse(System.Xml.XmlReader s, DatatypeFormatterParseResult result)
        {
            return Parse<EN>(s, result);
        }

        /// <summary>
        /// Generic parse method
        /// </summary>
        /// <typeparam name="T">The type of object to parse</typeparam>
        /// <param name="s">The stream to parse from</param>
        /// <returns>The parsed object</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2119:SealMethodsThatSatisfyPrivateInterfaces"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
        public T Parse<T>(System.Xml.XmlReader s, DatatypeFormatterParseResult result)
            where T : EN, new()
        {
            // Parse the address parts
            // Parse base (ANY) from the stream
            ANYFormatter baseFormatter = new ANYFormatter();

            // Parse EN
            T retVal = baseFormatter.Parse<T>(s, result);

            // Now parse our data out... Attributes
            if (s.GetAttribute("use") != null)
                retVal.Use = (SET<CS<EntityNameUse>>)Util.FromWireFormat(s.GetAttribute("use"), typeof(SET<CS<EntityNameUse>>));

            // Loop through content
            // Elements
            #region Elements
            if (!s.IsEmptyElement)
            {

                int sDepth = s.Depth;
                string sName = s.Name;

                s.Read();
                // string Name
                while (!(s.NodeType == System.Xml.XmlNodeType.EndElement && s.Depth == sDepth && s.Name == sName))
                {
                    string oldName = s.Name; // Name
                    try
                    {
                        EntityNamePartType? enxpType; // entity part type

                        if (reverseMapping.TryGetValue(s.LocalName, out enxpType)) // Reverse map exists, so this is a part
                        {
                            ENXPFormatter adxpFormatter = new ENXPFormatter(); // ENXP Formatter
                            adxpFormatter.Host = this.Host;
                            ENXP part = (ENXP)adxpFormatter.Parse(s, result); // Parse
                            part.Type = enxpType;
                            retVal.Part.Add(part); // Add to EN
                        }
                        else if (s.NodeType == System.Xml.XmlNodeType.Text ||
                            s.NodeType == System.Xml.XmlNodeType.CDATA)
                            retVal.Part.Add(new ENXP(s.Value));
                        else if (s.NodeType == System.Xml.XmlNodeType.Element)
                            result.AddResultDetail(new NotImplementedElementResultDetail(ResultDetailType.Warning,
                                s.LocalName,
                                s.NamespaceURI,
                                s.ToString(), null));
                    }
                    catch (MessageValidationException e)
                    {
                        result.AddResultDetail(new ResultDetail(ResultDetailType.Error, e.Message, s.ToString(), e)); // Append details
                    }
                    finally
                    {
                        if (oldName == s.Name) s.Read(); // Read if we need to
                    }
                }
            }
            #endregion

            // Validate
            string pathName = s is XmlStateReader ? (s as XmlStateReader).CurrentPath : s.Name;
            baseFormatter.Validate(retVal, pathName, result);

            return retVal;
        }

        /// <summary>
        /// Get the type that this formatter handles
        /// </summary>
        public string HandlesType
        {
            get { return "EN"; }
        }

        /// <summary>
        /// Get or set the host of this formatter
        /// </summary>
        public IXmlStructureFormatter Host { get; set; }

        /// <summary>
        /// Get or set the generic type arguments for this formatter
        /// </summary>
        public Type[] GenericArguments { get; set; }

        /// <summary>
        /// Get the supported properties for the rendering
        /// </summary>
        public List<PropertyInfo> GetSupportedProperties()
        {
            List<PropertyInfo> retVal = new List<PropertyInfo>(10);
            retVal.Add(typeof(EN).GetProperty("Use"));
            retVal.Add(typeof(EN).GetProperty("Part"));
            retVal.AddRange(new ANYFormatter().GetSupportedProperties());
            return retVal;
        }
        #endregion
    }
}