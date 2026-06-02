using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public class WordDocumentExporter : IDocumentExporter
    {
        public byte[] ExportToWord(string title, List<TranscriptionSegment> segments, bool includeTimestamps)
        {
            using var stream = new MemoryStream();
            using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();
                mainPart.Document.Append(body);

                // Setup Page Margins (1 inch / 1440 twentieths of a point)
                var sectionProps = new SectionProperties();
                var pageMargin = new PageMargin
                {
                    Top = 1440,
                    Bottom = 1440,
                    Left = 1440,
                    Right = 1440
                };
                sectionProps.Append(pageMargin);

                // Add Title Paragraph
                var titlePara = new Paragraph();
                var titleParaProps = new ParagraphProperties(
                    new SpacingBetweenLines { After = "240" } // 12pt space after
                );
                titlePara.Append(titleParaProps);

                var titleRun = new Run();
                titleRun.AppendChild(new Text(title));
                titleRun.RunProperties = new RunProperties(
                    new RunFonts { Ascii = "Outfit", HighAnsi = "Outfit", EastAsia = "微软雅黑" },
                    new Bold(),
                    new FontSize { Val = "36" }, // 18pt font size
                    new Color { Val = "0F172A" } // Slate-900 (very dark blue-gray)
                );
                titlePara.Append(titleRun);
                body.AppendChild(titlePara);

                // Add Divider (border line)
                var dividerPara = new Paragraph();
                var dividerParaProps = new ParagraphProperties(
                    new SpacingBetweenLines { After = "240" }
                );
                dividerPara.Append(dividerParaProps);
                var dividerRun = new Run();
                dividerRun.AppendChild(new Text(new string('_', 60)));
                dividerRun.RunProperties = new RunProperties(
                    new Color { Val = "E2E8F0" } // Slate-200
                );
                dividerPara.Append(dividerRun);
                body.AppendChild(dividerPara);

                // Add Transcribed Segments
                foreach (var segment in segments)
                {
                    var p = new Paragraph();
                    var pProps = new ParagraphProperties(
                        new SpacingBetweenLines
                        {
                            After = "120", // 6pt space after
                            Line = "276",  // 1.15 line spacing
                            LineRule = LineSpacingRuleValues.Auto
                        }
                    );
                    p.Append(pProps);

                    if (includeTimestamps)
                    {
                        var timeRun = new Run();
                        var timeString = $"[{segment.Start:hh\\:mm\\:ss} - {segment.End:hh\\:mm\\:ss}]  ";
                        timeRun.AppendChild(new Text(timeString));
                        timeRun.RunProperties = new RunProperties(
                            new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", EastAsia = "微软雅黑" },
                            new Color { Val = "64748B" }, // Slate-500
                            new FontSize { Val = "18" }   // 9pt font size
                        );
                        p.Append(timeRun);
                    }

                    var textRun = new Run();
                    textRun.AppendChild(new Text(segment.Text));
                    textRun.RunProperties = new RunProperties(
                        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "微软雅黑" },
                        new Color { Val = "334155" }, // Slate-700
                        new FontSize { Val = "22" }   // 11pt font size
                    );
                    p.Append(textRun);

                    body.AppendChild(p);
                }

                // Append sections properties to body
                body.Append(sectionProps);

                wordDocument.Save();
            }

            return stream.ToArray();
        }
    }
}
