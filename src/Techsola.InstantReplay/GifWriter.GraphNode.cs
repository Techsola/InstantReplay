using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Techsola.InstantReplay
{
    partial class GifWriter
    {
        private sealed class GraphNode
        {
            private Dictionary<byte, GraphNode>? children;

            public GraphNode(ushort code)
            {
                Code = code;
            }

            public ushort Code { get; }

            public bool TryGetChildNode(byte childKey, [NotNullWhen(true)] out GraphNode? childNode)
            {
                if (children is not null && children.TryGetValue(childKey, out childNode))
                    return true;

                childNode = null;
                return false;
            }

            public void AddChildNode(byte childKey, ushort childCode)
            {
                (children ??= new()).Add(childKey, new(childCode));
            }
        }
    }
}
