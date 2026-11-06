using System.Security.Cryptography.Xml;
using System.Xml;

namespace EetKit.Internal;

/// <summary>Exclusive XML canonicalization (exc-c14n) of one element inside its document.</summary>
internal static class ExclusiveCanonicalizer
{
    /// <summary>
    /// Canonical bytes of <paramref name="element"/>. Namespaces declared on its ancestors stay
    /// in scope, so prefixes from <paramref name="inclusivePrefixes"/> are rendered like in the document.
    /// </summary>
    public static byte[] Canonicalize(XmlElement element, string inclusivePrefixes)
    {
        var copy = new XmlDocument { PreserveWhitespace = true };
        var root = (XmlElement)copy.ImportNode(element, deep: true);
        copy.AppendChild(root);
        PropagateAncestorNamespaces(element, root);

        var transform = new XmlDsigExcC14NTransform(includeComments: false, inclusivePrefixes);
        transform.LoadInput(copy);
        using var output = (Stream)transform.GetOutput(typeof(Stream));
        using var buffer = new MemoryStream();
        output.CopyTo(buffer);
        return buffer.ToArray();
    }

    // The closest declaration of a prefix wins, so ancestors are walked upwards and only
    // prefixes not yet declared on the copy are added.
    private static void PropagateAncestorNamespaces(XmlElement original, XmlElement copy)
    {
        for (var ancestor = original.ParentNode as XmlElement; ancestor is not null; ancestor = ancestor.ParentNode as XmlElement)
        {
            foreach (XmlAttribute attribute in ancestor.Attributes)
            {
                if (attribute.NamespaceURI == EetNamespaces.Xmlns && copy.GetAttributeNode(attribute.Name) is null)
                {
                    copy.Attributes.Append((XmlAttribute)copy.OwnerDocument.ImportNode(attribute, deep: true));
                }
            }
        }
    }
}
