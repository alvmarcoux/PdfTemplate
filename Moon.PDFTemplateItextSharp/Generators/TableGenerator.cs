﻿using System;
using System.Collections;
using System.Xml;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Moon.PDFDraw;
using Moon.PDFDrawItextSharp;
using Moon.PDFTemplate;
using Moon.PDFTemplateItextSharp.Model;

namespace Moon.PDFTemplateItextSharp.Generators
{
    /// <summary>
    /// This generator is responsible for outputing table element to pdf
    /// table element can be from table or body/table 
    /// </summary>
    public class TableGenerator
    {

        private readonly Table tableElement;
        private readonly TableRowGroup tableRowGroupHead = new TableRowGroup();
        private readonly TableRowGroup tableRowGroupLoop = new TableRowGroup();
        private readonly TableRowGroup tableRowGroupFoot = new TableRowGroup();

        private TableData tableData;
        private IPDFDraw pdfDrawer;

        private readonly PDFTemplate.PDFTemplate pdfTemplate;


        /// <summary>
        /// initialise template
        /// </summary>
        /// <param name="template">caller instance</param>
        /// <param name="tableNode"></param>
        public TableGenerator(PDFTemplate.PDFTemplate template, XmlNode tableNode)
        {
            pdfTemplate = template;

            if (tableNode != null)
            {
                var tableElt = new Table(tableNode.Attributes);
                XmlNode tableHeadNode = tableNode.SelectSingleNode(".//tablehead");
                XmlNode tableLoopNode = tableNode.SelectSingleNode(".//tableloop");
                XmlNode tableFootNode = tableNode.SelectSingleNode(".//tablefoot");

                this.tableElement = tableElt;
                tableRowGroupHead = tableHeadNode != null ? BuildTableRowGroup(tableHeadNode) : null;
                tableRowGroupLoop = tableLoopNode != null ? BuildTableRowGroup(tableLoopNode) : null;
                tableRowGroupFoot = tableFootNode != null ? BuildTableRowGroup(tableFootNode) : null;
            }
        }

        /// <summary>
        /// Render 
        /// </summary>
        public void DrawTable(TableData data, IPDFDraw drawer)
        {
            tableData = data;
            pdfDrawer = drawer;

            if (tableData.LoopData != null)
            {
                PdfPTable table = DrawTableHead();
                DrawTableLoop(ref table);
                DrawTableFoot(ref table);

                if (table.Rows.Count > 0)
                {
                    var pdfDraw = (PDFDrawItextSharp.PDFDrawItextSharp)pdfDrawer;
                    pdfDrawer.NextRow(1, DocumentGroup.Table);
                    pdfDraw.DrawTable(table, pdfDraw.Current_x, pdfDraw.Current_y);
                    pdfDraw.NextRow(table.TotalHeight, DocumentGroup.Table);
                }
            }
        }


        #region table output functions

        private TableCell BuildTableCell(XmlNode node, XmlAttributeCollection fontAttrs)
        {
            TableCell tableCell = new TableCell(node.Attributes, fontAttrs);
            BuildTableCellChildElement(tableCell, node, fontAttrs);

            return tableCell;
        }

        /// <summary>
        /// 20130612 :: Add custom element.
        /// </summary>
        /// <param name="tableCell"></param>
        /// <param name="tableCellNode"></param>
        /// <param name="fontAttrs"></param>
        private void BuildTableCellChildElement(
            TableCell tableCell,
            XmlNode tableCellNode,
            XmlAttributeCollection fontAttrs)
        {
            foreach (XmlNode child in tableCellNode.ChildNodes)
            {
                switch (child.Name)
                {
                    case "textbox":
                        tableCell.AddDrawElement(pdfTemplate.BuildTextBox(child, fontAttrs));
                        break;
                    case "image":
                        tableCell.AddDrawElement(pdfTemplate.BuildImage(child, fontAttrs));
                        break;
                    //20130612 :: mellorasinxelas :: custom
                    case PDFTemplate.PDFTemplate.CustomElementConstant:
                        tableCell.AddDrawElement(pdfTemplate.BuildCustomElement(child, fontAttrs));
                        break;

                }
                if (child.Name == "font" && child.HasChildNodes)
                {
                    BuildTableCellChildElement(tableCell, child, child.Attributes);
                }
            }
        }

        private void BuildTableRowElement(TableRow tableRow, XmlNode tableRowNode, XmlAttributeCollection fontAttrs)
        {
            foreach (XmlNode child in tableRowNode)
            {
                switch (child.Name)
                {
                    case "tablecell":
                        tableRow.AddTableCell(BuildTableCell(child, fontAttrs));
                        break;
                }
                if (child.Name == "font" && child.HasChildNodes)
                {
                    BuildTableRowElement(tableRow, child, child.Attributes);
                }
            }
        }

        private TableRow BuildTableRow(XmlNode tableRowNode, XmlAttributeCollection fontAttrs)
        {
            TableRow tableRow = new TableRow();
            if (tableRowNode.HasChildNodes)
                BuildTableRowElement(tableRow, tableRowNode, fontAttrs);

            return tableRow;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node">tablehead, tableloop, tablefoot</param>
        /// <returns></returns>
        private TableRowGroup BuildTableRowGroup(XmlNode node)
        {
            TableRowGroup tableRowGroup = new TableRowGroup();
            XmlAttributeCollection font = pdfTemplate.DefaultFontAttrs;
            if (node.FirstChild.Name == "font")
            {
                font = node.FirstChild.Attributes;
            }

            XmlNodeList tableRowNodes = node.SelectNodes(".//tablerow");
            if (tableRowNodes == null)
                throw new Exception("Table must have rows");
            foreach (XmlNode tableRowNode in tableRowNodes)
            {
                tableRowGroup.AddTableRow(BuildTableRow(tableRowNode, font));
            }
            return tableRowGroup;
        }

        /// <summary>
        /// Add a row of pdfpcell to table.
        /// return True, current page have enought size for that row
        /// return False, current page not enought size for that row, row will be remove and need to draw on
        /// next page
        /// 20130606 :: jaimelopez :: mellorasinxelas to support absolute footer.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="tableRowElement"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected bool DrawTableRow(
            PdfPTable table,
            TableRow tableRowElement,
            IDictionary data)
        {
            bool enoughSpace = true;
            PDFDrawItextSharp.PDFDrawItextSharp pdfDraw = (PDFDrawItextSharp.PDFDrawItextSharp)pdfDrawer;
            foreach (TableCell tableCell in tableRowElement.TableCells)
            {
                PdfPCell cell = pdfDraw.CreateTableCell(tableCell.Attributes, data);
                foreach (DrawElement drawElement in tableCell.DrawElements)
                {
                    if (drawElement is TextBox)
                    {
                        //iTextSharp.text.Phrase phrase = _pdfDraw.CreatePhrase(
                        //    ((PDFTemplate.TextBox)drawElement).GetText(data), drawElement.FontAttributes);
                        Paragraph paragraph = pdfDraw.CreateParagraph(((TextBox)drawElement).GetText(data), drawElement.FontAttributes);
                        paragraph.Alignment = PDFDrawItextSharpHelper.Align(Helper.GetAttributeValue("align", drawElement.Attributes, "Left"));

                        cell.AddElement(paragraph);
                    }
                    else if (drawElement is PDFTemplate.Image)
                    {
                        //iTextSharp.text.Image image = pdfDraw.CreateImageFromAttribute(drawElement.Attributes);
                        //image.Alignment = PDFDrawItextSharpHelper.Align(Helper.GetAttributeValue("align", drawElement.Attributes, "Left"));

                        //cell.AddElement(image);

                        // 2017-11-23 : fix error image in table
                        string src = Moon.PDFDraw.Helper.GetAttributeValue("src", drawElement.Attributes, "");
                        if (data.Contains(src))
                        {
                            iTextSharp.text.Image image = pdfDraw.CreateImageFromAttribute(data[src].ToString(), drawElement.Attributes);
                            image.Alignment = PDFDrawItextSharpHelper.Align(Helper.GetAttributeValue("align", drawElement.Attributes, "Left"));

                            cell.AddElement(image);
                        }
                    }
                }
                table.AddCell(cell);
            }
            table.CompleteRow();

            //if(table.row
            //fixme need to check if any row span
            if (pdfDrawer.isNoMoreY(table.TotalHeight, DocumentGroup.Table))
            {
                enoughSpace = false;
                table.DeleteLastRow();
            }

            return enoughSpace;
        }

        /// <summary>
        /// Draws table row group
        /// </summary>
        /// <param name="table"></param>
        /// <param name="tableRowGroup"></param>
        /// <param name="data"></param>
        protected void DrawTableRowGroup(
        ref PdfPTable table,
        TableRowGroup tableRowGroup,
        IDictionary data)
        {
            //int count = 0;
            foreach (TableRow tableRow in tableRowGroup.TableRows)
            {

                bool haveSize = DrawTableRow(table, tableRow, data);
                //Console.WriteLine("_drawTableRowGroup count: " + count + " haveSize: " + haveSize);
                if (!haveSize)
                {
                    //Console.WriteLine("_drawTableRowGroup in no more size");
                    //1. draw the current table to pdf
                    pdfDrawer.NextRow(1, DocumentGroup.Table);
                    PDFDrawItextSharp.PDFDrawItextSharp pdfDraw = (PDFDrawItextSharp.PDFDrawItextSharp)pdfDrawer;
                    pdfDraw.DrawTable(table, pdfDraw.Current_x, pdfDraw.Current_y);
                    //2. move pdf to next page
                    pdfTemplate.NextPage();
                    pdfTemplate.DrawHeader();
                    //Console.WriteLine("Call nextPage, DrawHeader()");
                    //3. recreate table with the header
                    table = DrawTableHead();
                    //Console.WriteLine("call _drawTableHead(), table.totalheight: " + table.TotalHeight);
                    //4. add the current row to the new table
                    DrawTableRow(table, tableRow, data);
                }
                //count++;
            }
        }

        /// <summary>
        /// Draws table header
        /// </summary>
        protected PdfPTable DrawTableHead()
        {
            PDFDrawItextSharp.PDFDrawItextSharp pdfDraw = (PDFDrawItextSharp.PDFDrawItextSharp)pdfDrawer;
            PdfPTable table = pdfDraw.CreateTable(tableElement.Attributes);
            float widthpercentage = Helper.GetAttributeWidthPercent(tableElement.Attributes);
            if (widthpercentage > 0)
            {
                table.TotalWidth = (pdfTemplate.PageDefinition.Width - pdfTemplate.PageDefinition.Margin_left - pdfTemplate.PageDefinition.Margin_right) * widthpercentage;
            }

            DrawTableRowGroup(ref table, tableRowGroupHead, tableData.HeadData);
            return table;
        }

        /// <summary>
        /// Draws table loop
        /// </summary>
        protected void DrawTableLoop(ref PdfPTable table)
        {
            if (tableData.LoopData == null)
            {
                return;
            }
            foreach (Hashtable data in tableData.LoopData)
            {
                DrawTableRowGroup(ref table, tableRowGroupLoop, data);
            }
        }

        /// <summary>
        /// Draws table footer
        /// </summary>
        /// <param name="table"></param>
        protected void DrawTableFoot(ref PdfPTable table)
        {
            if (tableRowGroupFoot != null)
                DrawTableRowGroup(ref table, tableRowGroupFoot, tableData.FootData);
        }
        #endregion
    }
}
