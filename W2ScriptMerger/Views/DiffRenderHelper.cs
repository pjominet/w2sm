using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace W2ScriptMerger.Views;

internal static class DiffRenderHelper
{
    private static readonly Differ Differ = new();

    public static List<int> RenderDiff(RichTextBox rtb, string leftText, string rightText, bool isLeft)
    {
        var diffBuilder = new SideBySideDiffBuilder(Differ);
        var diff = diffBuilder.BuildDiffModel(leftText, rightText);

        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            PageWidth = 10000
        };

        var lines = isLeft ? diff.OldText.Lines : diff.NewText.Lines;
        var lineNumber = 1;
        var diffPositions = new List<int>();

        // Track diff positions (only for left panel to avoid duplicates)
        if (isLeft)
        {
            var inDiffBlock = false;

            for (var i = 0; i < lines.Count; i++)
            {
                var isDiff = lines[i].Type is ChangeType.Inserted or ChangeType.Deleted or ChangeType.Modified;
                switch (isDiff)
                {
                    case true when !inDiffBlock:
                        diffPositions.Add(i);
                        inDiffBlock = true;
                        break;
                    case false:
                        inDiffBlock = false;
                        break;
                }
            }
        }

        foreach (var line in lines)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                LineHeight = 1
            };

            var lineNumRun = new Run($"{lineNumber,4} ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
            };
            paragraph.Inlines.Add(lineNumRun);

            var contentRun = new Run(line.Text ?? string.Empty);

            switch (line.Type)
            {
                case ChangeType.Inserted:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x23, 0x42, 0x23));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
                    break;
                case ChangeType.Deleted:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x42, 0x23, 0x23));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
                    break;
                case ChangeType.Modified:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x23));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA));
                    break;
                case ChangeType.Imaginary:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));
                    contentRun.Text = "~";
                    break;
                case ChangeType.Unchanged:
                default:
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                    break;
            }

            paragraph.Inlines.Add(contentRun);
            document.Blocks.Add(paragraph);
            lineNumber++;
        }

        rtb.Document = document;
        return diffPositions;
    }

    public static void ScrollToDiff(RichTextBox rtb, List<int> diffPositions, int diffIndex, int paddingLines = 0)
    {
        if (diffIndex < 0 || diffIndex >= diffPositions.Count) return;

        var lineIndex = diffPositions[diffIndex];
        var paddedIndex = Math.Max(0, lineIndex - paddingLines);
        var blocks = rtb.Document.Blocks.ToList();

        if (paddedIndex < blocks.Count && blocks[paddedIndex] is Paragraph paragraph)
            paragraph.BringIntoView();
    }

    public static string FormatDiffPositionText(List<int> diffPositions, int currentDiffIndex)
    {
        if (diffPositions.Count == 0)
            return "No differences";

        return currentDiffIndex < 0
            ? $"{diffPositions.Count} diff(s)"
            : $"{currentDiffIndex + 1}/{diffPositions.Count}";
    }
}
