using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Xml.Linq;

namespace WpfApp2
{
    public class ActRow
    {
        public int No { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public double Qty { get; set; }
    }

    public static class DocxSaver
    {
        static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        static readonly string[] Months = {
            "января", "февраля", "марта", "апреля", "мая", "июня",
            "июля", "августа", "сентября", "октября", "ноября", "декабря"
        };

        public static void SaveAct(string path, string order, string dtFrom, string giver,
                                   string receiver, List<ActRow> rows, double total)
        {
            // дата выдачи
            DateTime d;
            if (!DateTime.TryParse(dtFrom, out d)) d = DateTime.Now;
            string dateLine = string.Format("с. Кушнаренково   «{0}» {1} {2} г.",
                d.Day, Months[d.Month - 1], d.Year);

            if (string.IsNullOrEmpty(receiver)) receiver = "_____________________";

            var body = new List<XElement>();

            // --- шапка ---
            body.Add(P("Акт приема-передачи", true, true, 28));
            body.Add(P("Особо ценного имущества в ГБУЗ РБ Кушнаренковская ЦРБ", true, true, 26));
            body.Add(P(dateLine, false, false, 22));
            body.Add(P("", false, false, 22));

            // --- преамбула ---
            body.Add(P("ГБУЗ РБ Кушнаренковская ЦРБ, именуемое в дальнейшем «ЦРБ», " +
                "в лице сотрудника ЦРБ, " + giver + ", действующей на основании " +
                "должностной инструкции, с одной стороны и сотрудник ЦРБ, именуемый " +
                "в дальнейшем «сотрудник», в лице " + receiver +
                ", действующего на основании трудового договора, с другой стороны, " +
                "вместе в дальнейшем именуемые Стороны, подписали настоящий Акт " +
                "о нижеследующем:", false, false, 22));

            body.Add(P("", false, false, 22));
            body.Add(P("Для работы передает сотруднику, а сотрудник принимает " +
                "товарно-материальные ценности (далее – ТМЦ):", false, false, 22));

            // --- перечень ТМЦ ---
            if (rows.Count == 0)
            {
                body.Add(P("____________________________________________________________________",
                    false, false, 22));
            }
            else
            {
                foreach (var r in rows)
                {
                    string unit = string.IsNullOrEmpty(r.Unit) ? "" : " " + r.Unit;
                    body.Add(P(r.No + ". " + r.Name + " — " + r.Qty.ToString("0.##") + unit,
                        false, false, 22));
                }
            }

            body.Add(P("", false, false, 22));
            body.Add(P("При передаче Устройства представителями Сторон проверена его " +
                "целостность. Претензии у сотрудника к целостности Устройства и факту " +
                "его передачи отсутствуют. Настоящий Акт составлен в двух экземплярах " +
                "для каждой из сторон.", false, false, 22));

            body.Add(P("", false, false, 22));
            body.Add(P("ЦРБ обязуется заменить Устройство в случае выхода его из строя " +
                "только в течение первого года его эксплуатации при условии, что " +
                "сотрудником в должной мере соблюдались условия эксплуатации и хранения, " +
                "установленные производителем Устройства.", false, false, 22));

            body.Add(P("", false, false, 22));

            // --- подписи (таблица без рамок, как в бланке) ---
            body.Add(new XElement(W + "tbl",
                new XElement(W + "tblPr",
                    new XElement(W + "tblW",
                        new XAttribute(W + "w", 9000), new XAttribute(W + "type", "dxa"))),
                new XElement(W + "tblGrid", ColW(4500), ColW(4500)),
                new XElement(W + "tr",
                    SigCell("от ЦРБ:", true),
                    SigCell("от сотрудника:", true)),
                new XElement(W + "tr",
                    SigCell("_________________/_________/", false),
                    SigCell("_________________/_________/", false))));

            body.Add(new XElement(W + "sectPr",
                new XElement(W + "pgSz",
                    new XAttribute(W + "w", 11906), new XAttribute(W + "h", 16838)),
                new XElement(W + "pgMar",
                    new XAttribute(W + "top", 850), new XAttribute(W + "right", 850),
                    new XAttribute(W + "bottom", 850), new XAttribute(W + "left", 850))));

            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(W + "document",
                    new XAttribute(XNamespace.Xmlns + "w", W),
                    new XElement(W + "body", body)));

            using (var pkg = Package.Open(path, FileMode.Create))
            {
                var part = pkg.CreatePart(new Uri("/word/document.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
                    CompressionOption.Maximum);
                using (var s = part.GetStream(FileMode.Create))
                    doc.Save(s);

                pkg.CreateRelationship(new Uri("word/document.xml", UriKind.Relative),
                    TargetMode.Internal,
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            }
        }

        // ---------- элементы WordprocessingML ----------
        static XElement P(string text, bool bold, bool center, int halfPoints)
        {
            var rPr = new XElement(W + "rPr");
            if (bold) rPr.Add(new XElement(W + "b"));
            rPr.Add(new XElement(W + "sz", halfPoints));
            rPr.Add(new XElement(W + "szCs", halfPoints));

            var pPr = new XElement(W + "pPr");
            if (center) pPr.Add(new XElement(W + "jc",
                new XAttribute(W + "val", "center")));
            pPr.Add(new XElement(W + "spacing",
                new XAttribute(W + "after", "120")));

            return new XElement(W + "p", pPr,
                new XElement(W + "r", rPr, new XElement(W + "t", text)));
        }

        static XElement ColW(int w)
        {
            return new XElement(W + "gridCol", new XAttribute(W + "w", w));
        }

        static XElement SigCell(string text, bool bold)
        {
            var rPr = new XElement(W + "rPr");
            if (bold) rPr.Add(new XElement(W + "b"));
            return new XElement(W + "tc",
                new XElement(W + "tcPr",
                    new XElement(W + "tcW",
                        new XAttribute(W + "w", 4500), new XAttribute(W + "type", "dxa"))),
                new XElement(W + "p",
                    new XElement(W + "r", rPr, new XElement(W + "t", text))));
        }
    }
}
