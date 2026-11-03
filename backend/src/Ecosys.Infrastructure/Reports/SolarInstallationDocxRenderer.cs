using System.Buffers.Binary;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Ecosys.Infrastructure.Reports;

/// <summary>
/// Fills the client's own Word template (Reports/Templates/solar-installation-report.docx) so the
/// editable download is their document exactly, not a lookalike. Placeholders are <c>{{key}}</c>, with
/// the same keys as <see cref="SolarInstallationReportData"/>.
/// </summary>
public static partial class SolarInstallationDocxRenderer
{
    private const string TemplateResource = "Ecosys.Infrastructure.Reports.Templates.solar-installation-report.docx";
    private const long EmuPerInch = 914400;

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("See Annex A")]
    private static partial Regex SeeAnnex();

    /// <summary>"— %" / "— MΩ" left by a blank measurement next to the template's fixed unit.</summary>
    [GeneratedRegex(@"^—\s+(%|MΩ|Ω|kWh|kWp|kW|Wp)$")]
    private static partial Regex DashWithUnit();

    [GeneratedRegex("All tests met the acceptance criteria")]
    private static partial Regex AllTestsMet();

    public static byte[] Render(SolarInstallationReportData d)
    {
        using var output = new MemoryStream();
        using (var template = typeof(SolarInstallationDocxRenderer).Assembly.GetManifestResourceStream(TemplateResource)
            ?? throw new InvalidOperationException($"Embedded report template '{TemplateResource}' is missing."))
        {
            template.CopyTo(output);
        }

        using (var doc = WordprocessingDocument.Open(output, true))
        {
            var main = doc.MainDocumentPart!;
            var body = main.Document.Body!;
            var imageId = 1000u;

            ReplaceLogos(main, d.LogoBytes);
            RemoveUnusedStringRows(body, d);
            body.Descendants<Paragraph>()
                .Where(p => p.InnerText.TrimStart().StartsWith("Note: add or delete rows", StringComparison.OrdinalIgnoreCase))
                .ToList().ForEach(p => p.Remove());

            // Photo cells: the placeholder paragraph becomes the picture (or "No photo").
            var photos = SolarInstallationTemplate.PhotoSlots.Zip(d.SlotPhotos, (slot, photo) => (slot.Key, photo.ImageBytes))
                .ToDictionary(x => x.Key, x => x.ImageBytes, StringComparer.OrdinalIgnoreCase);
            foreach (var paragraph in body.Descendants<Paragraph>().ToList())
            {
                var match = Placeholder().Match(paragraph.InnerText.Trim());
                if (!match.Success || match.Length != paragraph.InnerText.Trim().Length || !photos.TryGetValue(match.Groups[1].Value, out var bytes))
                    continue;
                paragraph.RemoveAllChildren<Run>();
                paragraph.AppendChild(bytes is { Length: > 0 } && ImageRun(main, bytes, (long)(2.8 * EmuPerInch), (long)(2.2 * EmuPerInch), imageId++) is { } run
                    ? run
                    : new Run(new Text("No photo")));
            }

            InsertSignatures(main, body, d, ref imageId);

            // A paragraph that is only a placeholder (an extra bullet, a notes paragraph) goes when it has no value.
            foreach (var paragraph in body.Descendants<Paragraph>().Where(p => !p.Ancestors<TableCell>().Any()).ToList())
            {
                var text = paragraph.InnerText.Trim();
                var match = Placeholder().Match(text);
                if (match.Success && match.Length == text.Length && !d.Has(match.Groups[1].Value))
                    paragraph.Remove();
            }

            var testsFailed = d.TestsSentence != "All tests met the acceptance criteria";
            var hasSerials = d.Has("module_serials");
            foreach (var paragraph in AllParagraphs(doc))
            {
                var inTable = paragraph.Ancestors<TableCell>().Any();
                ReplaceInParagraph(paragraph, Placeholder(), m => inTable ? d.Show(m.Groups[1].Value) : d.Get(m.Groups[1].Value));
                if (!hasSerials) ReplaceInParagraph(paragraph, SeeAnnex(), _ => SolarInstallationReportData.Empty);
                if (testsFailed) ReplaceInParagraph(paragraph, AllTestsMet(), _ => d.TestsSentence);
                if (inTable) ReplaceInParagraph(paragraph, DashWithUnit(), _ => SolarInstallationReportData.Empty);
            }

            if (hasSerials)
            {
                AppendAnnex(body, d.Get("module_serials"));
            }

            main.Document.Save();
        }

        return output.ToArray();
    }

    private static IEnumerable<Paragraph> AllParagraphs(WordprocessingDocument doc)
    {
        var main = doc.MainDocumentPart!;
        foreach (var p in main.Document.Descendants<Paragraph>().ToList()) yield return p;
        foreach (var header in main.HeaderParts)
            foreach (var p in header.Header.Descendants<Paragraph>().ToList()) yield return p;
        foreach (var footer in main.FooterParts)
            foreach (var p in footer.Footer.Descendants<Paragraph>().ToList()) yield return p;
    }

    /// <summary>
    /// Replaces each match of <paramref name="pattern"/> in a paragraph while keeping its formatting.
    /// Word often splits one placeholder over several runs (it did for most of this template), so matching
    /// runs on the paragraph's joined text; each replacement is written into the run where the match
    /// starts and the rest of the match is cut from the runs it spans. Runs between matches are untouched.
    /// </summary>
    internal static void ReplaceInParagraph(Paragraph paragraph, Regex pattern, MatchEvaluator evaluator)
    {
        var texts = paragraph.Descendants<Text>().ToList();
        if (texts.Count == 0) return;
        var joined = string.Concat(texts.Select(t => t.Text));
        var matches = pattern.Matches(joined);
        if (matches.Count == 0) return;

        var starts = new int[texts.Count];
        for (int i = 0, offset = 0; i < texts.Count; offset += texts[i].Text.Length, i++) starts[i] = offset;
        int NodeAt(int position)
        {
            var index = Array.BinarySearch(starts, position);
            if (index < 0) index = ~index - 1;
            // Skip empty nodes so the replacement lands in a run that actually holds the text.
            while (index < texts.Count - 1 && starts[index + 1] == starts[index] && texts[index].Text.Length == 0) index++;
            return Math.Clamp(index, 0, texts.Count - 1);
        }

        var touched = new HashSet<Text>();
        // Right to left, so earlier offsets stay valid while later text changes length.
        foreach (var match in matches.Reverse())
        {
            var first = NodeAt(match.Index);
            var last = NodeAt(match.Index + match.Length - 1);
            var replacement = evaluator(match);
            var firstText = texts[first].Text;
            var firstStart = match.Index - starts[first];
            var lastEnd = match.Index + match.Length - starts[last];

            if (first == last)
            {
                texts[first].Text = firstText[..firstStart] + replacement + firstText[lastEnd..];
            }
            else
            {
                texts[first].Text = firstText[..firstStart] + replacement;
                for (var i = first + 1; i < last; i++) texts[i].Text = string.Empty;
                texts[last].Text = texts[last].Text[lastEnd..];
                texts[last].Space = SpaceProcessingModeValues.Preserve;
            }
            texts[first].Space = SpaceProcessingModeValues.Preserve;
            touched.Add(texts[first]);
        }

        foreach (var text in touched) SplitLineBreaks(text);
    }

    /// <summary>Multi-line answers (observations, serials) become line breaks within the run.</summary>
    private static void SplitLineBreaks(Text text)
    {
        if (!text.Text.Contains('\n') || text.Parent is not Run run) return;
        var lines = text.Text.Replace("\r", string.Empty).Split('\n');
        text.Text = lines[0];
        OpenXmlElement after = text;
        foreach (var line in lines.Skip(1))
        {
            var br = new Break();
            run.InsertAfter(br, after);
            var next = new Text(line) { Space = SpaceProcessingModeValues.Preserve };
            run.InsertAfter(next, br);
            after = next;
        }
    }

    private static void RemoveUnusedStringRows(Body body, SolarInstallationReportData d)
    {
        for (var i = 1; i <= SolarInstallationTemplate.StringCount; i++)
        {
            if (d.Strings.Contains(i)) continue;
            var marker = $"{{{{s{i}_qty}}}}";
            body.Descendants<TableRow>().Where(r => r.InnerText.Contains(marker, StringComparison.Ordinal)).ToList().ForEach(r => r.Remove());
        }
    }

    private static void InsertSignatures(MainDocumentPart main, Body body, SolarInstallationReportData d, ref uint imageId)
    {
        var row = body.Descendants<TableRow>().FirstOrDefault(r =>
            r.Elements<TableCell>().FirstOrDefault()?.InnerText.Trim().Equals("Signature", StringComparison.OrdinalIgnoreCase) == true);
        var cells = row?.Elements<TableCell>().ToList();
        if (cells is null || cells.Count < 3) return;

        foreach (var (cell, signature) in new[] { (cells[1], d.TechnicianSignature), (cells[2], d.ClientSignature) })
        {
            if (signature?.ImageBytes is not { Length: > 0 } bytes) continue;
            var run = ImageRun(main, bytes, (long)(1.8 * EmuPerInch), (long)(0.7 * EmuPerInch), imageId++);
            if (run is null) continue;
            var paragraph = cell.Elements<Paragraph>().FirstOrDefault() ?? cell.AppendChild(new Paragraph());
            paragraph.RemoveAllChildren<Run>();
            paragraph.AppendChild(run);
        }
    }

    private static void AppendAnnex(Body body, string serials)
    {
        var annex = new[]
        {
            new Paragraph(new Run(new Break { Type = BreakValues.Page })),
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), new Run(new Text("Annex A – PV Module Serial Numbers"))),
            new Paragraph(new Run(new Text(serials) { Space = SpaceProcessingModeValues.Preserve })),
        };
        var sectionProperties = body.Elements<SectionProperties>().LastOrDefault();
        foreach (var paragraph in annex)
        {
            if (sectionProperties is null) body.AppendChild(paragraph);
            else body.InsertBefore(paragraph, sectionProperties);
            if (paragraph.Descendants<Text>().LastOrDefault() is { } text) SplitLineBreaks(text);
        }
    }

    /// <summary>Points the template's logo pictures (cover and page header) at the workspace logo.</summary>
    private static void ReplaceLogos(MainDocumentPart main, byte[]? logo)
    {
        if (logo is not { Length: > 0 } || ImageSize(logo) is not { } size) return;

        void Swap(OpenXmlPartContainer part, OpenXmlElement root)
        {
            foreach (var drawing in root.Descendants<Drawing>())
            {
                var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
                var extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
                if (blip is null || extent is null) continue;

                var imagePart = AddImagePart(part, logo);
                if (imagePart is null) continue;
                blip.Embed = part.GetIdOfPart(imagePart);

                // Keep the template's height and adapt the width to the logo's proportions.
                var height = extent.Cy!.Value;
                var width = (long)(height * size.Width / (double)size.Height);
                extent.Cx = width;
                foreach (var ext in drawing.Descendants<A.Extents>()) { ext.Cx = width; ext.Cy = height; }
            }
        }

        Swap(main, main.Document);
        foreach (var header in main.HeaderParts) Swap(header, header.Header);
    }

    private static ImagePart? AddImagePart(OpenXmlPartContainer part, byte[] bytes)
    {
        var type = IsPng(bytes) ? ImagePartType.Png : IsJpeg(bytes) ? ImagePartType.Jpeg : (PartTypeInfo?)null;
        if (type is null) return null;
        ImagePart imagePart = part switch
        {
            MainDocumentPart m => m.AddImagePart(type.Value),
            HeaderPart h => h.AddImagePart(type.Value),
            FooterPart f => f.AddImagePart(type.Value),
            _ => throw new NotSupportedException(),
        };
        using var stream = new MemoryStream(bytes);
        imagePart.FeedData(stream);
        return imagePart;
    }

    /// <summary>An inline picture scaled to fit the box. Null for formats Word can't show (e.g. WebP).</summary>
    private static Run? ImageRun(MainDocumentPart main, byte[] bytes, long maxWidth, long maxHeight, uint id)
    {
        if (ImageSize(bytes) is not { } size || AddImagePart(main, bytes) is not { } part) return null;
        var scale = Math.Min(maxWidth / (double)size.Width, maxHeight / (double)size.Height);
        var cx = (long)(size.Width * scale);
        var cy = (long)(size.Height * scale);
        var relId = main.GetIdOfPart(part);
        var name = $"Picture {id}";

        return new Run(new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = id, Name = name },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = id, Name = name },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0,
            }));
    }

    private static bool IsPng(byte[] b) => b.Length > 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
    private static bool IsJpeg(byte[] b) => b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8;

    /// <summary>Pixel size from the PNG or JPEG header, without decoding the image.</summary>
    internal static (int Width, int Height)? ImageSize(byte[] b)
    {
        if (IsPng(b))
        {
            var w = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16));
            var h = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20));
            return w > 0 && h > 0 ? (w, h) : null;
        }
        if (!IsJpeg(b)) return null;
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
            var length = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i + 2));
            // SOF0..SOF15 except DHT (C4), JPG (C8) and DAC (CC) carry the frame size.
            if (marker >= 0xC0 && marker <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                var h = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i + 5));
                var w = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i + 7));
                return w > 0 && h > 0 ? (w, h) : null;
            }
            i += 2 + length;
        }
        return null;
    }
}
