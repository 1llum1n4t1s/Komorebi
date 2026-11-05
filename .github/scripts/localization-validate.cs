using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml;

if (args.Length != 1) throw new ArgumentException("Artifact path required");
var artifact = args[0];
const string xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
const string localeDir = "src/Resources/Locales";
const string englishPath = localeDir + "/en_US.axaml";

static XmlDocument ReadXml(string path)
{
    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
    using var reader = XmlReader.Create(path, settings);
    var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
    document.Load(reader);
    return document;
}
static XmlNode[] Strings(XmlDocument document) => document.DocumentElement!.ChildNodes.Cast<XmlNode>()
    .Where(node => node.NodeType == XmlNodeType.Element && node.LocalName == "String" && node.NamespaceURI == xaml).ToArray();
static string Key(XmlNode node) => node.Attributes!.GetNamedItem("Key", xaml)?.Value
    ?? throw new InvalidDataException("Translation key is missing");
static string WithoutStrings(XmlDocument document)
{
    var copy = (XmlDocument)document.CloneNode(true);
    foreach (var node in Strings(copy)) node.ParentNode!.RemoveChild(node);
    foreach (var parent in new XmlNode[] { copy, copy.DocumentElement! })
        foreach (var node in parent.ChildNodes.Cast<XmlNode>().Where(node => node.NodeType == XmlNodeType.Whitespace).ToArray())
            parent.RemoveChild(node);
    return copy.OuterXml;
}
static bool[] Layout(XmlDocument document, bool keepStrings)
{
    var result = new List<bool>();
    var inStrings = false;
    var seenStrings = false;
    foreach (XmlNode node in document.DocumentElement!.ChildNodes)
    {
        if (node.NodeType == XmlNodeType.Whitespace) continue;
        var isString = node.NodeType == XmlNodeType.Element && node.LocalName == "String" && node.NamespaceURI == xaml;
        if (isString)
        {
            if (!inStrings)
            {
                if (seenStrings) throw new InvalidDataException("Translation group is interrupted");
                seenStrings = inStrings = true;
                if (keepStrings) result.Add(true);
            }
        }
        else { inStrings = false; result.Add(false); }
    }
    return result.ToArray();
}
static string ToFixed2(double value)
{
    var bits = BitConverter.DoubleToInt64Bits(value);
    var exponentBits = (int)((bits >> 52) & 0x7ff);
    if (bits < 0 || exponentBits == 0x7ff) throw new InvalidDataException("Invalid completion rate");
    var significand = new BigInteger(bits & 0x000f_ffff_ffff_ffffL);
    if (exponentBits != 0) significand += BigInteger.One << 52;
    var exponent = (exponentBits == 0 ? -1022 : exponentBits - 1023) - 52;
    var numerator = significand * 100;
    BigInteger rounded;
    if (exponent >= 0) rounded = numerator << exponent;
    else
    {
        var denominator = BigInteger.One << -exponent;
        rounded = (numerator + denominator / 2) / denominator;
    }
    return (rounded / 100).ToString(CultureInfo.InvariantCulture) + "." +
        (rounded % 100).ToString("D2", CultureInfo.InvariantCulture);
}

if (!File.ReadAllBytes(englishPath).SequenceEqual(File.ReadAllBytes(Path.Combine(artifact, englishPath))))
    throw new InvalidDataException("English locale changed");
var english = ReadXml(englishPath);
var keys = new List<string>();
var order = new Dictionary<string, int>(StringComparer.Ordinal);
foreach (var node in Strings(english))
{
    var key = Key(node);
    if (order.TryAdd(key, keys.Count)) keys.Add(key);
}
if (keys.Count == 0) throw new InvalidDataException("English locale has no keys");
var lines = new List<string> {
    "# Translation Status",
    "This document shows the translation status of each locale file in the repository.",
    "## Details",
    "### ![en_US](https://img.shields.io/badge/en__US-%E2%88%9A-brightgreen)"
};
foreach (var file in Directory.GetFiles(localeDir, "*.axaml").OrderBy(path => path, StringComparer.Ordinal))
{
    var name = Path.GetFileName(file);
    if (name == "en_US.axaml") continue;
    var original = ReadXml(file);
    var candidate = ReadXml(Path.Combine(artifact, localeDir, name));
    if (WithoutStrings(original) != WithoutStrings(candidate)) throw new InvalidDataException($"Non-string XML changed: {name}");
    var originalNodes = Strings(original);
    var present = originalNodes.Select(Key).ToHashSet(StringComparer.Ordinal);
    var expected = originalNodes.Where(node => order.ContainsKey(Key(node))).OrderBy(node => order[Key(node)]).Select(node => node.OuterXml).ToArray();
    if (!expected.SequenceEqual(Strings(candidate).Select(node => node.OuterXml), StringComparer.Ordinal))
        throw new InvalidDataException($"Translation changed: {name}");
    if (!Layout(original, expected.Length > 0).SequenceEqual(Layout(candidate, true)))
        throw new InvalidDataException($"Translation group moved: {name}");
    var missing = keys.Where(key => !present.Contains(key)).ToArray();
    var locale = name.Remove(name.IndexOf(".axaml", StringComparison.Ordinal), ".axaml".Length);
    var underscore = locale.IndexOf('_');
    if (underscore >= 0) locale = locale.Insert(underscore, "_");
    if (missing.Length > 0)
    {
        var progress = (double)(keys.Count - missing.Length) / keys.Count * 100;
        var percentage = ToFixed2(progress);
        var color = progress >= 75 ? "yellow" : "red";
        lines.Add($"### ![{locale}](https://img.shields.io/badge/{locale}-{percentage}%25-{color})");
        lines.Add($"<details>\n<summary>Missing keys in {name}</summary>\n\n{string.Join('\n', missing.Select(key => "- " + key))}\n\n</details>");
    }
    else lines.Add($"### ![{locale}](https://img.shields.io/badge/{locale}-%E2%88%9A-brightgreen)");
}
if (!File.ReadAllBytes(Path.Combine(artifact, "docs/TRANSLATION.md")).SequenceEqual(Encoding.UTF8.GetBytes(string.Join("\n\n", lines))))
    throw new InvalidDataException("Translation status does not match base locales");
