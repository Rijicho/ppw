/******************************************************************************
 * Author: Seung-Tak Noh
 * Simple SVG exporter for parametric curves (no use for general purpose)
 *****************************************************************************/

// Modified by Hiroki Harada

using System.Collections.Generic;
using UnityEngine;

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace StNoh
{
    public class SvgData
    {
        public List<(List<Vector2> line, Color col)> Lines { get; set; } = new List<(List<Vector2> line, Color col)>();
        public List<(Vector2 point, Color col)> Points { get; set; } = new List<(Vector2 point, Color col)>();
    }

    public class SvgExporter
    {
        #region PUBLIC_MEMBERS

        public static void WriteSVG(
            string filepath,
            Vector2Int canvas,
            SvgData data,
            bool darkmode)
        {
            var lines = data.Lines;
            var points = data.Points;

            using (StreamWriter writer = new StreamWriter(filepath))
            {
                // Xml setting
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.OmitXmlDeclaration = true;
                settings.NewLineOnAttributes = true;


                // write file as XML
                using (XmlWriter xmlWriter = XmlWriter.Create(writer, settings))
                {
                    xmlWriter.WriteStartDocument();

                    // <svg width="..", height="..", xmlns="...", xmlns:xlink="...">
                    string ns = "http://www.w3.org/2000/svg";
                    xmlWriter.WriteStartElement("svg", ns); // first element. should not be closed.
                    xmlWriter.WriteAttributeString("width", canvas.x.ToString());
                    xmlWriter.WriteAttributeString("height", canvas.y.ToString());
                    xmlWriter.WriteAttributeString("xmlns", ns);
                    xmlWriter.WriteAttributeString("xmlns", "xlink", null, "http://www.w3.org/1999/xlink");

                    xmlWriter.WriteStartElement("g"); // <g>
                    xmlWriter.WriteAttributeString("transform", "scale(1,1)");
                    {
                        // each single curve as one path
                        foreach (var (line,col) in lines)
                        {
                            Color32 col32 = col;
                            xmlWriter.WriteStartElement("path");
                            xmlWriter.WriteAttributeString("fill", "none"); 
                            xmlWriter.WriteAttributeString("stroke", $"rgb({col32.r:D},{col32.g:D},{col32.b:D})");
                            xmlWriter.WriteAttributeString("stroke-opacity", $"{col32.a/256f}");
                            xmlWriter.WriteAttributeString("stroke-width", "2");
                            xmlWriter.WriteAttributeString("stroke-linecap", "round");
                            xmlWriter.WriteAttributeString("paint-order", "fill stroke markers");

                            // line segments
                            string d_command = "";
                            for (int i = 0; i < line.Count; i++)
                            {
                                Vector2 vertex = line[i];

                                d_command += (0==i) ? "M " : "L "; // first vertex: "Move to" / from the second~: "Line"
                                d_command += string.Format("{0} {1} ", vertex.x, vertex.y);
                            }
                            xmlWriter.WriteAttributeString("d", d_command);

                            Debug.Log(d_command);

                            xmlWriter.WriteEndElement();
                        }
                    }
                    xmlWriter.WriteEndElement(); // </g>

                    // some "dot"s with svg, for post-process
                    xmlWriter.WriteStartElement("g"); // <g>
                    xmlWriter.WriteAttributeString("transform", "scale(1,1)");
                    {
                        // each single circle as one "path"
                        foreach (var (point,col) in points)
                        {
                            Color32 col32 = col;
                            xmlWriter.WriteStartElement("path");

                            // add predefine circle
                            string d_command = "";
                            d_command += string.Format("M {0} {1} ", point.x, point.y);

                            d_command += "m -3.500 0.000 ";
                            d_command += "c +0.000 -1.933 +1.567 -3.500 +3.500 -3.500 ";
                            d_command += "c +1.933  0.000 +3.500 +1.567 +3.500 +3.500 ";
                            d_command += "c  0.000 +1.933 -1.567 +3.500 -3.500 +3.500 ";
                            d_command += "c -1.933  0.000 -3.500 -1.567 -3.500 -3.500 ";
                            d_command += "z";
                            xmlWriter.WriteAttributeString("d", d_command);

                            var edgeCol = Color32.Lerp(col32, darkmode ? Color.white : Color.black, 0.7f);
                            xmlWriter.WriteAttributeString("stroke", $"rgb({edgeCol.r:D},{edgeCol.g:D},{edgeCol.b:D})");
                            xmlWriter.WriteAttributeString("stroke-width", "2");
                            xmlWriter.WriteAttributeString("stroke-miterlimit", "8");

                            xmlWriter.WriteAttributeString("fill", $"rgb({col32.r:D},{col32.g:D},{col32.b:D})");
                            xmlWriter.WriteAttributeString("fill-rule", "evenodd");

                            xmlWriter.WriteAttributeString("paint-order", "fill stroke markers");
                            xmlWriter.WriteEndElement();
                        }
                    }
                    xmlWriter.WriteEndElement(); // </g>

                    xmlWriter.WriteEndElement(); // </svg>
                }
            }
        }

        #endregion // PUBLIC_MEMBERS
    }
}
